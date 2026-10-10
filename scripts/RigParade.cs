using System.Collections.Generic;
using Godot;

/// <summary>
/// A debug view that lays out one copy of each fighter in every animation the shared library
/// contains, side by side and large. Run it with:
///
///     "$GODOT_BIN" --path . -- --parade --shot=90
///     "$GODOT_BIN" --path . -- --parade --attacks --shot=90   (every fighter's own attacks)
///     "$GODOT_BIN" --path . -- --parade --film=fsmash --only=1 --shot=10   (one move, frame by frame)
///
/// This exists because a build cannot tell you whether a puppet is assembled correctly, and
/// squinting at two 120-pixel fighters in a match cannot either. When a pivot is wrong or a
/// rotation sign is flipped, it is obvious here and invisible everywhere else.
/// </summary>
public partial class RigParade : Node2D
{
	sealed class Slot
	{
		public FighterRig Rig;
		public AnimationClip Clip;
		public Pose Direct;
		public FighterData Data;
		public MoveData Move;
		public string Label;
		public Node2D Holder;
	}

	readonly List<Slot> slots = new List<Slot>();
	readonly Pose scratch = new Pose();

	/// <summary>
	/// Show each fighter's own normal attacks at the moment they hit, instead of the locomotion
	/// clips. Because the jab combo and some animations differ by weight class, this is the view
	/// that shows the three fighters are not all doing the same thing.
	/// </summary>
	public bool Attacks;

	/// <summary>One fighter by catalog index (--only=N), shown big, instead of the whole roster.</summary>
	public int Only = -1;

	/// <summary>
	/// A size step (--size=-1 or --size=1) for any fighter who can change size - so Circy short
	/// or tall can be checked standing, running and attacking without a match. See SizeLevels.
	/// </summary>
	public int Size;

	/// <summary>
	/// A film strip (--film=NAME, with --only=N): one fighter doing one thing, frame by frame
	/// across the screen. Each cell is played up to its frame through the rig exactly as a match
	/// plays it - the same blending, the same planted feet, the same robot beats - and then held.
	/// The parade shows a pose; this shows the motion between poses, which is where a puppet looks
	/// like a puppet: a joint that snaps, a body that floats, every limb moving at once. NAME is a
	/// clip (idle, run, jump, land, crouch, block, hurt), a dodge roll (roll, on a floor line), or a
	/// move (jab, ftilt, utilt, dtilt, dash, fsmash, usmash, dsmash, nair, fair, bair, uair, dair),
	/// drawn with its smear. An attack starts from standing and settles back into it, so the way in
	/// and the way out show too.
	/// </summary>
	public string Film = "";

	/// <summary>Frames of standing still before a filmed attack starts.</summary>
	const int FilmLeadIn = 12;

	static MoveData FilmMove(FighterData d, string name)
	{
		switch (name)
		{
			case "jab": return d.Move(MoveSlot.Jab);
			case "ftilt": return d.Move(MoveSlot.ForwardTilt);
			case "utilt": return d.Move(MoveSlot.UpTilt);
			case "dtilt": return d.Move(MoveSlot.DownTilt);
			case "dash": return d.Move(MoveSlot.DashAttack);
			case "fsmash": return d.Move(MoveSlot.ForwardSmash);
			case "usmash": return d.Move(MoveSlot.UpSmash);
			case "dsmash": return d.Move(MoveSlot.DownSmash);
			case "nair": return d.Move(MoveSlot.NeutralAir);
			case "fair": return d.Move(MoveSlot.ForwardAir);
			case "bair": return d.Move(MoveSlot.BackAir);
			case "uair": return d.Move(MoveSlot.UpAir);
			case "dair": return d.Move(MoveSlot.DownAir);
			default: return null;
		}
	}

	static bool IsStandardFilm(string name) => name is "jab" or "ftilt" or "utilt" or "dtilt";

	static bool IsStandardColumn(string label) => label.StartsWith("jab") || label.EndsWith("tilt");

	static bool IsAerial(MoveData d, FighterData data) =>
		d == data.Move(MoveSlot.NeutralAir) || d == data.Move(MoveSlot.ForwardAir) || d == data.Move(MoveSlot.BackAir)
		|| d == data.Move(MoveSlot.UpAir) || d == data.Move(MoveSlot.DownAir);

