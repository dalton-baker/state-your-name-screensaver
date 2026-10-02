using System;
using System.Collections.Generic;
using System.Linq;

namespace StateYourNameScreensaver.World;

public enum BodyType { RockyPlanet, GasGiant, Star, AsteroidField, BlackHole }

public sealed class Asteroid
{
	public float OrbitAngle;
	public float OrbitSpeed;
	public float RadialOffset;
	public float WobbleAngle;
	public float WobbleSpeed;
	public float WobbleRange;
	public float Size;
	public float Opacity;
}

public sealed class Body
{
	public BodyType Type;
	public float OrbitAngle;
	public float OrbitSpeed;
	public int Diameter;
	public bool HasRing;
	public int Moons;
	public float Mass;
	public string? Classification;
	public int BandWidth;
	public List<Asteroid> Asteroids = new();
}

public sealed class Ring
{
	public int Index;
	public float MajorAxis;
	public Body Body = new();

	public float InnerEdge => Body.Type == BodyType.AsteroidField ? MajorAxis - Body.BandWidth / 2f : MajorAxis - Body.Diameter / 2f;
	public float OuterEdge => Body.Type == BodyType.AsteroidField ? MajorAxis + Body.BandWidth / 2f : MajorAxis + Body.Diameter / 2f;
}

public sealed class CentralStar
{
	public bool IsBinary;
	public string Classification = "";
	public string? SecondaryClassification;
	public int Diameter;
	public int SecondaryDiameter;
	public float Mass;
	public float SecondaryMass;
	public float BinaryOrbitAngle;
	public float BinaryOrbitSpeed;
	public float PrimaryOrbitRadius;
	public float SecondaryOrbitRadius;

	public bool PrimaryIsBlackHole => Classification == SolarSystem.BlackHole;
	public bool SecondaryIsBlackHole => SecondaryClassification == SolarSystem.BlackHole;

	public float Extent => IsBinary
		? Math.Max(PrimaryOrbitRadius + Diameter / 2f, SecondaryOrbitRadius + SecondaryDiameter / 2f)
		: Diameter / 2f;
}

public enum ManeuverPhase { None, RotateOut, Thrust, RotateBack }

public sealed class Ship
{
	public float MajorAxis;
	public float OrbitAngle;
	public float OrbitSpeed;
	public int GapIndex;
	public float HeadingOffset;

	public ManeuverPhase Phase;
	public float PhaseTime;
	public float FromAxis;
	public float ToAxis;
	public int TargetGap;
	public bool Inward;

	public bool Maneuvering => Phase != ManeuverPhase.None;
	public bool Thrusting => Phase == ManeuverPhase.Thrust;
}

/// <summary>
/// A procedurally generated star system, following the same rules as the State Your Name game:
/// 4–7 orbital shells around a single star, a binary pair, or a black hole, with the ship
/// parked in a gap between shells.
/// </summary>
public sealed class SolarSystem
{
	public const string BlackHole = "BLACK HOLE";

	static readonly string[] StarClassifications = { "ORANGE DWARF", "YELLOW DWARF", "BLUE-WHITE STAR", "RED GIANT" };
	static readonly string[] SmallStarClassifications = { "BROWN DWARF", "RED DWARF", "WHITE DWARF", "ORANGE DWARF" };

	const float BaseRingOffset = 52;
	const float RingSpacing = 34;
	const float OrbitalSpeedConstant = 960;

	/// <summary>Seconds the engines burn to cross one orbital gap.</summary>
	public const float ShipMoveDuration = 15;
	const float ShipRotateDuration = 2;

	public CentralStar Star = new();
	public List<Ring> Rings = new();
	public Ship Ship = new();
	public string Designation = "";

	readonly Random _random;

	SolarSystem(Random random) => _random = random;

	float Float(float min, float max) => min + (max - min) * (float)_random.NextDouble();
	int Int(int min, int max) => (int)Math.Floor(Float(min, max + 1));
	T Pick<T>(IReadOnlyList<T> values) => values[_random.Next(values.Count)];

	static float OrbitSpeedFor(float majorAxis) => OrbitalSpeedConstant / majorAxis;

	public static SolarSystem Generate(Random random)
	{
		var system = new SolarSystem(random);
		system.Build();
		return system;
	}

