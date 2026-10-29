using Godot;

/// <summary>What training's dummy does, picked on the pause menu (TrainingMode.Dummy).</summary>
public enum DummyBehaviour { Stand, Walk, Jump, Block, Fight }

/// <summary>
/// Training's dummy: a controller like any other, so the fighter it drives is an ordinary fighter
/// that cannot tell it is being practised on - nothing in Fighter knows there is such a thing as a
/// dummy. It stands, walks up and down, jumps, holds block, or hands over to the real CPU and
/// fights back, whichever the pause menu says now (Eric's call, 2026-10-29: Smash's training is
/// the model).
///
/// Whatever it is doing, knocked off the stage it comes home the way the CPU does. A dummy that
/// dropped to its death after every launch would spend the practice respawning, and launching it
/// off and watching it recover is half of what there is to practise.
/// </summary>
public sealed class DummyInputSource : IInputSource
{
	readonly Fighter self;
	readonly CpuInputSource cpu;
	readonly System.Func<DummyBehaviour> behaviour;

	int walkWay = 1;
	int walkFrames;
	int groundFrames;

	/// <summary>
	/// Under the run threshold, and pushed gradually rather than flicked, so it walks and never
	/// dashes (Fighter's DashThreshold is 0.55, and a dash wants a flick past 0.8).
	/// </summary>
	const float WalkStick = 0.45f;

	/// <summary>How long it walks one way before it turns, if no edge turns it first.</summary>
	const int WalkTurnFrames = 150;

	/// <summary>How long it stands between jumps: long enough to land, settle and be hit.</summary>
	const int JumpEveryFrames = 45;

	public DummyInputSource(Fighter self, CpuInputSource cpu, System.Func<DummyBehaviour> behaviour)
	{
		this.self = self;
		this.cpu = cpu;
		this.behaviour = behaviour;
	}

	public InputState Poll()
	{
		if (self == null || !self.IsInPlay) return InputState.None;
		DummyBehaviour now = behaviour();
		if (now == DummyBehaviour.Fight) return cpu.Poll();

		if (self.State == FighterState.LedgeHang || !self.IsLaunched && !self.IsOnFloor() && !GroundBelow()) return cpu.Poll();

		switch (now)
		{
			case DummyBehaviour.Walk: return Walk();
			case DummyBehaviour.Jump: return Jump();
			case DummyBehaviour.Block: return new InputState { BlockHeld = true };
			default: return InputState.None;
		}
	}

	InputState Walk()
	{
		if (!self.IsOnFloor() || self.IsLaunched) return InputState.None;
		// Turns at the end of what it is standing on, at the edge of the world, or after a while -
		// so it paces about in front of the player rather than off a walk-off floor.
		if (++walkFrames > WalkTurnFrames || !GroundAhead(walkWay) || NearBlastZone(walkWay))
		{
			walkWay = -walkWay;
			walkFrames = 0;
		}
		return new InputState { Move = new Vector2(walkWay * WalkStick, 0.0f) };
	}

	InputState Jump()
	{
		if (!self.IsOnFloor() || self.IsLaunched)
		{
			groundFrames = 0;
			return InputState.None;
		}
		if (++groundFrames < JumpEveryFrames) return InputState.None;
		groundFrames = 0;
		return new InputState { JumpPressed = true, JumpHeld = true };
	}

	bool GroundBelow() => Ray(self.GlobalPosition, 1600.0f);

	/// <summary>Floor a step ahead that way, not far under its feet: the end of a platform turns it round.</summary>
	bool GroundAhead(int direction)
	{
		float feet = self.Data.BodySize.Y * 0.5f;
		return Ray(self.GlobalPosition + new Vector2(direction * 80.0f, 0.0f), feet + 60.0f);
	}

	bool NearBlastZone(int direction)
	{
		float x = self.GlobalPosition.X + direction * 320.0f;
		Rect2 zone = self.Match.StageBounds;
		return x < zone.Position.X || x > zone.End.X;
	}

	bool Ray(Vector2 from, float down)
	{
		PhysicsDirectSpaceState2D space = self.GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0.0f, down), 0b11);
		return space.IntersectRay(query).Count > 0;
	}
}
