using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.Hud;

/// <summary>A scrolling log that types each line out, terminal style.</summary>
public sealed class ShipLog
{
	sealed class Line
	{
		public required string Text;
		public required Color Color;
		public bool Detail;
		public float Typed;
	}

	const float CharsPerSecond = 38;
	readonly List<Line> _lines = new();
	readonly Queue<Line> _pending = new();

	public void Add(string text, Color? color = null) =>
		_pending.Enqueue(new Line { Text = text, Color = color ?? Palette.Bright });

	/// <summary>A dimmer, indented sub-line.</summary>
	public void Detail(string text) =>
		_pending.Enqueue(new Line { Text = "  " + text, Color = Palette.Bright * 0.62f, Detail = true });

	public void Clear() { _lines.Clear(); _pending.Clear(); }

	public bool Idle => _pending.Count == 0 && (_lines.Count == 0 || _lines[^1].Typed >= _lines[^1].Text.Length);

	public void Update(float dt)
	{
		float budget = dt * CharsPerSecond;
		while (budget > 0)
		{
			if (_lines.Count > 0 && _lines[^1].Typed < _lines[^1].Text.Length)
			{
				var line = _lines[^1];
				float take = Math.Min(budget, line.Text.Length - line.Typed);
				line.Typed += take;
				budget -= take;
			}
			else if (_pending.Count > 0)
			{
				_lines.Add(_pending.Dequeue());
				budget -= 4; // a beat between lines
			}
			else break;
		}
		if (_lines.Count > 60) _lines.RemoveRange(0, _lines.Count - 60);
	}

	public void Draw(Text text, Vector2 at, Vector2 size, float time, float fade)
	{
		const float textSize = 19, detailSize = 16.5f;
		float lineHeight(Line l) => (l.Detail ? detailSize : textSize) * 1.5f;

		// Wrap to the panel, then show as many of the newest lines as fit.
		float cell = text.Cell(textSize);
		int columns = Math.Max(10, (int)(size.X / cell));
		var rows = new List<(string Text, Line Source)>();
		foreach (var line in _lines)
		{
			string shown = line.Text[..(int)Math.Min(line.Text.Length, line.Typed)];
			foreach (var row in Wrap(shown, line.Detail ? (int)(columns * textSize / detailSize) : columns))
				rows.Add((row, line));
		}

		float y = at.Y + size.Y;
		int first = rows.Count;
		while (first > 0 && y - lineHeight(rows[first - 1].Source) >= at.Y) y -= lineHeight(rows[--first].Source);

		float cursorX = at.X, cursorY = at.Y;
		for (int i = first; i < rows.Count; i++)
		{
			var (row, source) = rows[i];
			float sizeFor = source.Detail ? detailSize : textSize;
			text.Draw(row, new Vector2(at.X, y), sizeFor, source.Color * fade);
			cursorX = at.X + text.Measure(row, sizeFor).X + 3;
			cursorY = y;
			y += lineHeight(source);
		}

		if ((int)(time * 2) % 2 == 0)
			text.Draw("_", new Vector2(cursorX, cursorY), textSize, Palette.Bright * fade);
	}

	static IEnumerable<string> Wrap(string text, int columns)
	{
		if (text.Length <= columns) { yield return text; yield break; }
		int start = 0;
		while (start < text.Length)
		{
			int end = Math.Min(start + columns, text.Length);
			if (end < text.Length)
			{
				int space = text.LastIndexOf(' ', end - 1, end - start);
				if (space > start) end = space;
			}
			yield return text[start..end].TrimEnd();
			start = end;
			while (start < text.Length && text[start] == ' ') start++;
		}
	}
}
