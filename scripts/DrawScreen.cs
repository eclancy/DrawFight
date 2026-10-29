using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The drawing screen: a kid draws a fighter here, in the game's own look, and sends it in.
///
/// The page is lined paper, like every menu. The marker is for outlines and the coloured pencil
/// is for colouring in, and the paint bucket fills in pencil too, so a drawing made here sits
/// with the game's own coloured-pencil art (Painter). Pages hold the fighter and the things the
/// fighter uses - something they shoot, something they hold - and each page has layers. On the
/// fighter page a new layer is named after a body part, from buttons rather than typing, because
/// one part per layer, named, is exactly what the importer's layered path wants and "a
/// nine-year-old naming layers is not a spec" (.ai/art-pipeline.md).
///
/// Mouse, pen and touch draw; a pad's cursor can too, with A held down - one or the other, never
/// both: whichever was used last is the pointer, and the only one shown (OnePointer). Everything is saved as
/// it goes - a minute after a change, and on the way out - so nothing is ever lost to Escape or
/// to closing the window.
/// </summary>
public partial class DrawScreen : Node2D
{
	public Drawing Doc;

	const int W = Drawing.Width;
	const int H = Drawing.Height;

	// --- Layout (1920x1080) ----------------------------------------------------------------------

	static readonly Rect2 View = new Rect2(184.0f, 108.0f, 1396.0f, 962.0f);
	static readonly Rect2 BackBox = new Rect2(16.0f, 18.0f, 150.0f, 70.0f);
	static readonly Rect2 SaveBox = new Rect2(1446.0f, 18.0f, 160.0f, 70.0f);
	static readonly Rect2 SendBox = new Rect2(1620.0f, 18.0f, 284.0f, 70.0f);
	static readonly Rect2 UndoBox = new Rect2(12.0f, 636.0f, 79.0f, 62.0f);
	static readonly Rect2 RedoBox = new Rect2(95.0f, 636.0f, 79.0f, 62.0f);
	static readonly Rect2 ZoomOutBox = new Rect2(12.0f, 712.0f, 50.0f, 54.0f);
	static readonly Rect2 FitBox = new Rect2(66.0f, 712.0f, 54.0f, 54.0f);
	static readonly Rect2 ZoomInBox = new Rect2(124.0f, 712.0f, 50.0f, 54.0f);
	static readonly Rect2 ColourBox = new Rect2(1594.0f, 112.0f, 96.0f, 84.0f);
	static readonly Rect2 MixBox = new Rect2(1700.0f, 120.0f, 200.0f, 68.0f);
	static readonly Rect2 AddLayerBox = new Rect2(1780.0f, 612.0f, 120.0f, 48.0f);
	const int LayerSlots = 6;

	static Rect2 TabRect(int i) => new Rect2(184.0f + i * 144.0f, 24.0f, 136.0f, 64.0f);
	static Rect2 TabCloseRect(int i) => new Rect2(TabRect(i).End.X - 34.0f, TabRect(i).Position.Y + 4.0f, 30.0f, 30.0f);
	static Rect2 ToolRect(int i) => new Rect2(12.0f, 112.0f + i * 86.0f, 162.0f, 78.0f);
	static Rect2 SwatchRect(int i) => new Rect2(1594.0f + i % 6 * 52.0f, 210.0f + i / 6 * 52.0f, 46.0f, 46.0f);
	static Rect2 SizeRect(int i) => new Rect2(1594.0f + i * 52.0f, 512.0f, 46.0f, 46.0f);
	static Rect2 FillRect(int i) => new Rect2(1594.0f + i * 156.0f, 566.0f, 150.0f, 44.0f);
	static Rect2 LayerRow(int slot) => new Rect2(1594.0f, 668.0f + slot * 48.0f, 306.0f, 44.0f);
	static Rect2 EyeRect(int slot) => new Rect2(1598.0f, 672.0f + slot * 48.0f, 36.0f, 36.0f);
	static Rect2 LayerButton(int k) => new Rect2(1594.0f + k * 61.5f, 1014.0f, 60.0f, 52.0f);

	// --- Tools ---------------------------------------------------------------------------------------

	static readonly PaintTool[] Tools = { PaintTool.Marker, PaintTool.Pencil, PaintTool.Bucket, PaintTool.Eraser, PaintTool.Picker, PaintTool.Move };
	static readonly string[] ToolNames = { "Marker", "Pencil", "Bucket", "Eraser", "Pick", "Move" };
	static readonly string[] ToolArt = { "marker", "pencil", "paint_bucket", "eraser", null, null };
	static readonly string[] ToolHints =
	{
		"For outlines. Draw them dark, so they show up in the game.",
		"For colouring in. Go over it again to make it darker.",
		"Fills a shape. The lines round it have to join up.",
		"Rubs out, on this layer only.",
		"Click a colour in your drawing to use it.",
		"Drag to move everything on this layer.",
	};

	/// <summary>Brush sizes, in pixels across, for the marker, pencil and eraser. Shared with the doodle screen.</summary>
	public static readonly float[][] Sizes =
	{
		new[] { 4.0f, 8.0f, 14.0f, 22.0f, 34.0f, 50.0f },
		new[] { 8.0f, 14.0f, 22.0f, 34.0f, 50.0f, 76.0f },
		new[] { 0.0f },
		new[] { 8.0f, 16.0f, 28.0f, 44.0f, 64.0f, 96.0f },
	};

	/// <summary>A big box of colours: greys, skin tones, and a rainbow, light and dark. Shared with the doodle screen.</summary>
	public static readonly Color[] Palette =
	{
		new Color(0.08f, 0.08f, 0.10f), new Color(0.30f, 0.30f, 0.33f), new Color(0.58f, 0.58f, 0.62f),
		new Color(0.82f, 0.82f, 0.85f), new Color(1.00f, 1.00f, 1.00f), new Color(0.36f, 0.22f, 0.12f),
		new Color(0.58f, 0.36f, 0.18f), new Color(0.82f, 0.62f, 0.40f), new Color(0.98f, 0.84f, 0.70f),
		new Color(0.86f, 0.62f, 0.44f), new Color(0.55f, 0.34f, 0.22f), new Color(0.62f, 0.08f, 0.12f),
		new Color(0.90f, 0.16f, 0.18f), new Color(0.97f, 0.52f, 0.70f), new Color(1.00f, 0.78f, 0.86f),
		new Color(0.98f, 0.52f, 0.12f), new Color(1.00f, 0.74f, 0.45f), new Color(1.00f, 0.86f, 0.14f),
		new Color(1.00f, 0.95f, 0.55f), new Color(0.62f, 0.86f, 0.20f), new Color(0.18f, 0.66f, 0.26f),
		new Color(0.08f, 0.38f, 0.18f), new Color(0.10f, 0.62f, 0.62f), new Color(0.45f, 0.76f, 0.98f),
		new Color(0.16f, 0.40f, 0.90f), new Color(0.10f, 0.16f, 0.48f), new Color(0.52f, 0.26f, 0.78f),
		new Color(0.76f, 0.64f, 0.96f), new Color(0.84f, 0.18f, 0.62f), new Color(0.84f, 0.64f, 0.16f),
	};

	/// <summary>A new layer on the fighter page is named for a body part, by button.</summary>
	static readonly string[] PartNames = { "Head", "Body", "Front arm", "Back arm", "Front leg", "Back leg", "Held thing", "Just a layer" };

	/// <summary>On a stage page, a new layer is named for a part of the stage, the same way.</summary>
	static readonly string[] StagePartNames = { "Background", "Ground", "Platforms", "Hazard", "Moving part", "Just a layer" };

	string[] LayerNames => Page.Kind == PageKind.Stage ? StagePartNames : PartNames;

