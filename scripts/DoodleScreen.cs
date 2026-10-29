using System.Collections.Generic;
using Godot;

/// <summary>
/// Doodle brawl's drawing page, between stage select and the fight: each person playing in turn -
/// "Player 1, draw your item!" in their seat's colour - draws one item on a blank page with their
/// own controller's pointer or the mouse, then says what it does: Throw it or Swing it. CPUs do
/// not draw. A page left blank is skipped. See .ai/modes.md.
///
/// It is the drawing screen cut down to one page and one layer: the same paper, the same brushes
/// (Painter), the same colours and sizes, the same one-pointer rule (OnePointer) - a smaller set
/// of tools, because this is a minute's doodle before a fight, not a fighter. Nothing drawn here is
/// saved or sent: it lives on GameRoot for the session (DoodleSet), so a rematch starts from it.
/// </summary>
public partial class DoodleScreen : Node2D
{
	/// <summary>Opens with the Throw it / Swing it choice up (--doodle=choice), to look at it.</summary>
	public bool OpenOnChoice;

	const int W = Drawing.Width;
	const int H = Drawing.Height;

	// --- Layout (1920x1080) ----------------------------------------------------------------------

	static readonly Rect2 View = new Rect2(184.0f, 108.0f, 1396.0f, 962.0f);
	static readonly Rect2 BackBox = new Rect2(16.0f, 18.0f, 150.0f, 70.0f);
	static readonly Rect2 DoneBox = new Rect2(1620.0f, 18.0f, 284.0f, 70.0f);
	static readonly Rect2 UndoBox = new Rect2(12.0f, 470.0f, 79.0f, 62.0f);
	static readonly Rect2 RedoBox = new Rect2(95.0f, 470.0f, 79.0f, 62.0f);
	static readonly Rect2 ClearBox = new Rect2(12.0f, 546.0f, 162.0f, 62.0f);
	static readonly Rect2 ColourBox = new Rect2(1594.0f, 112.0f, 306.0f, 84.0f);
	static readonly Rect2 TipBox = new Rect2(1594.0f, 620.0f, 306.0f, 360.0f);

	static Rect2 ToolRect(int i) => new Rect2(12.0f, 112.0f + i * 86.0f, 162.0f, 78.0f);
	static Rect2 SwatchRect(int i) => new Rect2(1594.0f + i % 6 * 52.0f, 210.0f + i / 6 * 52.0f, 46.0f, 46.0f);
	static Rect2 SizeRect(int i) => new Rect2(1594.0f + i * 52.0f, 532.0f, 46.0f, 46.0f);

	// The choice: what does it do?
	static readonly Rect2 ChoiceBox = new Rect2(460.0f, 130.0f, 1000.0f, 820.0f);
	static readonly Rect2 PreviewBox = new Rect2(760.0f, 236.0f, 400.0f, 300.0f);
	static readonly Rect2 ThrowBox = new Rect2(510.0f, 580.0f, 430.0f, 190.0f);
	static readonly Rect2 SwingBox = new Rect2(980.0f, 580.0f, 430.0f, 190.0f);
	static readonly Rect2 KeepDrawingBox = new Rect2(800.0f, 820.0f, 320.0f, 80.0f);

	// --- Tools ---------------------------------------------------------------------------------------

	static readonly PaintTool[] Tools = { PaintTool.Marker, PaintTool.Pencil, PaintTool.Bucket, PaintTool.Eraser };
	static readonly string[] ToolNames = { "Marker", "Pencil", "Bucket", "Eraser" };

	readonly Painter painter = new Painter();
	int toolIndex;
	readonly int[] sizePick = { 2, 2, 0, 3 };
	Color colour = DrawScreen.Palette[0];

	PaintTool Tool => Tools[toolIndex];

	/// <summary>Which of DrawScreen.Sizes a tool's brush comes from - the drawing screen's own.</summary>
	int SizeSet => Tool switch { PaintTool.Pencil => 1, PaintTool.Eraser => 3, _ => 0 };
	bool ToolHasSize => Tool is PaintTool.Marker or PaintTool.Pencil or PaintTool.Eraser;
	float BrushSize => DrawScreen.Sizes[SizeSet][sizePick[SizeSet]];

	static float ViewScale => Mathf.Min((View.Size.X - 30.0f) / W, (View.Size.Y - 30.0f) / H);
	static Vector2 Origin => View.GetCenter() - new Vector2(W, H) * ViewScale * 0.5f;
	static Vector2 ToCanvas(Vector2 screen) => (screen - Origin) / ViewScale;

