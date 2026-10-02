using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StateYourNameScreensaver.Hud;
using StateYourNameScreensaver.Rendering;
using StateYourNameScreensaver.World;

namespace StateYourNameScreensaver;

/// <summary>
/// The screensaver. Everything is laid out in a 1920×1080 design space, scaled to fit the
/// screen, rendered to a texture, then given a phosphor glow and CRT scanlines.
/// </summary>
public sealed class ScreensaverGame : Game
{
	const float DesignWidth = 1920, DesignHeight = 1080;
	const int InputGraceFrames = 10;   // ignore input noise right after launch (from the MonogameScreenSaver template)

	readonly GraphicsDeviceManager _graphics;
	readonly Options _options;
	readonly Random _random;

	SpriteBatch _batch = null!;
	Prim _prim = null!;
	Text _text = null!;
	Director _director = null!;
	readonly SystemView _view = new();

	RenderTarget2D _scene = null!, _bloomHalf = null!, _bloomQuarter = null!, _final = null!;
	Texture2D _scanlines = null!, _vignette = null!;

	float _time;
	int _frames, _captured;
	MouseState _startMouse;
	KeyboardState _previousKeys;

	public ScreensaverGame(Options options)
	{
		_options = options;
		_random = options.Seed is int seed ? new Random(seed) : new Random();
		_graphics = new GraphicsDeviceManager(this)
		{
			SynchronizeWithVerticalRetrace = true,
			PreferMultiSampling = false,
			GraphicsProfile = GraphicsProfile.HiDef
		};
		IsMouseVisible = options.Windowed;
		// Variable timestep means exactly one Update per Draw. Capture mode relies on that to
		// advance precisely 1/fps per recorded frame, however slowly frames render.
		IsFixedTimeStep = false;
		Window.Title = "State Your Name";
	}

	protected override void Initialize()
	{
		if (_options.Windowed)
		{
			_graphics.PreferredBackBufferWidth = _options.Width;
			_graphics.PreferredBackBufferHeight = _options.Height;
			_graphics.IsFullScreen = false;
		}
		else
		{
			var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
			_graphics.PreferredBackBufferWidth = mode.Width;
			_graphics.PreferredBackBufferHeight = mode.Height;
			_graphics.HardwareModeSwitch = false;   // borderless: instant, no display mode flicker
			_graphics.IsFullScreen = true;
		}
		_graphics.ApplyChanges();
		base.Initialize();
	}

	protected override void LoadContent()
	{
		_batch = new SpriteBatch(GraphicsDevice);
		_prim = new Prim(GraphicsDevice, _batch);
		_text = new Text(_batch);
		_director = new Director(_random);

		int w = GraphicsDevice.PresentationParameters.BackBufferWidth, h = GraphicsDevice.PresentationParameters.BackBufferHeight;
		_scene = new RenderTarget2D(GraphicsDevice, w, h, false, SurfaceFormat.Color, DepthFormat.None, 4, RenderTargetUsage.DiscardContents);
		_bloomHalf = new RenderTarget2D(GraphicsDevice, Math.Max(1, w / 4), Math.Max(1, h / 4));
		_bloomQuarter = new RenderTarget2D(GraphicsDevice, Math.Max(1, w / 10), Math.Max(1, h / 10));
		_final = new RenderTarget2D(GraphicsDevice, w, h);
		_scanlines = Scanlines(GraphicsDevice);
		_vignette = Vignette(GraphicsDevice);

		// Fast-forward (for recording a specific moment).
		for (float t = 0; t < _options.SkipSeconds; t += 1 / 30f) Step(1 / 30f);

		_startMouse = Mouse.GetState();
		_previousKeys = Keyboard.GetState();
	}

	void Step(float dt)
	{
		_time += dt;
		var stage = _director.Stage;
		_director.Update(dt);
		if (_options.Windowed && _director.Stage != stage) Console.WriteLine($"{_time,7:0.0}s  {_director.Stage}  {_director.System.Designation}  {(_director.System.Star.IsBinary ? "binary " : "")}{_director.System.Star.Classification}");
	}

	protected override void Update(GameTime gameTime)
	{
		_frames++;
		if (_options.CaptureDirectory == null && ShouldExit()) { Exit(); return; }
		float dt = _options.CaptureDirectory != null
			? 1f / _options.CaptureFps
			: (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.1);
		Step(dt);
		base.Update(gameTime);
	}