	/// <summary>The pages a drawing can add, and what each is called: a fighter's things, or a stage's.</summary>
	PageKind[] AddableKinds => Doc.IsStage
		? new[] { PageKind.Hazard, PageKind.Mover, PageKind.Extra }
		: new[] { PageKind.Shot, PageKind.Held, PageKind.Extra };

	readonly Painter painter = new Painter();
	int toolIndex;
	int toolBeforePick;
	readonly int[] sizePick = { 1, 2, 0, 2 };
	Color colour = Palette[0];
	bool solidFill;

	// --- View ---------------------------------------------------------------------------------------

	float zoom = 1.0f;
	Vector2 pan;

	static float FitScale => Mathf.Min((View.Size.X - 30.0f) / W, (View.Size.Y - 30.0f) / H);
	float ViewScale => FitScale * zoom;
	Vector2 Origin => View.GetCenter() - new Vector2(W, H) * ViewScale * 0.5f + pan;
	Vector2 ToCanvas(Vector2 screen) => (screen - Origin) / ViewScale;

	// --- Pointer --------------------------------------------------------------------------------

	enum Drag { None, Paint, Move, Pan }
	Drag drag;
	Vector2 dragFrom;
	Vector2 moveOffset;
	Vector2 pointer = new Vector2(-100.0f, -100.0f);
	float lastPressure = 1.0f;
	bool penInverted;
	bool spaceHeld;

	/// <summary>The pad's cursor - one, the first pad's - and whether it or the mouse is the pointer.</summary>
	readonly List<MenuCursor> padCursors = new List<MenuCursor>();
	OnePointer pointers;
	MenuCursor padCursor => pointers?.Current;
	bool padWasHeld;
	Node2D cursorLayer;

	// --- Popups --------------------------------------------------------------------------------

	enum Popup { None, AddPage, AddPart, Rename, DeletePage, Mix }
	Popup popup;
	bool renamingPage;
	static readonly Rect2 PopupBox = new Rect2(560.0f, 230.0f, 800.0f, 600.0f);
	static readonly Rect2 MixPopupBox = new Rect2(560.0f, 150.0f, 800.0f, 800.0f);
	static readonly Rect2 CancelBox = new Rect2(1110.0f, 740.0f, 220.0f, 64.0f);
	static readonly Rect2 OkBox = new Rect2(870.0f, 560.0f, 200.0f, 70.0f);
	static readonly Rect2 RenameCancelBox = new Rect2(1090.0f, 560.0f, 200.0f, 70.0f);
	static readonly Rect2 YesBox = new Rect2(620.0f, 560.0f, 320.0f, 80.0f);
	static readonly Rect2 NoBox = new Rect2(980.0f, 560.0f, 320.0f, 80.0f);
	static readonly Rect2 DoneBox = new Rect2(1110.0f, 860.0f, 200.0f, 66.0f);
	static Rect2 KindRect(int i) => new Rect2(620.0f, 360.0f + i * 100.0f, 680.0f, 84.0f);
	static Rect2 PartRect(int i) => new Rect2(600.0f + i % 2 * 370.0f, 390.0f + i / 2 * 82.0f, 350.0f, 70.0f);

	LineEdit nameEdit;
	ColorPicker picker;
	Node2D canvas;

	string toast = "";
	int toastFrames;
	double sinceSave;
	int frames;

	DrawingPage Page => Doc.ActivePage;
	DrawingLayer Layer => Page.Active;
	PaintTool Tool => Tools[toolIndex];
	float BrushSize => Sizes[SizeSet][sizePick[SizeSet]];
	int SizeSet => Tool switch { PaintTool.Pencil => 1, PaintTool.Eraser => 3, _ => 0 };
	bool ToolHasSize => Tool is PaintTool.Marker or PaintTool.Pencil or PaintTool.Eraser;

	public override void _Ready()
	{
		Painter.Prewarm();
		Doc ??= Drawing.New();
		_ = Painter.Grain;
		if (FighterIntake.State == FighterIntake.Link.Unknown) FighterIntake.Check();

		// Every motion event, not one merged per frame, so a quick curve is a curve rather than a
		// handful of straight lines.
		Input.UseAccumulatedInput = false;

		canvas = new Node2D { ZIndex = -1 };
		AddChild(canvas);
		canvas.Draw += DrawCanvas;

		var layer = new CanvasLayer { Layer = 5 };
		AddChild(layer);
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(root);

		nameEdit = new LineEdit { Visible = false, Position = new Vector2(620.0f, 420.0f), Size = new Vector2(680.0f, 72.0f), MaxLength = 30 };
		nameEdit.Theme = FormTheme();
		nameEdit.TextSubmitted += _ => FinishRename();
		root.AddChild(nameEdit);

		picker = new ColorPicker
		{
			Visible = false, Position = new Vector2(610.0f, 190.0f), Scale = new Vector2(1.35f, 1.35f),
			EditAlpha = false, PresetsVisible = false, ColorModesVisible = false, SamplerVisible = false,
			PickerShape = ColorPicker.PickerShapeType.HsvWheel,
		};
		picker.ColorChanged += c => colour = new Color(c.R, c.G, c.B);
		root.AddChild(picker);

		// A pad plugged in has its cursor from the start, so a pad that chose to come here is the
		// pointer straight away (OnePointer.StartWith).
		if (Input.GetConnectedJoypads().Count > 0) AddPadCursor();
		pointers = new OnePointer(padCursors);
		cursorLayer = MenuCursor.MakeOverlay(this, c => pointers.Draw(c));
	}

	/// <summary>Text boxes in the menus' look: white, ink-edged, big enough to read.</summary>
	public static Theme FormTheme()
	{
		var theme = new Theme { DefaultFontSize = 32 };
		foreach (string type in new[] { "LineEdit", "TextEdit" })
		{
			var normal = new StyleBoxFlat
			{
				BgColor = Colors.White, BorderColor = MenuTheme.Ink,
				BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
				CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
				ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8,
			};
			var focus = (StyleBoxFlat)normal.Duplicate();
			focus.BorderColor = MenuTheme.AccentTwo;
			focus.DrawCenter = false;
			focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = focus.BorderWidthBottom = 5;
			theme.SetStylebox("normal", type, normal);
			theme.SetStylebox("focus", type, focus);
			theme.SetColor("font_color", type, MenuTheme.Text);
			theme.SetColor("caret_color", type, MenuTheme.Ink);
			theme.SetColor("font_placeholder_color", type, MenuTheme.Soft);
		}
		return theme;
	}

	public override void _ExitTree()
	{
		Input.UseAccumulatedInput = true;
		OnePointer.Leave();
	}

	public override void _Notification(int what)
	{
		// The window closing: keep the drawing.
		if (what == NotificationWMCloseRequest && Doc != null && Doc.Unsaved) Doc.Save();
	}

	// --- Frame --------------------------------------------------------------------------------

	public override void _Process(double delta)
	{
		frames++;
		sinceSave += delta;
		if (toastFrames > 0) toastFrames--;
		FighterIntake.CheckEvery(30.0f);

		// Saved a minute after a change - never in the middle of a stroke.
		if (Doc.Unsaved && drag == Drag.None && popup == Popup.None && sinceSave > 60.0)
		{
			Doc.Save();
			sinceSave = 0.0;
		}
		if (!Doc.Unsaved) sinceSave = 0.0;

		canvas.QueueRedraw();
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		// A pad's cursor can turn up when a pad does; the keyboard is for shortcuts here.
		if (padCursors.Count == 0 && frames % 30 == 0 && Input.GetConnectedJoypads().Count > 0) AddPadCursor();

		// Only one pointer at a time (OnePointer): a pad taking over ends whatever the mouse was
		// in the middle of, and the touch that hands over does nothing else.
		bool wasPad = OnePointer.UsingPad;
		Vector2 was = padCursor?.Position ?? Vector2.Zero;
		MenuCursor pad = pointers.Tick(GetViewportRect().Size, pointer);
		if (OnePointer.UsingPad && !wasPad)
		{
			if (drag != Drag.None) Release(pointer);
			padWasHeld = padCursor != null && padCursor.Nav.Held;
		}
		if (pad == null) return;

		bool held = pad.Nav.Held;
		if (pad.Nav.Confirm) Press(pad.Position, 1.0f);
		else if (held && pad.Position != was) MoveTo(pad.Position, 1.0f);
		if (!held && padWasHeld) Release(pad.Position);
		padWasHeld = held;
		if (pad.Cancel)
		{
			if (popup != Popup.None) ClosePopup();
			else Undo();
		}
	}

