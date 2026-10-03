using System.Collections.Generic;
using Godot;

/// <summary>
/// A debug view that lays out one copy of each fighter in every animation the shared library
/// contains, side by side and large. Run it with:
///
///     "$GODOT_BIN" --path . -- --parade --shot=90
///     "$GODOT_BIN" --path . -- --parade --attacks --shot=90   (every fighter's own attacks)
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

	/// <summary>A move's strike pose - what is on screen the frame its hitbox goes live.</summary>
	static Pose StrikeOf(MoveData move)
	{
		if (move == null) return null;
		var pose = new Pose();
		FighterAnimations.SampleAttack(move, FighterAnimations.ShowFrame(move), pose);
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
		float matchHeight = data.BodySize.Y * 1.18f * data.VisualScale;
		float k = 300.0f / matchHeight;
		float halfBody = data.BodySize.Y * 0.5f;
		var centre = new Vector2(move.HitboxOffset.X * k, (move.HitboxOffset.Y - halfBody) * k + 150.0f);
		float radius = move.HitboxRadius * k;

		var marker = new Node2D { ZIndex = 5 };
		marker.Draw += () =>
		{
			marker.DrawCircle(centre, radius, new Color(0.95f, 0.30f, 0.30f, 0.18f));
			marker.DrawArc(centre, radius, 0.0f, Mathf.Tau, 32, new Color(0.85f, 0.20f, 0.22f, 0.8f), 3.0f);
		};
		return marker;
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
		var fighters = new FighterData[Only >= 0 ? 1 : FighterCatalog.Count];
		for (int i = 0; i < fighters.Length; i++) fighters[i] = FighterCatalog.Get(Only >= 0 ? Only : i);

		var columns = Attacks
			? new (string label, AnimationClip clip, System.Func<FighterData, Pose> direct)[]
			{
				("jab 1", null, d => StrikeOf(ComboHit(d, 0))),
				("jab 2", null, d => StrikeOf(ComboHit(d, 1))),
				("jab 3", null, d => StrikeOf(ComboHit(d, 2))),
				("f tilt", null, d => StrikeOf(d.Move(MoveSlot.ForwardTilt))),
				("u tilt", null, d => StrikeOf(d.Move(MoveSlot.UpTilt))),
				("d tilt", null, d => StrikeOf(d.Move(MoveSlot.DownTilt))),
				("dash", null, d => StrikeOf(d.Move(MoveSlot.DashAttack))),
				("f smash", null, d => StrikeOf(d.Move(MoveSlot.ForwardSmash))),
				("u smash", null, d => StrikeOf(d.Move(MoveSlot.UpSmash))),
				("d smash", null, d => StrikeOf(d.Move(MoveSlot.DownSmash))),
				("n air", null, d => StrikeOf(d.Move(MoveSlot.NeutralAir))),
				("f air", null, d => StrikeOf(d.Move(MoveSlot.ForwardAir))),
				("b air", null, d => StrikeOf(d.Move(MoveSlot.BackAir))),
				("u air", null, d => StrikeOf(d.Move(MoveSlot.UpAir))),
				("d air", null, d => StrikeOf(d.Move(MoveSlot.DownAir))),
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
			};

		const float ColumnWidth = 238.0f;
		const float RowHeight = 420.0f;

		for (int row = 0; row < fighters.Length; row++)
		{
			FighterData data = fighters[row];

			for (int col = 0; col < columns.Length; col++)
			{
				Pose direct = columns[col].direct?.Invoke(data);

				// A heavy's jab combo is two hits, so its third column is empty.
				if (columns[col].clip == null && direct == null) continue;

				// The rig positions ITSELF inside its parent (Normalise places the hip so the
				// feet land on the body box), so layout has to live on a holder above it
				// rather than on the rig's own transform.
				var holder = new Node2D
				{
					Position = new Vector2(col * ColumnWidth, row * RowHeight),
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

				// Each attack shows the weapon it puts in his hand, or an empty hand, as in a match.
				MoveData shown = Attacks ? MoveFor(data, columns[col].label) : null;
				if (shown != null)
				{
					rig.ShowProp(shown.PropArt);
					if (shown.PropArt == "-") rig.SetPartVisible(RigBone.PropFront, false);
				}

				// Blocking puts on any block-only extra (Lug's hard hat), exactly as in a match.
				if (columns[col].clip == FighterAnimations.Block) rig.SetExtraVisible("hardhat", true);
				if (shown != null && !string.IsNullOrEmpty(shown.ShowExtra)) rig.SetExtraVisible(shown.ShowExtra, true);

				// Where the attack actually hits, drawn over the pose. A weapon that reaches past
				// its hitbox, or a hitbox floating off where no weapon is, is obvious here.
				if (shown != null && shown.HitboxRadius > 0.0f) holder.AddChild(HitboxMarker(data, shown));

				slots.Add(new Slot
				{
					Rig = rig,
					Clip = columns[col].clip,
					Direct = direct,
					Data = data,
					Move = shown,
					Label = columns[col].label,
					Holder = holder,
				});

				if (row != 0) continue;

				var label = new Label
				{
					Text = columns[col].label,
					Position = new Vector2(col * ColumnWidth - 40.0f, -230.0f),
				};
				label.AddThemeFontSizeOverride("font_size", 30);
				label.AddThemeColorOverride("font_color", new Color(0.16f, 0.16f, 0.20f));
				AddChild(label);
			}
		}

		var camera = new Camera2D
		{
			Position = new Vector2(ColumnWidth * (columns.Length - 1) * 0.5f,
				RowHeight * (fighters.Length - 1) * 0.5f - 60.0f),
			// Zoomed out as the roster grows, so every fighter stays on one screen.
			Zoom = Vector2.One * Mathf.Min(0.78f, Mathf.Min(
				1000.0f / (fighters.Length * RowHeight + 120.0f),
				1880.0f / (columns.Length * ColumnWidth + 60.0f))),
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
			}
		}
	}
}
