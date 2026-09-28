using System.Collections.Generic;
using Godot;

/// <summary>
/// A debug view that lays out one copy of each fighter in every animation the shared library
/// contains, side by side and large. Run it with:
///
///     "$GODOT_BIN" --path . -- --parade --shot=90
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
	}

	readonly List<Slot> slots = new List<Slot>();
	readonly Pose scratch = new Pose();

	public override void _Ready()
	{
		var fighters = new[] { FighterData.PlaceholderLight(), FighterData.PlaceholderHeavy() };

		var columns = new (string label, AnimationClip clip, Pose direct)[]
		{
			("idle", FighterAnimations.Idle, null),
			("run", FighterAnimations.Run, null),
			("jump", FighterAnimations.Jump, null),
			("fall", FighterAnimations.Fall, null),
			("land", FighterAnimations.Land, null),
			("block", FighterAnimations.Block, null),
			("hurt", FighterAnimations.Hurt, null),
			("windup", null, FighterAnimations.AttackWindup),
			("strike", null, FighterAnimations.AttackStrike),
			("dash", null, FighterAnimations.LungeStrike),
		};

		const float ColumnWidth = 238.0f;
		const float RowHeight = 420.0f;

		for (int row = 0; row < fighters.Length; row++)
		{
			FighterData data = fighters[row];

			for (int col = 0; col < columns.Length; col++)
			{
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

				slots.Add(new Slot
				{
					Rig = rig,
					Clip = columns[col].clip,
					Direct = columns[col].direct,
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
			Position = new Vector2(ColumnWidth * (columns.Length - 1) * 0.5f, RowHeight * 0.4f),
			Zoom = new Vector2(0.78f, 0.78f),
		};
		AddChild(camera);
		camera.MakeCurrent();

		GD.Print($"RigParade: {slots.Count} rigs across {columns.Length} animations");
	}

	public override void _PhysicsProcess(double delta)
	{
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