	void AddPadCursor() => padCursors.Add(new MenuCursor(ControllerAssignment.ForPlayers(1)[0], -1, MenuTheme.AccentTwo, View.GetCenter()));

	// --- Input ----------------------------------------------------------------------------------

	public override void _Input(InputEvent @event)
	{
		// Escape here, before anything else sees it: elsewhere in the game it quits.
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			if (popup != Popup.None) ClosePopup();
			else Back();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// The mouse taking back over from a pad: the pad's stroke ends where its cursor is.
		if (@event is InputEventMouse && OnePointer.UsingPad)
		{
			if (drag != Drag.None && padCursor != null) Release(padCursor.Position);
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

			case InputEventMouseButton button:
				pointer = button.Position;
				switch (button.ButtonIndex)
				{
					case MouseButton.Left:
						if (button.Pressed) Press(button.Position, lastPressure);
						else Release(button.Position);
						break;
					case MouseButton.Right:
						if (button.Pressed && popup == Popup.None && View.HasPoint(button.Position)) PickAt(ToCanvas(button.Position), false);
						break;
					case MouseButton.Middle:
						if (button.Pressed && View.HasPoint(button.Position))
						{
							drag = Drag.Pan;
							dragFrom = button.Position;
						}
						else if (!button.Pressed && drag == Drag.Pan)
						{
							drag = Drag.None;
						}
						break;
					case MouseButton.WheelUp:
					case MouseButton.WheelDown:
						if (!button.Pressed || popup != Popup.None) break;
						int step = button.ButtonIndex == MouseButton.WheelUp ? -1 : 1;
						if (new Rect2(1594.0f, 668.0f, 306.0f, LayerSlots * 48.0f).HasPoint(button.Position)) ScrollLayers(step);
						else if (View.HasPoint(button.Position)) ZoomAt(button.Position, step < 0 ? 1.15f : 1.0f / 1.15f);
						break;
				}
				break;

			case InputEventKey key:
				if (key.Keycode == Key.Space)
				{
					spaceHeld = key.Pressed;
					break;
				}
				if (!key.Pressed || key.Echo || popup != Popup.None) break;
				if (key.CtrlPressed || key.MetaPressed)
				{
					if (key.Keycode == Key.Z && key.ShiftPressed) Redo();
					else if (key.Keycode == Key.Z) Undo();
					else if (key.Keycode == Key.Y) Redo();
					else if (key.Keycode == Key.S) SaveNow();
					break;
				}
				switch (key.Keycode)
				{
					case Key.M: case Key.B: PickTool(0); break;
					case Key.P: PickTool(1); break;
					case Key.G: case Key.F: PickTool(2); break;
					case Key.E: PickTool(3); break;
					case Key.I: PickTool(4); break;
					case Key.V: PickTool(5); break;
					case Key.Bracketleft: StepSize(-1); break;
					case Key.Bracketright: StepSize(1); break;
				}
				break;
		}
	}

	void Press(Vector2 at, float pressure)
	{
		pointer = at;
		if (popup != Popup.None)
		{
			PressPopup(at);
			return;
		}
		if (PressUi(at)) return;
		if (!View.HasPoint(at)) return;

		if (painter.Painting) Commit(painter.End());
		Vector2 c = ToCanvas(at);
		if (spaceHeld)
		{
			drag = Drag.Pan;
			dragFrom = at;
			return;
		}

		PaintTool tool = Tool;
		// The pen turned round is its eraser.
		if (penInverted && tool is PaintTool.Marker or PaintTool.Pencil) tool = PaintTool.Eraser;
		switch (tool)
		{
			case PaintTool.Marker:
			case PaintTool.Pencil:
			case PaintTool.Eraser:
				if (!CanDrawHere()) return;
				float size = tool == PaintTool.Eraser ? Sizes[3][sizePick[3]] : BrushSize;
				painter.Begin(Page, Layer, tool, colour, size, c, pressure);
				drag = Drag.Paint;
				break;
			case PaintTool.Bucket:
				if (!CanDrawHere()) return;
				Commit(Painter.Fill(Page, Layer, new Vector2I(Mathf.FloorToInt(c.X), Mathf.FloorToInt(c.Y)), colour, !solidFill));
				break;
			case PaintTool.Picker:
				PickAt(c, true);
				break;
			case PaintTool.Move:
				if (!CanDrawHere()) return;
				drag = Drag.Move;
				dragFrom = at;
				moveOffset = Vector2.Zero;
				break;
		}
	}

	bool CanDrawHere()
	{
		if (Layer.Visible) return true;
		Toast("That layer is hidden. Show it to draw on it.");
		return false;
	}

	void MoveTo(Vector2 at, float pressure)
	{
		pointer = at;
		switch (drag)
		{
			case Drag.Paint:
				painter.To(ToCanvas(at), pressure);
				break;
			case Drag.Move:
				moveOffset = (at - dragFrom) / ViewScale;
				break;
			case Drag.Pan:
				pan += at - dragFrom;
				dragFrom = at;
				break;
		}
	}

	void Release(Vector2 at)
	{
		switch (drag)
		{
			case Drag.Paint:
				painter.To(ToCanvas(at), lastPressure);
				Commit(painter.End());
				break;
			case Drag.Move:
				Commit(Painter.Shift(Page, Layer, new Vector2I(Mathf.RoundToInt(moveOffset.X), Mathf.RoundToInt(moveOffset.Y))));
				moveOffset = Vector2.Zero;
				break;
		}
		drag = Drag.None;
	}

	void Commit(Edit edit)
	{
		if (edit == null) return;
		Doc.History.Push(edit);
		Doc.Unsaved = true;
	}

	void Undo()
	{
		if (drag != Drag.None) return;
		Edit edit = Doc.History.Undo();
		if (edit == null)
		{
			Toast("Nothing to undo.");
			return;
		}
		Doc.Unsaved = true;
		ShowPageOf(edit);
	}

	void Redo()
	{
		if (drag != Drag.None) return;
		Edit edit = Doc.History.Redo();
		if (edit == null) return;
		Doc.Unsaved = true;
		ShowPageOf(edit);
	}

	/// <summary>Undoing something on another page goes to that page, so the change is seen.</summary>
	void ShowPageOf(Edit edit)
	{
		int page = Doc.Pages.IndexOf(edit.Page);
		if (page >= 0) Doc.ActivePageIndex = page;
		KeepActiveLayerShown();
	}

	void PickAt(Vector2 c, bool fromTool)
	{
		Color? found = Painter.Pick(Page, new Vector2I(Mathf.FloorToInt(c.X), Mathf.FloorToInt(c.Y)));
		if (found is not Color picked) return;
		colour = picked;
		SfxPlayer.Ui("ui_move");
		if (fromTool) toolIndex = toolBeforePick;
	}

	void ZoomAt(Vector2 screen, float factor)
	{
		Vector2 under = ToCanvas(screen);
		zoom = Mathf.Clamp(zoom * factor, 0.5f, 6.0f);
		// Keep the point under the pointer where it is.
		pan = screen - under * ViewScale - View.GetCenter() + new Vector2(W, H) * ViewScale * 0.5f;
	}

