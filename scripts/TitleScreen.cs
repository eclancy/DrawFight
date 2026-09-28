using System.Collections.Generic;
using Godot;

/// <summary>
/// The front door: two fighters trading a punch on a loop, the name of the game, and
/// PRESS START - nothing else.
///
/// The fight is driven by the real rig and the real shared animation library rather than by a
/// bespoke title animation. That is deliberate - the title screen then shows exactly what the
/// game looks like, and it gets better on its own every time the animations improve or a real
/// drawing replaces a stick figure.
/// </summary>
public partial class TitleScreen : Node2D
{
	readonly List<MenuNav> navs = new List<MenuNav>();
	readonly Pose attackPose = new Pose();

	FighterRig attacker;
	FighterRig victim;
	MoveData loopMove;

	int frame;
	bool leaving;

	/// <summary>Frames in one full cycle: wind up, hit, react, breathe, repeat.</summary>
	const int CycleFrames = 132;

	public override void _Ready()
	{
		foreach (IInputSource source in ControllerAssignment.ForPlayers(2))
		{
			navs.Add(new MenuNav(source));
		}

		loopMove = MoveData.PlaceholderHeavySwing();

		// Viewport coordinates, not world ones: this screen has no camera, so the node origin
		// is the top-left corner of the window.
		attacker = BuildFighter(FighterCatalog.Get(0), new Vector2(1150.0f, 560.0f), 1);
		victim = BuildFighter(FighterCatalog.Get(1), new Vector2(1570.0f, 560.0f), -1);

		var layer = new CanvasLayer();
		AddChild(layer);

		var root = new Control { AnchorsPreset = (int)Control.LayoutPreset.FullRect };
		layer.AddChild(root);

		root.AddChild(MenuTheme.MakeLabel("DrawFight", new Vector2(190.0f, 130.0f), 148, MenuTheme.Text));
		root.AddChild(MenuTheme.MakeLabel(
			"a fighting game made out of your drawings",
			new Vector2(200.0f, 338.0f), 30, MenuTheme.Soft));

		// The only instruction on the page. Any button works, so there is nothing else to say,
		// and quitting lives on the fighter select screen.
		pressStart = MenuTheme.MakeLabel("PRESS  START", new Vector2(200.0f, 830.0f), 54, MenuTheme.Ink);
		root.AddChild(pressStart);
	}

	Label pressStart;

	FighterRig BuildFighter(FighterData data, Vector2 position, int facing)
	{
		// A holder carries the layout position, because the rig positions itself inside its
		// own parent - the same reason RigParade needs one.
		var holder = new Node2D { Position = position };
		AddChild(holder);

		var rig = new FighterRig();
		holder.AddChild(rig);

		if (!rig.Load(data.RigPath)) return rig;

		rig.Normalise(380.0f, 190.0f, 1.0f);
		rig.SetFacing(facing);
		return rig;
	}

	public override void _PhysicsProcess(double delta)
	{
		frame++;
		AnimateBrawl();

		// Blink the prompt, because a static PRESS START does not look like it is waiting.
		if (pressStart != null)
		{
			pressStart.Modulate = new Color(1.0f, 1.0f, 1.0f, frame % 64 < 42 ? 1.0f : 0.25f);
		}

		if (leaving) return;

		foreach (MenuNav nav in navs)
		{
			nav.Poll();
			if (!nav.AnyPress) continue;

			leaving = true;
			GameRoot.Instance.GoCharacterSelect();
			return;
		}
	}

	/// <summary>One fighter swings, the other takes it, both reset. On a loop, forever.</summary>
	void AnimateBrawl()
	{
		if (attacker == null || !attacker.Loaded) return;

		int t = frame % CycleFrames;
		int moveLength = loopMove.TotalFrames;

		if (t < moveLength)
		{
			FighterAnimations.SampleAttack(loopMove, t, attackPose, lunging: true);
			attacker.ApplyDirect(attackPose, FighterAnimations.AttackBlend(loopMove, t));
		}
		else
		{
			attacker.Play(FighterAnimations.Idle);
			attacker.Advance();
		}

		if (victim == null || !victim.Loaded) return;

		// The victim reacts on the frame the hitbox would have been live, so the timing reads
		// as a real exchange rather than as two separate loops.
		int contact = loopMove.StartupFrames + 1;
		if (t >= contact && t < contact + 46)
		{
			victim.Play(FighterAnimations.Hurt);
			victim.Advance();
			victim.Modulate = new Color(1.0f, 0.68f, 0.68f);
		}
		else
		{
			victim.Play(FighterAnimations.Idle);
			victim.Advance();
			victim.Modulate = Colors.White;
		}
	}

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
	}
}
