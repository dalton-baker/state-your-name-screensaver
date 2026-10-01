using System;
using System.IO;
using System.Reflection;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StateYourNameScreensaver.Rendering;

/// <summary>
/// Monospace terminal text. Uses Windows' Courier New (the game's font) when available,
/// otherwise the bundled Courier Prime. Glyphs are rasterized at the real on-screen size so
/// text stays crisp under the design-space transform.
/// </summary>
public sealed class Text
{
	readonly FontSystem _fonts;
	readonly SpriteBatch _batch;

	/// <summary>Screen pixels per design unit.</summary>
	public float PixelScale { get; set; } = 1;

	public Text(SpriteBatch batch)
	{
		_batch = batch;
		_fonts = new FontSystem(new FontSystemSettings { FontResolutionFactor = 1, KernelWidth = 0, KernelHeight = 0 });
		_fonts.AddFont(LoadFont());
	}

	static byte[] LoadFont()
	{
		var windowsFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cour.ttf");
		if (OperatingSystem.IsWindows() && File.Exists(windowsFont)) return File.ReadAllBytes(windowsFont);

		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CourierPrime-Regular.ttf")
			?? throw new InvalidOperationException("Embedded font missing");
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}

	SpriteFontBase Font(float size) => _fonts.GetFont(MathF.Max(6, MathF.Round(size * PixelScale)));

	public Vector2 Measure(string text, float size, float spacing = 0) =>
		Font(size).MeasureString(text, characterSpacing: spacing * PixelScale) / PixelScale;

	/// <summary>Draws text; <paramref name="spacing"/> is extra letter spacing in design units.</summary>
	public void Draw(string text, Vector2 position, float size, Color color, float spacing = 0)
	{
		if (string.IsNullOrEmpty(text)) return;
		Font(size).DrawText(_batch, text, position, color, scale: new Vector2(1 / PixelScale), characterSpacing: spacing * PixelScale);
	}

	/// <summary>Width of one character cell, for monospace layout.</summary>
	public float Cell(float size) => Measure("M", size).X;
}
