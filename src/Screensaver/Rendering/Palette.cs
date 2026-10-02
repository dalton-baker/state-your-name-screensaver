using Microsoft.Xna.Framework;

namespace StateYourNameScreensaver.Rendering;

/// <summary>The game's green phosphor palette.</summary>
public static class Palette
{
	public static readonly Color Bright = new(0x00, 0xff, 0x41);   // #00ff41 bodies, text
	public static readonly Color Mid = new(0x00, 0xaa, 0x33);      // #00aa33 orbits
	public static readonly Color Soft = new(0x00, 0xcc, 0x44);     // #00cc44 corona
	public static readonly Color Lime = new(0x00, 0xff, 0x55);     // #00ff55 photon ring
	public static readonly Color Mint = new(0x00, 0xff, 0x99);     // #00ff99 inner disk
	public static readonly Color Pale = new(0x8c, 0xff, 0x8c);     // #8cff8c ship
	public static readonly Color Dim = new(0x00, 0x6e, 0x0c);      // panel titles (a touch brighter than the game's #005500 at this size)
	public static readonly Color Frame = new(0x00, 0x33, 0x00);    // #003300 panel borders
	public static readonly Color Rule = new(0x00, 0x22, 0x00);     // #002200 title rules
	public static readonly Color BodyFill = new(0x00, 0x1f, 0x06); // #001f06 planet fill
	public static readonly Color StarFill = new(0x00, 0x1a, 0x04); // #001a04 star fill
	public static readonly Color Amber = new(0xff, 0xa5, 0x00);    // alerts
	public static readonly Color Red = new(0xff, 0x33, 0x33);      // negative deltas
}
