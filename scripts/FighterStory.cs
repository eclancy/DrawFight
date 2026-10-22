using Godot;

/// <summary>
/// What a story can do to a fighter that a controller cannot: turn him round on the spot, set
/// his heat, put him back on the stage after a KO for the scene after the fight, and clear the
/// scene off him before the fight starts. Everything else a story does goes through his
/// controller (StoryDirector), so it looks and sounds exactly as it does in a fight.
/// </summary>
public partial class Fighter
{
	/// <summary>
	/// A pose a story has him strike and hold - "cheer", "slump", "point", "shrug", "laugh",
	/// "stomp", "tremble", "crossarms" (FighterAnimations.StoryPose) - over whatever standing would
	/// show; "" for none.
	/// </summary>
	public string StoryPose = "";

	/// <summary>Sat down at a story's table, on the ground.</summary>
	bool IsSitting => (StoryPose == "sit" || StoryPose == "eat") && State == FighterState.Grounded && IsOnFloor();

	/// <summary>
	/// Where the stool under him has its seat, and how wide it is, while he sits - under his thighs,
	/// at the height planting his feet brought his hips down to - or null standing.
	/// </summary>
	public (Vector2 seat, float width)? SeatNow()
	{
		if (!IsSitting || rig == null || !rig.Loaded) return null;
		Vector2? front = rig.JointGlobal(RigBone.LegFrontUpper), back = rig.JointGlobal(RigBone.LegBackUpper);
		if (front == null || back == null) return null;
		float under = Mathf.Max(front.Value.Y, back.Value.Y) + Data.BodySize.Y * 0.035f;
		float x = (front.Value.X + back.Value.X) * 0.5f - Facing * Data.BodySize.X * 0.08f;
		return (new Vector2(x, under), Data.BodySize.X * 0.62f);
	}

	static readonly Color LunchInk = new Color(0.19f, 0.31f, 0.61f);

	/// <summary>
	/// His lunch in his hand while a story has him eat - half a sandwich, or a spoon - drawn over
	/// the hand holding it, so it goes up to his mouth and back down with every bite.
	/// </summary>
	void DrawLunchInHand(Node2D canvas)
	{
		if (StoryPose != "eat" || !IsSitting) return;
		Vector2? grip = rig.JointGlobal(RigBone.PropFront) ?? rig.JointGlobal(RigBone.ArmFrontLower);
		if (grip == null) return;
		Vector2 at = canvas.ToLocal(grip.Value);
		float s = Data.BodySize.Y * 0.15f;
		if (Data.Lunch == "spoon")
		{
			// Handle out of his fist, the bowl up, with a bit of custard in it.
			Vector2 bowl = at + new Vector2(Facing * s * 0.55f, -s * 0.7f);
			canvas.DrawLine(at, bowl, LunchInk, 5.0f, true);
			canvas.DrawLine(at, bowl, new Color(0.86f, 0.87f, 0.90f), 2.5f, true);
			canvas.DrawCircle(bowl, s * 0.24f, LunchInk);
			canvas.DrawCircle(bowl, s * 0.24f - 2.0f, new Color(0.86f, 0.87f, 0.90f));
			canvas.DrawCircle(bowl + new Vector2(0.0f, -1.0f), s * 0.13f, new Color(0.98f, 0.86f, 0.52f));
			return;
		}
		// Half a sandwich, the cut side up: bread, and the egg salad showing along the cut.
		Vector2[] half =
		{
			at + new Vector2(-Facing * s * 0.45f, s * 0.15f),
			at + new Vector2(Facing * s * 0.55f, s * 0.15f),
			at + new Vector2(Facing * s * 0.05f, -s * 0.8f),
		};
		canvas.DrawColoredPolygon(half, new Color(0.96f, 0.86f, 0.62f));
		canvas.DrawLine(half[1] + (half[2] - half[1]) * 0.1f, half[2] + (half[1] - half[2]) * 0.1f,
			new Color(0.98f, 0.88f, 0.36f), 4.0f, true);
		canvas.DrawPolyline(new[] { half[0], half[1], half[2], half[0] }, LunchInk, 2.5f, true);
	}

