using System;
using System.IO;
using System.Runtime.InteropServices;
using StateYourNameScreensaver;

try
{
	Run(args);
}
catch (Exception e)
{
	Crash.Report(e);
	return 1;
}
return 0;

static void Run(string[] args)
{
	var options = Options.Parse(args);

	switch (options.Mode)
	{
		case LaunchMode.Preview:
			// No live preview in the Screen Saver Settings thumbnail; exit quietly.
			return;
		case LaunchMode.Configure:
			NativeMethods.Message("State Your Name has no settings.\n\nIt sweeps a new star system, cruises for a while, then jumps to the next one.", error: false);
			return;
	}

	NativeLibraries.Preload();
	using var game = new ScreensaverGame(options);
	game.Run();
}

/// <summary>
/// MonoGame finds SDL2/OpenAL by looking next to its own DLL, then by bare file name. In a
/// single-file build both are unpacked to a temp folder it doesn't know about, so load them from
/// there first; MonoGame's bare-name lookup then picks up the already-loaded copies.
/// </summary>
static class NativeLibraries
{
	public static void Preload()
	{
		string[] names =
			OperatingSystem.IsWindows() ? new[] { "SDL2.dll", "openal.dll" } :
			OperatingSystem.IsMacOS() ? new[] { "libSDL2-2.0.0.dylib", "libopenal.dylib" } :
			new[] { "libSDL2-2.0.so.0", "libopenal.so" };

		foreach (var name in names)
		{
			if (NativeLibrary.TryLoad(name, typeof(NativeLibraries).Assembly, DllImportSearchPath.AssemblyDirectory, out _)) continue;
			foreach (var dir in NativeSearchDirectories())
				if (File.Exists(Path.Combine(dir, name)) && NativeLibrary.TryLoad(Path.Combine(dir, name), out _)) break;
		}
	}

	// Where the runtime unpacked bundled native libraries (single-file) plus the app folder.
	static string[] NativeSearchDirectories() =>
		((AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string) ?? "")
			.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>A windowed app has no console, so failures go to a log file and a message box.</summary>
static class Crash
{
	public static string LogPath => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StateYourName", "crash.log");

	public static void Report(Exception e)
	{
		string text = $"{DateTime.Now:u}\n{RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}\n{e}\n\n";
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
			File.AppendAllText(LogPath, text);
		}
		catch { /* nowhere to write; the message box still shows */ }
		Console.Error.Write(text);
		NativeMethods.Message($"State Your Name couldn't start.\n\n{e.GetBaseException().Message}\n\nDetails were saved to:\n{LogPath}", error: true);
	}
}

static class NativeMethods
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

	public static void Message(string text, bool error)
	{
		if (!OperatingSystem.IsWindows()) return;
		const uint IconError = 0x10, IconInformation = 0x40;
		MessageBoxW(IntPtr.Zero, text, "State Your Name Screensaver", error ? IconError : IconInformation);
	}
}