	/// <summary>One frame of the filmed action on a rig, set up as Fighter.UpdateRig sets it up.</summary>
	static void FilmFrame(FighterRig rig, FighterData data, string film, MoveData move, int frame, Pose scratch)
	{
		rig.SetLookingBack(false);
		rig.SetRoll(0.0f);
		rig.HandOnHip = false;
		rig.CarryOnShoulder = false;
		rig.ShowProp("");
		rig.SetPartVisible(RigBone.PropFront, true);

		int moveFrame = frame - FilmLeadIn;
		if (move != null && moveFrame >= 0 && moveFrame <= move.TotalFrames)
		{
			bool aerial = IsAerial(move, data);
			bool standard = !aerial && IsStandardFilm(film);
			rig.SetPlanted(!aerial);
			rig.SetStanding(standard && FighterAnimations.StandsThrough(move, MoveSlot.Jab));
			rig.SetLookingBack(move.LooksBack);
			rig.CarryOnShoulder = data.ShouldersProp && move.CarryOnShoulder;
			rig.ShowProp(move.PropArt);
			rig.SetPartVisible(RigBone.PropFront, move.PropArt != "-");
			FighterAnimations.ShowAttack(rig, move, moveFrame, data.AnimationDrama, scratch,
				standard ? FighterAnimations.GroundAdjust(move, MoveSlot.Jab) : null);
			if (standard && !rig.Robotic && rig.GetParent() is Node2D holder)
			{
				FighterAnimations.ThrowPunch(rig, move, moveFrame, holder.ToGlobal(HitboxInView(data, move)),
					RadiusInView(data, move), 1, rig.HandEmpty(move.PropArt));
			}
			return;
		}
		if (move != null)
		{
			// Before the move and after it: standing (or, for an aerial, hanging in the air).
			bool aerial = IsAerial(move, data);
			rig.SetPlanted(!aerial);
			rig.SetStanding(!aerial);
			rig.CarryOnShoulder = data.ShouldersProp && !aerial;
			rig.HandOnHip = data.HandOnHip && !aerial;
			rig.Play(aerial ? FighterAnimations.Fall : FighterAnimations.Idle);
			rig.Advance();
			return;
		}

		switch (film)
		{
			case "run":
				rig.SetPlanted(true);
				rig.CarryOnShoulder = data.ShouldersProp;
				rig.Play(data.Robotic ? FighterAnimations.StompWalk : FighterAnimations.Run);
				rig.Advance(1.25f);
				break;
			case "jump":
				rig.SetPlanted(false);
				rig.Play(frame < 26 ? FighterAnimations.Jump : FighterAnimations.Fall);
				rig.Advance();
				break;
			case "land":
				rig.SetPlanted(true);
				rig.CarryOnShoulder = data.ShouldersProp;
				rig.Play(FighterAnimations.Land);
				rig.Advance();
				break;
			case "crouch":
				rig.SetPlanted(true);
				rig.CarryOnShoulder = data.ShouldersProp;
				rig.Play(FighterAnimations.Crouch);
				rig.Advance();
				break;
			case "block":
				rig.SetPlanted(true);
				rig.SetStanding(true);
				rig.CarryOnShoulder = data.ShouldersProp;
				rig.Play(FighterAnimations.Block);
				rig.Advance();
				break;
			case "hurt":
				rig.SetPlanted(false);
				rig.Play(FighterAnimations.Hurt);
				rig.Advance();
				break;
			case "roll" when frame >= FilmLeadIn && frame < FilmLeadIn + Fighter.RollFrames:
				// A dodge roll, as Fighter.UpdateRig plays it: curled up, rolling along the floor.
				rig.SetPlanted(false);
				rig.SetStanding(false);
				rig.CarryOnShoulder = data.ShouldersProp;
				rig.SetRoll(Mathf.Tau * FighterAnimations.RollTurned((frame - FilmLeadIn + 1) / (float)Fighter.RollFrames), true);
				rig.ApplyDirect(FighterAnimations.Tuck, 0.7f);
				break;
			default:
				rig.SetPlanted(true);
				rig.SetStanding(true);
				rig.CarryOnShoulder = data.ShouldersProp;
				rig.HandOnHip = data.HandOnHip;
				rig.Play(FighterAnimations.Idle);
				rig.Advance();
				break;
		}
	}

