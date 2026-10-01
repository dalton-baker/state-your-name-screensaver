using System;
using System.Runtime.InteropServices;
using StateYourNameScreensaver;

var options = Options.Parse(args);

switch (options.Mode)
{
	case LaunchMode.Preview:
		// No live preview in the Screen Saver Settings thumbnail; exit quietly.
		return;
	case LaunchMode.Configure:
		if (OperatingSystem.IsWindows())
			NativeMethods.MessageBoxW(IntPtr.Zero,
				"State Your Name has no settings.\n\nIt sweeps a new star system, cruises for a while, then jumps to the next one.",
				"State Your Name Screensaver", NativeMethods.MB_ICONINFORMATION);
		return;
}

using var game = new ScreensaverGame(options);
game.Run();

static class NativeMethods
{
	public const uint MB_ICONINFORMATION = 0x40;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