	void Build()
	{
		Designation = $"{(char)('A' + _random.Next(26))}{(char)('A' + _random.Next(26))}-{Int(1000, 9999)}";

		int ringCount = Int(4, 7);
		bool binary = _random.NextDouble() > 0.72;
		Star = binary
			? (_random.NextDouble() > 0.88 ? BinaryWithBlackHole() : BinaryStar())
			: (_random.NextDouble() > 0.80 ? CentralBlackHole() : PrimaryStar());

		float extent = Star.Extent;
		int forcedAsteroidIndex = Int(1, ringCount - 1);
		for (int i = 0; i < ringCount; i++)
		{
			float majorAxis = extent + BaseRingOffset + i * RingSpacing + Int(-2, 6);
			var type = i == forcedAsteroidIndex ? BodyType.AsteroidField : WeightedType();
			Rings.Add(new Ring { Index = i, MajorAxis = majorAxis, Body = CreateBody(type, OrbitSpeedFor(majorAxis)) });
		}

		int gap = Int(0, ringCount - 1);
		float axis = GapAxis(gap, Float(0.35f, 0.65f));
		Ship = new Ship
		{
			MajorAxis = axis,
			OrbitAngle = Float(0, 360),
			OrbitSpeed = OrbitSpeedFor(axis) * Float(0.85f, 1.15f),
			GapIndex = gap
		};
	}

	BodyType WeightedType()
	{
		double roll = _random.NextDouble();
		if (roll < 0.40) return BodyType.RockyPlanet;
		if (roll < 0.63) return BodyType.GasGiant;
		if (roll < 0.78) return BodyType.AsteroidField;
		if (roll < 0.90) return BodyType.Star;
		return BodyType.BlackHole;
	}

	Body CreateBody(BodyType type, float orbitSpeed)
	{
		var body = new Body { Type = type, OrbitAngle = Float(0, 360), OrbitSpeed = orbitSpeed, Diameter = Int(8, 18) };
		switch (type)
		{
			case BodyType.RockyPlanet:
				body.Diameter = Int(6, 18);
				body.HasRing = _random.NextDouble() > 0.75;
				body.Moons = Int(0, 8);
				body.Mass = Int(1, 12);
				break;
			case BodyType.GasGiant:
				body.Diameter = Int(18, 34);
				body.HasRing = _random.NextDouble() > 0.35;
				body.Moons = Int(0, 20);
				body.Mass = Int(200, 4000);
				break;
			case BodyType.Star:
				body.Diameter = Int(10, 18);
				body.Classification = Pick(SmallStarClassifications);
				body.Moons = Int(0, 3);
				body.Mass = MathF.Round(Float(0.30f, 0.90f), 2);
				break;
			case BodyType.BlackHole:
				body.Diameter = Int(8, 16);
				body.Mass = Int(2, 6);
				break;
			case BodyType.AsteroidField:
				body.BandWidth = Int(12, 20);
				int count = Int(28, 44);
				for (int i = 0; i < count; i++)
				{
					body.Asteroids.Add(new Asteroid
					{
						OrbitAngle = Float(0, 360),
						OrbitSpeed = orbitSpeed * Float(0.72f, 1.28f),
						RadialOffset = Float(-9, 9),
						WobbleAngle = Float(0, 360),
						WobbleSpeed = Float(6, 16),
						WobbleRange = Float(0.5f, 2.4f),
						Size = Float(0.7f, 1.8f),
						Opacity = Float(0.35f, 0.85f)
					});
				}
				break;
		}
		return body;
	}

	CentralStar PrimaryStar() => new() { Classification = Pick(StarClassifications), Diameter = Int(72, 126), Mass = Int(20, 150) };

	CentralStar CentralBlackHole() => new() { Classification = BlackHole, Diameter = Int(24, 40), Mass = Int(10, 100) };

	CentralStar BinaryStar()
	{
		int primary = Int(58, 94);
		int secondary = Int(30, Math.Max(38, primary - 8));
		int primaryMass = Int(20, 150);
		return Binary(Pick(StarClassifications), Pick(StarClassifications), primary, secondary, primaryMass, Int(10, primaryMass));
	}

	CentralStar BinaryWithBlackHole()
	{
		int hole = Int(22, 34), star = Int(44, 82);
		bool holeIsPrimary = _random.NextDouble() > 0.5;
		int holeMass = Int(10, 100), starMass = Int(20, 150);
		return holeIsPrimary
			? Binary(BlackHole, Pick(StarClassifications), hole, star, holeMass, starMass)
			: Binary(Pick(StarClassifications), BlackHole, star, hole, starMass, holeMass);
	}

	CentralStar Binary(string primaryClass, string secondaryClass, int primary, int secondary, float primaryMass, float secondaryMass)
	{
		int minimum = (int)Math.Ceiling(primary / 2f + secondary / 2f + 18);
		int separation = Int(minimum, minimum + 18);
		float primaryRadius = separation * secondary / (float)(primary + secondary);
		return new CentralStar
		{
			IsBinary = true,
			Classification = primaryClass,
			SecondaryClassification = secondaryClass,
			Diameter = primary,
			SecondaryDiameter = secondary,
			Mass = primaryMass,
			SecondaryMass = secondaryMass,
			BinaryOrbitAngle = Float(0, 360),
			BinaryOrbitSpeed = 780f / separation,
			PrimaryOrbitRadius = primaryRadius,
			SecondaryOrbitRadius = separation - primaryRadius
		};
	}

