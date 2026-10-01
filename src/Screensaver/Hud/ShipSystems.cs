using System;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.Hud;

/// <summary>OXYGEN / POWER / FUEL / SCRAP / HULL readouts with segmented bars.</summary>
public sealed class ShipSystems
{
	public sealed class Gauge
	{
		public required string Name;
		public float Value;
		public float Max = 100;
		public bool Percent;
		public float Rate;       // per second, shown as the +n/-n delta
		public float Shown;      // eased value for display
	}

	public readonly Gauge Oxygen = new() { Name = "OXYGEN", Value = 17, Percent = true, Rate = 0.12f };
	public readonly Gauge Power = new() { Name = "POWER", Value = 23, Rate = 0.1f };
	public readonly Gauge Fuel = new() { Name = "FUEL", Value = 41, Rate = -0.02f };
	public readonly Gauge Scrap = new() { Name = "SCRAP", Value = 0 };
	public readonly Gauge Hull = new() { Name = "HULL", Value = 69, Percent = true };

	Gauge[] All => new[] { Oxygen, Power, Fuel, Scrap, Hull };

	public void Update(float dt)
	{
		foreach (var g in All)
		{
			g.Value = Math.Clamp(g.Value + g.Rate * dt, 0, g.Max);
			g.Shown += (g.Value - g.Shown) * Math.Min(1, dt * 2.5f);
		}
		// Systems settle into a slow equilibrium rather than pinning at the ends.
		if (Oxygen.Value > 92) Oxygen.Rate = -0.05f; else if (Oxygen.Value < 35) Oxygen.Rate = 0.12f;
		if (Power.Value > 88) Power.Rate = -0.04f; else if (Power.Value < 30) Power.Rate = 0.1f;
		if (Fuel.Value < 12) Fuel.Rate = 0.08f; else if (Fuel.Value > 70) Fuel.Rate = -0.02f;
	}

	public void Draw(Prim p, Text text, Vector2 at, float width, float fade)
	{
		const float size = 19, rowHeight = 34;
		float cell = text.Cell(size);
		float barX = at.X + cell * 8;
		float barWidth = width * 0.42f;
		float y = at.Y;
		foreach (var g in All)
		{
			text.Draw(g.Name, new Vector2(at.X, y), size, Palette.Bright * fade);
			text.Draw("[", new Vector2(barX - cell * 0.9f, y), size, Palette.Bright * (0.8f * fade));
			text.Draw("]", new Vector2(barX + barWidth + cell * 0.15f, y), size, Palette.Bright * (0.8f * fade));

			const int cells = 10;
			float cellWidth = barWidth / cells;
			float filled = g.Shown / g.Max * cells;
			for (int i = 0; i < cells; i++)
			{
				var cellPos = new Vector2(barX + i * cellWidth + 1, y + size * 0.22f);
				var cellSize = new Vector2(cellWidth - 2, size * 0.78f);
				p.RectF(cellPos, cellSize, Palette.Bright * (0.10f * fade));
				float f = Math.Clamp(filled - i, 0, 1);
				if (f > 0) p.RectF(cellPos, new Vector2(cellSize.X * f, cellSize.Y), Palette.Bright * (0.88f * fade));
			}

			string value = g.Percent ? $"{(int)MathF.Round(g.Shown)}%" : $"{(int)MathF.Round(g.Shown)}/{g.Max:0}";
			float valueRight = at.X + width - cell * 3.6f;
			text.Draw(value, new Vector2(valueRight - text.Measure(value, size).X, y), size, Palette.Bright * fade);

			int delta = (int)MathF.Round(g.Rate * 10);
			if (delta != 0)
			{
				string d = delta > 0 ? $"+{delta}" : $"{delta}";
				var color = delta > 0 ? Palette.Bright : Palette.Red;
				text.Draw(d, new Vector2(at.X + width - text.Measure(d, 15).X, y + 2), 15, color * fade);
			}
			y += rowHeight;
		}
	}
}
