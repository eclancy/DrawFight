using System.Collections.Generic;
using Godot;

/// <summary>
/// "Your drawings": every fighter drawn in the game, saved, newest first, and a card to start a new
/// one. Opening this is when the game checks it can reach ecec.dev - so a kid finds out there is
/// no internet before drawing for an hour, not after - and says so, with the reassurance that
/// drawing and saving work anyway.
/// </summary>
public partial class DrawingsScreen : Node2D
{
	static readonly Rect2 BackBox = new Rect2(190.0f, 40.0f, 160.0f, 66.0f);
	const float CardWidth = 360.0f;
	const float CardHeight = 330.0f;
	const float Gap = 32.0f;
	const float GridTop = 230.0f;
	const int Columns = 4;

	readonly List<MenuCursor> cursors = new List<MenuCursor>();
	List<SavedDrawing> saved;
	Node2D cursorLayer;
	Vector2 mouse = new Vector2(-100.0f, -100.0f);
	float scroll;
	string confirmDelete;
	bool leaving;

	public override void _Ready()
	{
		FighterIntake.Check();
		saved = Drawing.ListSaved();

		int pads = Input.GetConnectedJoypads().Count;
		IInputSource[] sources = ControllerAssignment.ForPlayers(Mathf.Max(1, pads));
		for (int i = 0; i < sources.Length; i++)
		{
			cursors.Add(new MenuCursor(sources[i], i, i == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent,
				new Vector2(380.0f + i * 120.0f, 420.0f)));
		}
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in cursors) cursor.Draw(canvas);
		});
	}

	int CardCount => saved.Count + 1;

	Rect2 CardRect(int i)
	{
		int col = i % Columns, row = i / Columns;
		return new Rect2(190.0f + col * (CardWidth + Gap), GridTop + row * (CardHeight + Gap) - scroll, CardWidth, CardHeight);
	}

	Rect2 DeleteRect(int i)
	{
		Rect2 card = CardRect(i);
		return new Rect2(card.End.X - 58.0f, card.Position.Y + 12.0f, 46.0f, 46.0f);
	}

	static readonly Rect2 ConfirmBox = new Rect2(560.0f, 360.0f, 800.0f, 340.0f);
	static readonly Rect2 ConfirmYes = new Rect2(620.0f, 580.0f, 320.0f, 80.0f);
	static readonly Rect2 ConfirmNo = new Rect2(980.0f, 580.0f, 320.0f, 80.0f);

	float MaxScroll => Mathf.Max(0.0f, GridTop + Mathf.Ceil(CardCount / (float)Columns) * (CardHeight + Gap) - 1040.0f);

	public override void _Input(InputEvent @event)
	{
		if (leaving) return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			if (confirmDelete != null) confirmDelete = null;
			else Back();
			return;
		}
		if (@event is InputEventMouseMotion motion) mouse = motion.Position;
		if (@event is not InputEventMouseButton click || !click.Pressed) return;
		mouse = click.Position;
		if (click.ButtonIndex == MouseButton.WheelDown) scroll = Mathf.Min(MaxScroll, scroll + 80.0f);
		if (click.ButtonIndex == MouseButton.WheelUp) scroll = Mathf.Max(0.0f, scroll - 80.0f);
		if (click.ButtonIndex == MouseButton.Left) Press(click.Position);
	}

	public override void _PhysicsProcess(double delta)
	{
		FighterIntake.CheckEvery(20.0f);
		Vector2 viewport = GetViewportRect().Size;
		foreach (MenuCursor cursor in cursors)
		{
			if (leaving) return;
			cursor.Update(viewport);
			// A cursor pushed against the top or bottom of the screen scrolls the cards.
			if (cursor.Position.Y > viewport.Y - 30.0f) scroll = Mathf.Min(MaxScroll, scroll + 14.0f);
			if (cursor.Position.Y < 160.0f && cursor.Nav.Stick.Y < -0.5f) scroll = Mathf.Max(0.0f, scroll - 14.0f);
			if (cursor.Cancel)
			{
				if (confirmDelete != null) confirmDelete = null;
				else Back();
				return;
			}
			if (cursor.Confirm) Press(cursor.Position);
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Back()
	{
		leaving = true;
		SfxPlayer.Ui("ui_back");
		GameRoot.Instance.GoMainMenu();
	}

	void Press(Vector2 point)
	{
		if (leaving) return;
		if (confirmDelete != null)
		{
			if (ConfirmYes.HasPoint(point))
			{
				Drawing.Delete(confirmDelete);
				saved = Drawing.ListSaved();
				scroll = Mathf.Min(scroll, MaxScroll);
				SfxPlayer.Ui("ui_back");
				confirmDelete = null;
			}
			else if (ConfirmNo.HasPoint(point) || !ConfirmBox.HasPoint(point))
			{
				SfxPlayer.Ui("ui_back");
				confirmDelete = null;
			}
			return;
		}

		if (BackBox.HasPoint(point))
		{
			Back();
			return;
		}
		if (point.Y < GridTop - 20.0f) return;
		for (int i = 0; i < CardCount; i++)
		{
			if (i > 0 && DeleteRect(i).HasPoint(point))
			{
				SfxPlayer.Ui("ui_select");
				confirmDelete = saved[i - 1].Id;
				return;
			}
			if (!CardRect(i).HasPoint(point)) continue;
			leaving = true;
			SfxPlayer.Ui("ui_select");
			Drawing drawing = i == 0 ? Drawing.New() : Drawing.Load(saved[i - 1].Id);
			GameRoot.Instance.OpenDrawing(drawing ?? Drawing.New());
			return;
		}
	}

	bool Hot(Rect2 rect)
	{
		if (rect.HasPoint(mouse)) return true;
		foreach (MenuCursor cursor in cursors) if (rect.HasPoint(cursor.Position)) return true;
		return false;
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;

		for (int i = 0; i < CardCount; i++)
		{
			Rect2 card = CardRect(i);
			if (card.End.Y < 150.0f || card.Position.Y > size.Y) continue;
			DrawCard(i, card);
		}

		// The header goes over the cards as they scroll up under it.
		DrawRect(new Rect2(0.0f, 0.0f, size.X, 205.0f), MenuTheme.Paper);
		CrayonBrush.InkLine(this, new Vector2(150.0f, 0.0f), new Vector2(150.0f, 205.0f), MenuTheme.Margin, 3.0f, 991, 2.0f);
		MenuTheme.DrawButton(this, BackBox, "Back", 801, Hot(BackBox), fontSize: 32);
		DrawString(font, new Vector2(190.0f, 185.0f), "Your drawings", HorizontalAlignment.Left, -1, 60, MenuTheme.Text);

		(string words, Color light) = FighterIntake.Describe();
		DrawCircle(new Vector2(1200.0f, 74.0f), 12.0f, light);
		DrawString(font, new Vector2(1224.0f, 86.0f), words, HorizontalAlignment.Left, -1, 30, MenuTheme.Text);
		if (FighterIntake.State == FighterIntake.Link.Offline)
		{
			DrawString(font, new Vector2(1224.0f, 126.0f), "You can still draw and save.", HorizontalAlignment.Left, -1, 26, MenuTheme.Soft);
			DrawString(font, new Vector2(1224.0f, 160.0f), "Send it in when you're back online.", HorizontalAlignment.Left, -1, 26, MenuTheme.Soft);
		}

		if (saved.Count == 0)
		{
			DrawString(font, new Vector2(190.0f, 660.0f), "Draw a fighter, then send it in to be put in the game.", HorizontalAlignment.Left, -1, 32, MenuTheme.Soft);
			DrawString(font, new Vector2(190.0f, 706.0f), "Everything you draw is saved here.", HorizontalAlignment.Left, -1, 32, MenuTheme.Soft);
		}

		if (confirmDelete != null) DrawConfirm();
	}

	void DrawCard(int i, Rect2 card)
	{
		Font font = ThemeDB.FallbackFont;
		bool hot = Hot(card) && confirmDelete == null;
		DrawRect(new Rect2(card.Position + new Vector2(7.0f, 9.0f), card.Size), new Color(0.0f, 0.0f, 0.0f, 0.06f));
		DrawRect(card, new Color(1.0f, 1.0f, 0.99f));
		CrayonBrush.InkRect(this, card, MenuTheme.Ink, 4.0f, 820 + i, 2.0f);
		if (hot) CrayonBrush.InkRect(this, card.Grow(8.0f), MenuTheme.Accent, 6.0f, 860 + i, 2.6f);

		var thumb = new Rect2(card.Position + new Vector2(12.0f, 12.0f), new Vector2(card.Size.X - 24.0f, (card.Size.X - 24.0f) * 0.75f));
		if (i == 0)
		{
			DrawRect(thumb, MenuTheme.Paper);
			Vector2 c = thumb.GetCenter();
			CrayonBrush.InkLine(this, c + new Vector2(-60.0f, 0.0f), c + new Vector2(60.0f, 0.0f), MenuTheme.Ink, 12.0f, 841, 2.0f);
			CrayonBrush.InkLine(this, c + new Vector2(0.0f, -60.0f), c + new Vector2(0.0f, 60.0f), MenuTheme.Ink, 12.0f, 842, 2.0f);
			DrawString(font, new Vector2(card.Position.X, card.End.Y - 26.0f), "New drawing", HorizontalAlignment.Center, card.Size.X, 34, MenuTheme.Text);
			return;
		}

		SavedDrawing drawing = saved[i - 1];
		if (drawing.Thumbnail != null) DrawTextureRect(drawing.Thumbnail, thumb, false);
		else DrawRect(thumb, MenuTheme.Paper);
		DrawRect(thumb, MenuTheme.Rule, false, 2.0f);
		DrawString(font, new Vector2(card.Position.X + 14.0f, card.End.Y - 26.0f), drawing.Title,
			HorizontalAlignment.Left, card.Size.X - 130.0f, 30, MenuTheme.Text);
		if (drawing.Sent)
		{
			var badge = new Rect2(card.End.X - 112.0f, card.End.Y - 56.0f, 98.0f, 40.0f);
			DrawRect(badge, new Color(0.80f, 0.94f, 0.80f));
			MenuTheme.DrawCentredText(this, badge, "Sent!", 26, new Color(0.18f, 0.45f, 0.22f));
		}

		Rect2 delete = DeleteRect(i);
		bool deleteHot = Hot(delete) && confirmDelete == null;
		DrawRect(delete, new Color(1.0f, 1.0f, 1.0f, deleteHot ? 1.0f : 0.8f));
		Color cross = deleteHot ? new Color(0.86f, 0.30f, 0.30f) : MenuTheme.Soft;
		DrawLine(delete.Position + new Vector2(12.0f, 12.0f), delete.End - new Vector2(12.0f, 12.0f), cross, 5.0f, true);
		DrawLine(new Vector2(delete.End.X - 12.0f, delete.Position.Y + 12.0f), new Vector2(delete.Position.X + 12.0f, delete.End.Y - 12.0f), cross, 5.0f, true);
		CrayonBrush.InkRect(this, delete, cross, 2.5f, 870 + i, 1.4f);
	}

	void DrawConfirm()
	{
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0.9f, 0.92f, 0.95f, 0.75f));
		DrawRect(ConfirmBox, MenuTheme.Paper);
		CrayonBrush.InkRect(this, ConfirmBox, MenuTheme.Ink, 5.0f, 880, 2.4f);
		string title = "this drawing";
		foreach (SavedDrawing d in saved) if (d.Id == confirmDelete) title = d.Title;
		MenuTheme.DrawCentredText(this, new Rect2(ConfirmBox.Position + new Vector2(0.0f, 40.0f), new Vector2(ConfirmBox.Size.X, 60.0f)),
			$"Throw away {title}?", 42, MenuTheme.Text);
		MenuTheme.DrawCentredText(this, new Rect2(ConfirmBox.Position + new Vector2(0.0f, 110.0f), new Vector2(ConfirmBox.Size.X, 50.0f)),
			"It can't be got back.", 30, MenuTheme.Soft);
		MenuTheme.DrawButton(this, ConfirmYes, "Throw it away", 881, Hot(ConfirmYes), fontSize: 32);
		MenuTheme.DrawButton(this, ConfirmNo, "Keep it", 882, Hot(ConfirmNo), fontSize: 32);
	}
}