	/// <summary>Screensaver rules: any key, click, or mouse movement dismisses it.</summary>
	bool ShouldExit()
	{
		var keys = Keyboard.GetState();
		var mouse = Mouse.GetState();
		bool exit;
		if (_options.Windowed) exit = keys.IsKeyDown(Keys.Escape);
		else if (_frames < InputGraceFrames) { _startMouse = mouse; exit = false; }
		else exit = (keys.GetPressedKeyCount() > 0 && _previousKeys.GetPressedKeyCount() == 0)
			|| mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed
			|| Math.Abs(mouse.X - _startMouse.X) + Math.Abs(mouse.Y - _startMouse.Y) > 3;
		_previousKeys = keys;
		return exit;
	}

	protected override void Draw(GameTime gameTime)
	{
		int w = _scene.Width, h = _scene.Height;
		float scale = Math.Min(w / DesignWidth, h / DesignHeight);
		// Slow drift so nothing sits on the same pixels for hours (burn-in protection).
		var drift = new Vector2(MathF.Sin(_time * 0.011f) * 16, MathF.Sin(_time * 0.0073f + 1.3f) * 11);
		var origin = new Vector2((w - DesignWidth * scale) / 2, (h - DesignHeight * scale) / 2) + drift * scale;
		var transform = Matrix.CreateScale(scale, scale, 1) * Matrix.CreateTranslation(origin.X, origin.Y, 0);
		_prim.PixelScale = scale;
		_text.PixelScale = scale;

		// 1. Scene in design space.
		GraphicsDevice.SetRenderTarget(_scene);
		GraphicsDevice.Clear(Color.Black);
		_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, transform);
		DrawScene(new Vector2(w, h) / scale);
		_batch.End();

		// 2. Cheap bloom: progressively downsample, then add back blurred copies.
		Blit(_scene, _bloomHalf);
		Blit(_bloomHalf, _bloomQuarter);

