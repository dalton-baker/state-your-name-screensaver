using System;
using System.Linq;
using StateYourNameScreensaver.Hud;
using StateYourNameScreensaver.Rendering;
using StateYourNameScreensaver.World;

namespace StateYourNameScreensaver;

public enum Stage { Boot, Scan, Cruise, Jump }

/// <summary>
/// Runs the show: boot sequence, then an endless loop of arriving in a system, sweeping it with
/// sensors, cruising (orbit changes, mining, ship chatter), and jumping to the next one.
/// </summary>
public sealed class Director
{
	public const float BootDuration = 9;
	const float ScanDuration = 6;
	const float JumpDuration = 7;
	const float ExhaustPeriod = 10, ExhaustOn = 2, ManeuverBuffer = 15;

	readonly Random _random;
	public readonly ShipLog Log = new();
	public readonly ShipSystems Systems = new();
	public readonly Starfield Stars;
	public SolarSystem System;

	public Stage Stage { get; private set; } = Stage.Boot;
	public float StageTime { get; private set; }
	public int SystemsVisited { get; private set; } = 1;
	public float Clock { get; private set; }

	float _cruiseLength;
	float _nextEvent;
	float _lastManeuverEnd = -999;
	int _scanned;
	bool _jumped;
	int _nameAttempts;

	public Director(Random random)
	{
		_random = random;
		Stars = new Starfield(random);
		System = SolarSystem.Generate(random);
	}

	public bool ExhaustPulse =>
		!System.Ship.Maneuvering && Clock - _lastManeuverEnd > ManeuverBuffer && Clock % ExhaustPeriod < ExhaustOn;

	/// <summary>0..1 sensor reveal of the current system.</summary>
	public float Reveal => Stage switch
	{
		Stage.Boot => 0,
		Stage.Scan => Math.Clamp(StageTime / (ScanDuration - 1), 0, 1),
		_ => 1
	};

	/// <summary>0..1 visibility of the system view (fades out into a jump and back in after).</summary>
	public float ViewFade => Stage switch
	{
		Stage.Boot => 0,
		Stage.Scan => Math.Clamp(StageTime / 1.2f, 0, 1),
		Stage.Jump => Math.Clamp(1 - (StageTime - 1.5f) / 1.2f, 0, 1),
		_ => 1
	};

	/// <summary>Zoom applied to the system view while jumping away.</summary>
	public float ViewZoom => Stage == Stage.Jump ? 1 + MathF.Pow(Math.Clamp((StageTime - 1.5f) / 1.5f, 0, 1), 2) * 1.8f : 1;

	/// <summary>White flash at the moment of the jump.</summary>
	public float Flash => Stage == Stage.Jump ? Math.Max(0, 1 - Math.Abs(StageTime - 4.6f) / 0.35f) * 0.55f : 0;

	public float HudFade => Stage == Stage.Boot ? Math.Clamp((StageTime - (BootDuration - 1.5f)) / 1.5f, 0, 1) : 1;

	public void Update(float dt)
	{
		Clock += dt;
		StageTime += dt;
		bool wasManeuvering = System.Ship.Maneuvering;
		System.Advance(dt);
		if (wasManeuvering && !System.Ship.Maneuvering) ManeuverComplete();
		Systems.Update(dt);
		Log.Update(dt);
		Stars.Update(dt);

		switch (Stage)
		{
			case Stage.Boot:
				if (StageTime >= BootDuration) Enter(Stage.Scan);
				break;
			case Stage.Scan:
				ScanStep();
				if (StageTime >= ScanDuration) Enter(Stage.Cruise);
				break;
			case Stage.Cruise:
				if (StageTime >= _nextEvent && Log.Idle) { CruiseEvent(); _nextEvent = StageTime + Float(7, 13); }
				if (StageTime >= _cruiseLength && !System.Ship.Maneuvering && Log.Idle) Enter(Stage.Jump);
				break;
			case Stage.Jump:
				JumpStep();
				break;
		}
	}

