using Godot;

/// <summary>
/// A fighter's face, frame by frame, and the paper twirl a story uses - kept apart from the rest
/// of Fighter, which only calls in from UpdateRig.
///
/// The face follows the fight: blinking and glancing about while nothing is happening, a squint
/// and a set jaw winding up a hit, the mouth open as it lands, eyes squeezed shut when hit and
/// wide when launched. A story can take it over (Expression, Talking). Every face is the
/// fighter's own drawn eyes and mouth, moved (FighterRig.SetFace) - none is drawn new.
/// </summary>
public partial class Fighter
{
	FaceState faceNow;
	int faceHitAge = 999;
	int lastHitstun;
	int glanceUntil, nextGlance = 90, nextBlink = 120;
	float glanceTo;

	/// <summary>A face a story has set by name (FaceState.Named), over whatever the fight would show; "" for none.</summary>
	public string Expression = "";

	/// <summary>A story has them talking: the mouth moves.</summary>
	public bool Talking;

	const int BlinkFrames = 9;

	void UpdateFace()
	{
		if (rig == null || !rig.HasFace) return;

		if (hitstunFrames > lastHitstun) faceHitAge = 0;
		else faceHitAge++;
		lastHitstun = hitstunFrames;

		FaceState want = FightFace();
		if (!string.IsNullOrEmpty(Expression)) want = FaceState.Named(Expression);

		// The eyes drift about while nothing is happening, and every few seconds a blink - hashed,
		// so it is never two fighters blinking together, and never random.
		if (fxFrames >= nextGlance)
		{
			glanceTo = CrayonBrush.Noise(fxFrames, PlayerIndex + 11) * 0.7f;
			glanceUntil = fxFrames + 40 + (int)(Mathf.Abs(CrayonBrush.Noise(fxFrames, PlayerIndex + 13)) * 50.0f);
			nextGlance = glanceUntil + 80 + (int)(Mathf.Abs(CrayonBrush.Noise(fxFrames, PlayerIndex + 17)) * 160.0f);
		}
		if (State == FighterState.Grounded && string.IsNullOrEmpty(Expression) && fxFrames < glanceUntil) want.LookX += glanceTo;

		if (Talking)
		{
			float beat = Mathf.Abs(Mathf.Sin(fxFrames * 0.42f + CrayonBrush.Noise(fxFrames / 6, PlayerIndex) * 1.5f));
			want.Open = Mathf.Max(want.Open, 0.15f + 0.7f * beat);
		}

		faceNow = FaceState.Lerp(faceNow, want, 0.35f);

		if (fxFrames >= nextBlink)
		{
			nextBlink = fxFrames + 150 + (int)(Mathf.Abs(CrayonBrush.Noise(fxFrames, PlayerIndex + 23)) * 170.0f);
		}
		FaceState shown = faceNow;
		int untilBlink = nextBlink - fxFrames;
		bool blinking = untilBlink <= BlinkFrames && State != FighterState.Hitstun;
		if (blinking)
		{
			float t = 1.0f - Mathf.Abs(untilBlink - BlinkFrames * 0.5f) / (BlinkFrames * 0.5f);
			shown.Blink = Mathf.Max(shown.Blink, Mathf.Clamp(t * 1.4f, 0.0f, 1.0f));
		}
		rig.SetFace(shown);
	}

