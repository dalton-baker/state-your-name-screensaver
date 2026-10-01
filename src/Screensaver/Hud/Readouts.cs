using System;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;
using StateYourNameScreensaver.World;

namespace StateYourNameScreensaver.Hud;

/// <summary>The smaller panels: system scan list, vessel schematic, and nav status.</summary>
public static class Readouts
{
	const float Size = 18;

	public static void Scan(Text text, Vector2 at, float width, SolarSystem system, float reveal, float fade)
	{
		var star = system.Star;
		Row(text, at, width, "PRIMARY", star.IsBinary ? "BINARY PAIR" : star.Classification, fade * Math.Clamp(reveal * 8, 0, 1), false);
		float y = at.Y + 36;
		for (int gap = 0; gap <= system.Rings.Count; gap++)
		{
			if (gap == system.Ship.GapIndex)
			{
				text.Draw("> YOUR SHIP", new Vector2(at.X, y), Size, Palette.Pale * fade);
				y += 30;
			}
			if (gap == system.Rings.Count) break;
			var ring = system.Rings[gap];
			float visible = Math.Clamp((reveal - (gap + 1f) / (system.Rings.Count + 1)) * 8 + 1, 0, 1);
			string name = ring.Body.Type switch
			{
				BodyType.RockyPlanet => "ROCKY PLANET",
				BodyType.GasGiant => "GAS GIANT",
				BodyType.Star => "SMALL STAR",
				BodyType.BlackHole => "BLACK HOLE",
				_ => "ASTEROID BELT"
			};
			Row(text, new Vector2(at.X, y), width, $"ORBIT {gap + 1}", visible > 0 ? name : "...", fade * Math.Max(visible, 0.35f), ring.Body.Type == BodyType.BlackHole);
			y += 30;
		}
	}

	static void Row(Text text, Vector2 at, float width, string label, string value, float fade, bool alert)
	{
		text.Draw(label, at, Size, Palette.Bright * (0.65f * fade));
		float w = text.Measure(value, Size).X;
		text.Draw(value, new Vector2(at.X + width - w, at.Y), Size, (alert ? Palette.Amber : Palette.Bright) * fade);
	}

	public static void Vessel(Prim p, Text text, Vector2 at, Vector2 size, float time, bool exhaust, float fade)
	{
		var center = at + new Vector2(size.X * 0.52f, size.Y * 0.42f + MathF.Sin(time * 0.6f) * 3);
		SystemView.DrawShipShape(p, center, 0, size.X / 30f, fade, exhaust, lineScale: 0.45f);
		string caption = "OBJECT VESSEL  //  CREW IN STASIS";
		text.Draw(caption, new Vector2(at.X + (size.X - text.Measure(caption, 14, 1.5f).X) / 2, at.Y + size.Y - 26), 14, Palette.Dim * fade, 1.5f);
	}

	public static void Status(Text text, Vector2 at, float width, Director director, float time, float fade)
	{
		var system = director.System;
		var ship = system.Ship;
		var rows = new (string, string)[]
		{
			("SYSTEM", system.Designation),
			("SHELLS", system.Rings.Count.ToString()),
			("ORBIT", ship.Maneuvering ? $"BURN -> GAP {ship.TargetGap + 1}" : $"GAP {ship.GapIndex + 1}"),
			("RADIUS", $"{Math.Round(ship.MajorAxis)} Mm"),
			("CHARTED", $"{director.SystemsVisited} SYSTEM{(director.SystemsVisited == 1 ? "" : "S")}"),
			("CLOCK", (int)(time * 1.25f) % 2 == 0 ? "UNKNOWN" : ""),
			("CAPTAIN", "UNIDENTIFIED"),
		};
		float y = at.Y;
		foreach (var (label, value) in rows)
		{
			Row(text, new Vector2(at.X, y), width, label, value, fade, label == "CLOCK");
			y += 34;
		}
	}
}
