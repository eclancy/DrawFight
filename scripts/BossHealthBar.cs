using Godot;

/// <summary>
/// The giant's health, across the top of the screen in a boss fight (BossFight): his name in his
/// own letters, glowing with his heat, over a long bar coloured in with pencil on paper and outlined
/// in the indigo every drawn outline is (.ai/art-direction.md) - a readout like the heat gauge, not
/// a box behind text. What a hit takes off shows pale for a moment before it drains, the bar jolts
/// with it, and a tick marks where he makes his last stand, so it can be seen coming.
/// </summary>
public partial class BossHealthBar : CanvasLayer
{
	public BossFight Fight;

	Control root;
	NameTag name;

	/// <summary>Where the bar is on the 1920x1080 screen the HUD is laid out on.</summary>
	static readonly Rect2 Bar = new Rect2(420.0f, 84.0f, 1080.0f, 44.0f);

	/// <summary>The health shown pale behind the real health - what was just lost - draining after a beat.</summary>
	float shownLost;
	int drainWait;
	const int DrainAfterFrames = 30;

	/// <summary>A jolt on each hit, bigger for a bigger one, settling in a few frames.</summary>
	float jolt;
	int flashFrames;
	int frames;
	int goneFrames;

	static readonly Color Paper = new Color(0.98f, 0.97f, 0.94f);
	static readonly Color Health = new Color(0.90f, 0.33f, 0.29f);
	static readonly Color HealthPale = new Color(0.98f, 0.80f, 0.74f);
	static readonly Color Lost = new Color(0.99f, 0.86f, 0.52f);
	static readonly Color Critical = new Color(0.99f, 0.62f, 0.24f);

	public override void _Ready()
	{
		Layer = 4;
		root = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);
		root.Draw += DrawBar;

		Fighter giant = Fight.Giant;
		name = new NameTag
		{
			FontSize = 50,
			Align = HorizontalAlignment.Center,
			FitWidth = true,
			Position = new Vector2(Bar.Position.X, 14.0f),
			Size = new Vector2(Bar.Size.X, 62.0f),
			Heat = giant.Data.HasHeat ? () => giant.IsOverheated ? 1.0f : giant.HeatFraction : null,
		};
		name.SetFighter(giant.Data);
		root.AddChild(name);
		shownLost = Fight.MaxHealth;
	}

	/// <summary>A hit landed on him: the jolt and the flash.</summary>
	public void Hit(float amount)
	{
		jolt = Mathf.Max(jolt, Mathf.Clamp(amount * 0.8f, 3.0f, 14.0f));
		flashFrames = 5;
		drainWait = DrainAfterFrames;
	}

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		if (flashFrames > 0) flashFrames--;
		jolt = Mathf.MoveToward(jolt * 0.82f, 0.0f, 0.2f);
		if (drainWait > 0) drainWait--;
		else shownLost = Mathf.MoveToward(shownLost, Fight.Health, Fight.MaxHealth / 90.0f);
		shownLost = Mathf.Max(shownLost, Fight.Health);

		// Gone once it is over, a moment after he is beaten, so the winning banner up here has the top.
		if (Fight.Defeated || Fight.PlayersOut) goneFrames++;
		root.Visible = goneFrames < 40;
		root.QueueRedraw();
	}

	void DrawBar()
	{
		var shake = new Vector2(CrayonBrush.Noise(frames, 3) * jolt, CrayonBrush.Noise(frames, 7) * jolt * 0.5f);
		Rect2 bar = new Rect2(Bar.Position + shake, Bar.Size);
		name.Position = new Vector2(Bar.Position.X, 14.0f) + shake;

		root.DrawRect(bar.Grow(8.0f), Paper);
		float max = Mathf.Max(1.0f, Fight.MaxHealth);
		float health = Mathf.Clamp(Fight.Health / max, 0.0f, 1.0f);
		float lost = Mathf.Clamp(shownLost / max, 0.0f, 1.0f);

		if (lost > health)
		{
			var pale = new Rect2(bar.Position.X + bar.Size.X * health, bar.Position.Y, bar.Size.X * (lost - health), bar.Size.Y);
			root.DrawRect(pale, Lost);
		}
		if (health > 0.0f)
		{
			var fill = new Rect2(bar.Position, new Vector2(bar.Size.X * health, bar.Size.Y));
			// Critical, it flickers hot, as he does.
			bool hot = Fight.LastStand && (frames / 6) % 2 == 0;
			Color colour = flashFrames > 0 ? Colors.White : hot ? Critical : Health;
			root.DrawRect(fill, flashFrames > 0 ? Colors.White : HealthPale);
			CrayonBrush.PencilFill(root, fill, colour, 41, 5.0f, 3.2f);
		}

		// Where his last stand starts.
		float tick = bar.Position.X + bar.Size.X * BossFight.LastStandAt;
		CrayonBrush.InkLine(root, new Vector2(tick, bar.Position.Y - 6.0f), new Vector2(tick, bar.End.Y + 6.0f), MenuTheme.Ink, 3.0f, 43, 1.0f);
		CrayonBrush.InkRect(root, bar, MenuTheme.Ink, 4.0f, 45, 2.0f);
	}
}
