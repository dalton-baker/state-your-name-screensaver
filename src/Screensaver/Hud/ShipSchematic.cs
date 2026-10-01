using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.Hud;

/// <summary>
/// The top-down ship plan from the game's SHIP NAV panel (bow to the right): a long hull with
/// the bridge at the nose, two engine pods with flared nozzles, and the ship's sections.
/// Coordinates are in the game's 680×300 schematic space.
/// </summary>
public static class ShipSchematic
{
	static readonly Vector2[] Hull = P(
		120, 92, 500, 92, 540, 95, 575, 104, 608, 118, 625, 133, 625, 167, 608, 182, 575, 196, 540, 205,
		500, 208, 120, 208, 100, 200, 88, 180, 85, 150, 88, 120, 100, 100);

	static readonly Vector2[] PortPod = P(150, 50, 430, 50, 440, 68, 440, 92, 120, 92, 110, 80, 120, 68);
	static readonly Vector2[] StarboardPod = Mirror(PortPod);

	// Engine nozzle flaring back from the pod; the part under the pod is covered when the pod is drawn.
	static readonly Vector2[] PortNozzle = P(190, 61, 168, 61, 150, 58, 128, 54, 95, 51, 95, 91, 128, 88, 150, 84, 168, 81, 190, 81);
	static readonly Vector2[] StarboardNozzle = Mirror(PortNozzle);

	static readonly (Vector2 From, Vector2 To)[] Exhaust =
	{
		(new(95, 52), new(48, 44)), (new(95, 61), new(44, 57)), (new(95, 71), new(38, 71)),
		(new(95, 81), new(44, 85)), (new(95, 90), new(48, 98))
	};

	static readonly (Vector2 From, Vector2 To)[] Dividers =
	{
		(new(500, 92), new(500, 208)), (new(88, 150), new(500, 150)),
		(new(247, 92), new(247, 150)), (new(373, 92), new(373, 150)), (new(300, 150), new(300, 208))
	};

	static readonly (string Label, Vector2 At)[] Sections =
	{
		("BRIDGE", new(555, 150)), ("???", new(183, 122)), ("STASIS BAY", new(310, 122)),
		("CREW QUARTERS", new(437, 122)), ("???", new(400, 179)), ("???", new(193, 179))
	};

	// Bridge viewports: (center, size, tilt in degrees)
	static readonly (Vector2 Center, Vector2 Size, float Tilt)[] Windows =
	{
		(new(591, 128), new(20, 6), -12), (new(591, 150), new(20, 12), 0), (new(591, 172), new(20, 6), 12)
	};

	// The game's view box.
	static readonly Rectangle View = new(28, 34, 610, 232);

	static Vector2[] P(params float[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => new Vector2(xy[i * 2], xy[i * 2 + 1])).ToArray();
	static Vector2[] Mirror(Vector2[] points) => points.Select(p => new Vector2(p.X, 300 - p.Y)).ToArray();

	public static void Draw(Prim p, Text text, Vector2 at, Vector2 size, float time, bool exhaust, float fade)
	{
		float scale = Math.Min(size.X / View.Width, size.Y / View.Height);
		var origin = at + (size - new Vector2(View.Width, View.Height) * scale) / 2 - new Vector2(View.X, View.Y) * scale
			+ new Vector2(0, MathF.Sin(time * 0.6f) * 2);
		Vector2 T(Vector2 v) => origin + v * scale;
		Vector2[] Map(Vector2[] shape) => shape.Select(T).ToArray();
		float line = Math.Max(1.4f, 2.2f * scale);
		var stroke = Palette.Bright * fade;
		var fill = Palette.StarFill * fade;

		if (exhaust)
		{
			float flicker = 0.75f + 0.25f * MathF.Sin(time * 40);
			foreach (float mirror in new[] { 0f, 1f })
			foreach (var (from, to) in Exhaust)
			{
				Vector2 M(Vector2 v) => mirror == 0 ? v : new Vector2(v.X, 300 - v.Y);
				p.Line(T(M(from)), T(M(to)), Palette.Pale * (0.8f * flicker * fade), line * 0.8f);
			}
			p.Glow(T(new(70, 71)), 40 * scale, Palette.Pale * (0.16f * fade));
			p.Glow(T(new(70, 229)), 40 * scale, Palette.Pale * (0.16f * fade));
		}

		foreach (var nozzle in new[] { PortNozzle, StarboardNozzle })
		{
			p.FillPolygon(Map(nozzle), fill);
			p.Polyline(Map(nozzle), stroke * 0.85f, line * 0.8f);
		}

		// Soft glow under the hull outline, then the solid shapes.
		p.Polyline(Map(Hull), Palette.Bright * (0.12f * fade), line * 4);
		foreach (var shape in new[] { PortPod, StarboardPod, Hull })
		{
			p.FillPolygon(Map(shape), fill);
			p.Polyline(Map(shape), stroke, line);
		}

		foreach (var (from, to) in Dividers) p.Line(T(from), T(to), Palette.Bright * (0.45f * fade), line * 0.55f);

		foreach (var (center, extent, tilt) in Windows)
		{
			p.RotatedRect(T(center), extent * scale, MathHelper.ToRadians(tilt), Palette.Pale * (0.85f * fade));
			p.Glow(T(center), 14 * scale, Palette.Pale * (0.25f * fade));
		}

		float labelSize = Math.Max(7.5f, 12 * scale);
		foreach (var (label, center) in Sections)
		{
			var m = text.Measure(label, labelSize, 0.5f);
			// The bridge label sits a little aft of centre so it clears the viewports.
			var at2 = T(center) - m / 2 - (label == "BRIDGE" ? new Vector2(12 * scale, 0) : Vector2.Zero);
			text.Draw(label, at2, labelSize, Palette.Bright * ((label == "???" ? 0.35f : 0.6f) * fade), 0.5f);
		}
	}
}
