using StateYourNameScreensaver;

var options = Options.Parse(args);

switch (options.Mode)
{
	case LaunchMode.Preview:
		// No live preview in the Screen Saver Settings thumbnail; exit quietly.
		return;
	case LaunchMode.Configure:
#if WINDOWS
		System.Windows.Forms.MessageBox.Show(
			"State Your Name has no settings.\n\nIt sweeps a new star system, cruises for a while, then jumps to the next one.",
			"State Your Name Screensaver",
			System.Windows.Forms.MessageBoxButtons.OK,
			System.Windows.Forms.MessageBoxIcon.Information);
#endif
		return;
}

using var game = new ScreensaverGame(options);
game.Run();