	/// <summary>One of his own whole drawings shown instead of his puppet - Triguy's surprised face
	/// (rig.json "poses") - for as long as a story holds it; "" for none.</summary>
	public string StoryShow = "";

	/// <summary>A mark over his head while a story holds it: anger, sweat, sparkle, heart, "!", "?", steam, tears.</summary>
	public string Mark = "";

	/// <summary>A boss on his last stock: flashing red, MiniBots coming out (StoryDirector).</summary>
	public bool Raging;

	/// <summary>How the line he is saying is said ("shout", "whisper"...), for his body to say it too.</summary>
	public string TalkMood = "";

	/// <summary>He has won: standing still, he strikes his victory pose (FighterData.VictoryPose).</summary>
	public bool Celebrating;

	/// <summary>Frames his talking hands still have to settle after the words stop.</summary>
	int talkLinger;

	const int TalkLingerFrames = 18;

	readonly Pose storyPose = new Pose();

	/// <summary>
	/// The story layer (ApplyStoryLayer) poses him outright this frame, so standing still does not
	/// play the idle clip under it: two poses pulling on the same springs meet halfway.
	/// </summary>
	bool StoryLayerOwnsPose() => rig != null && rig.Loaded && IsStandingStill
		&& (rig.FrontOn || !string.IsNullOrEmpty(StoryPose) || Talking || talkLinger > 0
			|| Celebrating && string.IsNullOrEmpty(Data.VictoryShow));

	/// <summary>
	/// Striking a story pose, or his victory pose. An arm can go up and out behind him for it -
	/// a cheer, a flex - past where it swings in a fight (FighterData.ArmReachBack).
	/// </summary>
	bool StrikingPose => !string.IsNullOrEmpty(StoryPose) && (State == FighterState.Grounded || State == FighterState.Airborne)
		|| Celebrating && IsStandingStill && string.IsNullOrEmpty(Data.VictoryShow);

	/// <summary>On his feet and going nowhere: the only time he talks with his hands, or turns to face you.</summary>
	bool IsStandingStill => State == FighterState.Grounded && Mathf.Abs(Velocity.X) <= 45.0f && !IsBlocking
		&& !IsCrouching && landFrames <= 0 && !shieldRaised;

	/// <summary>
	/// What a story - or a win - has him doing over whatever standing would show, after the fight's
	/// own pose: a pose struck, his hands going while he talks, his victory pose. Face on, if he
	/// stands face on and is standing to talk or to celebrate.
	/// </summary>
	void ApplyStoryLayer()
	{
		if (rig == null || !rig.Loaded) return;
		if (Talking) talkLinger = TalkLingerFrames;
		else if (talkLinger > 0) talkLinger--;

		bool faceOn = rig.FrontOn;
		string pose = StoryPose;
		if (string.IsNullOrEmpty(pose) && Celebrating && IsStandingStill) pose = Data.VictoryPose;
		rig.SetFarArmAcross(faceOn && FighterAnimations.FarArmAcross(pose));
		if (!string.IsNullOrEmpty(pose) && (faceOn || State == FighterState.Grounded || State == FighterState.Airborne)
			&& StrikePose(pose, faceOn))
		{
			// His hands are busy: a hammer on the shoulder is put down for it - and sitting down to
			// eat, whatever he carries.
			rig.HandOnHip = false;
			rig.CarryOnShoulder = false;
			if (Data.ShouldersProp || IsSitting) rig.SetPartVisible(RigBone.PropFront, false);
			rig.ApplyDirect(storyPose, 0.75f);
			return;
		}

		float beat = Mathf.Abs(Mathf.Sin(fxFrames * 0.19f + PlayerIndex * 1.7f));
		if (faceOn)
		{
			// Arms out the way he was drawn, the raised claw going as he talks.
			if (talkLinger > 0) FighterAnimations.FrontTalkPose(TalkMood, beat, storyPose);
			else
			{
				storyPose.CopyFrom(FighterAnimations.FrontIdle);
				storyPose.HipOffset = new Vector2(0.0f, 1.5f * Mathf.Sin(fxFrames * 0.05f));
			}
			rig.HandOnHip = false;
			rig.ApplyDirect(storyPose, talkLinger > 0 ? 0.3f : 0.4f);
			return;
		}

		if (talkLinger > 0 && IsStandingStill)
		{
			FighterAnimations.TalkPose(TalkMood, beat, storyPose);
			// A hammer on his shoulder keeps that hand busy: he talks with the other one.
			if (rig.CarryOnShoulder) SwapArms(storyPose);
			rig.HandOnHip = false;
			rig.ApplyDirect(storyPose, 0.22f);
		}
	}

