using System;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.World;

/// <summary>Faint background stars that streak outward during an FTL jump.</summary>
public sealed class Starfield
{
	struct Star { public Vector2 Direction; public float Distance; public float Brightness; public float Twinkle; }

	readonly Star[] _stars;
	readonly Random _random;
	readonly float _maxDistance;

	/// <summary>0 = at rest, 1 = full warp.</summary>
	public float Warp;

	public Starfield(Random random, int count = 420, float maxDistance = 1500)
	{
		_random = random;
		_maxDistance = maxDistance;
		_stars = new Star[count];
		for (int i = 0; i < count; i++) _stars[i] = Spawn(spread: true);
	}

	Star Spawn(bool spread)
	{
		float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
		return new Star
		{
			Direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)),
			Distance = spread ? MathF.Sqrt((float)_random.NextDouble()) * _maxDistance : 30 + (float)_random.NextDouble() * 120,
			Brightness = 0.12f + (float)_random.NextDouble() * 0.45f,
			Twinkle = (float)(_random.NextDouble() * 10)
		};
	}

	public void Update(float dt)
	{
		if (Warp <= 0.001f) return;
		for (int i = 0; i < _stars.Length; i++)
		{
			ref var s = ref _stars[i];
			s.Distance += dt * Warp * Warp * (120 + s.Distance * 2.6f);
			if (s.Distance > _maxDistance) s = Spawn(spread: false);
		}
	}

	public void Draw(Prim p, Vector2 center, float time, float fade)
	{
		foreach (var s in _stars)
		{
			var at = center + s.Direction * s.Distance;
			float twinkle = 0.75f + 0.25f * MathF.Sin(time * 1.7f + s.Twinkle);
			float alpha = s.Brightness * twinkle * fade;
			if (Warp > 0.05f)
			{
				float length = Warp * Warp * (8 + s.Distance * 0.35f);
				p.Line(at - s.Direction * length, at, Palette.Pale * Math.Min(1, alpha * (1 + Warp * 2)), 1.2f + Warp);
			}
			else
			{
				p.Disc(at, 1.1f, Palette.Pale * alpha);
			}
		}
	}
}