	void Enter(Stage stage)
	{
		Stage = stage;
		StageTime = 0;
		switch (stage)
		{
			case Stage.Scan:
				_scanned = -1;
				Log.Add($"SYSTEM {System.Designation}: SENSOR SWEEP", Palette.Pale);
				break;
			case Stage.Cruise:
				_cruiseLength = Float(70, 100);
				_nextEvent = Float(4, 8);
				break;
			case Stage.Jump:
				_jumped = false;
				Log.Add("PLOTTING EXIT VECTOR...", Palette.Pale);
				Log.Detail("FTL core spooling");
				Systems.Fuel.Value = Math.Max(4, Systems.Fuel.Value - 9);
				break;
		}
	}

	void ScanStep()
	{
		// Report the star, then each shell as the sweep passes it.
		int due = (int)(Reveal * (System.Rings.Count + 1)) - 1;
		while (_scanned < due && _scanned < System.Rings.Count)
		{
			_scanned++;
			if (_scanned == 0) ReportStar();
			if (_scanned < System.Rings.Count && _scanned >= 0) ReportRing(System.Rings[_scanned]);
		}
	}

	void ReportStar()
	{
		var star = System.Star;
		if (star.IsBinary)
		{
			Log.Add($"BINARY: {star.Classification} + {star.SecondaryClassification}");
			Log.Detail($"barycenter {Math.Round(star.PrimaryOrbitRadius)} Mm");
		}
		else
		{
			Log.Add($"PRIMARY: {star.Classification}", star.PrimaryIsBlackHole ? Palette.Amber : null);
			Log.Detail($"diameter {star.Diameter} Mm, mass {star.Mass:0} sol");
		}
	}

	void ReportRing(Ring ring)
	{
		var b = ring.Body;
		string name = b.Type switch
		{
			BodyType.RockyPlanet => "ROCKY PLANET",
			BodyType.GasGiant => "GAS GIANT",
			BodyType.Star => b.Classification ?? "SMALLER STAR",
			BodyType.BlackHole => "BLACK HOLE",
			_ => "ASTEROID BELT"
		};
		Log.Add($"ORBIT {ring.Index + 1}: {name}", b.Type == BodyType.BlackHole ? Palette.Amber : null);
		string detail = b.Type switch
		{
			BodyType.AsteroidField => $"{b.Asteroids.Count} large objects",
			BodyType.BlackHole => $"mass {b.Mass:0} sol. keep clear",
			BodyType.Star => $"mass {b.Mass:0.00} sol, {b.Moons} moons",
			_ => $"{b.Moons} moons, mass {FormatEarthMass(b.Mass)}{(b.HasRing ? ", ringed" : "")}"
		};
		Log.Detail(detail);
	}

	static string FormatEarthMass(float mass) => mass >= 1000 ? $"{Math.Round(mass / 1000)}k earths" : $"{mass:0} earths";

	enum Event { Maneuver, Mine, Name, Meteoroid, Oxygen, Solar, Stasis, Repair, Status, Telemetry }

	Event? _lastEvent;

