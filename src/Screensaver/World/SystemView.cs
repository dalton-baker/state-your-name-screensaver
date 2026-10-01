using System;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.World;

/// <summary>
/// Draws a <see cref="SolarSystem"/> the way the game's LOCAL SYSTEM panel does, scaled up to
/// fill <see cref="Radius"/> design units around <see cref="Center"/>.
/// </summary>
public sealed class SystemView
{
	// The game lays systems out in a 420-unit viewbox with 34 units of padding; we keep those
	// proportions and multiply everything by K so strokes scale with the view.
	const float GameCenter = 210, GamePadding = 34;
	const float StarRadiusScale = 0.72f, BodyRadiusScale = 0.78f;

	public Vector2 Center;
	public float Radius;

	/// <summary>0..1 sensor reveal; rings beyond it are hidden while a scan sweeps outward.</summary>
	public float Reveal = 1;
	/// <summary>0..1 overall fade (used for jumps).</summary>
	public float Fade = 1;
	public bool ExhaustOn;

	float _layoutScale, _k;

	public void Layout(SolarSystem system)
	{
		_k = Radius / (GameCenter - GamePadding);
		_layoutScale = (GameCenter - GamePadding) / Math.Max(system.MaxExtent(), 1);
	}

	float U(float gameUnits) => gameUnits * _k;                      // viewbox units → design units
	float R(float orbitUnits) => orbitUnits * _layoutScale * _k;    // system units → design units

	Vector2 OnOrbit(float radius, float angleDegrees)
	{
		float radians = MathHelper.ToRadians(angleDegrees - 90);
		return Center + new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * R(radius);
	}

	float RingVisibility(Ring ring, SolarSystem system)
	{
		float position = (ring.Index + 1f) / (system.Rings.Count + 1f);
		return Math.Clamp((Reveal - position) * (system.Rings.Count + 1) * 1.5f + 1, 0, 1);
	}

	public void Draw(Prim p, SolarSystem system, float time)
	{
		Layout(system);
		float fade = Fade;

		// Orbit shells and asteroid belts
		foreach (var ring in system.Rings)
		{
			float v = RingVisibility(ring, system) * fade;
			if (v <= 0) continue;
			if (ring.Body.Type == BodyType.AsteroidField)
			{
				float halfWidth = Math.Max(U(8), R(ring.Body.BandWidth) * 0.9f) / 2 * 1.8f;
				p.Band(Center, R(ring.MajorAxis), halfWidth, Palette.Bright, 0.06f * v, 0.0f);
				foreach (var a in ring.Body.Asteroids)
				{
					float wobble = MathF.Sin(MathHelper.ToRadians(a.WobbleAngle)) * a.WobbleRange;
					var pos = OnOrbit(ring.MajorAxis + a.RadialOffset + wobble, a.OrbitAngle);
					p.Disc(pos, Math.Max(U(0.45f), R(a.Size)), Palette.Bright * (a.Opacity * v));
				}
			}
			else
			{
				p.Circle(Center, R(ring.MajorAxis), Palette.Mid * (0.28f * v), U(1));
			}
		}

		// Ship orbit (dashed)
		var ship = system.Ship;
		p.DashedCircle(Center, R(ship.MajorAxis), Palette.Mid * (0.5f * fade), U(1.5f), U(2), U(4), time * U(3));

		DrawCentralStars(p, system.Star, fade);

		foreach (var ring in system.Rings)
		{
			float v = RingVisibility(ring, system) * fade;
			if (v <= 0 || ring.Body.Type == BodyType.AsteroidField) continue;
			DrawBody(p, ring, v);
		}

		DrawShip(p, ship, fade);
	}

	void DrawCentralStars(Prim p, CentralStar star, float fade)
	{
		if (!star.IsBinary)
		{
			DrawStar(p, Center, R(star.Diameter / 2f) * StarRadiusScale / 1f, star.PrimaryIsBlackHole, fade, central: true);
			return;
		}
		var primary = OnOrbit(star.PrimaryOrbitRadius, star.BinaryOrbitAngle);
		var secondary = OnOrbit(star.SecondaryOrbitRadius, star.BinaryOrbitAngle + 180);
		DrawStar(p, primary, R(star.Diameter / 2f) * StarRadiusScale, star.PrimaryIsBlackHole, fade, central: true);
		DrawStar(p, secondary, R(star.SecondaryDiameter / 2f) * StarRadiusScale, star.SecondaryIsBlackHole, fade, central: true);
	}