	// --- Whose turn ---------------------------------------------------------------------------------

	/// <summary>The seats drawing, in seat order: everyone playing who is not a CPU.</summary>
	readonly List<int> turns = new List<int>();
	int turn;

	int Seat => turns[turn];
	Doodle Doc => GameRoot.Instance.Doodles.For(Seat);

	// --- Pointer --------------------------------------------------------------------------------

	bool painting;
	Vector2 pointer = new Vector2(-100.0f, -100.0f);
	float lastPressure = 1.0f;
	bool penInverted;

	readonly List<MenuCursor> padCursors = new List<MenuCursor>();
	OnePointer pointers;
	MenuCursor PadCursor => pointers?.Current;
	bool padWasHeld;
	Node2D cursorLayer;
	Node2D canvas;

	/// <summary>The Throw it / Swing it choice is up, and where the drawing is on its page, for its picture.</summary>
	bool choosing;
	Rect2I inkBounds;

	string toast = "";
	int toastFrames;

	public override void _Ready()
	{
		_ = Painter.Grain;
		Input.UseAccumulatedInput = false;

		GameRoot root = GameRoot.Instance;
		for (int seat = 0; seat < GameRoot.MaxPlayers; seat++)
		{
			if (root.SelectedFighters[seat] >= 0 && !root.CpuPlayers[seat]) turns.Add(seat);
		}
		if (turns.Count == 0) turns.Add(0);

		canvas = new Node2D { ZIndex = -1 };
		AddChild(canvas);
		canvas.Draw += DrawCanvas;
		cursorLayer = MenuCursor.MakeOverlay(this, c => pointers?.Draw(c));
		StartTurn();
		if (OpenOnChoice) Done();
	}

	public override void _ExitTree()
	{
		Input.UseAccumulatedInput = true;
		OnePointer.Leave();
	}

	/// <summary>
	/// The next person's go: their controller is the only pad that points, starting where they
	/// will draw. A page with last time's drawing on it is theirs to keep or change.
	/// </summary>
	void StartTurn()
	{
		painting = false;
		choosing = false;
		toolIndex = 0;
		colour = DrawScreen.Palette[0];
		IInputSource source = GameRoot.Instance.SourceForSeat(Seat);
		padCursors.Clear();
		padCursors.Add(new MenuCursor(source, Seat, MenuTheme.PlayerTint(Seat), View.GetCenter()));
		// A pad player's pointer is up straight away; anyone on the keyboard draws with the mouse.
		OnePointer.StartWith(source is GamepadInputSource);
		pointers = new OnePointer(padCursors);
		padWasHeld = true;
		if (!Doc.IsBlank) Toast("Your item from last time. Done keeps it.");
	}

	// --- Frame --------------------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (toastFrames > 0) toastFrames--;
		canvas.QueueRedraw();
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		bool wasPad = OnePointer.UsingPad;
		Vector2 was = PadCursor?.Position ?? Vector2.Zero;
		MenuCursor pad = pointers.Tick(GetViewportRect().Size, pointer);
		if (OnePointer.UsingPad && !wasPad)
		{
			if (painting) Release(pointer);
			padWasHeld = PadCursor != null && PadCursor.Nav.Held;
		}
		if (pad == null) return;

