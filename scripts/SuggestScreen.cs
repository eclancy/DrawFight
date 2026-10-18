using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// An idea for the game, typed and sent in (Eric, 2026-10-18), from "Ideas" on the main menu. It
/// goes to the same server as a fighter (EricClancyEngineeringAndCrafts: intake/server.js, POST
/// /api/draw/suggest), which saves it and emails it to Eric. A name is optional - the last name
/// given on fighter select is filled in. It is a typing screen, so like the drawing screens the
/// mouse works here, or one pad's cursor for the buttons (OnePointer).
/// </summary>
public partial class SuggestScreen : Node2D
{
	static readonly Rect2 BackBox = new Rect2(190.0f, 30.0f, 160.0f, 66.0f);
	static readonly Rect2 SendBox = new Rect2(190.0f, 900.0f, 460.0f, 92.0f);
	static readonly Rect2 DoneMenuBox = new Rect2(770.0f, 640.0f, 380.0f, 84.0f);

	const int MaxChars = 2000;

	LineEdit name;
	TextEdit idea;
	CanvasLayer formLayer;
	readonly List<MenuCursor> cursors = new List<MenuCursor>();
	OnePointer pointers;
	Node2D cursorLayer;
	Vector2 mouse = new Vector2(-100.0f, -100.0f);

	enum Phase { Editing, Sending, Done }
	Phase phase;
	Task<string> sending;
	string message = "";
	bool messageIsError;