	void PickTool(int index)
	{
		if (Tools[index] == PaintTool.Picker && Tool != PaintTool.Picker) toolBeforePick = toolIndex;
		toolIndex = index;
	}

	void StepSize(int by)
	{
		if (!ToolHasSize) return;
		sizePick[SizeSet] = Mathf.Clamp(sizePick[SizeSet] + by, 0, Sizes[SizeSet].Length - 1);
	}

	void Toast(string text)
	{
		toast = text;
		toastFrames = 150;
	}

	void SaveNow()
	{
		Doc.Save();
		sinceSave = 0.0;
		Toast("Saved!");
		SfxPlayer.Ui("ui_select");
	}

	void Back()
	{
		if (drag == Drag.Paint) Commit(painter.End());
		drag = Drag.None;
		Doc.Save();
		SfxPlayer.Ui("ui_back");
		GameRoot.Instance.GoDrawings();
	}

	// --- The buttons -------------------------------------------------------------------------------

	bool PressUi(Vector2 at)
	{
		if (BackBox.HasPoint(at))
		{
			Back();
			return true;
		}
		if (SaveBox.HasPoint(at))
		{
			SaveNow();
			return true;
		}
		if (SendBox.HasPoint(at))
		{
			if (drag == Drag.Paint) Commit(painter.End());
			Doc.Save();
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.GoSendFighter(Doc);
			return true;
		}

		for (int i = 0; i < Doc.Pages.Count; i++)
		{
			if (i > 0 && i == Doc.ActivePageIndex && TabCloseRect(i).HasPoint(at))
			{
				OpenPopup(Popup.DeletePage);
				return true;
			}
			if (!TabRect(i).HasPoint(at)) continue;
			if (i == Doc.ActivePageIndex)
			{
				renamingPage = true;
				OpenPopup(Popup.Rename);
			}
			else
			{
				Doc.ActivePageIndex = i;
				SfxPlayer.Ui("ui_move");
			}
			return true;
		}
		if (Doc.Pages.Count < Drawing.MaxPages && TabRect(Doc.Pages.Count).HasPoint(at))
		{
			OpenPopup(Popup.AddPage);
			return true;
		}

		for (int i = 0; i < Tools.Length; i++)
		{
			if (!ToolRect(i).HasPoint(at)) continue;
			PickTool(i);
			SfxPlayer.Ui("ui_move");
			return true;
		}
		if (UndoBox.HasPoint(at)) { Undo(); return true; }
		if (RedoBox.HasPoint(at)) { Redo(); return true; }
		if (ZoomOutBox.HasPoint(at)) { ZoomAt(View.GetCenter(), 1.0f / 1.25f); return true; }
		if (ZoomInBox.HasPoint(at)) { ZoomAt(View.GetCenter(), 1.25f); return true; }
		if (FitBox.HasPoint(at))
		{
			zoom = 1.0f;
			pan = Vector2.Zero;
			return true;
		}

		if (MixBox.HasPoint(at) || ColourBox.HasPoint(at))
		{
			OpenPopup(Popup.Mix);
			return true;
		}
		for (int i = 0; i < Palette.Length; i++)
		{
			if (!SwatchRect(i).HasPoint(at)) continue;
			colour = Palette[i];
			if (Tool is PaintTool.Eraser or PaintTool.Picker or PaintTool.Move) toolIndex = 0;
			SfxPlayer.Ui("ui_move");
			return true;
		}
		for (int i = 0; i < 6; i++)
		{
			if (!SizeRect(i).HasPoint(at) || !ToolHasSize) continue;
			sizePick[SizeSet] = i;
			return true;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!FillRect(i).HasPoint(at)) continue;
			solidFill = i == 1;
			toolIndex = 2;
			return true;
		}

		if (AddLayerBox.HasPoint(at))
		{
			if (Page.Layers.Count >= Drawing.MaxLayers) Toast($"A page can have {Drawing.MaxLayers} layers at most.");
			else if (Page.Kind == PageKind.Fighter || Page.Kind == PageKind.Stage) OpenPopup(Popup.AddPart);
			else AddLayer($"Layer {Page.Layers.Count + 1}");
			return true;
		}
		for (int slot = 0; slot < LayerSlots; slot++)
		{
			int index = LayerAtSlot(slot);
			if (index < 0) break;
			if (EyeRect(slot).HasPoint(at))
			{
				Page.Layers[index].Visible = !Page.Layers[index].Visible;
				Doc.Unsaved = true;
				return true;
			}
			if (!LayerRow(slot).HasPoint(at)) continue;
			Page.ActiveLayer = index;
			return true;
		}
		for (int k = 0; k < 5; k++)
		{
			if (!LayerButton(k).HasPoint(at)) continue;
			switch (k)
			{
				case 0: MoveLayer(1); break;
				case 1: MoveLayer(-1); break;
				case 2: MergeDown(); break;
				case 3: renamingPage = false; OpenPopup(Popup.Rename); break;
				case 4: DeleteLayer(); break;
			}
			return true;
		}

