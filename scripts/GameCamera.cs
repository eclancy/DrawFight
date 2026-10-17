using System.Collections.Generic;
using Godot;

/// <summary>
/// Frames every living fighter, and carries the screen shake. Both are listed as part of the
/// feature in .ai/fighting-design.md rather than as polish - a fight you cannot see and a hit
/// that does not shove the screen both read as broken.
/// </summary>
public partial class GameCamera : Camera2D
{
	[Export] public float MinZoom { get; set; } = 0.42f;
	[Export] public float MaxZoom { get; set; } = 1.05f;
	[Export] public float Margin { get; set; } = 420.0f;
	[Export] public float FollowSharpness { get; set; } = 7.0f;

	/// <summary>
	/// Where the camera may look: the blast zone. Someone flying out past it - the winner jumping
	/// off after the last KO - is followed to the edge, not out into nothing.
	/// </summary>
	public Rect2 Bounds = new Rect2(-100000.0f, -100000.0f, 200000.0f, 200000.0f);

	/// <summary>
	/// What is painted: the screen never shows past it. Following someone up into a corner of
	/// Open Plains looked out past the sky onto blank page (Eric, 2026-10-15).
	/// </summary>
	public Rect2 Limits = new Rect2(-100000.0f, -100000.0f, 200000.0f, 200000.0f);

	/// <summary>
	/// The lowest the bottom of the screen ever goes: where the stage's drawing ends (Stage.ArtBottom).
	/// Wins over everything else - zoomed far out, the view rises rather than showing what is under
	/// the stage - so a fall off the bottom, even after the match is over, never pulls the camera
	/// down past the art.
	/// </summary>
	public float Floor = 100000.0f;

	float shake;
	Vector2 shakeOffset;
	readonly RandomNumberGenerator rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		rng.Randomize();
		MakeCurrent();
	}

	public void AddShake(float knockback)
	{
		shake = Mathf.Min(Tuning.ScreenShakeMax * GameOptions.ShakeScale, shake + knockback * Tuning.ScreenShakePerKnockback * GameOptions.ShakeScale);
	}

	/// <summary>The part of the world on screen right now, shake and all.</summary>
	public Rect2 VisibleRect()
	{
		Vector2 half = GetViewportRect().Size / (2.0f * Zoom.X);
		return new Rect2(GlobalPosition - half, half * 2.0f);
	}

	public void FrameFighters(List<Fighter> fighters, float dt)
	{
		Vector2 min = Vector2.Zero;
		Vector2 max = Vector2.Zero;
		bool any = false;

		foreach (Fighter fighter in fighters)
		{
			if (fighter.State == FighterState.Eliminated) continue;
			// Gone and waiting to come back: nothing to look at yet.
			if (fighter.IsWaitingToRespawn) continue;

			Vector2 p = fighter.GlobalPosition.Clamp(Bounds.Position, Bounds.End);
			if (!any)
			{
				min = max = p;
				any = true;
				continue;
			}
			min = new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y));
			max = new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y));
		}

		if (!any) return;

		Vector2 centre = (min + max) * 0.5f;
		Vector2 span = (max - min) + Vector2.One * Margin;
		Vector2 viewport = GetViewportRect().Size;

		// Zoom to fit the span, but never so far in that two players standing together fill
		// the screen, and never so far out that a launched player becomes a speck.
		float fit = Mathf.Min(viewport.X / Mathf.Max(span.X, 1.0f), viewport.Y / Mathf.Max(span.Y, 1.0f));
		float target = Mathf.Clamp(fit, MinZoom, MaxZoom);

		FrameOn(centre, target, FollowSharpness, dt);
	}

	/// <summary>
	/// Eases toward a centre and zoom of a story's choosing - a push in on whoever is talking, a
	/// two-shot, a slow pan up the fridge - at its own speed. The shake carries on as in a fight.
	/// </summary>
	public void FrameOn(Vector2 centre, float target, float sharpness, float dt)
	{
		float t = 1.0f - Mathf.Exp(-sharpness * dt);
		Vector2 smoothed = GlobalPosition.Lerp(centre, t);
		float z = Mathf.Lerp(Zoom.X, target, t);

		shake = Mathf.MoveToward(shake, 0.0f, Tuning.ScreenShakeDecayPerSecond * dt);
		shakeOffset = shake > 0.05f
			? new Vector2(rng.RandfRange(-shake, shake), rng.RandfRange(-shake, shake))
			: Vector2.Zero;

		z = Mathf.Max(z, FittingZoom());
		Zoom = new Vector2(z, z);
		GlobalPosition = InsideLimits(smoothed, z) + shakeOffset;
	}

	/// <summary>
	/// The furthest out the camera can go and still have nothing but art on screen: the painted
	/// backdrop's width, and from its top down to the Floor.
	/// </summary>
	float FittingZoom()
	{
		Vector2 viewport = GetViewportRect().Size;
		float tall = Mathf.Min(Floor, Limits.End.Y) - Limits.Position.Y;
		return Mathf.Max(viewport.X / Mathf.Max(1.0f, Limits.Size.X), viewport.Y / Mathf.Max(1.0f, tall));
	}

	/// <summary>A centre moved in until the whole screen round it is inside Limits.</summary>
	Vector2 InsideLimits(Vector2 centre, float zoom)
	{
		Vector2 half = GetViewportRect().Size / (2.0f * zoom);
		float Axis(float c, float low, float high, float h) =>
			high - low <= h * 2.0f ? (low + high) * 0.5f : Mathf.Clamp(c, low + h, high - h);
		float y = Axis(centre.Y, Limits.Position.Y, Limits.End.Y, half.Y);
		return new Vector2(Axis(centre.X, Limits.Position.X, Limits.End.X, half.X), Mathf.Min(y, Floor - half.Y));
	}

	/// <summary>A cut: there at once, no easing.</summary>
	public void CutTo(Vector2 centre, float zoom)
	{
		zoom = Mathf.Max(zoom, FittingZoom());
		Zoom = new Vector2(zoom, zoom);
		GlobalPosition = InsideLimits(centre, zoom) + shakeOffset;
	}
}
