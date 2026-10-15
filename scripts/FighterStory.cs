using Godot;

/// <summary>
/// What a story can do to a fighter that a controller cannot: turn him round on the spot, set
/// his heat, put him back on the stage after a KO for the scene after the fight, and clear the
/// scene off him before the fight starts. Everything else a story does goes through his
/// controller (StoryDirector), so it looks and sounds exactly as it does in a fight.
/// </summary>
public partial class Fighter
{
	/// <summary>Turns to face a way - as a turn round, the paper turn and all, not a snap.</summary>
	public void TurnTo(int direction) => Facing = direction < 0 ? -1 : 1;

	/// <summary>Sets his heat outright: DoomBot, mocked, starts the fight at full heat.</summary>
	public void SetHeat(float amount)
	{
		if (!Data.HasHeat) return;
		heat = Mathf.Clamp(amount, 0.0f, Heat.Max);
		framesSinceHeat = 0;
		framesAtMaxHeat = 0;
	}

	/// <summary>The top of his head, in the world: where a speech bubble points.</summary>
	public Vector2 HeadGlobal() => new Vector2(GlobalPosition.X, BodyRect().Position.Y);

	/// <summary>His eye, if his drawing has one marked (DoomBot's laser eye); otherwise his head.</summary>
	public Vector2 EyeGlobal()
	{
		Vector2? eye = rig != null && rig.Loaded ? rig.PointGlobal("eye") : null;
		return eye ?? HeadGlobal() + new Vector2(Facing * bodySize.X * 0.2f, bodySize.Y * 0.15f);
	}

	/// <summary>
	/// Back on the stage at <paramref name="at"/>, visible and in control, whatever the fight
	/// left him as - KO'd out of the match, or waiting to respawn. For the scene after a fight,
	/// where the one who lost still has something to say.
	/// </summary>
	public void ReviveForScene(Vector2 at)
	{
		if (State == FighterState.Eliminated || State == FighterState.Respawning)
		{
			Stocks = Mathf.Max(Stocks, 1);
			respawnDelayFrames = 0;
			respawnFreezeFrames = 0;
			State = FighterState.Airborne;
		}
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;
		GlobalPosition = at;
		Velocity = Vector2.Zero;
	}

	/// <summary>Falls through soft platforms for a couple of frames - a story's drop lands on the floor.</summary>
	public void FallThroughPlatforms()
	{
		dropThroughFrames = 2;
		CollisionMask = SolidOnlyMask;
	}

	/// <summary>The scene is over and the fight begins: no damage carried in, and his own face back.</summary>
	public void ReadyForFight()
	{
		Percent = 0.0f;
		Expression = "";
		Talking = false;
	}
}