		GraphicsDevice.SetRenderTarget(_final);
		GraphicsDevice.Clear(Color.Black);
		_batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
		_batch.Draw(_scene, new Rectangle(0, 0, w, h), Color.White);
		_batch.End();
		_batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp);
		_batch.Draw(_bloomHalf, new Rectangle(0, 0, w, h), Color.White * 0.55f);
		_batch.Draw(_bloomQuarter, new Rectangle(0, 0, w, h), Color.White * 0.5f);
		if (_director.Flash > 0) _batch.Draw(_prim.Pixel, new Rectangle(0, 0, w, h), new Color(0.75f, 1f, 0.8f) * _director.Flash);
		_batch.End();

		// 3. CRT: scanlines, vignette, a whisper of flicker.
		_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap);
		int line = Math.Max(2, (int)MathF.Round(3 * scale));
		_batch.Draw(_scanlines, Vector2.Zero, new Rectangle(0, 0, w, h * 4 / line), Color.White, 0, Vector2.Zero, new Vector2(1, line / 4f), SpriteEffects.None, 0);
		_batch.End();
		_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
		_batch.Draw(_vignette, new Rectangle(0, 0, w, h), Color.White);
		float flicker = 0.015f + 0.015f * MathF.Sin(_time * 53) * MathF.Sin(_time * 7.3f);
		_batch.Draw(_prim.Pixel, new Rectangle(0, 0, w, h), Color.Black * flicker);
		_batch.End();

		GraphicsDevice.SetRenderTarget(null);
		_batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
		_batch.Draw(_final, Vector2.Zero, Color.White);
		_batch.End();

		if (_options.CaptureDirectory is { } dir) Capture(dir);
		base.Draw(gameTime);
	}

	void DrawScene(Vector2 visibleDesignSize)
	{
		var d = _director;
		var center = new Vector2(DesignWidth / 2, DesignHeight / 2);
		float hud = d.HudFade;

		d.Stars.Draw(_prim, center, _time, d.Stage == Stage.Boot ? hud : 1);

		// The system view, centred between the side panels.
		_view.Center = center;
		_view.Radius = 455 * d.ViewZoom;
		_view.Reveal = d.Reveal;
		_view.Fade = d.ViewFade;
		_view.ExhaustOn = d.ExhaustPulse;
		if (d.ViewFade > 0) _view.Draw(_prim, d.System, _time);

		// A sensor sweep arm while scanning.
		if (d.Stage == Stage.Scan && d.Reveal < 1)
		{
			float a = d.StageTime * 2.4f;
			var tip = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 455 * d.Reveal;
			_prim.Line(center, tip, Palette.Bright * 0.35f, 1.5f);
			_prim.Circle(center, 455 * d.Reveal, Palette.Bright * 0.25f, 1.2f);
		}

		if (hud > 0) DrawHud(hud);

		var title = "S T A T E   Y O U R   N A M E";
		_text.Draw(title, new Vector2(center.X - _text.Measure(title, 15).X / 2, DesignHeight - 34), 15, Palette.Dim * (0.8f * hud));

		if (d.Stage == Stage.Boot) BootScreen.Draw(_prim, _text, center, d.StageTime, Director.BootDuration);
	}

	void DrawHud(float fade)
	{
		var d = _director;
		const float left = 36, right = 1920 - 36 - 440, width = 440;

		var logContent = Panel.Draw(_prim, _text, new Vector2(left, 36), new Vector2(width, 620), "SHIP LOGS", fade);
		d.Log.Draw(_text, logContent, new Vector2(width - Panel.Padding * 2, 620 - (logContent.Y - 36) - Panel.Padding), _time, fade);

		var scanContent = Panel.Draw(_prim, _text, new Vector2(left, 676), new Vector2(width, 368), "LOCAL SYSTEM", fade);
		Readouts.Scan(_text, scanContent, width - Panel.Padding * 2, d.System, d.Reveal, fade);

		var systemsContent = Panel.Draw(_prim, _text, new Vector2(right, 36), new Vector2(width, 238), "SHIP SYSTEMS", fade);
		d.Systems.Draw(_prim, _text, systemsContent, width - Panel.Padding * 2, fade);

		var navAt = new Vector2(right, 294);
		var navContent = Panel.Draw(_prim, _text, navAt, new Vector2(width, 300), "SHIP NAV", fade);
		Readouts.Vessel(_prim, _text, navContent, new Vector2(width - Panel.Padding * 2, 300 - (navContent.Y - navAt.Y) - 8), _time, d.ExhaustPulse || d.System.Ship.Thrusting || d.Stage == Stage.Jump, fade);

		var statusContent = Panel.Draw(_prim, _text, new Vector2(right, 614), new Vector2(width, 430), "NAV STATUS", fade);
		Readouts.Status(_text, statusContent, width - Panel.Padding * 2, d, _time, fade);
	}

	void Blit(RenderTarget2D from, RenderTarget2D to)
	{
		GraphicsDevice.SetRenderTarget(to);
		GraphicsDevice.Clear(Color.Black);
		_batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
		_batch.Draw(from, new Rectangle(0, 0, to.Width, to.Height), Color.White);
		_batch.End();
	}

	void Capture(string dir)
	{
		Directory.CreateDirectory(dir);
		using (var file = File.Create(Path.Combine(dir, $"frame_{_captured:D4}.png")))
			_final.SaveAsPng(file, _final.Width, _final.Height);
		if (++_captured >= _options.CaptureFrames) Exit();
	}

	static Texture2D Scanlines(GraphicsDevice device)
	{
		// A 4-pixel repeating pattern: one dark row in four, stretched to the line pitch.
		var data = new Color[4];
		data[0] = Color.Black * 0.0f; data[1] = Color.Black * 0.0f; data[2] = Color.Black * 0.12f; data[3] = Color.Black * 0.28f;
		var t = new Texture2D(device, 1, 4);
		t.SetData(data);
		return t;
	}

	static Texture2D Vignette(GraphicsDevice device)
	{
		const int size = 128;
		var data = new Color[size * size];
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float dx = (x - size / 2f) / (size / 2f), dy = (y - size / 2f) / (size / 2f);
			float d = MathF.Sqrt(dx * dx * 0.8f + dy * dy);
			float a = Math.Clamp((d - 0.75f) / 0.6f, 0, 1) * 0.55f;
			data[y * size + x] = Color.Black * a;
		}
		var t = new Texture2D(device, size, size);
		t.SetData(data);
		return t;
	}
}