		// A held A draws, as on the drawing screen; B undoes, or puts the choice away.
		bool held = pad.Nav.Held;
		if (pad.Nav.Confirm) Press(pad.Position, 1.0f);
		else if (held && pad.Position != was) MoveTo(pad.Position, 1.0f);
		if (!held && padWasHeld) Release(pad.Position);
		padWasHeld = held;
		if (pad.Cancel)
		{
			if (choosing) CloseChoice();
			else Undo();
		}
	}

	// --- Input ----------------------------------------------------------------------------------

	public override void _Input(InputEvent @event)
	{
		// Escape here, before anything else sees it: elsewhere in the game it quits.
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			if (choosing) CloseChoice();
			else Back();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouse && OnePointer.UsingPad)
		{
			if (painting && PadCursor != null) Release(PadCursor.Position);
			padWasHeld = false;
		}
		if (@event is InputEventMouse) pointers.MouseUsed();
		switch (@event)
		{
			case InputEventMouseMotion motion:
				lastPressure = motion.Pressure > 0.0f ? motion.Pressure : 1.0f;
				penInverted = motion.PenInverted;
				MoveTo(motion.Position, lastPressure);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
				pointer = button.Position;
				if (button.Pressed) Press(button.Position, lastPressure);
				else Release(button.Position);
				break;
			case InputEventKey key when key.Pressed && !key.Echo && !choosing:
				if (key.CtrlPressed || key.MetaPressed)
				{
					if (key.Keycode == Key.Z && key.ShiftPressed) Redo();
					else if (key.Keycode == Key.Z) Undo();
					else if (key.Keycode == Key.Y) Redo();
					break;
				}
				switch (key.Keycode)
				{
					case Key.M: case Key.B: toolIndex = 0; break;
					case Key.P: toolIndex = 1; break;
					case Key.G: case Key.F: toolIndex = 2; break;
					case Key.E: toolIndex = 3; break;
					case Key.Bracketleft: StepSize(-1); break;
					case Key.Bracketright: StepSize(1); break;
				}
				break;
		}
	}

	void Press(Vector2 at, float pressure)
	{
		pointer = at;
		if (choosing)
		{
			PressChoice(at);
			return;
		}
		if (PressUi(at) || !View.HasPoint(at)) return;
		if (painting) Commit(painter.End());
		Vector2 c = ToCanvas(at);
		PaintTool tool = penInverted && Tool is PaintTool.Marker or PaintTool.Pencil ? PaintTool.Eraser : Tool;
		if (tool == PaintTool.Bucket)
		{
			Commit(Painter.Fill(Doc.Page, Doc.Layer, new Vector2I(Mathf.FloorToInt(c.X), Mathf.FloorToInt(c.Y)), colour, true));
			return;
		}
		float size = tool == PaintTool.Eraser ? DrawScreen.Sizes[3][sizePick[3]] : BrushSize;
		painter.Begin(Doc.Page, Doc.Layer, tool, colour, size, c, pressure);
		painting = true;
	}

	void MoveTo(Vector2 at, float pressure)
	{
		pointer = at;
		if (painting) painter.To(ToCanvas(at), pressure);
	}

	void Release(Vector2 at)
	{
		if (!painting) return;
		painter.To(ToCanvas(at), lastPressure);
		Commit(painter.End());
		painting = false;
	}

	void Commit(Edit edit)
	{
		if (edit != null) Doc.History.Push(edit);
	}

	void Undo()
	{
		if (painting) return;
		if (Doc.History.Undo() == null) Toast("Nothing to undo.");
	}

	void Redo()
	{
		if (!painting) Doc.History.Redo();
	}

	/// <summary>A clean page - undoable, like everything else.</summary>
	void StartAgain()
	{
		if (Doc.IsBlank) return;
		var all = new Rect2I(0, 0, W, H);
		byte[] before = PixelEdit.Cut(Doc.Layer.Pixels, all);
		System.Array.Clear(Doc.Layer.Pixels);
		Doc.Layer.TouchAll();
		Commit(new PixelEdit(Doc.Page, Doc.Layer, all, before, PixelEdit.Cut(Doc.Layer.Pixels, all)));
		SfxPlayer.Ui("ui_back");
	}

	void StepSize(int by)
	{
		if (ToolHasSize) sizePick[SizeSet] = Mathf.Clamp(sizePick[SizeSet] + by, 0, DrawScreen.Sizes[SizeSet].Length - 1);
	}

	void Toast(string text)
	{
		toast = text;
		toastFrames = 180;
	}

	/// <summary>Back to the stages. What everyone has drawn so far stays drawn.</summary>
	void Back()
	{
		if (painting) Commit(painter.End());
		painting = false;
		SfxPlayer.Ui("ui_back");
		GameRoot.Instance.GoStageSelect();
	}

	/// <summary>Finished drawing: what does it do? A blank page has nothing to ask about, and is skipped.</summary>
	void Done()
	{
		if (painting) Commit(painter.End());
		painting = false;
		if (DoodleSet.InkBounds(Drawing.Composite(Doc.Page)) is not Rect2I ink)
		{
			NextTurn();
			return;
		}
		inkBounds = ink;
		choosing = true;
		SfxPlayer.Ui("ui_select");
	}

	void CloseChoice()
	{
		choosing = false;
		SfxPlayer.Ui("ui_back");
	}

	void NextTurn()
	{
		turn++;
		if (turn < turns.Count)
		{
			SfxPlayer.Ui("ui_select");
			StartTurn();
			return;
		}
		SfxPlayer.Ui("ui_ready");
		GameRoot.Instance.GoMatch();
	}

	bool PressUi(Vector2 at)
	{
		if (BackBox.HasPoint(at)) { Back(); return true; }
		if (DoneBox.HasPoint(at)) { Done(); return true; }
		for (int i = 0; i < Tools.Length; i++)
		{
			if (!ToolRect(i).HasPoint(at)) continue;
			toolIndex = i;
			SfxPlayer.Ui("ui_move");
			return true;
		}
		if (UndoBox.HasPoint(at)) { Undo(); return true; }
		if (RedoBox.HasPoint(at)) { Redo(); return true; }
		if (ClearBox.HasPoint(at)) { StartAgain(); return true; }
		for (int i = 0; i < DrawScreen.Palette.Length; i++)
		{
			if (!SwatchRect(i).HasPoint(at)) continue;
			colour = DrawScreen.Palette[i];
			if (Tool == PaintTool.Eraser) toolIndex = 0;
			SfxPlayer.Ui("ui_move");
			return true;
		}
		for (int i = 0; i < 6; i++)
		{
			if (!SizeRect(i).HasPoint(at) || !ToolHasSize) continue;
			sizePick[SizeSet] = i;
			return true;
		}
		return !View.HasPoint(at);
	}

	void PressChoice(Vector2 at)
	{
		if (ThrowBox.HasPoint(at) || SwingBox.HasPoint(at))
		{
			Doc.Use = ThrowBox.HasPoint(at) ? ItemUse.Throw : ItemUse.Swing;
			Doc.Picked = true;
			choosing = false;
			NextTurn();
			return;
		}
		if (KeepDrawingBox.HasPoint(at) || !ChoiceBox.HasPoint(at)) CloseChoice();
	}

	// --- Drawing ---------------------------------------------------------------------------------

	bool Hot(Rect2 rect) => !choosing && rect.HasPoint(pointers.At(pointer));
	bool ChoiceHot(Rect2 rect) => rect.HasPoint(pointers.At(pointer));

	string Who
	{
		get
		{
			string name = GameRoot.Instance.SeatNames[Seat];
			return string.IsNullOrEmpty(name) ? $"Player {Seat + 1}" : name;
		}
	}

	void DrawCanvas()
	{
		canvas.DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), DrawScreen.Desk);
		float s = ViewScale;
		Vector2 o = Origin;
		canvas.DrawRect(new Rect2(o + new Vector2(10.0f, 12.0f), new Vector2(W, H) * s), new Color(0.0f, 0.0f, 0.0f, 0.10f));
		canvas.DrawSetTransform(o, 0.0f, new Vector2(s, s));
		DrawScreen.DrawLinedPage(canvas);
		// The pencilled reminder: how a swung item is held. Never part of the drawing.
		canvas.DrawString(ThemeDB.FallbackFont, new Vector2(150.0f, 84.0f), "Draw it standing up. You hold the bottom.",
			HorizontalAlignment.Left, -1, 34, DrawScreen.HintInk);
		Doc.Layer.DrawOn(canvas, Vector2.Zero, Colors.White);
		canvas.DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	public override void _Draw()
	{
		Font font = ThemeDB.FallbackFont;
		DrawTopBar(font);
		DrawTools(font);
		DrawColours(font);
		DrawTip(font);
		DrawBrushOutline();

		if (toastFrames > 0 && !choosing)
		{
			float alpha = Mathf.Min(1.0f, toastFrames / 20.0f);
			float width = font.GetStringSize(toast, HorizontalAlignment.Left, -1, 30).X + 60.0f;
			var box = new Rect2(View.GetCenter().X - width * 0.5f, View.End.Y - 86.0f, width, 62.0f);
			DrawRect(box, new Color(1.0f, 0.98f, 0.86f, 0.96f * alpha));
			CrayonBrush.InkRect(this, box, new Color(MenuTheme.Accent, alpha), 4.0f, 77, 1.6f);
			MenuTheme.DrawCentredText(this, box, toast, 30, new Color(MenuTheme.Text, alpha));
		}

		if (choosing) DrawChoice(font);
	}

	void DrawTopBar(Font font)
	{
		MenuTheme.DrawButton(this, BackBox, "Back", 901, Hot(BackBox), fontSize: 30);

		// Whose go it is, on a band of their colour - the colour their cursor and tag wear.
		string heading = $"{Who}, draw your item!";
		Vector2 size = font.GetStringSize(heading, HorizontalAlignment.Left, -1, 46);
		var band = new Rect2(184.0f, 20.0f, size.X + 56.0f, 72.0f);
		DrawRect(band, MenuTheme.PlayerTint(Seat).Lightened(0.35f));
		CrayonBrush.InkRect(this, band, MenuTheme.PlayerTint(Seat).Darkened(0.2f), 5.0f, 902 + Seat, 2.2f);
		MenuTheme.DrawCentredText(this, band, heading, 46, MenuTheme.Text);

		if (turns.Count > 1)
		{
			DrawString(font, new Vector2(DoneBox.Position.X - 210.0f, 66.0f), $"{turn + 1} of {turns.Count}",
				HorizontalAlignment.Right, 180.0f, 30, MenuTheme.Soft);
		}
		MenuTheme.DrawButton(this, DoneBox, "Done", 906, Hot(DoneBox), true, fontSize: 36);
	}

	void DrawTools(Font font)
	{
		for (int i = 0; i < Tools.Length; i++)
		{
			Rect2 box = ToolRect(i);
			MenuTheme.DrawButton(this, box, "", 910 + i, Hot(box), i == toolIndex);
			DrawScreen.DrawToolIcon(this, Tools[i], new Rect2(box.Position + new Vector2(6.0f, 6.0f), new Vector2(64.0f, 66.0f)), colour);
			DrawString(font, new Vector2(box.Position.X + 74.0f, box.GetCenter().Y + 9.0f), ToolNames[i],
				HorizontalAlignment.Left, box.Size.X - 78.0f, 24, MenuTheme.Text);
		}
		MenuTheme.DrawButton(this, UndoBox, "Undo", 920, Hot(UndoBox), enabled: Doc.History.CanUndo, fontSize: 22);
		MenuTheme.DrawButton(this, RedoBox, "Redo", 921, Hot(RedoBox), enabled: Doc.History.CanRedo, fontSize: 22);
		MenuTheme.DrawButton(this, ClearBox, "Start again", 922, Hot(ClearBox), fontSize: 24);
	}

	void DrawColours(Font font)
	{
		DrawRect(ColourBox, colour);
		CrayonBrush.InkRect(this, ColourBox, MenuTheme.Ink, 4.0f, 930, 1.6f);
		for (int i = 0; i < DrawScreen.Palette.Length; i++)
		{
			Rect2 swatch = SwatchRect(i);
			DrawRect(swatch, DrawScreen.Palette[i]);
			bool chosen = DrawScreen.Palette[i].IsEqualApprox(colour);
			DrawRect(swatch, chosen ? MenuTheme.Ink : MenuTheme.Rule, false, chosen ? 4.0f : 2.0f);
			if (Hot(swatch)) DrawRect(swatch.Grow(3.0f), MenuTheme.Accent, false, 3.0f);
		}
		DrawString(font, new Vector2(1594.0f, 520.0f), ToolHasSize ? "Size" : "Size (not for this tool)",
			HorizontalAlignment.Left, -1, 22, MenuTheme.Soft);
		for (int i = 0; i < 6; i++)
		{
			Rect2 box = SizeRect(i);
			bool chosen = ToolHasSize && sizePick[SizeSet] == i;
			MenuTheme.DrawButton(this, box, "", 940 + i, Hot(box) && ToolHasSize, chosen, ToolHasSize);
			DrawCircle(box.GetCenter(), 3.0f + i * 3.2f, ToolHasSize ? (Tool == PaintTool.Eraser ? MenuTheme.Soft : colour) : MenuTheme.Rule);
		}
	}

	/// <summary>The few words they need before they draw.</summary>
	void DrawTip(Font font)
	{
		DrawRect(TipBox, MenuTheme.Paper);
		CrayonBrush.InkRect(this, TipBox, MenuTheme.Rule, 3.0f, 950, 1.6f);
		DrawMultilineString(font, TipBox.Position + new Vector2(18.0f, 48.0f),
			"Draw one thing to fight with.\n\nDraw it standing up. You hold the bottom and hit with the top.\n\nThen pick: throw it, or swing it.",
			HorizontalAlignment.Left, TipBox.Size.X - 36.0f, 26, -1, MenuTheme.Text);
	}

	void DrawBrushOutline()
	{
		if (choosing || !ToolHasSize) return;
		Vector2 at = pointers.At(pointer);
		if (!View.HasPoint(at)) return;
		float radius = BrushSize * 0.5f * ViewScale;
		if (radius < 3.0f) return;
		DrawArc(at, radius, 0.0f, Mathf.Tau, 48, new Color(1.0f, 1.0f, 1.0f, 0.8f), 3.0f, true);
		DrawArc(at, radius, 0.0f, Mathf.Tau, 48, new Color(MenuTheme.Ink, 0.8f), 1.5f, true);
	}

	void DrawChoice(Font font)
	{
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0.9f, 0.92f, 0.95f, 0.7f));
		DrawRect(new Rect2(ChoiceBox.Position + new Vector2(8.0f, 10.0f), ChoiceBox.Size), new Color(0.0f, 0.0f, 0.0f, 0.08f));
		DrawRect(ChoiceBox, MenuTheme.Paper);
		CrayonBrush.InkRect(this, ChoiceBox, MenuTheme.PlayerTint(Seat).Darkened(0.2f), 5.0f, 960, 2.4f);
		MenuTheme.DrawCentredText(this, new Rect2(ChoiceBox.Position + new Vector2(0.0f, 30.0f), new Vector2(ChoiceBox.Size.X, 60.0f)),
			"What does it do?", 46, MenuTheme.Text);

		// Their drawing, fitted to the box - just as drawn.
		float s = Mathf.Min(PreviewBox.Size.X / inkBounds.Size.X, PreviewBox.Size.Y / inkBounds.Size.Y);
		Vector2 at = PreviewBox.GetCenter() - (Vector2)inkBounds.Size * s * 0.5f - (Vector2)inkBounds.Position * s;
		DrawSetTransform(at, 0.0f, new Vector2(s, s));
		Doc.Layer.DrawOn(this, Vector2.Zero, Colors.White);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);

		DrawChoiceButton(font, ThrowBox, "Throw it", "It bounces off whoever it hits.", ItemUse.Throw, 961);
		DrawChoiceButton(font, SwingBox, "Swing it", "Hold the bottom, hit with the top.", ItemUse.Swing, 962);
		MenuTheme.DrawButton(this, KeepDrawingBox, "Keep drawing", 963, ChoiceHot(KeepDrawingBox), fontSize: 30);
	}

	void DrawChoiceButton(Font font, Rect2 box, string title, string line, ItemUse use, int seed)
	{
		// Last time's pick shows, so keeping a drawing is one press of the same button.
		MenuTheme.DrawButton(this, box, "", seed, ChoiceHot(box), Doc.Picked && Doc.Use == use, hotTint: MenuTheme.PlayerTint(Seat));
		MenuTheme.DrawCentredText(this, new Rect2(box.Position + new Vector2(0.0f, 18.0f), new Vector2(box.Size.X, 60.0f)), title, 44, MenuTheme.Text);
		MenuTheme.DrawCentredText(this, new Rect2(box.Position + new Vector2(0.0f, 76.0f), new Vector2(box.Size.X, 40.0f)), line, 24, MenuTheme.Soft);

		// A pencilled arrow for what it does: an arc flying off, or a swing over the top.
		Vector2 c = box.Position + new Vector2(box.Size.X * 0.5f, 150.0f);
		var points = new List<Vector2>();
		for (int i = 0; i <= 12; i++)
		{
			float t = i / 12.0f;
			points.Add(use == ItemUse.Throw
				? c + new Vector2(-110.0f + 220.0f * t, -26.0f * Mathf.Sin(t * Mathf.Pi) + 10.0f)
				: c + new Vector2(0.0f, 30.0f) + Vector2.FromAngle(Mathf.Lerp(-2.6f, -0.3f, t)) * 56.0f);
		}
		for (int i = 1; i < points.Count; i++) CrayonBrush.InkLine(this, points[i - 1], points[i], MenuTheme.Ink, 4.0f, seed * 10 + i, 0.4f);
		Vector2 tip = points[^1], way = (points[^1] - points[^2]).Normalized();
		CrayonBrush.InkLine(this, tip, tip - way.Rotated(0.55f) * 16.0f, MenuTheme.Ink, 4.0f, seed * 10 + 20, 0.3f);
		CrayonBrush.InkLine(this, tip, tip - way.Rotated(-0.55f) * 16.0f, MenuTheme.Ink, 4.0f, seed * 10 + 21, 0.3f);
	}
}