		// Anywhere else on the panels: nothing, but the canvas must not get it either.
		return !View.HasPoint(at);
	}

	// --- Layers -----------------------------------------------------------------------------------

	int layerScroll;

	/// <summary>The layer shown in a row of the list - the top layer first - or -1.</summary>
	int LayerAtSlot(int slot)
	{
		int index = Page.Layers.Count - 1 - (slot + layerScroll);
		return index >= 0 && index < Page.Layers.Count ? index : -1;
	}

	void ScrollLayers(int by) => layerScroll = Mathf.Clamp(layerScroll + by, 0, Mathf.Max(0, Page.Layers.Count - LayerSlots));

	void KeepActiveLayerShown()
	{
		int slot = Page.Layers.Count - 1 - Page.ActiveLayer;
		if (slot < layerScroll) layerScroll = slot;
		if (slot >= layerScroll + LayerSlots) layerScroll = slot - LayerSlots + 1;
		ScrollLayers(0);
	}

	void AddLayer(string name)
	{
		DrawingLayer layer = Doc.NewLayer(name);
		int index = Page.ActiveLayer + 1;
		Page.Layers.Insert(index, layer);
		Page.ActiveLayer = index;
		Doc.History.Push(new LayerEdit(Page, layer, index, true));
		Doc.Unsaved = true;
		KeepActiveLayerShown();
		SfxPlayer.Ui("ui_select");
	}

	void DeleteLayer()
	{
		if (Page.Layers.Count == 1)
		{
			Toast("A page needs one layer. Use the eraser to clear it.");
			return;
		}
		int index = Page.ActiveLayer;
		DrawingLayer layer = Page.Layers[index];
		Page.Layers.RemoveAt(index);
		Page.ActiveLayer = Mathf.Clamp(index - 1, 0, Page.Layers.Count - 1);
		Doc.History.Push(new LayerEdit(Page, layer, index, false));
		Doc.Unsaved = true;
		KeepActiveLayerShown();
		Toast($"{layer.Name} thrown away. Undo brings it back.");
		SfxPlayer.Ui("ui_back");
	}

	void MoveLayer(int by)
	{
		int from = Page.ActiveLayer, to = from + by;
		if (to < 0 || to >= Page.Layers.Count) return;
		var edit = new LayerOrderEdit(Page, from, to);
		edit.Redo();
		Doc.History.Push(edit);
		Doc.Unsaved = true;
		KeepActiveLayerShown();
	}

	void MergeDown()
	{
		int index = Page.ActiveLayer;
		if (index == 0)
		{
			Toast("There's no layer under this one to merge it into.");
			return;
		}
		DrawingLayer upper = Page.Layers[index], lower = Page.Layers[index - 1];
		if (!upper.Visible || !lower.Visible)
		{
			Toast("Show both layers to merge them.");
			return;
		}
		var all = new Rect2I(0, 0, W, H);
		byte[] before = (byte[])lower.Pixels.Clone();
		Drawing.Over(lower.Pixels, upper.Pixels);
		lower.TouchAll();
		var pixels = new PixelEdit(Page, lower, all, before, (byte[])lower.Pixels.Clone());
		Page.Layers.RemoveAt(index);
		Page.ActiveLayer = index - 1;
		Doc.History.Push(new MergeEdit(Page, new LayerEdit(Page, upper, index, false), pixels));
		Doc.Unsaved = true;
		KeepActiveLayerShown();
		Toast($"{upper.Name} merged into {lower.Name}.");
	}

	// --- Popups --------------------------------------------------------------------------------------

	void OpenPopup(Popup which)
	{
		if (drag == Drag.Paint) Commit(painter.End());
		drag = Drag.None;
		popup = which;
		SfxPlayer.Ui("ui_select");
		if (which == Popup.Rename)
		{
			nameEdit.Text = renamingPage ? Page.Name : Layer.Name;
			nameEdit.Visible = true;
			nameEdit.GrabFocus();
			nameEdit.SelectAll();
		}
		if (which == Popup.Mix)
		{
			picker.Color = colour;
			picker.Visible = true;
		}
	}

	void ClosePopup()
	{
		popup = Popup.None;
		nameEdit.ReleaseFocus();
		nameEdit.Visible = false;
		picker.Visible = false;
		SfxPlayer.Ui("ui_back");
	}

	void FinishRename()
	{
		string name = nameEdit.Text.Trim();
		if (name != "")
		{
			if (renamingPage) Page.Name = name;
			else Layer.Name = name;
			Doc.Unsaved = true;
		}
		ClosePopup();
	}

	void PressPopup(Vector2 at)
	{
		switch (popup)
		{
			case Popup.AddPage:
				for (int i = 0; i < 3; i++)
				{
					if (!KindRect(i).HasPoint(at)) continue;
					PageKind kind = AddableKinds[i];
					string name = kind switch
					{
						PageKind.Shot => "Shot", PageKind.Held => "Held thing",
						PageKind.Hazard => "Hazard", PageKind.Mover => "Moving part", _ => "Extra",
					};
					int same = 0;
					foreach (DrawingPage p in Doc.Pages) if (p.Kind == kind) same++;
					if (same > 0) name += $" {same + 1}";
					Doc.AddPage(kind, name);
					Doc.ActivePageIndex = Doc.Pages.Count - 1;
					Doc.Unsaved = true;
					layerScroll = 0;
					ClosePopup();
					return;
				}
				if (CancelBox.HasPoint(at) || !PopupBox.HasPoint(at)) ClosePopup();
				break;

			case Popup.AddPart:
				string[] names = LayerNames;
				for (int i = 0; i < names.Length; i++)
				{
					if (!PartRect(i).HasPoint(at)) continue;
					popup = Popup.None;
					AddLayer(i == names.Length - 1 ? $"Layer {Page.Layers.Count + 1}" : names[i]);
					return;
				}
				if (CancelBox.HasPoint(at) || !PopupBox.HasPoint(at)) ClosePopup();
				break;

			case Popup.Rename:
				if (OkBox.HasPoint(at)) FinishRename();
				else if (RenameCancelBox.HasPoint(at) || !PopupBox.HasPoint(at)) ClosePopup();
				break;

			case Popup.DeletePage:
				if (YesBox.HasPoint(at))
				{
					DrawingPage page = Page;
					Doc.History.Forget(page);
					Doc.Pages.Remove(page);
					Doc.ActivePageIndex = Mathf.Clamp(Doc.ActivePageIndex - 1, 0, Doc.Pages.Count - 1);
					Doc.Unsaved = true;
					ClosePopup();
				}
				else if (NoBox.HasPoint(at) || !PopupBox.HasPoint(at))
				{
					ClosePopup();
				}
				break;

			case Popup.Mix:
				if (DoneBox.HasPoint(at) || !MixPopupBox.HasPoint(at)) ClosePopup();
				break;
		}
	}

	// --- Drawing the page ---------------------------------------------------------------------------

	public static readonly Color Desk = new Color(0.84f, 0.86f, 0.89f);
	public static readonly Color HintInk = new Color(0.55f, 0.66f, 0.80f);

	void DrawCanvas()
	{
		Vector2 size = GetViewportRect().Size;
		canvas.DrawRect(new Rect2(Vector2.Zero, size), Desk);

		float s = ViewScale;
		Vector2 o = Origin;
		canvas.DrawRect(new Rect2(o + new Vector2(10.0f, 12.0f), new Vector2(W, H) * s), new Color(0.0f, 0.0f, 0.0f, 0.10f));
		canvas.DrawSetTransform(o, 0.0f, new Vector2(s, s));

		DrawLinedPage(canvas);
		DrawPageHint();

		foreach (DrawingLayer layer in Page.Layers)
		{
			if (!layer.Visible) continue;
			Vector2 offset = drag == Drag.Move && layer == Layer ? moveOffset : Vector2.Zero;
			layer.DrawOn(canvas, offset, Colors.White);
		}
		canvas.DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>
	/// Lined paper, in page pixels. It is only the page: none of it is ever sent with the drawing.
	/// The doodle screen's page is the same paper.
	/// </summary>
	public static void DrawLinedPage(CanvasItem canvas)
	{
		canvas.DrawRect(new Rect2(0.0f, 0.0f, W, H), MenuTheme.Paper);
		for (float y = 120.0f; y < H; y += 56.0f)
		{
			CrayonBrush.InkLine(canvas, new Vector2(0.0f, y), new Vector2(W, y), MenuTheme.Rule, 2.5f, Mathf.RoundToInt(y), 1.4f);
		}
		CrayonBrush.InkLine(canvas, new Vector2(120.0f, 0.0f), new Vector2(120.0f, H), MenuTheme.Margin, 3.5f, 991, 2.0f);
	}

	/// <summary>A reminder pencilled on the page, the same advice the drawing guide gives.</summary>
	void DrawPageHint()
	{
		string hint = Page.Kind switch
		{
			PageKind.Fighter => "Three-quarter view, facing right",
			PageKind.Shot => "Draw it flying to the right",
			PageKind.Held => "Draw it standing up, handle at the bottom",
			PageKind.Stage => "Draw it wide, like the guide",
			PageKind.Hazard => "Draw the thing that hurts",
			PageKind.Mover => "Draw the part that moves, on its own",
			_ => "",
		};
		if (Page.Kind == PageKind.Stage) DrawStageGuide();
		if (hint == "") return;
		Font font = ThemeDB.FallbackFont;
		var at = new Vector2(150.0f, 84.0f);
		canvas.DrawString(font, at, hint, HorizontalAlignment.Left, -1, 34, HintInk);
		if (Page.Kind != PageKind.Fighter && Page.Kind != PageKind.Shot) return;
		float end = at.X + font.GetStringSize(hint, HorizontalAlignment.Left, -1, 34).X + 20.0f;
		var tip = new Vector2(end + 70.0f, at.Y - 11.0f);
		CrayonBrush.InkLine(canvas, new Vector2(end, tip.Y), tip, HintInk, 4.0f, 7, 1.0f);
		CrayonBrush.InkLine(canvas, tip, tip + new Vector2(-16.0f, -12.0f), HintInk, 4.0f, 8, 0.5f);
		CrayonBrush.InkLine(canvas, tip, tip + new Vector2(-16.0f, 12.0f), HintInk, 4.0f, 9, 0.5f);
		if (Page.Kind != PageKind.Fighter) return;
		DrawThreeQuarterHead(tip + new Vector2(60.0f, 0.0f), 34.0f);
		// The guide's rule 3. Crossed limbs are one stroke where they cross, and have to be taken
		// apart before the game can move them (Triguy's legs; Eric, 2026-10-23).
		canvas.DrawString(font, at + new Vector2(0.0f, 46.0f), "Arms and legs spread out - not crossing or touching",
			HorizontalAlignment.Left, -1, 30, HintInk);
	}

	/// <summary>
	/// The guide's three-quarter head, small after the arrow: both eyes, crowded toward the side it
	/// faces, the far one smaller, and the nose breaking the outline. "Three-quarter" is a word a kid
	/// may not know; the picture is what says it. Fighters are drawn this way from Eric's call,
	/// 2026-10-21 - see `.ai/art-pipeline.md`.
	/// </summary>
	void DrawThreeQuarterHead(Vector2 centre, float radius)
	{
		const int Sides = 20;
		for (int i = 0; i < Sides; i++)
		{
			Vector2 from = centre + Vector2.FromAngle(Mathf.Tau * i / Sides) * radius;
			Vector2 to = centre + Vector2.FromAngle(Mathf.Tau * (i + 1) / Sides) * radius;
			CrayonBrush.InkLine(canvas, from, to, HintInk, 4.5f, 31 + i, 0.4f);
		}
		canvas.DrawCircle(centre + new Vector2(radius * 0.18f, -radius * 0.22f), radius * 0.17f, HintInk);
		canvas.DrawCircle(centre + new Vector2(radius * 0.66f, -radius * 0.24f), radius * 0.13f, HintInk);
		canvas.DrawColoredPolygon(new[]
		{
			centre + new Vector2(radius * 0.84f, -radius * 0.1f),
			centre + new Vector2(radius * 1.5f, radius * 0.2f),
			centre + new Vector2(radius * 0.9f, radius * 0.32f),
		}, HintInk);
		Vector2 smile = centre + new Vector2(radius * 0.38f, radius * 0.56f);
		CrayonBrush.InkLine(canvas, centre + new Vector2(radius * 0.06f, radius * 0.4f), smile, HintInk, 4.0f, 52, 0.3f);
		CrayonBrush.InkLine(canvas, smile, centre + new Vector2(radius * 0.66f, radius * 0.42f), HintInk, 4.0f, 53, 0.3f);
	}

	/// <summary>
	/// The stage guide, faint on the page and never part of the drawing - the same picture as the
	/// drawing guide's "draw a stage" (ecec.dev/draw/guide): dashed edges where fighters get knocked
	/// out, a wide ground, and dashed platforms you can jump through.
	/// </summary>
	void DrawStageGuide()
	{
		Font font = ThemeDB.FallbackFont;
		var knock = new Color(0.86f, 0.36f, 0.34f, 0.55f);
		CrayonBrush.InkLine(canvas, new Vector2(140.0f, 120.0f), new Vector2(140.0f, 1020.0f), knock, 4.0f, 21, 1.0f, dashed: true);
		CrayonBrush.InkLine(canvas, new Vector2(1260.0f, 120.0f), new Vector2(1260.0f, 1020.0f), knock, 4.0f, 22, 1.0f, dashed: true);
		CrayonBrush.InkLine(canvas, new Vector2(140.0f, 130.0f), new Vector2(1260.0f, 130.0f), knock, 4.0f, 23, 1.0f, dashed: true);
		canvas.DrawString(font, new Vector2(140.0f, 176.0f), "knocked past here = you're out", HorizontalAlignment.Center, 1120.0f, 30, knock);

		var ground = new Rect2(330.0f, 790.0f, 740.0f, 60.0f);
		canvas.DrawRect(ground, new Color(HintInk, 0.12f));
		CrayonBrush.InkRect(canvas, ground, HintInk, 4.0f, 24, 1.2f);
		canvas.DrawString(font, new Vector2(ground.Position.X, ground.End.Y + 46.0f), "the ground", HorizontalAlignment.Center, ground.Size.X, 30, HintInk);

		foreach (Rect2 platform in new[] { new Rect2(390.0f, 610.0f, 270.0f, 32.0f), new Rect2(740.0f, 610.0f, 270.0f, 32.0f), new Rect2(585.0f, 450.0f, 230.0f, 32.0f) })
		{
			CrayonBrush.InkRect(canvas, platform, HintInk, 3.0f, 25 + (int)platform.Position.X, 1.0f, dashed: true);
		}
		canvas.DrawString(font, new Vector2(150.0f, 560.0f), "things to", HorizontalAlignment.Left, -1, 28, HintInk);
		canvas.DrawString(font, new Vector2(150.0f, 594.0f), "jump through", HorizontalAlignment.Left, -1, 28, HintInk);
	}

	// --- Drawing the panels ---------------------------------------------------------------------------

	bool Hot(Rect2 rect) => popup == Popup.None && rect.HasPoint(pointers.At(pointer));
	bool PopupHot(Rect2 rect) => rect.HasPoint(pointers.At(pointer));

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		Font font = ThemeDB.FallbackFont;

		// The panels round the page, opaque, so a zoomed-in page goes under them.
		DrawRect(new Rect2(0.0f, 0.0f, size.X, View.Position.Y), Desk);
		DrawRect(new Rect2(0.0f, 0.0f, View.Position.X, size.Y), Desk);
		DrawRect(new Rect2(View.End.X, 0.0f, size.X - View.End.X, size.Y), Desk);
		DrawRect(new Rect2(0.0f, View.End.Y, size.X, size.Y - View.End.Y), Desk);

		DrawBrushOutline();
		DrawTopBar(font);
		DrawTools(font);
		DrawColours(font);
		DrawLayers(font);

		if (toastFrames > 0)
		{
			float alpha = Mathf.Min(1.0f, toastFrames / 20.0f);
			float width = font.GetStringSize(toast, HorizontalAlignment.Left, -1, 30).X + 60.0f;
			var box = new Rect2(View.GetCenter().X - width * 0.5f, View.End.Y - 86.0f, width, 62.0f);
			DrawRect(box, new Color(1.0f, 0.98f, 0.86f, 0.96f * alpha));
			CrayonBrush.InkRect(this, box, new Color(MenuTheme.Accent, alpha), 4.0f, 77, 1.6f);
			MenuTheme.DrawCentredText(this, box, toast, 30, new Color(MenuTheme.Text, alpha));
		}

		if (popup != Popup.None) DrawPopup(font, size);
	}

	/// <summary>A ring the size of the brush, so it is clear how big a line it will make.</summary>
	void DrawBrushOutline()
	{
		if (popup != Popup.None || !ToolHasSize) return;
		Vector2 at = pointers.At(pointer);
		if (!View.HasPoint(at)) return;
		float radius = BrushSize * 0.5f * ViewScale;
		if (radius < 3.0f) return;
		DrawArc(at, radius, 0.0f, Mathf.Tau, 48, new Color(1.0f, 1.0f, 1.0f, 0.8f), 3.0f, true);
		DrawArc(at, radius, 0.0f, Mathf.Tau, 48, new Color(MenuTheme.Ink, 0.8f), 1.5f, true);
	}

	void DrawTopBar(Font font)
	{
		MenuTheme.DrawButton(this, BackBox, "Back", 701, Hot(BackBox), fontSize: 30);

		for (int i = 0; i < Doc.Pages.Count; i++)
		{
			Rect2 tab = TabRect(i);
			bool active = i == Doc.ActivePageIndex;
			DrawRect(tab, active ? MenuTheme.Paper : new Color(0.93f, 0.94f, 0.95f));
			CrayonBrush.InkRect(this, tab, active ? MenuTheme.Ink : MenuTheme.Rule, active ? 4.0f : 3.0f, 710 + i, 1.8f);
			if (Hot(tab) && !active) CrayonBrush.InkRect(this, tab.Grow(4.0f), MenuTheme.Accent, 4.0f, 720 + i, 2.0f);
			float room = active && i > 0 ? tab.Size.X - 44.0f : tab.Size.X - 16.0f;
			DrawString(font, new Vector2(tab.Position.X + 10.0f, tab.Position.Y + 42.0f), Doc.Pages[i].Name,
				HorizontalAlignment.Left, room, 26, active ? MenuTheme.Text : MenuTheme.Soft);
			if (active && i > 0)
			{
				Rect2 close = TabCloseRect(i);
				Color x = Hot(close) ? new Color(0.86f, 0.30f, 0.30f) : MenuTheme.Soft;
				DrawLine(close.Position + new Vector2(8.0f, 8.0f), close.End - new Vector2(8.0f, 8.0f), x, 4.0f, true);
				DrawLine(new Vector2(close.End.X - 8.0f, close.Position.Y + 8.0f), new Vector2(close.Position.X + 8.0f, close.End.Y - 8.0f), x, 4.0f, true);
			}
		}
		if (Doc.Pages.Count < Drawing.MaxPages)
		{
			Rect2 add = TabRect(Doc.Pages.Count);
			MenuTheme.DrawButton(this, add, "+ Page", 730, Hot(add), fontSize: 26);
		}

		Color light = FighterIntake.Describe().light;
		string state = FighterIntake.State switch
		{
			FighterIntake.Link.Online => "connected",
			FighterIntake.Link.Offline => "no internet",
			_ => "checking...",
		};
		DrawCircle(new Vector2(1222.0f, 54.0f), 11.0f, light);
		DrawString(font, new Vector2(1242.0f, 46.0f), FighterIntake.Where, HorizontalAlignment.Left, 200.0f, 22, MenuTheme.Text);
		DrawString(font, new Vector2(1242.0f, 74.0f), state, HorizontalAlignment.Left, 200.0f, 22, MenuTheme.Soft);

		MenuTheme.DrawButton(this, SaveBox, Doc.Unsaved ? "Save" : "Saved", 740, Hot(SaveBox), fontSize: 30);
		MenuTheme.DrawButton(this, SendBox, "Send it in!", 741, Hot(SendBox), true, fontSize: 34);
	}

	void DrawTools(Font font)
	{
		for (int i = 0; i < Tools.Length; i++)
		{
			Rect2 box = ToolRect(i);
			bool chosen = i == toolIndex;
			MenuTheme.DrawButton(this, box, "", 750 + i, Hot(box), chosen);
			DrawToolIcon(this, Tools[i], new Rect2(box.Position + new Vector2(6.0f, 6.0f), new Vector2(64.0f, 66.0f)), colour);
			DrawString(font, new Vector2(box.Position.X + 74.0f, box.GetCenter().Y + 9.0f), ToolNames[i],
				HorizontalAlignment.Left, box.Size.X - 78.0f, 24, MenuTheme.Text);
		}

		MenuTheme.DrawButton(this, UndoBox, "Undo", 760, Hot(UndoBox), enabled: Doc.History.CanUndo, fontSize: 22);
		MenuTheme.DrawButton(this, RedoBox, "Redo", 761, Hot(RedoBox), enabled: Doc.History.CanRedo, fontSize: 22);
		MenuTheme.DrawButton(this, ZoomOutBox, "-", 762, Hot(ZoomOutBox), fontSize: 34);
		MenuTheme.DrawButton(this, FitBox, "Fit", 763, Hot(FitBox), fontSize: 20);
		MenuTheme.DrawButton(this, ZoomInBox, "+", 764, Hot(ZoomInBox), fontSize: 34);

		DrawMultilineString(font, new Vector2(14.0f, 810.0f), ToolHints[toolIndex], HorizontalAlignment.Left, 160.0f, 22, 8, MenuTheme.Soft);
	}

	/// <summary>A tool's picture on its button: the item it is, or a dropper, or arrows. Shared with the doodle screen.</summary>
	public static void DrawToolIcon(CanvasItem canvas, PaintTool tool, Rect2 box, Color colour)
	{
		if (ToolArt[System.Array.IndexOf(Tools, tool)] is string id && ItemCatalog.Art(id) is ItemArt art)
		{
			float s = Mathf.Min(box.Size.X / art.Size.X, box.Size.Y / art.Size.Y) * 0.95f;
			float angle = id is "marker" or "pencil" ? 0.6f : 0.0f;
			if (angle != 0.0f) s = box.Size.Y * 1.05f / art.Size.Y;
			canvas.DrawSetTransformMatrix(new Transform2D(angle, new Vector2(s, s), 0.0f, box.GetCenter()));
			canvas.DrawTexture(art.Texture, -art.Size * 0.5f);
			canvas.DrawSetTransformMatrix(Transform2D.Identity);
			return;
		}
		Vector2 c = box.GetCenter();
		if (tool == PaintTool.Picker)
		{
			// A dropper: a bulb, a glass tube, a drop of the colour at its tip.
			var tip = c + new Vector2(-18.0f, 20.0f);
			canvas.DrawLine(tip, c + new Vector2(10.0f, -8.0f), MenuTheme.Ink, 9.0f, true);
			canvas.DrawLine(tip, c + new Vector2(10.0f, -8.0f), Colors.White, 5.0f, true);
			canvas.DrawCircle(c + new Vector2(16.0f, -14.0f), 10.0f, new Color(0.86f, 0.36f, 0.36f));
			canvas.DrawCircle(tip + new Vector2(-4.0f, 8.0f), 6.0f, colour);
			return;
		}
		// Move: four arrows out from the middle.
		for (int k = 0; k < 4; k++)
		{
			Vector2 way = Vector2.Right.Rotated(k * Mathf.Pi * 0.5f);
			Vector2 end = c + way * 24.0f;
			canvas.DrawLine(c, end, MenuTheme.Ink, 4.0f, true);
			canvas.DrawLine(end, end - way.Rotated(0.6f) * 10.0f, MenuTheme.Ink, 4.0f, true);
			canvas.DrawLine(end, end - way.Rotated(-0.6f) * 10.0f, MenuTheme.Ink, 4.0f, true);
		}
	}

	void DrawColours(Font font)
	{
		DrawRect(ColourBox, colour);
		CrayonBrush.InkRect(this, ColourBox, MenuTheme.Ink, 4.0f, 770, 1.6f);
		MenuTheme.DrawButton(this, MixBox, "Mix a colour", 771, Hot(MixBox), fontSize: 26);

		for (int i = 0; i < Palette.Length; i++)
		{
			Rect2 swatch = SwatchRect(i);
			DrawRect(swatch, Palette[i]);
			bool chosen = Palette[i].IsEqualApprox(colour);
			DrawRect(swatch, chosen ? MenuTheme.Ink : MenuTheme.Rule, false, chosen ? 4.0f : 2.0f);
			if (Hot(swatch)) DrawRect(swatch.Grow(3.0f), MenuTheme.Accent, false, 3.0f);
		}

		DrawString(font, new Vector2(1594.0f, 500.0f), ToolHasSize ? "Size" : "Size (not for this tool)",
			HorizontalAlignment.Left, -1, 22, MenuTheme.Soft);
		for (int i = 0; i < 6; i++)
		{
			Rect2 box = SizeRect(i);
			bool chosen = ToolHasSize && sizePick[SizeSet] == i;
			MenuTheme.DrawButton(this, box, "", 780 + i, Hot(box) && ToolHasSize, chosen, ToolHasSize);
			float dot = 3.0f + i * 3.2f;
			DrawCircle(box.GetCenter(), dot, ToolHasSize ? (Tool == PaintTool.Eraser ? MenuTheme.Soft : colour) : MenuTheme.Rule);
		}

		MenuTheme.DrawButton(this, FillRect(0), "Pencil fill", 790, Hot(FillRect(0)), !solidFill, fontSize: 22);
		MenuTheme.DrawButton(this, FillRect(1), "Solid fill", 791, Hot(FillRect(1)), solidFill, fontSize: 22);
	}

	void DrawLayers(Font font)
	{
		DrawString(font, new Vector2(1594.0f, 650.0f), "Layers", HorizontalAlignment.Left, -1, 32, MenuTheme.Text);
		MenuTheme.DrawButton(this, AddLayerBox, "+ Layer", 800, Hot(AddLayerBox), enabled: Page.Layers.Count < Drawing.MaxLayers, fontSize: 24);

		for (int slot = 0; slot < LayerSlots; slot++)
		{
			int index = LayerAtSlot(slot);
			if (index < 0) break;
			DrawingLayer layer = Page.Layers[index];
			Rect2 row = LayerRow(slot);
			bool active = index == Page.ActiveLayer;
			DrawRect(row, active ? MenuTheme.AccentTwo.Lightened(0.6f) : new Color(1.0f, 1.0f, 1.0f, 0.85f));
			DrawRect(row, active ? MenuTheme.AccentTwo : MenuTheme.Rule, false, active ? 3.0f : 1.5f);
			if (Hot(row) && !active) DrawRect(row.Grow(2.0f), MenuTheme.Accent, false, 2.5f);

			// The eye: open while the layer shows.
			Rect2 eye = EyeRect(slot);
			Vector2 ec = eye.GetCenter();
			Color eyeInk = layer.Visible ? MenuTheme.Ink : MenuTheme.Rule;
			DrawArc(ec + new Vector2(0.0f, 8.0f), 16.0f, -Mathf.Pi * 0.82f, -Mathf.Pi * 0.18f, 12, eyeInk, 3.0f, true);
			DrawArc(ec + new Vector2(0.0f, -8.0f), 16.0f, Mathf.Pi * 0.18f, Mathf.Pi * 0.82f, 12, eyeInk, 3.0f, true);
			if (layer.Visible) DrawCircle(ec, 5.5f, eyeInk);
			else DrawLine(ec + new Vector2(-12.0f, 10.0f), ec + new Vector2(12.0f, -10.0f), eyeInk, 3.0f, true);

			// A tiny copy of the layer.
			var thumb = new Rect2(row.Position + new Vector2(44.0f, 4.0f), new Vector2(48.0f, 36.0f));
			DrawRect(thumb, MenuTheme.Paper);
			float s = thumb.Size.X / W;
			DrawSetTransform(thumb.Position, 0.0f, new Vector2(s, s));
			layer.DrawOn(this, Vector2.Zero, Colors.White);
			DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
			DrawRect(thumb, MenuTheme.Rule, false, 1.0f);

			DrawString(font, new Vector2(row.Position.X + 100.0f, row.Position.Y + 30.0f), layer.Name,
				HorizontalAlignment.Left, row.Size.X - 106.0f, 24, layer.Visible ? MenuTheme.Text : MenuTheme.Soft);
		}
		if (Page.Layers.Count > LayerSlots)
		{
			DrawString(font, new Vector2(1594.0f, 990.0f), "Scroll for more layers", HorizontalAlignment.Left, -1, 20, MenuTheme.Soft);
		}

		string[] labels = { "Up", "Down", "Merge", "Name", "Bin" };
		for (int k = 0; k < 5; k++)
		{
			bool enabled = k switch
			{
				0 => Page.ActiveLayer < Page.Layers.Count - 1,
				1 => Page.ActiveLayer > 0,
				2 => Page.ActiveLayer > 0,
				4 => Page.Layers.Count > 1,
				_ => true,
			};
			MenuTheme.DrawButton(this, LayerButton(k), labels[k], 810 + k, Hot(LayerButton(k)), enabled: enabled, fontSize: 18);
		}
	}

	void DrawPopup(Font font, Vector2 size)
	{
		DrawRect(new Rect2(Vector2.Zero, size), new Color(0.9f, 0.92f, 0.95f, 0.7f));
		Rect2 box = popup == Popup.Mix ? MixPopupBox : PopupBox;
		DrawRect(new Rect2(box.Position + new Vector2(8.0f, 10.0f), box.Size), new Color(0.0f, 0.0f, 0.0f, 0.08f));
		DrawRect(box, MenuTheme.Paper);
		CrayonBrush.InkRect(this, box, MenuTheme.Ink, 5.0f, 820, 2.4f);
		var heading = new Rect2(box.Position + new Vector2(0.0f, 30.0f), new Vector2(box.Size.X, 60.0f));

		switch (popup)
		{
			case Popup.AddPage:
				MenuTheme.DrawCentredText(this, heading, "What goes on this page?", 42, MenuTheme.Text);
				string[] kinds = Doc.IsStage
					? new[] { "A hazard - something that hurts", "A part that moves", "Something else" }
					: new[] { "Something they shoot or throw", "Something they hold", "Something else" };
				for (int i = 0; i < 3; i++) MenuTheme.DrawButton(this, KindRect(i), kinds[i], 830 + i, PopupHot(KindRect(i)), fontSize: 32);
				MenuTheme.DrawButton(this, CancelBox, "Cancel", 834, PopupHot(CancelBox), fontSize: 30);
				break;

			case Popup.AddPart:
				MenuTheme.DrawCentredText(this, heading, "What goes on this layer?", 42, MenuTheme.Text);
				MenuTheme.DrawCentredText(this, new Rect2(box.Position + new Vector2(0.0f, 84.0f), new Vector2(box.Size.X, 40.0f)),
					Page.Kind == PageKind.Stage ? "One part of the stage per layer makes the best stage." : "One body part per layer makes the best fighter.", 26, MenuTheme.Soft);
				for (int i = 0; i < LayerNames.Length; i++) MenuTheme.DrawButton(this, PartRect(i), LayerNames[i], 840 + i, PopupHot(PartRect(i)), fontSize: 30);
				MenuTheme.DrawButton(this, CancelBox, "Cancel", 849, PopupHot(CancelBox), fontSize: 30);
				break;

			case Popup.Rename:
				MenuTheme.DrawCentredText(this, heading, renamingPage ? "Name this page" : "Name this layer", 42, MenuTheme.Text);
				MenuTheme.DrawButton(this, OkBox, "OK", 850, PopupHot(OkBox), true, fontSize: 32);
				MenuTheme.DrawButton(this, RenameCancelBox, "Cancel", 851, PopupHot(RenameCancelBox), fontSize: 30);
				break;

			case Popup.DeletePage:
				MenuTheme.DrawCentredText(this, heading, $"Throw away the {Page.Name} page?", 42, MenuTheme.Text);
				MenuTheme.DrawCentredText(this, new Rect2(box.Position + new Vector2(0.0f, 120.0f), new Vector2(box.Size.X, 50.0f)),
					"Everything drawn on it goes too. It can't be undone.", 28, MenuTheme.Soft);
				MenuTheme.DrawButton(this, YesBox, "Throw it away", 852, PopupHot(YesBox), fontSize: 32);
				MenuTheme.DrawButton(this, NoBox, "Keep it", 853, PopupHot(NoBox), fontSize: 32);
				break;

			case Popup.Mix:
				var swatch = new Rect2(1110.0f, 700.0f, 200.0f, 130.0f);
				DrawRect(swatch, colour);
				CrayonBrush.InkRect(this, swatch, MenuTheme.Ink, 4.0f, 860, 1.6f);
				MenuTheme.DrawButton(this, DoneBox, "Done", 861, PopupHot(DoneBox), true, fontSize: 32);
				break;
		}
	}
}
