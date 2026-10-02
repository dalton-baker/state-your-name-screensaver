using System;
using System.Globalization;

namespace StateYourNameScreensaver;

public enum LaunchMode { Run, Configure, Preview }

/// <summary>
/// Command line. Windows passes /s (run), /c (settings) or /p &lt;hwnd&gt; (the little preview
/// in Screen Saver Settings). The --flags are for development and recording previews.
/// </summary>
public sealed class Options
{
	public LaunchMode Mode = LaunchMode.Run;
	public bool Windowed;
	public int Width = 1600, Height = 900;
	public int? Seed;
	public string? CaptureDirectory;
	public int CaptureFrames = 150;
	public int CaptureFps = 15;
	public float SkipSeconds;

	public static Options Parse(string[] args)
	{
		var o = new Options();
		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i].Trim();
			string lower = arg.ToLowerInvariant();
			string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{arg} needs a value");

			if (lower.StartsWith("/s") || lower.StartsWith("-s")) o.Mode = LaunchMode.Run;
			else if (lower.StartsWith("/c") || lower.StartsWith("-c")) o.Mode = LaunchMode.Configure;
			else if (lower.StartsWith("/p") || lower.StartsWith("-p")) o.Mode = LaunchMode.Preview;
			else if (lower == "--windowed") o.Windowed = true;
			else if (lower == "--size")
			{
				var parts = Next().Split('x');
				o.Width = int.Parse(parts[0], CultureInfo.InvariantCulture);
				o.Height = int.Parse(parts[1], CultureInfo.InvariantCulture);
			}
			else if (lower == "--seed") o.Seed = int.Parse(Next(), CultureInfo.InvariantCulture);
			else if (lower == "--capture") { o.CaptureDirectory = Next(); o.Windowed = true; }
			else if (lower == "--frames") o.CaptureFrames = int.Parse(Next(), CultureInfo.InvariantCulture);
			else if (lower == "--fps") o.CaptureFps = int.Parse(Next(), CultureInfo.InvariantCulture);
			else if (lower == "--skip") o.SkipSeconds = float.Parse(Next(), CultureInfo.InvariantCulture);
		}
		return o;
	}
}