	void CruiseEvent()
	{
		var ship = System.Ship;
		var mineable = System.AdjacentRings().FirstOrDefault(r => r.Body.Type is BodyType.AsteroidField or BodyType.RockyPlanet);
		var choices = new (Event Event, float Weight)[]
		{
			(Event.Maneuver, !ship.Maneuvering && Clock - _lastManeuverEnd > ManeuverBuffer ? 3 : 0),
			(Event.Mine, mineable != null ? 2 : 0),
			(Event.Name, 1.6f),
			(Event.Meteoroid, 1),
			(Event.Oxygen, 1),
			(Event.Solar, 1),
			(Event.Stasis, 0.8f),
			(Event.Repair, Systems.Hull.Value < 90 ? 1 : 0),
			(Event.Status, 0.8f),
			(Event.Telemetry, 1.2f),
		};
		float total = choices.Where(c => c.Event != _lastEvent).Sum(c => c.Weight);
		float roll = Float(0, total);
		var pick = Event.Status;
		foreach (var (e, w) in choices)
		{
			if (e == _lastEvent || w <= 0) continue;
			if ((roll -= w) <= 0) { pick = e; break; }
		}
		_lastEvent = pick;

		switch (pick)
		{
			case Event.Maneuver:
				int direction = ship.GapIndex == 0 ? 1 : ship.GapIndex >= System.Rings.Count - 1 ? -1 : (_random.Next(2) == 0 ? -1 : 1);
				if (!System.BeginManeuver(direction)) goto default;
				Log.Add($"ORBIT CHANGE: {(direction < 0 ? "INWARD" : "OUTWARD")} TO GAP {ship.TargetGap + 1}", Palette.Pale);
				Log.Detail(direction < 0 ? "turning retrograde. main engine burn 15s" : "main engine burn 15s");
				Systems.Fuel.Value = Math.Max(0, Systems.Fuel.Value - 5);
				break;
			case Event.Mine:
				int scrap = _random.Next(2, 7);
				Systems.Scrap.Value = Math.Min(100, Systems.Scrap.Value + scrap);
				Log.Add(mineable!.Body.Type == BodyType.AsteroidField ? "MINING DRONES DEPLOYED" : "SURFACE SALVAGE SWEEP");
				Log.Detail($"orbit {mineable.Index + 1}: recovered {scrap} scrap");
				break;
			case Event.Name:
				_nameAttempts++;
				Log.Add("CAPTAIN, PLEASE STATE YOUR NAME.", Palette.Pale);
				Log.Detail(_nameAttempts % 3 == 0 ? "voice input: \"...nnngh.\" name not recognized" : "voice input: no response");
				break;
			case Event.Meteoroid:
				Systems.Hull.Value = Math.Max(10, Systems.Hull.Value - 2);
				Log.Add("MICROMETEOROID IMPACT", Palette.Amber);
				Log.Detail("hull integrity -2%");
				break;
			case Event.Oxygen:
				Log.Add("OXYGEN RECYCLER CYCLE COMPLETE");
				Log.Detail("scrubbers nominal");
				break;
			case Event.Solar:
				int power = _random.Next(2, 5);
				Systems.Power.Value = Math.Min(100, Systems.Power.Value + power);
				Log.Add("SOLAR COLLECTORS ALIGNED");
				Log.Detail($"power +{power}");
				break;
			case Event.Stasis:
				Log.Add("STASIS BAY: ALL PODS OCCUPIED");
				Log.Detail("crew manifest: unavailable");
				break;
			case Event.Repair:
				Systems.Hull.Value = Math.Min(100, Systems.Hull.Value + 3);
				Log.Add("REPAIR DRONE PATCHED HULL PLATING");
				Log.Detail("hull integrity +3%");
				break;
			case Event.Telemetry:
				var ring = System.Rings[_random.Next(System.Rings.Count)];
				Log.Add($"TELEMETRY: ORBIT {ring.Index + 1}");
				Log.Detail($"period {Math.Round(360 / ring.Body.OrbitSpeed)}s, radius {Math.Round(ring.MajorAxis)} Mm");
				break;
			default:
				Log.Add($"ORBIT STABLE. RADIUS {Math.Round(System.Ship.MajorAxis)} Mm");
				break;
		}
	}

	void ManeuverComplete()
	{
		_lastManeuverEnd = Clock;
		Log.Add($"ORBIT STABLE. RADIUS {Math.Round(System.Ship.MajorAxis)} Mm");
	}

	void JumpStep()
	{
		Stars.Warp = StageTime switch
		{
			< 1.5f => 0,
			< 4.6f => (StageTime - 1.5f) / 3.1f,
			_ => Math.Max(0, 1 - (StageTime - 4.6f) / 1.6f)
		};

		if (!_jumped && StageTime >= 4.6f)
		{
			_jumped = true;
			System = SolarSystem.Generate(_random);
			SystemsVisited++;
		}

		if (StageTime >= JumpDuration)
		{
			Stars.Warp = 0;
			Log.Add($"JUMP COMPLETE. SYSTEMS CHARTED: {SystemsVisited}", Palette.Pale);
			Enter(Stage.Scan);
		}
	}

	float Float(float min, float max) => min + (max - min) * (float)_random.NextDouble();
}