	/// <summary>Orbit radius at fraction t across a gap (gap 0 is between the star and the first shell).</summary>
	public float GapAxis(int gap, float t = 0.5f)
	{
		float inner, outer;
		if (gap == 0) { inner = Star.Extent; outer = Rings[0].InnerEdge; }
		else if (gap >= Rings.Count) { inner = Rings[^1].OuterEdge; outer = inner + RingSpacing; }
		else { inner = Rings[gap - 1].OuterEdge; outer = Rings[gap].InnerEdge; }
		return inner + Math.Max(outer - inner, 4) * t;
	}

	/// <summary>Starts a burn to an adjacent gap. Inward burns turn retrograde first, like the game.</summary>
	public bool BeginManeuver(int direction)
	{
		var ship = Ship;
		int target = ship.GapIndex + direction;
		if (ship.Maneuvering || target < 0 || target > Rings.Count - 1) return false;

		ship.TargetGap = target;
		ship.FromAxis = ship.MajorAxis;
		ship.ToAxis = GapAxis(target);
		ship.Inward = direction < 0;
		ship.Phase = ship.Inward ? ManeuverPhase.RotateOut : ManeuverPhase.Thrust;
		ship.PhaseTime = 0;
		return true;
	}

	public void Advance(float dt)
	{
		if (Star.IsBinary) Star.BinaryOrbitAngle = Wrap(Star.BinaryOrbitAngle + Star.BinaryOrbitSpeed * dt);

		foreach (var ring in Rings)
		{
			var body = ring.Body;
			body.OrbitAngle = Wrap(body.OrbitAngle + body.OrbitSpeed * dt);
			foreach (var a in body.Asteroids)
			{
				a.OrbitAngle = Wrap(a.OrbitAngle + a.OrbitSpeed * dt);
				a.WobbleAngle = Wrap(a.WobbleAngle + a.WobbleSpeed * dt);
			}
		}

		var ship = Ship;
		ship.OrbitAngle = Wrap(ship.OrbitAngle + ship.OrbitSpeed * dt);
		if (!ship.Maneuvering) return;

		ship.PhaseTime += dt;
		switch (ship.Phase)
		{
			case ManeuverPhase.RotateOut:
				ship.HeadingOffset = 180 * Smooth(ship.PhaseTime / ShipRotateDuration);
				if (ship.PhaseTime >= ShipRotateDuration) Next(ManeuverPhase.Thrust);
				break;
			case ManeuverPhase.Thrust:
				float t = Smooth(ship.PhaseTime / ShipMoveDuration);
				ship.MajorAxis = ship.FromAxis + (ship.ToAxis - ship.FromAxis) * t;
				ship.OrbitSpeed = OrbitSpeedFor(ship.MajorAxis);
				if (ship.PhaseTime >= ShipMoveDuration)
				{
					ship.GapIndex = ship.TargetGap;
					Next(ship.Inward ? ManeuverPhase.RotateBack : ManeuverPhase.None);
				}
				break;
			case ManeuverPhase.RotateBack:
				ship.HeadingOffset = 180 * (1 - Smooth(ship.PhaseTime / ShipRotateDuration));
				if (ship.PhaseTime >= ShipRotateDuration) Next(ManeuverPhase.None);
				break;
		}

		void Next(ManeuverPhase phase)
		{
			ship.Phase = phase;
			ship.PhaseTime = 0;
			if (phase == ManeuverPhase.None) ship.HeadingOffset = 0;
		}
	}

	/// <summary>Rings touching the ship's current gap: what it could mine.</summary>
	public IEnumerable<Ring> AdjacentRings()
	{
		int gap = Ship.GapIndex;
		return Rings.Where(r => r.Index == gap - 1 || r.Index == gap);
	}

	public float MaxExtent() => Math.Max(Star.Extent, Rings.Max(r =>
		r.Body.Type == BodyType.AsteroidField
			? r.MajorAxis + Math.Max(r.Body.BandWidth / 2f, r.Body.Asteroids.Max(a => Math.Abs(a.RadialOffset) + a.WobbleRange + a.Size))
			: r.MajorAxis + r.Body.Diameter / 2f));

	static float Wrap(float angle) => ((angle % 360) + 360) % 360;
	static float Smooth(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
}
