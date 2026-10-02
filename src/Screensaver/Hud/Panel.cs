using Microsoft.Xna.Framework;
using StateYourNameScreensaver.Rendering;

namespace StateYourNameScreensaver.Hud;

/// <summary>A bordered terminal panel with a spaced-out title and rule, like the game's UI.</summary>
public static class Panel
{
	public const float TitleSize = 15;
	public const float Padding = 20;

	/// <returns>The top-left of the panel's content area.</returns>
	public static Vector2 Draw(Prim p, Text text, Vector2 position, Vector2 size, string title, float fade = 1)
	{
		p.RectF(position, size, Color.Black * (0.55f * fade));
		p.Frame(position, size, Palette.Frame * fade, 1.2f);
		var titleAt = position + new Vector2(Padding, Padding * 0.7f);
		text.Draw(title, titleAt, TitleSize, Palette.Dim * fade, spacing: 3.5f);
		float ruleY = titleAt.Y + TitleSize * 1.55f;
		p.Line(new Vector2(position.X + Padding, ruleY), new Vector2(position.X + size.X - Padding, ruleY), Palette.Rule * fade, 1.2f);
		return new Vector2(position.X + Padding, ruleY + 14);
	}
}