	/// <summary>
	/// Several moves at once (--film=ground, smash, air or moves): one row per move, each at its
	/// key moments - standing, the windup, the coil, the hit, the follow-through, the recovery and
	/// back to standing - so every move a fighter has can be looked over for how it moves.
	/// </summary>
	static readonly Dictionary<string, string[]> FilmPages = new Dictionary<string, string[]>
	{
		{ "ground", new[] { "jab", "ftilt", "utilt", "dtilt", "dash" } },
		{ "smash", new[] { "fsmash", "usmash", "dsmash" } },
		{ "air", new[] { "nair", "fair", "bair", "uair", "dair" } },
	};

	void BuildFilmPage(string[] names)
	{
		FighterData data = FighterCatalog.Get(Mathf.Max(0, Only));
		const float Column = 290.0f;
		const float Row = 430.0f;
		string[] beats = { "rest", "windup", "coil", "hit", "follow", "recover", "settled" };
		var pose = new Pose();
		int row = 0;
		foreach (string name in names)
		{
			MoveData move = FilmMove(data, name);
			if (move == null) continue;
			int s = move.StartupFrames, active = s + move.ActiveFrames, end = move.TotalFrames;
			int[] frames =
			{
				-2, s / 2, s, s + 1,
				active + Mathf.Max(1, move.EndlagFrames / 4),
				active + Mathf.Max(2, move.EndlagFrames * 2 / 3),
				end + 10,
			};
			for (int c = 0; c < frames.Length; c++)
			{
				var holder = new Node2D { Position = new Vector2(c * Column, row * Row) };
				AddChild(holder);
				var rig = new FighterRig();
				holder.AddChild(rig);
				if (!rig.Load(data.RigPath)) return;
				rig.Normalise(300.0f, 150.0f, 1.0f);
				rig.DramaScale = data.AnimationDrama;
				rig.Robotic = data.Robotic;
				rig.Inertia = data.Inertia;
				int last = FilmLeadIn + frames[c];
				var trail = new SmearTrail();
				for (int f = 0; f <= last; f++)
				{
					FilmFrame(rig, data, name, move, f, pose);
					if (move != null && SmearTrail.Smears(move) && f >= FilmLeadIn) trail.Record(rig, holder, f - FilmLeadIn);
				}
				if (move != null) holder.AddChild(SmearView(data, move, frames[c], trail));
				if (row == 0)
				{
					var head = new Label { Text = beats[c], Position = new Vector2(-50.0f, -280.0f) };
					head.AddThemeFontSizeOverride("font_size", 34);
					head.AddThemeColorOverride("font_color", new Color(0.16f, 0.16f, 0.20f));
					holder.AddChild(head);
				}
			}
			var label = new Label { Text = name + "\n" + move.MoveName, Position = new Vector2(-330.0f, row * Row - 120.0f) };
			label.AddThemeFontSizeOverride("font_size", 30);
			label.AddThemeColorOverride("font_color", new Color(0.16f, 0.16f, 0.20f));
			AddChild(label);
			row++;
		}
		var camera = new Camera2D
		{
			Position = new Vector2(Column * 3.0f - 120.0f, Row * (row - 1) * 0.5f - 40.0f),
			Zoom = Vector2.One * Mathf.Min(1040.0f / (Row * row + 160.0f), 1880.0f / (Column * 7 + 420.0f)),
		};
		AddChild(camera);
		camera.MakeCurrent();
	}

