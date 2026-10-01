using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StateYourNameScreensaver.Rendering;

/// <summary>
/// Vector-style drawing on top of SpriteBatch: lines, circles, ellipses, and soft glows,
/// all from three generated textures (no content pipeline needed).
/// </summary>
public sealed class Prim
{
	readonly SpriteBatch _batch;
	readonly Texture2D _pixel;
	readonly Texture2D _disc;
	readonly Texture2D _glow;

	/// <summary>Screen pixels per design unit, so hairlines stay at least one pixel wide.</summary>
	public float PixelScale { get; set; } = 1;

	public Prim(GraphicsDevice device, SpriteBatch batch)
	{
		_batch = batch;
		_pixel = new Texture2D(device, 1, 1);
		_pixel.SetData(new[] { Color.White });
		_disc = Radial(device, 256, d => Math.Clamp((1 - d) * 128, 0, 1));          // antialiased solid disc
		_glow = Radial(device, 256, d => d >= 1 ? 0 : MathF.Pow(1 - d, 2.2f));       // soft falloff
	}

	public SpriteBatch Batch => _batch;
	public Texture2D Pixel => _pixel;

	static Texture2D Radial(GraphicsDevice device, int size, Func<float, float> alphaAt)
	{
		var data = new Color[size * size];
		float c = (size - 1) / 2f;
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
			float a = alphaAt(d);
			data[y * size + x] = new Color(a, a, a, a); // premultiplied white
		}
		var texture = new Texture2D(device, size, size);
		texture.SetData(data);
		return texture;
	}

	float Hair(float thickness) => Math.Max(thickness, 1f / PixelScale);

	public void Line(Vector2 a, Vector2 b, Color color, float thickness = 1)
	{
		var delta = b - a;
		float length = delta.Length();
		if (length < 0.0001f) return;
		float angle = MathF.Atan2(delta.Y, delta.X);
		float t = Hair(thickness);
		_batch.Draw(_pixel, a, null, color, angle, new Vector2(0, 0.5f), new Vector2(length, t), SpriteEffects.None, 0);
	}

	public void Rect(Rectangle r, Color color) => _batch.Draw(_pixel, r, color);

	public void RectF(Vector2 position, Vector2 size, Color color) =>
		_batch.Draw(_pixel, position, null, color, 0, Vector2.Zero, size, SpriteEffects.None, 0);

	public void Frame(Vector2 position, Vector2 size, Color color, float thickness = 1)
	{
		var tl = position; var tr = position + new Vector2(size.X, 0);
		var bl = position + new Vector2(0, size.Y); var br = position + size;
		Line(tl, tr, color, thickness); Line(tr, br, color, thickness);
		Line(br, bl, color, thickness); Line(bl, tl, color, thickness);
	}

	int Segments(float radius) => Math.Clamp((int)(radius * PixelScale * 0.6f), 24, 360);

	public void Circle(Vector2 center, float radius, Color color, float thickness = 1) =>
		Arc(center, radius, radius, 0, 0, MathHelper.TwoPi, color, thickness);

	/// <summary>Dashed circle; dash/gap are in design units along the circumference.</summary>
	public void DashedCircle(Vector2 center, float radius, Color color, float thickness, float dash, float gap, float phase = 0)
	{
		float circumference = MathHelper.TwoPi * radius;
		float step = dash + gap;
		for (float s = -(phase % step); s < circumference; s += step)
		{
			float a0 = Math.Max(s, 0) / radius, a1 = Math.Min(s + dash, circumference) / radius;
			if (a1 > a0) Arc(center, radius, radius, 0, a0, a1, color, thickness, 3);
		}
	}

	/// <summary>
	/// Rotated ellipse arc. If <paramref name="keep"/> is given, only segments whose midpoint
	/// passes it are drawn (used for front/back halves of rings and accretion disks).
	/// </summary>
	public void Arc(Vector2 center, float rx, float ry, float rotation, float from, float to, Color color, float thickness = 1,
		int? segments = null, Func<Vector2, bool>? keep = null)
	{
		int n = segments ?? Math.Max(3, (int)(Segments(Math.Max(rx, ry)) * (to - from) / MathHelper.TwoPi));
		float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
		Vector2 At(float t)
		{
			float x = MathF.Cos(t) * rx, y = MathF.Sin(t) * ry;
			return center + new Vector2(x * cos - y * sin, x * sin + y * cos);
		}
		var prev = At(from);
		for (int i = 1; i <= n; i++)
		{
			var next = At(from + (to - from) * i / n);
			if (keep == null || keep((prev + next) / 2)) Line(prev, next, color, thickness);
			prev = next;
		}
	}

	public void Ellipse(Vector2 center, float rx, float ry, float rotation, Color color, float thickness = 1, Func<Vector2, bool>? keep = null) =>
		Arc(center, rx, ry, rotation, 0, MathHelper.TwoPi, color, thickness, null, keep);

	public void Disc(Vector2 center, float radius, Color color) =>
		_batch.Draw(_disc, center, null, color, 0, new Vector2(128, 128), radius / 127.5f, SpriteEffects.None, 0);

	/// <summary>Filled, rotated ellipse.</summary>
	public void DiscEllipse(Vector2 center, float rx, float ry, float rotation, Color color) =>
		_batch.Draw(_disc, center, null, color, rotation, new Vector2(128, 128), new Vector2(rx, ry) / 127.5f, SpriteEffects.None, 0);

	/// <summary>Filled rectangle rotated about its center.</summary>
	public void RotatedRect(Vector2 center, Vector2 size, float rotation, Color color) =>
		_batch.Draw(_pixel, center, null, color, rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0);

	public void Glow(Vector2 center, float radius, Color color) =>
		_batch.Draw(_glow, center, null, color, 0, new Vector2(128, 128), radius / 127.5f, SpriteEffects.None, 0);

	/// <summary>A soft band (like an asteroid belt halo) built from concentric hairlines.</summary>
	public void Band(Vector2 center, float radius, float halfWidth, Color color, float coreOpacity, float edgeOpacity)
	{
		int steps = Math.Clamp((int)(halfWidth * 2 * PixelScale / 1.5f), 6, 80);
		float stepWidth = halfWidth * 2 / steps;
		for (int i = 0; i <= steps; i++)
		{
			float offset = -halfWidth + i * stepWidth;
			float d = Math.Abs(offset) / halfWidth;               // 0 at core, 1 at edge
			float alpha = d < 0.3f ? coreOpacity : MathHelper.Lerp(coreOpacity, edgeOpacity, (d - 0.3f) / 0.7f) * (1 - d * d * 0.6f);
			Circle(center, radius + offset, color * alpha, stepWidth * 1.15f);
		}
	}
}
