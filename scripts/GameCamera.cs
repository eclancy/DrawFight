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

	/// <summary>
	/// The strip along the bottom of the screen the HUD covers - names, percents and lives
	/// (MatchHud, from y 890 down). The screen may reach this far, in screen pixels, below the
	/// Floor, so the art's bottom edge sits behind the HUD rather than the ground does: pinned to
	/// the art's very bottom, every stage sank down into the percentages (Eric, 2026-10-18).
	/// </summary>
	public const float HudBand = 190.0f;

	/// <summary>The lowest the bottom of the screen goes at this zoom: under the HUD, at the art's end.</summary>
	float FloorFor(float zoom) => Mathf.Min(Floor + HudBand / zoom, Limits.End.Y);

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
		GlobalPosition = Snapped(InsideLimits(smoothed + shakeOffset, z), z);
	}

	/// <summary>
	/// On a whole screen pixel. Between pixels, every fine line on the stage - the fridge door's
	/// grain and pencil hatching, a page's margin - lands on a different mix of pixels each frame,
	/// and panning across them shimmered (Eric, 2026-10-24: flickering up in the corner as the
	/// camera moved). On a whole pixel, a pan only moves them.
	/// </summary>
	Vector2 Snapped(Vector2 centre, float zoom) => (centre * zoom).Round() / zoom;

	/// <summary>
	/// The furthest out the camera can go and still have nothing but art on screen: the painted
	/// backdrop's width, and from its top down to the Floor.
	/// </summary>
	float FittingZoom()
	{
		Vector2 viewport = GetViewportRect().Size;
		// The view, less the HUD band, has to fit from the top of the backdrop down to the Floor; and
		// all of it inside the backdrop.
		float toFloor = Floor - Limits.Position.Y;
		float toEnd = Limits.End.Y - Limits.Position.Y;
		return Mathf.Max(viewport.X / Mathf.Max(1.0f, Limits.Size.X),
			Mathf.Max((viewport.Y - HudBand) / Mathf.Max(1.0f, toFloor), viewport.Y / Mathf.Max(1.0f, toEnd)));
	}

	/// <summary>
	/// A centre moved in until the whole screen round it is inside Limits - a few pixels inside, so
	/// rounding it onto a whole pixel (Snapped) never shows a sliver past the painting. Shake is
	/// moved in with it: shaken past the edge, the bare page flashed along it.
	/// </summary>
	Vector2 InsideLimits(Vector2 centre, float zoom)
	{
		Vector2 half = GetViewportRect().Size / (2.0f * zoom) + Vector2.One * (2.0f / zoom);
		float Axis(float c, float low, float high, float h) =>
			high - low <= h * 2.0f ? (low + high) * 0.5f : Mathf.Clamp(c, low + h, high - h);
		float y = Axis(centre.Y, Limits.Position.Y, Limits.End.Y, half.Y);
		return new Vector2(Axis(centre.X, Limits.Position.X, Limits.End.X, half.X), Mathf.Min(y, FloorFor(zoom) - half.Y));
	}

	/// <summary>A cut: there at once, no easing.</summary>
	public void CutTo(Vector2 centre, float zoom)
	{
		zoom = Mathf.Max(zoom, FittingZoom());
		Zoom = new Vector2(zoom, zoom);
		GlobalPosition = Snapped(InsideLimits(centre + shakeOffset, zoom), zoom);
	}
}
