using Godot;

/// <summary>
/// The frame the application icon is cropped from (--icon; tools/art/app_icon.py): Circy - Elim's
/// drawing, the first fighter a kid drew - standing idle on the game's lined notepad page, its
/// blue rules and red margin (Eric, 2026-10-19), drawn big, so the square is his face: his ball
/// and the tops of his legs, as the icon always was. It is the game rendering its own art, not a
/// drawing made for the icon; only the page's lines are drawn bolder than the menus', so they
/// still read once the icon is shrunk to a taskbar's 32 pixels. The icon is the square at the
/// top left of the screen, IconFrame.Square.
/// </summary>
public partial class IconFrame : Node2D
{
	public static readonly Rect2 Square = new Rect2(0.0f, 0.0f, 512.0f, 512.0f);

	FighterRig rig;

	public override void _Ready()
	{
		var holder = new Node2D { Position = new Vector2(370.0f, 430.0f) };
		AddChild(holder);
		rig = new FighterRig();
		holder.AddChild(rig);
		FighterData circy = FighterCatalog.Get(2);
		if (!rig.Load(circy)) return;
		rig.ArmBackLimit = circy.ArmReachBack;
		rig.Normalise(744.0f, 372.0f, 1.0f);
		rig.SetFacing(1);
		rig.SetPlanted(true);
		rig.SetStanding(true);
		rig.Play(FighterAnimations.Idle);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (rig != null && rig.Loaded) rig.Advance();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), MenuTheme.Paper);
		for (float y = 66.0f; y < Square.End.Y; y += 92.0f)
		{
			CrayonBrush.InkLine(this, new Vector2(0.0f, y), new Vector2(Square.End.X, y), MenuTheme.Rule.Darkened(0.12f), 7.0f, Mathf.RoundToInt(y), 1.6f);
		}
		CrayonBrush.InkLine(this, new Vector2(96.0f, 0.0f), new Vector2(96.0f, Square.End.Y), MenuTheme.Margin, 9.0f, 991, 2.0f);
	}
}
