using System;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.Hud;

/// <summary>The ship computer's emergency boot, as seen when the game starts.</summary>
public static class BootScreen
{
	static readonly (float At, string Text, bool Detail, bool Alert)[] Lines =
	{
		(0.4f, "SYSTEM COLD START", false, false),
		(1.0f, "................", false, false),
		(2.0f, "PROXIMITY ALERT: STELLAR OBJECT DETECTED", false, true),
		(2.4f, "stellar proximity threshold exceeded", true, false),
		(2.7f, "emergency wake protocol triggered", true, false),
		(3.4f, "AURORA OS v0.9.1 - EMERGENCY BOOT MODE", false, false),
		(3.8f, "kernel: loaded", true, false),
		(4.1f, "drivers: [ok]", true, false),
		(4.4f, "memory banks: CLEARED", true, false),
		(5.1f, "SYSTEM CLOCK: UNKNOWN - NO TIMESTAMP", false, false),
		(5.8f, "CREW MANIFEST: UNAVAILABLE", false, false),
		(6.2f, "command hierarchy: UNKNOWN", true, false),
	};

	public static void Draw(Prim p, Text text, Vector2 center, float time, float duration)
	{
		float fade = Math.Clamp((duration - time) / 1.2f, 0, 1) * Math.Clamp(time / 0.3f, 0, 1);
		if (fade <= 0) return;

		var size = new Vector2(760, 470);
		var at = center - size / 2;
		p.RectF(at, size, Color.Black * fade);
		p.Frame(at, size, Palette.Bright * (0.9f * fade), 1.5f);
		text.Draw("// BOOT //", at + new Vector2(42, 32), 16, Palette.Bright * (0.6f * fade), spacing: 4);

		float y = at.Y + 82;
		foreach (var (start, line, detail, alert) in Lines)
		{
			if (time < start) break;
			int chars = Math.Min(line.Length, (int)((time - start) * 60));
			var color = alert ? Palette.Amber : detail ? Palette.Bright * 0.7f : Palette.Bright;
			text.Draw(line[..chars], new Vector2(at.X + (detail ? 66 : 42), y), detail ? 16.5f : 20, color * fade);
			y += detail ? 26 : 30;
		}
	}
}
