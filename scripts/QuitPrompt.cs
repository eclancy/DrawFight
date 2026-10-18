using System.Collections.Generic;
using Godot;

/// <summary>
/// "Are you sure you want to quit?" - QUIT asks first (Eric, 2026-10-18), so a stray press on
/// the main menu or fighter select does not close the game. Drawn over the screen on its cursor
/// layer; while it is open, a press answers it and B means no.
/// </summary>
public sealed class QuitPrompt
{
	public bool Open;

	static readonly Rect2 Box = new Rect2(560.0f, 340.0f, 800.0f, 360.0f);
	static readonly Rect2 YesBox = new Rect2(620.0f, 560.0f, 320.0f, 90.0f);
	static readonly Rect2 NoBox = new Rect2(980.0f, 560.0f, 320.0f, 90.0f);

	/// <summary>A press while it is open: Yes quits; No, or anywhere else, closes it.</summary>
	public void Press(Node from, Vector2 at)
	{
		if (YesBox.HasPoint(at))
		{
			from.GetTree().Quit();
			return;
		}
		Open = false;
		SfxPlayer.Ui("ui_back");
	}

	public void Cancel()
	{
		Open = false;
		SfxPlayer.Ui("ui_back");
	}

	public void Draw(CanvasItem canvas, IEnumerable<Vector2> pointers)
	{
		if (!Open) return;
		canvas.DrawRect(new Rect2(0.0f, 0.0f, 1920.0f, 1080.0f), new Color(0.9f, 0.92f, 0.95f, 0.75f));
		canvas.DrawRect(new Rect2(Box.Position + new Vector2(8.0f, 10.0f), Box.Size), new Color(0.0f, 0.0f, 0.0f, 0.08f));
		canvas.DrawRect(Box, MenuTheme.Paper);
		CrayonBrush.InkRect(canvas, Box, MenuTheme.Ink, 5.0f, 880, 2.4f);
		MenuTheme.DrawCentredText(canvas, new Rect2(Box.Position + new Vector2(0.0f, 60.0f), new Vector2(Box.Size.X, 80.0f)),
			"Are you sure you want to quit?", 46, MenuTheme.Text);
		bool yesHot = false, noHot = false;
		foreach (Vector2 p in pointers)
		{
			yesHot |= YesBox.HasPoint(p);
			noHot |= NoBox.HasPoint(p);
		}
		MenuTheme.DrawButton(canvas, YesBox, "Yes, quit", 881, yesHot, fontSize: 36);
		MenuTheme.DrawButton(canvas, NoBox, "No", 882, noHot, fontSize: 36);
	}
}