	void DrawStar(Prim p, Vector2 at, float radius, bool blackHole, float fade, bool central)
	{
		if (blackHole) { DrawBlackHole(p, at, radius, fade, central); return; }
		p.Glow(at, radius + U(10) * _layoutScale * 2.2f, Palette.Bright * (0.30f * fade));
		p.Disc(at, radius, Palette.StarFill * fade);
		p.Glow(at, radius * 0.95f, Palette.Bright * (0.10f * fade));
		p.Circle(at, radius, Palette.Bright * (0.95f * fade), U(central ? 2.5f : 2f));
	}

	void DrawBlackHole(Prim p, Vector2 at, float r, float fade, bool central)
	{
		float s = r / U(12);       // disk stroke scale, relative to a typical horizon
		float tilt = MathHelper.ToRadians(-8);
		var (d1x, d1y) = (r * 2.6f, r * 0.62f);
		var (d2x, d2y) = (r * 2.2f, r * 0.50f);
		var (d3x, d3y) = (r * 1.85f, r * 0.40f);
		bool Behind(Vector2 m) => m.Y < at.Y && Vector2.Distance(m, at) > r;   // far half, outside the horizon
		bool Front(Vector2 m) => m.Y >= at.Y;

		p.Glow(at, r * 2.4f, Palette.Soft * (0.22f * fade));
		p.Ellipse(at, d1x, d1y, tilt, Palette.Soft * (0.18f * fade), U(3) * s, Behind);
		p.Ellipse(at, d2x, d2y, tilt, Palette.Lime * (0.28f * fade), U(2) * s, Behind);
		p.Ellipse(at, d3x, d3y, tilt, Palette.Mint * (0.20f * fade), U(1) * s, Behind);
		p.Disc(at, r, Color.Black);
		p.Circle(at, r * 1.22f, Palette.Lime * (0.9f * fade), U(central ? 1.6f : 1.3f));
		p.Circle(at, r * 1.6f, Palette.Soft * (0.36f * fade), U(central ? 0.9f : 0.7f));
		p.Ellipse(at, d1x, d1y, tilt, Palette.Soft * (0.35f * fade), U(3.5f) * s, Front);
		p.Ellipse(at, d2x, d2y, tilt, Palette.Lime * (0.70f * fade), U(2.5f) * s, Front);
		p.Ellipse(at, d3x, d3y, tilt, Palette.Mint * (0.55f * fade), U(1.2f) * s, Front);
	}

	void DrawBody(Prim p, Ring ring, float v)
	{
		var body = ring.Body;
		var at = OnOrbit(ring.MajorAxis, body.OrbitAngle);
		// The game's bodies are tiny at screensaver distance; keep them readable (black holes most of all).
		float r = Math.Max(R(body.Diameter / 2f) * BodyRadiusScale, U(body.Type == BodyType.BlackHole ? 4.2f : 3.2f));
		bool big = body.Type is BodyType.GasGiant or BodyType.BlackHole;
		p.Disc(at, r + U(big ? 3.5f : 2.2f) * _layoutScale, Color.Black * v);   // backdrop hides the orbit line

		if (body.Type == BodyType.BlackHole) { DrawBlackHole(p, at, r, v, central: false); return; }

		bool ringed = body.HasRing && body.Type is BodyType.GasGiant or BodyType.RockyPlanet;
		float tilt = MathHelper.ToRadians(body.OrbitAngle);
		var ringSize = new Vector2(r * 2.05f, r * 0.62f);
		float cos = MathF.Cos(-tilt), sin = MathF.Sin(-tilt);
		bool FrontHalf(Vector2 m) { var d = m - at; return d.X * sin + d.Y * cos >= 0; }
		bool BackHalf(Vector2 m) => !FrontHalf(m);

		if (ringed) p.Ellipse(at, ringSize.X, ringSize.Y, tilt, Palette.Mid * (0.34f * v), U(1.5f), BackHalf);

		if (body.Type == BodyType.Star) p.Glow(at, r * 2.6f, Palette.Bright * (0.25f * v));
		p.Disc(at, r, Palette.BodyFill * v);
		p.Glow(at, r, Palette.Bright * (0.24f * v));
		float stroke = body.Type == BodyType.Star ? 2.5f : 2f;
		float opacity = body.Type == BodyType.GasGiant ? 0.9f : 0.85f;
		p.Circle(at, r, Palette.Bright * (opacity * v), U(stroke) * Math.Max(_layoutScale, 0.55f));

		if (ringed) p.Ellipse(at, ringSize.X, ringSize.Y, tilt, Palette.Mid * (0.78f * v), U(1.5f), FrontHalf);
	}

