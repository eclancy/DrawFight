using Godot;

/// <summary>
/// Startup validation for the tuning invariants that dotnet build cannot see. Runs from
/// MatchManager._Ready and prints to the Godot console as "RegressionChecks:" lines, following
/// the same pattern as ContentValidator in the sibling project.
///
/// The calibration table it prints is the point of the file. Knockback numbers are meaningless
/// in the abstract - what matters is "does this move kill at a sensible percent", and that can
/// only be judged against the stage's actual dimensions.
/// </summary>
public static class RegressionChecks
{
	public static void RunAll()
	{
		CheckKnockbackIsMonotonic();
		CheckWeightReducesKnockback();
		CheckMovesetsAreComplete();
		CheckEveryUpSpecialRecovers();
		CheckWeightSpeedRule();
		CheckNoMoveIsAlwaysCorrect();
		PrintCalibrationTable();
	}

	/// <summary>Higher percent must always mean further launch, or the core rule is broken.</summary>
	static void CheckKnockbackIsMonotonic()
	{
		float previous = -1.0f;
		for (float percent = 0.0f; percent <= 200.0f; percent += 10.0f)
		{
			float kb = Knockback.Compute(percent, 10.0f, 100.0f, 30.0f, 1.0f);
			if (kb <= previous)
			{
				GD.PushError($"RegressionChecks: knockback not monotonic at {percent}% ({kb} <= {previous})");
				return;
			}
			previous = kb;
		}

		if (Knockback.HitstunFrames(10.0f) < 1)
		{
			GD.PushError("RegressionChecks: hitstun must always be at least one frame");
		}
	}

	/// <summary>A heavier fighter must fly less from an identical hit.</summary>
	static void CheckWeightReducesKnockback()
	{
		float light = Knockback.Compute(80.0f, 12.0f, 80.0f, 30.0f, 1.0f);
		float heavy = Knockback.Compute(80.0f, 12.0f, 130.0f, 30.0f, 1.0f);

		if (heavy >= light)
		{
			GD.PushError($"RegressionChecks: weight is not reducing knockback (light {light:0.0}, heavy {heavy:0.0})");
		}
	}

