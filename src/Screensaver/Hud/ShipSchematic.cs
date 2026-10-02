using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.Hud;

/// <summary>
/// The top-down ship plan from the game's SHIP NAV panel (bow to the right): a long hull with
/// the bridge at the nose and two engine pods with flared nozzles. It's drawn as a worn old
/// vessel: plating seams, rivets, dents, scorching, a crack, a patch, and a cracked viewport.
/// Coordinates are in the game's 680×300 schematic space.
/// </summary>
public static class ShipSchematic
{
	// Outlines include a few dents (the small notches in otherwise straight edges).
	static readonly Vector2[] Hull = P(
		120, 92, 500, 92, 540, 95, 575, 104, 608, 118, 625, 133, 625, 167, 608, 182, 575, 196, 540, 205,
		500, 208, 300, 208, 292, 203, 283, 204, 276, 208, 120, 208, 100, 200, 88, 180, 85, 150, 88, 120, 100, 100);

	static readonly Vector2[] PortPod = P(150, 50, 296, 50, 303, 55, 311, 54, 318, 50, 430, 50, 440, 68, 440, 92, 120, 92, 110, 80, 120, 68);
	static readonly Vector2[] StarboardPod = Mirror(P(150, 50, 430, 50, 440, 68, 440, 92, 120, 92, 110, 80, 120, 68));

	// Engine nozzle flaring back from the pod; only the part outside the pod is outlined.
	static readonly Vector2[] PortNozzle = P(190, 61, 168, 61, 150, 58, 128, 54, 95, 51, 95, 91, 128, 88, 150, 84, 168, 81, 190, 81);
	static readonly Vector2[] StarboardNozzle = Mirror(PortNozzle);

	// Just the part of each nozzle the pod doesn't cover (nozzle outline cut along the pod's
	// rear edge), so translucent fills don't double up while fading in.
	static readonly Vector2[] PortNozzleVisible = P(139.8f, 56.1f, 128, 54, 95, 51, 95, 91, 116.6f, 88.9f, 110, 80, 120, 68);
	static readonly Vector2[] StarboardNozzleVisible = Mirror(PortNozzleVisible);

	static readonly (Vector2 From, Vector2 To)[] Exhaust =
	{
		(new(95, 52), new(48, 44)), (new(95, 61), new(44, 57)), (new(95, 71), new(38, 71)),
		(new(95, 81), new(44, 85)), (new(95, 90), new(48, 98))
	};

	// Bridge viewports: (center, size, tilt in degrees)
	static readonly (Vector2 Center, Vector2 Size, float Tilt)[] Windows =
	{
		(new(591, 128), new(20, 6), -12), (new(591, 150), new(20, 12), 0), (new(591, 172), new(20, 6), 12)
	};

	// Wear and tear
	static readonly float[] HullSeamsX = { 165, 245, 330, 415, 470 };
	static readonly float[] PodSeamsX = { 205, 270, 350, 405 };
	static readonly (Vector2 Center, float Radius)[] Scorches = { (new(228, 182), 17), (new(402, 116), 11), (new(168, 238), 10) };
	static readonly Vector2[] Crack = P(338, 208, 341, 201, 337, 196, 345, 190, 349, 183, 347, 178);
	static readonly Vector2[] Crack2 = P(341, 201, 349, 199);
	static readonly Rectangle Patch = new(452, 166, 28, 18);
	static readonly Rectangle Stripped = new(358, 58, 44, 22);

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
		var wear = Palette.Bright * fade;

		if (exhaust)
		{
			float flicker = 0.75f + 0.25f * MathF.Sin(time * 40);
			foreach (bool mirror in new[] { false, true })
			foreach (var (from, to) in Exhaust)
			{
				Vector2 M(Vector2 v) => mirror ? new Vector2(v.X, 300 - v.Y) : v;
				p.Line(T(M(from)), T(M(to)), Palette.Pale * (0.8f * flicker * fade), line * 0.8f);
			}
			p.Glow(T(new(70, 71)), 40 * scale, Palette.Pale * (0.16f * fade));
			p.Glow(T(new(70, 229)), 40 * scale, Palette.Pale * (0.16f * fade));
		}

