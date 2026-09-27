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
		shake = Mathf.Min(Tuning.ScreenShakeMax, shake + knockback * Tuning.ScreenShakePerKnockback);
	}

	public void FrameFighters(List<Fighter> fighters, float dt)
	{
		Vector2 min = Vector2.Zero;
		Vector2 max = Vector2.Zero;
		bool any = false;

		foreach (Fighter fighter in fighters)
		{
			if (fighter.State == FighterState.Eliminated) continue;

			Vector2 p = fighter.GlobalPosition;
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

		float t = 1.0f - Mathf.Exp(-FollowSharpness * dt);
		Vector2 smoothed = GlobalPosition.Lerp(centre, t);
		float z = Mathf.Lerp(Zoom.X, target, t);

		shake = Mathf.MoveToward(shake, 0.0f, Tuning.ScreenShakeDecayPerSecond * dt);
		shakeOffset = shake > 0.05f
			? new Vector2(rng.RandfRange(-shake, shake), rng.RandfRange(-shake, shake))
			: Vector2.Zero;

		Zoom = new Vector2(z, z);
		GlobalPosition = smoothed + shakeOffset;
	}
}