	void BuildFilm()
	{
		if (FilmPages.TryGetValue(Film, out string[] page))
		{
			BuildFilmPage(page);
			return;
		}
		FighterData data = FighterCatalog.Get(Mathf.Max(0, Only));
		MoveData move = FilmMove(data, Film);
		const int Cells = 18;
		const int PerRow = 6;
		const float Column = 300.0f;
		const float Row = 430.0f;

		// The frames to show: through the whole move and a little after, or a stretch of a clip.
		int first = move != null || Film == "roll" ? FilmLeadIn - 2 : 0;
		int span = move != null ? move.TotalFrames + 14
			: Film == "roll" ? Fighter.RollFrames + 8 : (Film == "jump" ? 60 : Film == "run" ? 34 : 54);
		var pose = new Pose();

		for (int cell = 0; cell < Cells; cell++)
		{
			int frame = first + Mathf.RoundToInt(span * cell / (float)(Cells - 1));
			var holder = new Node2D { Position = new Vector2(cell % PerRow * Column, cell / PerRow * Row) };
			AddChild(holder);
			var rig = new FighterRig();
			holder.AddChild(rig);
			if (!rig.Load(data.RigPath)) return;
			rig.Normalise(300.0f, 150.0f, 1.0f);
			rig.DramaScale = data.AnimationDrama;
			rig.Robotic = data.Robotic;
			rig.Inertia = data.Inertia;
			var trail = new SmearTrail();
			for (int f = 0; f <= frame; f++)
			{
				FilmFrame(rig, data, Film, move, f, pose);
				if (move != null && SmearTrail.Smears(move) && f >= FilmLeadIn) trail.Record(rig, holder, f - FilmLeadIn);
			}
			if (move != null) holder.AddChild(SmearView(data, move, frame - FilmLeadIn, trail));
			// The floor, for a roll: the ball should ride along it, never lift off or sink in.
			if (Film == "roll")
			{
				holder.AddChild(new Line2D
				{
					Points = new[] { new Vector2(-130.0f, 150.0f), new Vector2(130.0f, 150.0f) },
					Width = 3.0f,
					DefaultColor = new Color(0.16f, 0.16f, 0.20f),
				});
			}

			int moveFrame = frame - FilmLeadIn;
			bool live = move != null && moveFrame > move.StartupFrames && moveFrame <= move.StartupFrames + move.ActiveFrames;
			var label = new Label
			{
				Text = move != null ? $"{moveFrame}" : $"{frame}",
				Position = new Vector2(-30.0f, -200.0f),
			};
			label.AddThemeFontSizeOverride("font_size", 34);
			label.AddThemeColorOverride("font_color", live ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.16f, 0.16f, 0.20f));
			holder.AddChild(label);
		}

		var title = new Label
		{
			Text = $"{data.DisplayName}: {Film}" + (move != null ? $"  ({move.MoveName}: startup {move.StartupFrames}, active {move.ActiveFrames}, endlag {move.EndlagFrames} - red frames hit)" : ""),
			Position = new Vector2(-120.0f, -300.0f),
		};
		title.AddThemeFontSizeOverride("font_size", 34);
		title.AddThemeColorOverride("font_color", new Color(0.16f, 0.16f, 0.20f));
		AddChild(title);

