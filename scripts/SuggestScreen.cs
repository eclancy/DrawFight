using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// A bug report, typed and sent in, from "Report a bug" on the main menu - only bugs (Eric,
/// 2026-10-20; it began as "Ideas", 2026-10-18): new fighters and stages come in as drawings,
/// through Draw your own. It goes to the same server as a fighter (EricClancyEngineeringAndCrafts: intake/server.js, POST
/// /api/draw/suggest), which saves it and emails it to Eric. A name is optional - the last name
/// given on fighter select is filled in. It is a typing screen, so like the drawing screens the
/// mouse works here, or one pad's cursor for the buttons (OnePointer).
///
/// It can send one screenshot, as screenshot.jpg: the last frame of the last match, which the
/// match left behind in memory as it was paused or left (BugSnapshot) - a picture taken here
/// would only show the main menu. "Include a screenshot" is on whenever there is one, beside a
/// preview of exactly the picture that goes (the JPEG itself, decoded) and what it is from;
/// unticked, nothing is sent. After a fresh launch there is none, and nothing is offered.
/// </summary>
public partial class SuggestScreen : Node2D
{
	static Rect2 BackBox => MenuTheme.BackBox;
	static readonly Rect2 SendBox = new Rect2(190.0f, 900.0f, 460.0f, 92.0f);
	static readonly Rect2 DoneMenuBox = new Rect2(770.0f, 640.0f, 380.0f, 84.0f);

	// The screenshot, to the right of a narrower "What happened" box when there is one.
	static readonly Rect2 ShotToggleBox = new Rect2(1290.0f, 440.0f, 440.0f, 64.0f);
	static readonly Rect2 ShotTickBox = new Rect2(1294.0f, 444.0f, 56.0f, 56.0f);
	static readonly Rect2 ShotPreviewBox = new Rect2(1290.0f, 528.0f, 440.0f, 248.0f);

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

	/// <summary>The screenshot as sent (BugSnapshot), its preview, and whether it goes.</summary>
	byte[] shotJpeg;
	ImageTexture shotPreview;
	bool includeShot;

	/// <summary>
	/// "--bugsend" (or "--bugsend=noshot", unticked) sends a test report by itself once connected,
	/// to check what goes - but only to a local intake server (DRAWFIGHT_INTAKE_URL), never to
	/// ecec.dev, where a report emails Eric.
	/// </summary>
	string testSend;

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

		// The preview is of the JPEG itself, decoded again, so it is exactly what is sent.
		shotJpeg = BugSnapshot.Jpeg();
		var decoded = new Image();
		if (shotJpeg != null && decoded.LoadJpgFromBuffer(shotJpeg) == Error.Ok) shotPreview = ImageTexture.CreateFromImage(decoded);
		else shotJpeg = null;
		includeShot = shotJpeg != null;
		if (!string.IsNullOrEmpty(OS.GetEnvironment("DRAWFIGHT_INTAKE_URL")))
		{
			foreach (string arg in OS.GetCmdlineUserArgs())
			{
				if (arg == "--bugsend" || arg.StartsWith("--bugsend=")) testSend = arg;
			}
		}

		idea = new TextEdit
		{
			Position = new Vector2(190.0f, 440.0f), Size = new Vector2(shotJpeg != null ? 1040.0f : 1540.0f, 400.0f), Theme = theme,
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			PlaceholderText = "What were you doing, and what went wrong? Which fighter, which stage, what did you press?",
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
		if (testSend != null && phase == Phase.Editing && FighterIntake.State == FighterIntake.Link.Online)
		{
			if (testSend == "--bugsend=noshot") includeShot = false;
			testSend = null;
			idea.Text = "A test report (--bugsend).";
			TrySend();
		}
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
		else if (shotJpeg != null && (ShotToggleBox.HasPoint(at) || ShotPreviewBox.HasPoint(at)))
		{
			includeShot = !includeShot;
			SfxPlayer.Ui("ui_select");
		}
	}

