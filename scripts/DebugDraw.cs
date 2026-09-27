using Godot;

/// <summary>
/// Draws live hitboxes and hurtboxes over the fight. Added last so it renders on top.
///
/// This is not throwaway debug code - it becomes the visualisation half of training mode at
/// M5, and it is the only way to tell whether a move's numbers match what the move looks like.
/// </summary>
public partial class DebugDraw : Node2D
{
	public MatchManager Match { get; set; }

	public override void _Process(double delta)
	{
		if (Match != null && Match.ShowHitboxes) QueueRedraw();
	}

	public override void _Draw()
	{
		if (Match == null || !Match.ShowHitboxes) return;

		foreach (Fighter fighter in Match.Fighters)
		{
			if (fighter.State == FighterState.Eliminated) continue;

			// Hurtbox: what can be hit.
			DrawRect(fighter.BodyRect(), new Color(0.35f, 1.0f, 0.55f, 0.85f), false, 2.0f);

			// Hitbox: live only on the exact frames the move is active.
			if (fighter.IsHitboxLive)
			{
				Vector2 centre = fighter.CurrentHitboxCentre();
				DrawCircle(centre, fighter.CurrentHitboxRadius, new Color(1.0f, 0.25f, 0.3f, 0.28f));
				DrawArc(centre, fighter.CurrentHitboxRadius, 0.0f, Mathf.Tau, 32,
					new Color(1.0f, 0.3f, 0.35f, 0.95f), 2.5f);
			}
		}
	}
}
