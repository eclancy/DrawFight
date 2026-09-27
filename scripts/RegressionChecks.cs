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
	static void CheckNoMoveIsAlwaysCorrect()
	{
		CheckMove(MoveData.PlaceholderJab());
		CheckMove(MoveData.PlaceholderHeavySwing());

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
	/// Prints, for each placeholder move against each placeholder fighter, how far a hit sends
	/// them at a range of percents - and therefore roughly what percent it starts killing at
	/// from the edge of the stage.
	/// </summary>
	static void PrintCalibrationTable()
	{
		// Distance from the stage edge to the side blast zone: how far a launch must carry to KO.
		float killDistance = 1560.0f - Stage.MainPlatformHalfWidth;

		GD.Print("RegressionChecks: ---- knockback calibration ----");
		GD.Print($"RegressionChecks: side blast zone is {killDistance:0} px from the stage edge");

		Report("Jab      -> Lug  ", MoveData.PlaceholderJab(), FighterData.PlaceholderHeavy());
		Report("Jab      -> Swift", MoveData.PlaceholderJab(), FighterData.PlaceholderLight());
		Report("Swing    -> Lug  ", MoveData.PlaceholderHeavySwing(), FighterData.PlaceholderHeavy());
		Report("Swing    -> Swift", MoveData.PlaceholderHeavySwing(), FighterData.PlaceholderLight());

		void Report(string label, MoveData move, FighterData victim)
		{
			string line = label + " |";
			foreach (float startPercent in new[] { 0.0f, 50.0f, 100.0f, 150.0f })
			{
				float percentAfter = startPercent + move.Damage;
				float kb = Knockback.Compute(
					percentAfter, move.Damage, victim.Weight, move.BaseKnockback, move.KnockbackGrowth);

				float launchSpeed = kb * Tuning.LaunchSpeedPerKnockback;
				float travel = launchSpeed * launchSpeed / (2.0f * Tuning.LaunchDecay);
				float horizontal = travel * Mathf.Cos(Mathf.DegToRad(move.LaunchAngleDegrees));
				string kills = horizontal >= killDistance ? " KO" : "   ";

				line += $" {startPercent,3:0}%: {horizontal,5:0}px{kills} |";
			}
			line += $"  hitstun@100%: {Knockback.HitstunFrames(Knockback.Compute(100.0f + move.Damage, move.Damage, victim.Weight, move.BaseKnockback, move.KnockbackGrowth))}f";
			GD.Print("RegressionChecks: " + line);
		}
	}
}