	/// <summary>
	/// A move with high base knockback AND high growth is always the right choice, which
	/// flattens the whole game. See .ai/fighting-design.md.
	/// </summary>
	/// <summary>
	/// Every up-special must give real vertical recovery. A fighter who cannot get back to the
	/// stage from below is unplayable whatever else its kit does, so this is checked rather
	/// than trusted - it is the easiest rule to break while designing a cool move.
	/// </summary>
	static void CheckEveryUpSpecialRecovers()
	{
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			FighterData fighter = FighterCatalog.Get(i);
			MoveData up = fighter.Move(MoveSlot.UpSpecial);

			if (up == null)
			{
				GD.PushError($"RegressionChecks: {fighter.DisplayName} has no up-special");
				continue;
			}

			if (up.Special != SpecialKind.Recovery)
			{
				GD.PushError($"RegressionChecks: {fighter.DisplayName} up-special '{up.MoveName}' "
					+ "is not a Recovery archetype");
			}

			// It has to beat a standing jump, or it is not a recovery, it is a small hop.
			if (up.SpecialRise < fighter.JumpForce * 0.8f)
			{
				GD.PushError($"RegressionChecks: {fighter.DisplayName} up-special rise "
					+ $"{up.SpecialRise:0} is too weak against a jump of {fighter.JumpForce:0}");
			}
		}
	}

	/// <summary>
	/// No fighter is above average in both weight and run speed. That single constraint is what
	/// prevents the character who is simply better than everyone else.
	/// </summary>
	static void CheckWeightSpeedRule()
	{
		float mediumRunSpeed = new FighterData().RunSpeed;

		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			FighterData fighter = FighterCatalog.Get(i);
			if (WeightProfiles.BreaksWeightSpeedRule(fighter.Weight, fighter.RunSpeed, mediumRunSpeed))
			{
				GD.PushError($"RegressionChecks: {fighter.DisplayName} is Heavy AND faster than "
					+ $"medium ({fighter.RunSpeed:0} > {mediumRunSpeed:0})");
			}
		}
	}

	/// <summary>Every fighter needs a full moveset; a null slot is a move that does nothing.</summary>
	static void CheckMovesetsAreComplete()
	{
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			FighterData fighter = FighterCatalog.Get(i);
			for (var slot = MoveSlot.Jab; slot < MoveSlot.Count; slot++)
			{
				if (fighter.Move(slot) == null)
				{
					GD.PushError($"RegressionChecks: {fighter.DisplayName} has no {slot}");
				}
			}
		}
	}

	static void CheckNoMoveIsAlwaysCorrect()
	{
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			FighterData fighter = FighterCatalog.Get(i);
			for (var slot = MoveSlot.Jab; slot < MoveSlot.Count; slot++)
			{
				MoveData move = fighter.Move(slot);
				if (move != null) CheckMove(move);
			}
		}

		static void CheckMove(MoveData move)
		{
			if (move.BaseKnockback > 45.0f && move.KnockbackGrowth > 1.2f)
			{
				GD.PushWarning(
					$"RegressionChecks: '{move.MoveName}' has high base ({move.BaseKnockback}) AND "
					+ $"high growth ({move.KnockbackGrowth}) - it will always be the correct choice");
			}

			if (move.StartupFrames < 1 || move.ActiveFrames < 1 || move.EndlagFrames < 1)
			{
				GD.PushError($"RegressionChecks: '{move.MoveName}' has a zero-length frame window");
			}
		}
	}

	/// <summary>
	/// Prints the percent at which each move starts KOing each fighter from the edge of the
	/// stage, by simulating the launch rather than by estimating a distance.
	///
	/// The estimate this replaced ignored gravity, which was survivable while gravity was low
	/// and became actively misleading once it nearly doubled: how far a launch carries depends
	/// on how long the victim stays in the air, so a heavier-falling game KOs later from the
	/// same knockback. A number that does not move when the thing it measures does is worse
	/// than no number.
	///
	/// Assumes the victim never recovers and never uses DI - this is the easiest case for the
	/// attacker, so read it as the floor of the real KO percent.
	/// </summary>
	static void PrintCalibrationTable()
	{
		GD.Print("RegressionChecks: ---- KO percents (no DI, no recovery) ----");

		for (int a = 0; a < FighterCatalog.Count; a++)
		{
			FighterData attacker = FighterCatalog.Get(a);
			FighterData victim = FighterCatalog.Get((a + 1) % FighterCatalog.Count);

			GD.Print($"RegressionChecks: -- {attacker.DisplayName} "
				+ $"({WeightProfiles.Describe(attacker.Weight)}) vs {victim.DisplayName} --");

			foreach (MoveSlot slot in new[]
			{
				MoveSlot.Jab, MoveSlot.ForwardTilt, MoveSlot.BackAir, MoveSlot.DownAir,
				MoveSlot.DashAttack, MoveSlot.ForwardSmash, MoveSlot.UpSmash, MoveSlot.DownSmash,
				MoveSlot.NeutralSpecial, MoveSlot.SideSpecial,
				MoveSlot.UpSpecial, MoveSlot.DownSpecial,
			})
			{
				MoveData move = attacker.Move(slot);
				if (move != null) Report(move.MoveName, move, victim);
			}
		}

		void Report(string label, MoveData move, FighterData victim)
		{
			int ko = FindKoPercent(move, victim, out string edge);
			// A spike kills by sending people below the stage, and this simulation only counts
			// clean side and top launches, so it cannot see those. Say so rather than printing
			// a number that looks like the move is weak.
			string verdict = move.Spikes
				? "spikes - not measurable here"
				: ko < 0 ? "never KOs below 300%" : $"KOs from {ko}% ({edge})";
			int hitstun = Knockback.HitstunFrames(Knockback.Compute(
				100.0f + move.Damage, move.Damage, victim.BodyWeight,
				move.BaseKnockback, move.KnockbackGrowth));
			GD.Print($"RegressionChecks:   {label,-14} | {verdict,-28} | {move.Damage,5:0.0} dmg | {move.StartupFrames,2} startup");
		}
	}

	static int FindKoPercent(MoveData move, FighterData victim, out string edge)
	{
		for (int percent = 0; percent <= 300; percent += 5)
		{
			if (SimulateLaunch(move, victim, percent, out edge)) return percent;
		}
		edge = "";
		return -1;
	}

	/// <summary>
	/// Flies a victim from the edge of the stage and reports whether the hit launches them
	/// clean off the side or the top.
	///
	/// Falling off the BOTTOM is deliberately counted as survived. Every hit pushes a victim
	/// off the edge eventually if you simulate long enough and never let them recover, so
	/// counting bottom falls made every move read as a 0% kill and told us nothing. The useful
	/// question for a Smash-like is "at what percent does this launch someone clean off the
	/// side", and that is what this measures. Landing back on the stage, or descending below
	/// it into recovery range, both count as survived.
	/// </summary>
	static bool SimulateLaunch(MoveData move, FighterData victim, float startPercent, out string edge)
	{
		// Measured against the default stage, which is the one the KO percents are quoted for.
		StageData stage = StageCatalog.FridgeDoor();
		const float stageEdge = 620.0f;

		float percentAfter = startPercent + move.Damage;
		float knockback = Knockback.Compute(
			percentAfter, move.Damage, victim.BodyWeight, move.BaseKnockback, move.KnockbackGrowth);

		Vector2 velocity = Knockback.LaunchVelocity(
			knockback, move.LaunchAngleDegrees, 1, Vector2.Zero);
		Vector2 position = new Vector2(stageEdge, 0.0f);

		int hitstun = Knockback.HitstunFrames(knockback);
		const float Dt = 1.0f / 60.0f;
		const float RecoveryDepth = 420.0f;

		for (int frame = 0; frame < 300; frame++)
		{
			float horizontalDrag = frame < hitstun ? Tuning.LaunchDecay : 320.0f;
			velocity.X = Mathf.MoveToward(velocity.X, 0.0f, horizontalDrag * Dt);
			velocity.Y = Mathf.Min(victim.MaxFallSpeed, velocity.Y + victim.Gravity * Dt);
			position += velocity * Dt;

			if (position.X > stage.BlastZone.End.X || position.X < stage.BlastZone.Position.X)
			{
				edge = "side";
				return true;
			}
			if (position.Y < stage.BlastZone.Position.Y)
			{
				edge = "top";
				return true;
			}

			// Back on the stage floor, or far enough below it that this is a recovery
			// situation rather than a launch. Either way the hit did not kill.
			bool overStage = Mathf.Abs(position.X) <= stageEdge;
			if (velocity.Y > 0.0f && ((overStage && position.Y >= 0.0f) || position.Y > RecoveryDepth))
			{
				break;
			}
		}

		edge = "";
		return false;
	}
}