	// The game's mini ship: a tapered hull between two cigar-shaped pods, nose along +X.
	static readonly Vector2[] Hull =
	{
		new(6.6f, 0), new(6.3f, -0.4f), new(5.5f, -1.15f), new(4.6f, -1.4f), new(-5.8f, -1.45f), new(-6.4f, -1.25f),
		new(-6.7f, -0.75f), new(-6.75f, 0), new(-6.7f, 0.75f), new(-6.4f, 1.25f), new(-5.8f, 1.45f), new(4.6f, 1.4f),
		new(5.5f, 1.15f), new(6.3f, 0.4f)
	};

	const float ShipScale = 1.9f;

	void DrawShip(Prim p, Ship ship, float fade)
	{
		var at = OnOrbit(ship.MajorAxis, ship.OrbitAngle);
		float heading = MathHelper.ToRadians(ship.OrbitAngle + ship.HeadingOffset);
		DrawShipShape(p, at, heading, U(ShipScale), fade, ExhaustOn || ship.Thrusting);
	}

	/// <summary>Shared with the large vessel schematic in the HUD.</summary>
	public static void DrawShipShape(Prim p, Vector2 at, float heading, float scale, float fade, bool exhaust, float lineScale = 1)
	{
		float cos = MathF.Cos(heading), sin = MathF.Sin(heading);
		Vector2 T(Vector2 v) => at + new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos) * scale;
		float w = 0.6f * scale * lineScale;

		if (exhaust)
		{
			var plume = Palette.Pale * (0.85f * fade);
			p.Line(T(new(-7, -2.5f)), T(new(-11.5f, -3.2f)), plume, 0.85f * scale * lineScale);
			p.Line(T(new(-7.5f, 0)), T(new(-12, 0)), plume, 0.85f * scale * lineScale);
			p.Line(T(new(-7, 2.5f)), T(new(-11.5f, 3.2f)), plume, 0.85f * scale * lineScale);
			p.Glow(T(new(-9.5f, 0)), 5 * scale, Palette.Pale * (0.18f * fade));
		}

		foreach (float y in new[] { -2.5f, 2.5f })
		{
			p.DiscEllipse(T(new(-2.3f, y)), 4.45f * scale, 0.9f * scale, heading, Palette.StarFill * fade);
			p.Arc(T(new(-2.3f, y)), 4.45f * scale, 0.9f * scale, heading, 0, MathHelper.TwoPi, Palette.Pale * fade, 0.5f * scale * lineScale, 40);
		}

		p.RotatedRect(T(new(-0.6f, 0)), new Vector2(12.2f, 2.8f) * scale, heading, Palette.StarFill * fade);
		for (int i = 0; i < Hull.Length; i++)
			p.Line(T(Hull[i]), T(Hull[(i + 1) % Hull.Length]), Palette.Pale * fade, w);
		p.Disc(T(new(5, 0)), 0.5f * scale, Palette.Pale * fade);
	}

	public Vector2 ShipPosition(SolarSystem system) { Layout(system); return OnOrbit(system.Ship.MajorAxis, system.Ship.OrbitAngle); }
}
