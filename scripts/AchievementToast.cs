using System.Collections.Generic;
using Godot;

/// <summary>
/// "Achievement!" and its name, on a card that slides in at the top right over whatever is
/// showing - a fight, a menu - and out again, one at a time. Lives on GameRoot, above the screens,
/// so it survives the screen changing under it. It says what was done, not what it gave.
/// </summary>
public partial class AchievementToast : CanvasLayer
{
	readonly Queue<Achievement> waiting = new Queue<Achievement>();
	Achievement showing;
	int frames;
	Node2D card;

	/// <summary>How long one stays up, sliding in and out included.</summary>
	const int ShownFrames = 210;
	const int SlideFrames = 14;

	static readonly Vector2 Size = new Vector2(560.0f, 120.0f);
	static readonly Color Gold = new Color(1.0f, 0.82f, 0.25f);

	public override void _Ready()
	{
		Layer = 60;
		card = new Node2D();
		AddChild(card);
		card.Draw += DrawCard;
	}

	public void Add(Achievement achievement) => waiting.Enqueue(achievement);

	public override void _PhysicsProcess(double delta)
	{
		if (showing == null)
		{
			if (waiting.Count == 0) return;
			showing = waiting.Dequeue();
			frames = 0;
			SfxPlayer.Ui("ui_ready");
		}
		frames++;
		if (frames >= ShownFrames) showing = null;
		card.QueueRedraw();
	}

	void DrawCard()
	{
		if (showing == null) return;
		float viewport = card.GetViewportRect().Size.X;
		float into = Mathf.Min(1.0f, Mathf.Min(frames, ShownFrames - frames) / (float)SlideFrames);
		into = into * (2.0f - into);
		var box = new Rect2(new Vector2(viewport - 30.0f - Size.X * into, 30.0f), Size);
		card.DrawRect(new Rect2(box.Position + new Vector2(6.0f, 8.0f), box.Size), new Color(0.2f, 0.22f, 0.3f, 0.18f));
		card.DrawRect(box, MenuTheme.Paper);
		CrayonBrush.InkRect(card, box, showing.Hard ? Gold.Darkened(0.2f) : MenuTheme.Ink, 5.0f, 61, 2.0f);
		Item.DrawStarOn(card, box.Position + new Vector2(62.0f, 60.0f), 38.0f, Gold);
		Font font = ThemeDB.FallbackFont;
		card.DrawString(font, box.Position + new Vector2(118.0f, 48.0f), "Achievement!", HorizontalAlignment.Left, -1, 28, MenuTheme.Soft);
		card.DrawString(font, box.Position + new Vector2(118.0f, 94.0f), showing.Title, HorizontalAlignment.Left, Size.X - 140.0f, 40, MenuTheme.Text);
	}
}