	/// <summary>
	/// A story pose into <see cref="storyPose"/>, face on or side on, with its motion for the ones
	/// that move. False if there is no such pose.
	/// </summary>
	bool StrikePose(string name, bool faceOn)
	{
		if (faceOn) FighterAnimations.FaceOn(name, storyPose);
		else
		{
			Pose pose = FighterAnimations.StoryPose(name);
			if (pose == null) return false;
			storyPose.CopyFrom(pose);
			FighterAnimations.SpreadArms(name, storyPose, rig.NearArmLeads);
		}

		// The arm that does the moving: the near one side on, the one on the facing side face on.
		RigBone upper = faceOn ? RigBone.ArmBackUpper : RigBone.ArmFrontUpper;
		RigBone lower = faceOn ? RigBone.ArmBackLower : RigBone.ArmFrontLower;
		float swing = Mathf.Sin(fxFrames * 0.35f);
		float pump = Mathf.Abs(Mathf.Sin(fxFrames * 0.2f));
		switch (name)
		{
			case "wave":
				storyPose.Set(lower, storyPose[lower] + 24.0f * swing);
				break;
			case "fist":
				// Pumping it: down to the shoulder and punched back up.
				storyPose.Set(upper, storyPose[upper] + 20.0f * pump);
				storyPose.Set(lower, storyPose[lower] - 56.0f * pump);
				break;
			case "beckon":
				storyPose.Set(lower, storyPose[lower] - 84.0f * Mathf.Abs(Mathf.Sin(fxFrames * 0.18f)));
				break;
			case "nod":
				storyPose.Set(RigBone.Head, storyPose[RigBone.Head] - 14.0f * Mathf.Abs(Mathf.Sin(fxFrames * 0.16f)));
				break;
			case "flex":
				// Squeezing: each fist curls further in toward his head, whichever way that bends.
				storyPose.Set(RigBone.ArmFrontLower, storyPose[RigBone.ArmFrontLower] * (1.0f + 0.14f * pump));
				storyPose.Set(RigBone.ArmBackLower, storyPose[RigBone.ArmBackLower] * (1.0f + 0.14f * pump));
				break;
			case "eat":
			{
				// A bite every couple of seconds: his lunch up to his mouth, a moment chewing, and
				// back down to the table.
				int t = (fxFrames + PlayerIndex * 37) % 110;
				float up = t < 16 ? t / 16.0f : t < 50 ? 1.0f : t < 66 ? 1.0f - (t - 50) / 16.0f : 0.0f;
				up = up * up * (3.0f - 2.0f * up);
				storyPose.Set(upper, Mathf.Lerp(storyPose[upper], -16.0f, up));
				storyPose.Set(lower, Mathf.Lerp(storyPose[lower], -140.0f, up));
				float chew = t >= 16 && t < 50 ? 2.5f * Mathf.Sin(t * 0.9f) : 0.0f;
				storyPose.Set(RigBone.Head, storyPose[RigBone.Head] - 5.0f * up + chew);
				break;
			}
			case "laugh":
				// Rocking back and forward with it.
				storyPose.Set(RigBone.Torso, storyPose[RigBone.Torso] + 7.0f * Mathf.Sin(fxFrames * 0.45f));
				storyPose.Set(RigBone.Head, storyPose[RigBone.Head] - 8.0f * Mathf.Sin(fxFrames * 0.45f));
				storyPose.HipOffset += new Vector2(0.0f, 2.0f * Mathf.Abs(Mathf.Sin(fxFrames * 0.45f)));
				break;
			case "cheer":
				// Bouncing, arms going.
				storyPose.HipOffset += new Vector2(0.0f, -6.0f * pump);
				storyPose.Set(RigBone.ArmFrontUpper, storyPose[RigBone.ArmFrontUpper] + 10.0f * swing);
				storyPose.Set(RigBone.ArmBackUpper, storyPose[RigBone.ArmBackUpper] - 10.0f * swing);
				break;
			case "stomp":
				// The foot up, and slammed down, and up again.
				if ((fxFrames / 14) % 2 == 1)
				{
					foreach (RigBone leg in new[] { RigBone.LegFrontUpper, RigBone.LegFrontLower, RigBone.LegBackUpper, RigBone.LegBackLower })
					{
						storyPose.Set(leg, 0.0f);
					}
				}
				if (fxFrames % 28 == 14 && IsOnFloor())
				{
					Match?.Shake(12.0f);
					SfxPlayer.At("stomp", GlobalPosition, 0.05f);
				}
				break;
		}
		return true;
	}