		var camera = new Camera2D
		{
			Position = new Vector2(Column * (PerRow - 1) * 0.5f, Row * (Cells / PerRow - 1) * 0.5f - 40.0f),
			Zoom = Vector2.One * Mathf.Min(1000.0f / (Row * (Cells / PerRow) + 200.0f), 1880.0f / (Column * PerRow + 60.0f)),
		};
		AddChild(camera);
		camera.MakeCurrent();
	}

	static bool Stretches(FighterData d) => d.Move(MoveSlot.NeutralSpecial)?.Special == SpecialKind.Resize;

	/// <summary>A move's strike pose - what is on screen the frame its hitbox goes live.</summary>
	/// <remarks>
	/// Sampled with the fighter's own AnimationDrama, exactly as a match samples it - without it a
	/// dramatic fighter's blade showed here up to 30 degrees from where it is in a fight.
	/// </remarks>
	static Pose StrikeOf(FighterData data, MoveData move)
	{
		if (move == null) return null;
		var pose = new Pose();
		FighterAnimations.SampleAttack(move, FighterAnimations.ShowFrame(move), pose, drama: data.AnimationDrama);
		return pose;
	}

	static MoveData ComboHit(FighterData data, int hit)
	{
		MoveData move = data.Move(MoveSlot.Jab);
		for (int i = 0; i < hit && move != null; i++) move = move.ComboNext;
		return move;
	}

	/// <summary>
	/// A ring where a move's hitbox is, mapped from match space into this view. The two differ in
	/// scale and in where the body box sits against the drawing, so the point is carried across
	/// relative to the feet, which stand in the same place in both.
	/// </summary>
	static Node2D HitboxMarker(FighterData data, MoveData move)
	{
		Vector2 centre = HitboxInView(data, move);
		float radius = move.HitboxRadius * 300.0f / (data.BodySize.Y * 1.18f * data.VisualScale);
		// The hit reaches from inside the body out to the ring (Fighter.HitboxRoot): drawn as a
		// faint band, so the whole of what can hit shows, not only its far end.
		Vector2 offset = move.HitboxOffset;
		float side = Mathf.Abs(offset.X) < 1.0f ? 0.0f : Mathf.Sign(offset.X);
		bool reaches = string.IsNullOrEmpty(move.SwingArt) && move.SweepDegrees == 0.0f;
		Vector2 root = InView(data, new Vector2(side * data.BodySize.X * 0.15f,
			Mathf.Clamp(offset.Y, -data.BodySize.Y * 0.35f, data.BodySize.Y * 0.35f)));

		// And on past the ring as far as the hit reaches beyond the limb (Fighter.HitReach): a
		// thinner ring where it ends.
		(Vector2 tip, float tipRadius) = ReachInView(data, move);
		bool further = tip.DistanceTo(centre) > 1.0f || tipRadius > radius + 1.0f;

		var marker = new Node2D { ZIndex = 5 };
		marker.Draw += () =>
		{
			if (reaches) marker.DrawLine(root, centre, new Color(0.95f, 0.30f, 0.30f, 0.12f), radius * 2.0f);
			marker.DrawCircle(centre, radius, new Color(0.95f, 0.30f, 0.30f, 0.18f));
			marker.DrawArc(centre, radius, 0.0f, Mathf.Tau, 32, new Color(0.85f, 0.20f, 0.22f, 0.8f), 3.0f);
			if (further)
			{
				marker.DrawLine(centre, tip, new Color(0.95f, 0.30f, 0.30f, 0.08f), tipRadius * 2.0f);
				marker.DrawArc(tip, tipRadius, 0.0f, Mathf.Tau, 32, new Color(0.85f, 0.20f, 0.22f, 0.45f), 1.5f);
			}
		};
		return marker;
	}

	/// <summary>
	/// The smear a filmed move leaves at its frame, drawn behind the rig as Fighter.DrawSmear
	/// draws it in a match, reaching out as far as the hit does.
	/// </summary>
	static Node2D SmearView(FighterData data, MoveData move, int moveFrame, SmearTrail trail)
	{
		var view = new Node2D { ZIndex = -1 };
		float power = SmearTrail.Power(move);
		float strength = SmearTrail.Strength(move, moveFrame, power, out bool reaching);
		(Vector2 tip, float radius) = ReachInView(data, move);
		Vector2 centre = HitboxInView(data, move);
		Color colour = data.TrailColor.A > 0.0f ? data.TrailColor : data.PlaceholderColor;
		Vector2 pivot = InView(data, new Vector2(0.0f, -data.BodySize.Y * 0.12f));
		view.Draw += () => trail.Draw(view, move, centre, tip, radius, pivot, power, strength, reaching, colour, moveFrame);
		return view;
	}

	/// <summary>Where a move's hit reaches to past its hitbox (Fighter.HitReach), in this view.</summary>
	static (Vector2 tip, float radius) ReachInView(FighterData data, MoveData move)
	{
		float k = 300.0f / (data.BodySize.Y * 1.18f * data.VisualScale);
		Vector2 offset = move.HitboxOffset;
		float side = Mathf.Abs(offset.X) < 1.0f ? 0.0f : Mathf.Sign(offset.X);
		bool reaches = string.IsNullOrEmpty(move.SwingArt) && move.SweepDegrees == 0.0f;
		Vector2 centre = HitboxInView(data, move);
		Vector2 root = reaches ? InView(data, new Vector2(side * data.BodySize.X * 0.15f,
			Mathf.Clamp(offset.Y, -data.BodySize.Y * 0.35f, data.BodySize.Y * 0.35f))) : centre;
		return SmearTrail.Extend(root, centre, RadiusInView(data, move), SmearTrail.Reach(move) * k,
			InView(data, new Vector2(0.0f, -data.BodySize.Y * 0.12f)));
	}

	/// <summary>A point given as an offset from the fighter's middle in a match, in this view.</summary>
	static Vector2 InView(FighterData data, Vector2 matchOffset)
	{
		float k = 300.0f / (data.BodySize.Y * 1.18f * data.VisualScale);
		return new Vector2(matchOffset.X * k, (matchOffset.Y - data.BodySize.Y * 0.5f) * k + 150.0f);
	}

	/// <summary>A move's hitbox radius in this view.</summary>
	static float RadiusInView(FighterData data, MoveData move) =>
		move.HitboxRadius * 300.0f / (data.BodySize.Y * 1.18f * data.VisualScale);

	/// <summary>Where a move's hitbox is in this view, in its holder's space.</summary>
	static Vector2 HitboxInView(FighterData data, MoveData move)
	{
		float k = 300.0f / (data.BodySize.Y * 1.18f * data.VisualScale);
		float halfBody = data.BodySize.Y * 0.5f;
		return new Vector2(move.HitboxOffset.X * k, (move.HitboxOffset.Y - halfBody) * k + 150.0f);
	}

	/// <summary>The move an attack column shows, so its weapon can be put in the fighter's hand.</summary>
	static MoveData MoveFor(FighterData d, string label)
	{
		switch (label)
		{
			case "jab 1": return ComboHit(d, 0);
			case "jab 2": return ComboHit(d, 1);
			case "jab 3": return ComboHit(d, 2);
			case "f tilt": return d.Move(MoveSlot.ForwardTilt);
			case "u tilt": return d.Move(MoveSlot.UpTilt);
			case "d tilt": return d.Move(MoveSlot.DownTilt);
			case "dash": return d.Move(MoveSlot.DashAttack);
			case "f smash": return d.Move(MoveSlot.ForwardSmash);
			case "u smash": return d.Move(MoveSlot.UpSmash);
			case "d smash": return d.Move(MoveSlot.DownSmash);
			case "n air": return d.Move(MoveSlot.NeutralAir);
			case "f air": return d.Move(MoveSlot.ForwardAir);
			case "b air": return d.Move(MoveSlot.BackAir);
			case "u air": return d.Move(MoveSlot.UpAir);
			case "d air": return d.Move(MoveSlot.DownAir);
			default: return null;
		}
	}

	public override void _Ready()
	{
		if (!string.IsNullOrEmpty(Film))
		{
			BuildFilm();
			return;
		}
		var fighters = new FighterData[Only >= 0 ? 1 : FighterCatalog.Count];
		for (int i = 0; i < fighters.Length; i++) fighters[i] = FighterCatalog.Get(Only >= 0 ? Only : i);

		var columns = Attacks
			? new (string label, AnimationClip clip, System.Func<FighterData, Pose> direct)[]
			{
				("jab 1", null, d => StrikeOf(d, ComboHit(d, 0))),
				("jab 2", null, d => StrikeOf(d, ComboHit(d, 1))),
				("jab 3", null, d => StrikeOf(d, ComboHit(d, 2))),
				("f tilt", null, d => StrikeOf(d, d.Move(MoveSlot.ForwardTilt))),
				("u tilt", null, d => StrikeOf(d, d.Move(MoveSlot.UpTilt))),
				("d tilt", null, d => StrikeOf(d, d.Move(MoveSlot.DownTilt))),
				("dash", null, d => StrikeOf(d, d.Move(MoveSlot.DashAttack))),
				("f smash", null, d => StrikeOf(d, d.Move(MoveSlot.ForwardSmash))),
				("u smash", null, d => StrikeOf(d, d.Move(MoveSlot.UpSmash))),
				("d smash", null, d => StrikeOf(d, d.Move(MoveSlot.DownSmash))),
				("n air", null, d => StrikeOf(d, d.Move(MoveSlot.NeutralAir))),
				("f air", null, d => StrikeOf(d, d.Move(MoveSlot.ForwardAir))),
				("b air", null, d => StrikeOf(d, d.Move(MoveSlot.BackAir))),
				("u air", null, d => StrikeOf(d, d.Move(MoveSlot.UpAir))),
				("d air", null, d => StrikeOf(d, d.Move(MoveSlot.DownAir))),
			}
			: new (string label, AnimationClip clip, System.Func<FighterData, Pose> direct)[]
			{
				("idle", FighterAnimations.Idle, null),
				("run", FighterAnimations.Run, null),
				("jump", FighterAnimations.Jump, null),
				("fall", FighterAnimations.Fall, null),
				("land", FighterAnimations.Land, null),
				("crouch", FighterAnimations.Crouch, null),
				("block", FighterAnimations.Block, null),
				("hurt", FighterAnimations.Hurt, null),
				("windup", null, d => FighterAnimations.AttackWindup),
				("strike", null, d => FighterAnimations.AttackStrike),
				("dash", null, d => FighterAnimations.LungeStrike),
				("stretch", null, d => Stretches(d) ? FighterAnimations.TPose : null),
			};

		// One fighter on its own gets room: wider columns, wrapped onto two rows, so a long reach
		// - a stretched leg, a greatsword - is not lost in the next column's fighter.
		float ColumnWidth = Only >= 0 ? 330.0f : 238.0f;
		const float RowHeight = 420.0f;
		int perRow = Only >= 0 ? (columns.Length + 1) / 2 : columns.Length;
		int rowsEach = (columns.Length + perRow - 1) / perRow;

		for (int row = 0; row < fighters.Length; row++)
		{
			FighterData data = fighters[row];

			for (int index = 0; index < columns.Length; index++)
			{
				int col = index % perRow;
				int line = row * rowsEach + index / perRow;
				Pose direct = columns[index].direct?.Invoke(data);

				// A heavy's jab combo is two hits, so its third column is empty.
				if (columns[index].clip == null && direct == null) continue;

				// The rig positions ITSELF inside its parent (Normalise places the hip so the
				// feet land on the body box), so layout has to live on a holder above it
				// rather than on the rig's own transform.
				var holder = new Node2D
				{
					Position = new Vector2(col * ColumnWidth, line * RowHeight),
				};
				AddChild(holder);

				var rig = new FighterRig();
				holder.AddChild(rig);

				if (!rig.Load(data.RigPath))
				{
					GD.PushError($"RigParade: {data.DisplayName} failed to load {data.RigPath}");
					continue;
				}

				// Much larger than in a match, which is the entire point of this view.
				rig.Normalise(300.0f, 150.0f, 1.0f);
				rig.DramaScale = data.AnimationDrama;
				rig.Robotic = data.Robotic;
				rig.Inertia = data.Inertia;
				if (Size != 0 && Stretches(data)) rig.SetLegStretch(SizeLevels.LegStretch(Size), 150.0f);
				// Feet on the floor for everything done standing, exactly as in a match.
				string columnLabel = columns[index].label;
				rig.SetPlanted(columnLabel != "jump" && columnLabel != "fall" && !columnLabel.EndsWith("air"));
				// Standing on flat feet, as in a match (FighterRig.StandOnFeet).
				rig.SetStanding(columnLabel == "idle" || columnLabel == "block" || columnLabel == "stretch");

				// Each attack shows the weapon it puts in his hand, or an empty hand, as in a match.
				MoveData shown = Attacks ? MoveFor(data, columns[index].label) : null;
				// A standard attack done with the arms keeps both feet planted, as in a match.
				if (shown != null && IsStandardColumn(columnLabel) && FighterAnimations.StandsThrough(shown, MoveSlot.Jab))
				{
					rig.SetStanding(true);
				}
				// A hammer carried on the shoulder stays there for what is done on the ground without
				// a swing - and the dash, a head-first barge - as in a match.
				bool carrying = Attacks ? shown != null && shown.CarryOnShoulder
					: columnLabel is "idle" or "run" or "crouch" or "block" or "land" or "dash";
				rig.CarryOnShoulder = data.ShouldersProp && carrying;
				// And a hand on his hip, standing about.
				rig.HandOnHip = data.HandOnHip && !Attacks && columnLabel == "idle";
				// Looking behind him for a back air, as in a match.
				rig.SetLookingBack(shown != null && shown.LooksBack);
				if (shown != null)
				{
					rig.ShowProp(shown.PropArt);
					if (shown.PropArt == "-") rig.SetPartVisible(RigBone.PropFront, false);
				}

				// Blocking shows any block-only extra (the glint on Lug's hard hat), exactly as in a match.
				if (columns[index].clip == FighterAnimations.Block) rig.SetExtraVisible("hardhat", true);
				if (shown != null && !string.IsNullOrEmpty(shown.ShowExtra)) rig.SetExtraVisible(shown.ShowExtra, true);

				// Where the attack actually hits, drawn over the pose. A weapon that reaches past
				// its hitbox, or a hitbox floating off where no weapon is, is obvious here.
				if (shown != null && shown.HitboxRadius > 0.0f) holder.AddChild(HitboxMarker(data, shown));

				slots.Add(new Slot
				{
					Rig = rig,
					Clip = columns[index].clip,
					Direct = direct,
					Data = data,
					Move = shown,
					Label = columns[index].label,
					Holder = holder,
				});

				if (row != 0) continue;

				var label = new Label
				{
					Text = columns[index].label,
					Position = new Vector2(col * ColumnWidth - 40.0f, line * RowHeight - 230.0f),
				};
				label.AddThemeFontSizeOverride("font_size", 30);
				label.AddThemeColorOverride("font_color", new Color(0.16f, 0.16f, 0.20f));
				AddChild(label);
			}
		}

		int lines = fighters.Length * rowsEach;
		var camera = new Camera2D
		{
			Position = new Vector2(ColumnWidth * (perRow - 1) * 0.5f,
				RowHeight * (lines - 1) * 0.5f - 60.0f),
			// Zoomed out as the roster grows, so every fighter stays on one screen.
			Zoom = Vector2.One * Mathf.Min(0.78f, Mathf.Min(
				1000.0f / (lines * RowHeight + 120.0f),
				1880.0f / (perRow * ColumnWidth + 60.0f))),
		};
		AddChild(camera);
		camera.MakeCurrent();

		GD.Print($"RigParade: {slots.Count} rigs across {columns.Length} animations");
	}

	int frames;

	/// <summary>
	/// Once the poses have settled, prints where each attack's weapon head is against where its
	/// hitbox is, both in match units before weight scaling - the numbers CharacterNormals uses.
	/// </summary>
	void ReportReach()
	{
		foreach (Slot slot in slots)
		{
			if (slot.Move == null || slot.Move.HitboxRadius <= 0.0f) continue;
			Vector2? head = slot.Rig.PropHeadGlobal();
			if (head == null) continue;

			FighterData d = slot.Data;
			float k = 300.0f / (d.BodySize.Y * 1.18f * d.VisualScale);
			float range = WeightProfiles.ScaleFor(d.Weight).Range;
			Vector2 local = slot.Holder.ToLocal(head.Value);
			var match = new Vector2(local.X / k, (local.Y - 150.0f) / k + d.BodySize.Y * 0.5f) / range;
			Vector2 hit = slot.Move.HitboxOffset / range;
			GD.Print($"RigParade: {d.DisplayName,-9} {slot.Label,-8} weapon head ({match.X:0}, {match.Y:0})  hitbox ({hit.X:0}, {hit.Y:0})");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Attacks && ++frames == 60) ReportReach();

		foreach (Slot slot in slots)
		{
			if (slot.Clip != null && !slot.Clip.Loops)
			{
				// A one-shot clip run to completion shows its resting pose, which is the one
				// frame that demonstrates nothing. Hold it partway through instead.
				slot.Rig.PoseAt(slot.Clip, slot.Clip.LengthFrames * 0.25f, 0.3f);
			}
			else if (slot.Clip != null)
			{
				slot.Rig.Play(slot.Clip);
				slot.Rig.Advance();
			}
			else if (slot.Direct != null)
			{
				slot.Rig.ApplyDirect(slot.Direct, 0.3f);
				// A standard punch goes out in a straight line to its hit, as in a match.
				if (slot.Move != null && IsStandardColumn(slot.Label) && !slot.Rig.Robotic)
				{
					FighterAnimations.ThrowPunch(slot.Rig, slot.Move, FighterAnimations.ShowFrame(slot.Move),
						slot.Holder.ToGlobal(HitboxInView(slot.Data, slot.Move)), RadiusInView(slot.Data, slot.Move), 1,
						slot.Rig.HandEmpty(slot.Move.PropArt));
				}
			}

			// A kick with a stretching leg reaches out to its hitbox, as it does in a match (see
			// Fighter.LegReachNow) - so a hitbox off the line the leg points along shows here.
			if (slot.Move != null && slot.Move.StretchLeg)
			{
				Vector2? hip = slot.Rig.JointGlobal(RigBone.LegFrontUpper);
				float leg = slot.Rig.LegLength * slot.Rig.PuppetScale;
				if (hip.HasValue && leg > 1.0f)
				{
					Vector2 hit = HitboxInView(slot.Data, slot.Move);
					Vector2 reach = hit - slot.Holder.ToLocal(hip.Value);
					slot.Rig.SetFrontLegReach(Mathf.Max(1.0f, reach.Length() / leg));
					slot.Rig.AimFrontLeg(slot.Holder.ToGlobal(hit), 1.0f);
				}
			}
		}
	}
}