	/// <summary>What the fight itself puts on their face.</summary>
	FaceState FightFace()
	{
		var f = new FaceState();

		// Just hit: eyes squeezed shut and the mouth open; then, sent flying, eyes wide.
		if (State == FighterState.Hitstun || hitlagFrames > 0 && faceHitAge < 20)
		{
			if (faceHitAge < 14) return FaceState.Hurt;
			f.Open = 0.5f;
			if (Velocity.LengthSquared() > 1100.0f * 1100.0f) f.Wide = 1.0f;
			else f.Squint = 0.5f;
			f.Sad = 0.4f;
			return f;
		}

		switch (State)
		{
			case FighterState.Attacking when currentMove != null:
				if (currentMove == Data.Taunt)
				{
					// Showing off: pleased with themselves, and saying their line.
					f = FaceState.Smug;
					if (moveFrame >= currentMove.StartupFrames && !string.IsNullOrEmpty(Data.TauntLine))
					{
						f.Open = 0.2f + 0.6f * Mathf.Abs(Mathf.Sin(moveFrame * 0.45f));
					}
					return f;
				}
				int startup = currentMove.StartupFrames;
				int activeEnd = startup + currentMove.ActiveFrames;
				float power = Mathf.Clamp(currentMove.Damage / 14.0f, 0.3f, 1.0f);
				if (IsCharging)
				{
					// Holding a smash: teeth set, eyes narrowed, getting angrier the longer it is held.
					f.Angry = 1.0f;
					f.Grit = 1.0f;
					f.Squint = 0.5f + 0.4f * chargeFrames / (float)MaxChargeFrames;
				}
				else if (moveFrame <= startup)
				{
					f.Squint = 0.6f;
					f.Angry = 0.9f;
					f.Grit = 0.7f;
				}
				else if (moveFrame <= activeEnd + 4)
				{
					f.Angry = 1.0f;
					f.Squint = 0.15f;
					f.Open = 0.45f + 0.55f * power;
				}
				else
				{
					f.Angry = 0.35f;
					f.Open = 0.15f;
				}
				return f;

			case FighterState.Dodging:
				f.Squint = 0.85f;
				f.Grit = 0.6f;
				return f;

			case FighterState.LedgeHang:
				f.Wide = 0.5f;
				f.Grit = 0.8f;
				f.LookY = -0.4f;
				return f;

			case FighterState.Respawning:
				f.Surprise = 0.5f;
				return f;
		}

		if (overheatFrames > 0)
		{
			// Stalled in his own steam: dazed.
			f.Squint = 0.7f;
			f.Sad = 0.6f;
			f.Open = 0.3f;
			return f;
		}
		if (IsBlocking)
		{
			f.Squint = 0.5f;
			f.Grit = 1.0f;
			f.Angry = 0.6f;
			return f;
		}
		if (helpless && State == FighterState.Airborne)
		{
			f.Wide = 0.7f;
			f.Sad = 0.5f;
			return f;
		}

		// Hot under the collar: the hotter, the angrier.
		f.Angry = HeatFraction * 0.8f;
		if (State == FighterState.Grounded && Mathf.Abs(Velocity.X) > 300.0f) f.Angry = Mathf.Max(f.Angry, 0.3f);
		return f;
	}

	// --- Twirling, for a story -----------------------------------------------------------------

	int twirlFrames, twirlLength;
	float twirlTurns;
	bool twirlInPlane;

	/// <summary>
	/// Turns the drawing round on the spot over <paramref name="frames"/> frames: about its upright
	/// axis, as a cut-out turns - narrowing to its edge and opening out again, so half a turn is
	/// facing the other way - or, <paramref name="inPlane"/>, round and round in the plane of the
	/// page. A story's flourish, never a fight's.
	/// </summary>
	public void Twirl(int frames, float turns, bool inPlane = false)
	{
		twirlLength = twirlFrames = Mathf.Max(1, frames);
		twirlTurns = turns;
		twirlInPlane = inPlane;
	}

	public bool Twirling => twirlFrames > 0;

	float TwirlProgress => twirlFrames <= 0 ? 0.0f : 1.0f - twirlFrames / (float)twirlLength;

	/// <summary>Eased in and out, so a twirl gathers speed and settles rather than starting dead.</summary>
	float TwirlAngle => twirlTurns * Mathf.Tau * (0.5f - 0.5f * Mathf.Cos(Mathf.Pi * TwirlProgress));

	float TwirlWidth() => twirlFrames <= 0 || twirlInPlane ? 1.0f : FighterRig.PaperTurnWidth(TwirlAngle);

	float TwirlRoll() => twirlFrames <= 0 || !twirlInPlane ? 0.0f : TwirlAngle;

	void TickTwirl()
	{
		if (twirlFrames > 0) twirlFrames--;
	}
}