		// Nozzles: fill, and outline only where the pod doesn't cover them, so nothing shows
		// through the pods while the panel is fading in.
		foreach (var (nozzle, visible, pod) in new[] { (PortNozzle, PortNozzleVisible, PortPod), (StarboardNozzle, StarboardNozzleVisible, StarboardPod) })
		{
			p.FillPolygon(Map(visible), fill);
			OutlineOutside(p, nozzle, pod, T, stroke * 0.85f, line * 0.8f);
		}

		p.Polyline(Map(Hull), Palette.Bright * (0.12f * fade), line * 4);   // soft glow under the hull
		foreach (var shape in new[] { PortPod, StarboardPod, Hull })
			p.FillPolygon(Map(shape), fill);

		DrawWear(p, T, scale, line, wear, fill, time);

		foreach (var shape in new[] { PortPod, StarboardPod })
			OutlineOutside(p, shape, Hull, T, stroke, line);
		p.Polyline(Map(Hull), stroke, line);

		for (int i = 0; i < Windows.Length; i++)
		{
			var (center, extent, tilt) = Windows[i];
			// The top viewport's light flickers now and then.
			float flicker = i == 0 && MathF.Sin(time * 0.9f) > 0.94f && MathF.Sin(time * 31) > 0 ? 0.25f : 1f;
			p.RotatedRect(T(center), extent * scale, MathHelper.ToRadians(tilt), Palette.Pale * (0.85f * flicker * fade));
			p.Glow(T(center), 14 * scale, Palette.Pale * (0.25f * flicker * fade));
		}
		// A crack across the middle viewport.
		p.Line(T(new(583, 145)), T(new(590, 151)), Palette.StarFill * fade, line * 0.7f);
		p.Line(T(new(590, 151)), T(new(597, 148)), Palette.StarFill * fade, line * 0.7f);
		p.Line(T(new(590, 151)), T(new(593, 156)), Palette.StarFill * fade, line * 0.6f);
	}

	static void DrawWear(Prim p, Func<Vector2, Vector2> T, float scale, float line, Color wear, Color fill, float time)
	{
		float thin = line * 0.45f;

		// Plating seams.
		foreach (float x in HullSeamsX) p.Line(T(new(x, 95)), T(new(x, 205)), wear * 0.13f, thin);
		p.Line(T(new(92, 150)), T(new(500, 150)), wear * 0.08f, thin);
		foreach (float x in PodSeamsX)
		{
			p.Line(T(new(x, 52)), T(new(x, 90)), wear * 0.13f, thin);
			p.Line(T(new(x, 210)), T(new(x, 248)), wear * 0.13f, thin);
		}

		// Rivets along the hull's long edges and the pods.
		for (float x = 130; x <= 490; x += 18)
		{
			p.Disc(T(new(x, 98)), 1.1f * scale, wear * 0.3f);
			p.Disc(T(new(x, 202)), 1.1f * scale, wear * 0.3f);
		}
		for (float x = 160; x <= 420; x += 22)
		{
			p.Disc(T(new(x, 56)), 1f * scale, wear * 0.25f);
			p.Disc(T(new(x, 244)), 1f * scale, wear * 0.25f);
		}

		// Scorch marks: ragged diagonal hatching.
		foreach (var (center, radius) in Scorches)
			Hatch(p, T, center, radius, 3.2f, wear * 0.28f, thin, ragged: true);

		// A stripped section of plating on the port pod.
		var s = Stripped;
		p.Polyline(new[] { T(new(s.Left, s.Top)), T(new(s.Right, s.Top)), T(new(s.Right, s.Bottom)), T(new(s.Left, s.Bottom)) }, wear * 0.35f, thin);
		for (float x = s.Left - s.Height; x < s.Right; x += 5)
		{
			var a = new Vector2(Math.Max(x, s.Left), s.Bottom - Math.Max(0, s.Left - x));
			var b = new Vector2(Math.Min(x + s.Height, s.Right), s.Top + Math.Max(0, x + s.Height - s.Right));
			p.Line(T(a), T(b), wear * 0.22f, thin);
		}

		// A riveted repair patch.
		var r = Patch;
		var patch = new[] { T(new(r.Left, r.Top)), T(new(r.Right, r.Top)), T(new(r.Right, r.Bottom)), T(new(r.Left, r.Bottom)) };
		p.FillPolygon(patch, fill);
		p.Polyline(patch, wear * 0.55f, thin * 1.3f);
		foreach (var corner in new Vector2[] { new(r.Left + 3, r.Top + 3), new(r.Right - 3, r.Top + 3), new(r.Left + 3, r.Bottom - 3), new(r.Right - 3, r.Bottom - 3) })
			p.Disc(T(corner), 1.2f * scale, wear * 0.55f);

		// A hull crack running up from a dent.
		for (int i = 0; i + 1 < Crack.Length; i++) p.Line(T(Crack[i]), T(Crack[i + 1]), wear * 0.7f, thin * 1.4f);
		p.Line(T(Crack2[0]), T(Crack2[1]), wear * 0.5f, thin);
	}

	/// <summary>Diagonal hatching clipped to a circle; ragged trims each line by a fixed pseudo-random amount.</summary>
	static void Hatch(Prim p, Func<Vector2, Vector2> T, Vector2 center, float radius, float spacing, Color color, float width, bool ragged)
	{
		var dir = Vector2.Normalize(new Vector2(1, -1));
		var normal = new Vector2(dir.Y, -dir.X);
		int i = 0;
		for (float k = -radius + spacing / 2; k < radius; k += spacing, i++)
		{
			float half = MathF.Sqrt(radius * radius - k * k);
			float trimA = ragged ? half * (0.15f + 0.35f * Hash(i * 2 + center.X)) : 0;
			float trimB = ragged ? half * (0.15f + 0.35f * Hash(i * 2 + 1 + center.Y)) : 0;
			var mid = center + normal * k;
			p.Line(T(mid - dir * (half - trimA)), T(mid + dir * (half - trimB)), color, width);
		}
	}

	static float Hash(float n) { float x = MathF.Sin(n * 12.9898f) * 43758.5453f; return x - MathF.Floor(x); }

	/// <summary>Outlines <paramref name="shape"/> except where it lies inside <paramref name="cover"/>.</summary>
	static void OutlineOutside(Prim p, Vector2[] shape, Vector2[] cover, Func<Vector2, Vector2> T, Color color, float width)
	{
		for (int i = 0; i < shape.Length; i++)
		{
			var a = shape[i]; var b = shape[(i + 1) % shape.Length];
			int steps = Math.Max(1, (int)(Vector2.Distance(a, b) / 1.5f));
			for (int s = 0; s < steps; s++)
			{
				var from = Vector2.Lerp(a, b, s / (float)steps);
				var to = Vector2.Lerp(a, b, (s + 1) / (float)steps);
				if (!Inside((from + to) / 2, cover, inset: 0.6f)) p.Line(T(from), T(to), color, width);
			}
		}
	}

	/// <summary>Point-in-polygon, treating points within <paramref name="inset"/> of the edge as outside (so shared edges still draw).</summary>
	static bool Inside(Vector2 point, Vector2[] polygon, float inset)
	{
		bool inside = false;
		for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
		{
			var a = polygon[i]; var b = polygon[j];
			if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
		}
		if (!inside) return false;
		for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
			if (DistanceToSegment(point, polygon[j], polygon[i]) < inset) return false;
		return true;
	}

	static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		var ab = b - a;
		float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
		return Vector2.Distance(p, a + ab * t);
	}
}