	void TrySend()
	{
		string text = idea.Text.Trim();
		if (text.Length == 0)
		{
			Say("Tell us what happened first.", true);
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
			new("kind", "bug"),
			new("version", UpdateScreen.CurrentVersion),
		};
		SfxPlayer.Ui("ui_select");
		phase = Phase.Sending;
		Say("Sending...", false);
		var files = new List<(string, byte[])>();
		if (includeShot && shotJpeg != null) files.Add(("screenshot.jpg", shotJpeg));
		sending = FighterIntake.Send(fields, files, "/api/draw/suggest");
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
			MenuTheme.DrawCentredText(this, new Rect2(0.0f, 450.0f, 1920.0f, 60.0f), "Your bug report is on its way to the people who make DrawFight.", 40, MenuTheme.Text);
			MenuTheme.DrawButton(this, DoneMenuBox, "Main menu", 961, Hot(DoneMenuBox), fontSize: 34);
			return;
		}

		MenuTheme.DrawButton(this, BackBox, "Back", 951, Hot(BackBox), fontSize: 30);
		DrawString(font, new Vector2(190.0f, 190.0f), "Found a bug?", HorizontalAlignment.Left, -1, 72, MenuTheme.Text);
		DrawString(font, new Vector2(190.0f, 250.0f), "Tell us what you were doing and what went wrong. Want a new fighter or stage? Draw it in Draw your own!", HorizontalAlignment.Left, -1, 30, MenuTheme.Soft);
		DrawString(font, new Vector2(190.0f, 420.0f), "What happened", HorizontalAlignment.Left, -1, 30, MenuTheme.Text);

		(string words, Color light) = FighterIntake.Describe();
		DrawCircle(new Vector2(870.0f, 332.0f), 11.0f, light);
		DrawString(font, new Vector2(892.0f, 343.0f), words, HorizontalAlignment.Left, 700.0f, 26, MenuTheme.Text);

		if (shotJpeg != null) DrawScreenshot(font);

		bool ready = phase == Phase.Editing && FighterIntake.State == FighterIntake.Link.Online;
		MenuTheme.DrawButton(this, SendBox, phase == Phase.Editing ? "Send bug report" : "Sending...", 952,
			Hot(SendBox) && phase == Phase.Editing, ready, phase == Phase.Editing, 40);
		if (message != "")
		{
			DrawString(font, new Vector2(690.0f, 958.0f), message, HorizontalAlignment.Left, 1040.0f, 28,
				messageIsError ? new Color(0.78f, 0.24f, 0.20f) : MenuTheme.Ink);
		}
	}

	/// <summary>"Include a screenshot", a tick box, and the picture under it - faded when it will not go.</summary>
	void DrawScreenshot(Font font)
	{
		bool hot = phase == Phase.Editing && (Hot(ShotToggleBox) || Hot(ShotPreviewBox));
		MenuTheme.DrawButton(this, ShotTickBox, "", 953, hot, includeShot);
		if (includeShot)
		{
			Vector2 o = ShotTickBox.Position;
			CrayonBrush.InkLine(this, o + new Vector2(12.0f, 30.0f), o + new Vector2(24.0f, 44.0f), MenuTheme.Ink, 6.0f, 954, 1.2f);
			CrayonBrush.InkLine(this, o + new Vector2(24.0f, 44.0f), o + new Vector2(46.0f, 12.0f), MenuTheme.Ink, 6.0f, 955, 1.2f);
		}
		DrawString(font, new Vector2(1366.0f, 483.0f), "Include a screenshot", HorizontalAlignment.Left, 364.0f, 30, MenuTheme.Text);

		// The picture keeps its shape inside the frame.
		Vector2 size = shotPreview.GetSize();
		float scale = Mathf.Min(ShotPreviewBox.Size.X / size.X, ShotPreviewBox.Size.Y / size.Y);
		Vector2 drawn = size * scale;
		var picture = new Rect2(ShotPreviewBox.GetCenter() - drawn * 0.5f, drawn);
		DrawTextureRect(shotPreview, picture, false, includeShot ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.3f));
		CrayonBrush.InkRect(this, picture.Grow(4.0f), includeShot ? MenuTheme.Ink : MenuTheme.Rule, 3.5f, 956, 2.0f);
		if (!includeShot)
		{
			MenuTheme.DrawCentredText(this, picture, "Not sent", 34, MenuTheme.Soft);
		}
		DrawString(font, new Vector2(1290.0f, ShotPreviewBox.End.Y + 40.0f), BugSnapshot.Caption, HorizontalAlignment.Left, 440.0f,
			MenuTheme.FitFontSize(BugSnapshot.Caption, 440.0f, 26), MenuTheme.Soft);
	}
}