	static void SwapArms(Pose pose)
	{
		float upper = pose[RigBone.ArmFrontUpper], lower = pose[RigBone.ArmFrontLower];
		pose.Set(RigBone.ArmFrontUpper, pose[RigBone.ArmBackUpper]);
		pose.Set(RigBone.ArmFrontLower, pose[RigBone.ArmBackLower]);
		pose.Set(RigBone.ArmBackUpper, upper);
		pose.Set(RigBone.ArmBackLower, lower);
	}

	// --- Face on ---------------------------------------------------------------------------------

	int frontTurn;
	const int FrontTurnFrames = 16;

	/// <summary>
	/// Whether a fighter who stands face on (FighterData.FrontOn) should be standing face on now: in a
	/// story scene, or having won, standing still - not walking, not mid-move, not in the air.
	/// </summary>
	bool WantsFrontOn() => Data.FrontOn && stillFrames >= StillBeforeFrontOn && string.IsNullOrEmpty(StoryShow) && !Twirling
		&& State != FighterState.Attacking && (Celebrating || Match != null && Match.InStoryScene);

	/// <summary>How long he has stood still - he turns to face you once he has settled, not mid-shuffle.</summary>
	int stillFrames;

	const int StillBeforeFrontOn = 8;

	/// <summary>
	/// Turns him face on with a paper turn - narrowing to his edge, swapped there, opening out face
	/// on - and back to side on the moment he moves, opening out from his edge.
	/// </summary>
	void TickFrontOn()
	{
		if (!Data.FrontOn || rig == null || !rig.Loaded) return;
		stillFrames = IsStandingStill ? stillFrames + 1 : 0;
		bool want = WantsFrontOn();
		if (want && !rig.FrontOn && frontTurn == 0) frontTurn = FrontTurnFrames;
		if (!want && rig.FrontOn)
		{
			rig.SetFrontOn(false);
			frontTurn = FrontTurnFrames / 2;
		}
		if (frontTurn > 0)
		{
			frontTurn--;
			if (frontTurn == FrontTurnFrames / 2 && want) rig.SetFrontOn(true);
		}
	}

	float FrontTurnWidth() => frontTurn <= 0 ? 1.0f
		: Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(Mathf.Pi * (1.0f - frontTurn / (float)FrontTurnFrames))));

	/// <summary>A trembling fighter's shiver, as a tiny roll this way and that.</summary>
	float TrembleRoll() => StoryPose == "tremble" ? CrayonBrush.Noise(fxFrames, PlayerIndex * 5 + 1) * 0.06f : 0.0f;

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
		ClearStoryLook();
	}

	/// <summary>Whatever a scene had him doing with his face, his body and over his head, let go of.</summary>
	public void ClearStoryLook()
	{
		Expression = "";
		Talking = false;
		TalkMood = "";
		StoryPose = "";
		StoryShow = "";
		Mark = "";
		Celebrating = false;
	}
}