	public override void _Ready()
	{
		FighterIntake.Check();

		formLayer = new CanvasLayer { Layer = 4 };
		AddChild(formLayer);
		var theme = DrawScreen.FormTheme();

		name = new LineEdit
		{
			Position = new Vector2(190.0f, 300.0f), Size = new Vector2(600.0f, 64.0f), MaxLength = 40, Theme = theme,
			Text = PlayerNames.All.Count > 0 ? PlayerNames.All[0] : "",
			PlaceholderText = "Your name (you can leave it out)",
		};
		name.AddThemeFontSizeOverride("font_size", 30);
		formLayer.AddChild(name);

		idea = new TextEdit
		{
			Position = new Vector2(190.0f, 440.0f), Size = new Vector2(1540.0f, 400.0f), Theme = theme,
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			PlaceholderText = "A new fighter, a stage, a move, something that would be fun, something that is broken...",
		};
		idea.AddThemeFontSizeOverride("font_size", 30);
		formLayer.AddChild(idea);
		idea.GrabFocus();

		int pads = Input.GetConnectedJoypads().Count;
		IInputSource[] sources = ControllerAssignment.ForPlayers(pads);
		for (int i = 0; i < sources.Length; i++) cursors.Add(new MenuCursor(sources[i], -1, MenuTheme.PlayerTint(i), SendBox.GetCenter()));
		pointers = new OnePointer(cursors);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas => pointers.Draw(canvas));
	}

	public override void _ExitTree() => OnePointer.Leave();

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			if (phase != Phase.Sending) Back();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouse) pointers.MouseUsed();
		if (@event is InputEventMouseMotion motion) mouse = motion.Position;
		if (@event is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left)
		{
			mouse = click.Position;
			Press(click.Position);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		MenuCursor cursor = pointers.Tick(GetViewportRect().Size, mouse);
		if (cursor == null) return;
		if (cursor.Cancel && phase != Phase.Sending)
		{
			Back();
			return;
		}
		if (cursor.Confirm) Press(cursor.Position);
	}

	public override void _Process(double delta)
	{
		FighterIntake.CheckEvery(15.0f);
		if (phase == Phase.Sending && sending != null && sending.IsCompleted) Finished(sending.Result);
		formLayer.Visible = phase != Phase.Done;
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Back()
	{
		SfxPlayer.Ui("ui_back");
		GameRoot.Instance.GoMainMenu();
	}

	void Press(Vector2 at)
	{
		if (phase == Phase.Done)
		{
			if (DoneMenuBox.HasPoint(at)) GameRoot.Instance.GoMainMenu();
			return;
		}
		if (phase != Phase.Editing) return;
		if (BackBox.HasPoint(at)) Back();
		else if (SendBox.HasPoint(at)) TrySend();
	}

	void TrySend()
	{
		string text = idea.Text.Trim();
		if (text.Length == 0)
		{
			Say("Write your idea first.", true);
			idea.GrabFocus();
			SfxPlayer.Ui("ui_back");
			return;
		}
		if (FighterIntake.State != FighterIntake.Link.Online)
		{
			FighterIntake.Check();
			Say($"Can't reach {FighterIntake.Where} right now. Try again when you're online.", true);
			return;
		}
		if (text.Length > MaxChars) text = text.Substring(0, MaxChars);
		var fields = new List<KeyValuePair<string, string>>
		{
			new("name", name.Text.Trim()),
			new("suggestion", text),
			new("version", UpdateScreen.CurrentVersion),
		};
		SfxPlayer.Ui("ui_select");
		phase = Phase.Sending;
		Say("Sending...", false);
		sending = FighterIntake.Send(fields, new List<(string, byte[])>(), "/api/draw/suggest");
	}

	void Finished(string error)
	{
		sending = null;
		if (error != null)
		{
			phase = Phase.Editing;
			Say(error, true);
			SfxPlayer.Ui("ui_back");
			return;
		}
		phase = Phase.Done;
		SfxPlayer.Ui("ui_ready");
	}

	void Say(string text, bool error)
	{
		message = text;
		messageIsError = error;
	}

	bool Hot(Rect2 rect) => rect.HasPoint(pointers.At(mouse));

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
		Font font = ThemeDB.FallbackFont;
		if (phase == Phase.Done)
		{
			MenuTheme.DrawCentredText(this, new Rect2(0.0f, 300.0f, 1920.0f, 120.0f), "Thank you!", 110, MenuTheme.Text);
			MenuTheme.DrawCentredText(this, new Rect2(0.0f, 450.0f, 1920.0f, 60.0f), "Your idea is on its way to the people who make DrawFight.", 40, MenuTheme.Text);
			MenuTheme.DrawButton(this, DoneMenuBox, "Main menu", 961, Hot(DoneMenuBox), fontSize: 34);
			return;
		}

		MenuTheme.DrawButton(this, BackBox, "Back", 951, Hot(BackBox), fontSize: 30);
		DrawString(font, new Vector2(190.0f, 190.0f), "Got an idea?", HorizontalAlignment.Left, -1, 72, MenuTheme.Text);
		DrawString(font, new Vector2(190.0f, 250.0f), "Tell us what would make DrawFight better. Every idea gets read.", HorizontalAlignment.Left, -1, 30, MenuTheme.Soft);
		DrawString(font, new Vector2(190.0f, 420.0f), "Your idea", HorizontalAlignment.Left, -1, 30, MenuTheme.Text);

		(string words, Color light) = FighterIntake.Describe();
		DrawCircle(new Vector2(870.0f, 332.0f), 11.0f, light);
		DrawString(font, new Vector2(892.0f, 343.0f), words, HorizontalAlignment.Left, 700.0f, 26, MenuTheme.Text);

		bool ready = phase == Phase.Editing && FighterIntake.State == FighterIntake.Link.Online;
		MenuTheme.DrawButton(this, SendBox, phase == Phase.Editing ? "Send my idea" : "Sending...", 952,
			Hot(SendBox) && phase == Phase.Editing, ready, phase == Phase.Editing, 40);
		if (message != "")
		{
			DrawString(font, new Vector2(690.0f, 958.0f), message, HorizontalAlignment.Left, 1040.0f, 28,
				messageIsError ? new Color(0.78f, 0.24f, 0.20f) : MenuTheme.Ink);
		}
	}
}
