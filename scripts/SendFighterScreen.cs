using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Sending a drawing in: which pictures go, the character sheet, and the button.
///
/// The questions are the ecec.dev/draw form's, word for word, and go to the same server, so a
/// fighter sent from the game arrives exactly like one sent from the website. Each page goes as
/// one picture, or as one picture per layer - the layered way is the better one for the fighter
/// page (every part arrives already cut out and in place), but the server takes at most 24
/// pictures, so the screen counts them and says when there are too many.
///
/// The answers are kept with the drawing, so nothing has to be typed twice.
/// </summary>
public partial class SendFighterScreen : Node2D
{
	public Drawing Doc;

	static readonly Rect2 BackBox = new Rect2(190.0f, 30.0f, 160.0f, 66.0f);
	static readonly Rect2 SendBox = new Rect2(190.0f, 962.0f, 540.0f, 92.0f);
	static readonly Rect2 RetryBox = new Rect2(560.0f, 262.0f, 170.0f, 50.0f);
	static readonly Rect2 DoneDrawingsBox = new Rect2(560.0f, 640.0f, 380.0f, 84.0f);
	static readonly Rect2 DoneMenuBox = new Rect2(980.0f, 640.0f, 380.0f, 84.0f);
	const float RowTop = 330.0f;
	const float RowStep = 104.0f;

	static Rect2 Row(int i) => new Rect2(190.0f, RowTop + i * RowStep, 540.0f, 96.0f);
	static Rect2 OneBox(int i) => new Rect2(330.0f, RowTop + i * RowStep + 46.0f, 182.0f, 44.0f);
	static Rect2 EachBox(int i) => new Rect2(520.0f, RowTop + i * RowStep + 46.0f, 210.0f, 44.0f);

	/// <summary>The form's questions: the server's field name, the question, the hint under it, and
	/// whether it is a one-line answer (and how long it can be).</summary>
	static readonly (string key, string question, string hint, int oneLine)[] Questions =
	{
		("name", "1. What are they called?", "", 60),
		("artist", "Who drew it?", "Just a first name, so we can say who made them.", 40),
		("what", "2. What are they?", "A robot? A wizard? A shark with legs? One or two sentences.", 0),
		("amazing", "3. What are they AMAZING at?", "Pick one thing. Strong? Fast? Jumps really high? Floats? Impossible to knock over?", 0),
		("terrible", "4. What are they TERRIBLE at?", "This is the most important question. Every fighter is bad at something, and that is what makes them fun to play and fun to fight. Slow? Easy to knock off? Bad at jumping? Short arms?", 0),
		("coolest", "5. What is their COOLEST move?", "The one they're famous for. What does it do, and what does it look like? This one goes in the game as close to how you describe it as possible, so go big.", 0),
		("reach", "6. A move for reaching someone far away", "A dash, a charge, a leap, something thrown.", 0),
		("recover", "A move for getting back onto the stage", "This one has to go UP. Rocket boots, a huge jump, wings, a grappling hook.", 0),
		("protect", "A move that protects them", "A shield, a counter-attack, a trap, turning invisible for a second.", 0),
		("story", "7. What's their story?", "Every drawing in DrawFight is fighting for a place on the fridge. Why is yours? Do they want to stay up there, grab the best spot, or keep the whole fridge to themselves? Who are their friends, and who can't they stand? Make up anything you like. This becomes their story in the game.", 0),
		("catchphrase", "Do they have a catchphrase?", "Something they shout when they show off. Like \"Too hot for you!\"", 80),
		("extra", "Anything else?", "A pet or sidekick you drew, what their taunt should be...", 0),
	};

	sealed class PageChoice
	{
		public DrawingPage Page;
		public int Layers;
		public bool EachLayer;
		public Texture2D Thumb;
		public int Count => Layers == 0 ? 0 : EachLayer ? Layers : 1;
	}

	readonly List<PageChoice> pages = new List<PageChoice>();
	readonly Dictionary<string, Control> fields = new Dictionary<string, Control>();
	readonly List<MenuCursor> cursors = new List<MenuCursor>();
	Node2D cursorLayer;
	ScrollContainer form;
	Vector2 mouse = new Vector2(-100.0f, -100.0f);

	enum Phase { Editing, Preparing, Sending, Done }
	Phase phase;
	Task<string> sending;
	string message = "";
	bool messageIsError;
	int frames;

	public override void _Ready()
	{
		FighterIntake.Check();

		foreach (DrawingPage page in Doc.Pages)
		{
			int layers = 0;
			foreach (DrawingLayer layer in page.Layers) if (layer.Visible && !layer.IsEmpty()) layers++;
			Image thumb = Drawing.Flatten(page, MenuTheme.Paper);
			thumb.Resize(128, 96, Image.Interpolation.Bilinear);
			pages.Add(new PageChoice
			{
				Page = page,
				Layers = layers,
				EachLayer = page.Kind == PageKind.Fighter && layers > 1,
				Thumb = ImageTexture.CreateFromImage(thumb),
			});
		}
		// Over the limit as it stands: the pages with the most layers go as one picture first.
		while (Total > FighterIntake.MaxPictures)
		{
			PageChoice biggest = null;
			foreach (PageChoice choice in pages) if (choice.EachLayer && (biggest == null || choice.Layers > biggest.Layers)) biggest = choice;
			if (biggest == null) break;
			biggest.EachLayer = false;
		}

		BuildForm();

		int pads = Input.GetConnectedJoypads().Count;
		if (pads > 0)
		{
			IInputSource[] sources = ControllerAssignment.ForPlayers(pads);
			for (int i = 0; i < sources.Length; i++)
			{
				cursors.Add(new MenuCursor(sources[i], i, i == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent, SendBox.GetCenter()));
			}
		}
		pointers = new OnePointer(cursors);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas => pointers.Draw(canvas));
	}

	/// <summary>The mouse or one pad - whichever was used last - and never both (OnePointer).</summary>
	OnePointer pointers;

	public override void _ExitTree() => OnePointer.Leave();

	int Total
	{
		get
		{
			int total = 0;
			foreach (PageChoice choice in pages) total += choice.Count;
			return total;
		}
	}

	void BuildForm()
	{
		var layer = new CanvasLayer { Layer = 4 };
		AddChild(layer);
		form = new ScrollContainer
		{
			Position = new Vector2(800.0f, 30.0f),
			Size = new Vector2(1090.0f, 1030.0f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			Theme = DrawScreen.FormTheme(),
		};
		layer.AddChild(form);
		var list = new VBoxContainer { CustomMinimumSize = new Vector2(1050.0f, 0.0f) };
		list.AddThemeConstantOverride("separation", 8);
		form.AddChild(list);

		var heading = new Label { Text = "Your character sheet" };
		heading.AddThemeFontSizeOverride("font_size", 44);
		heading.AddThemeColorOverride("font_color", MenuTheme.Text);
		list.AddChild(heading);

		foreach ((string key, string question, string hint, int oneLine) in Questions)
		{
			list.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, 14.0f) });
			list.AddChild(Text(question, 30, MenuTheme.Text));
			if (hint != "") list.AddChild(Text(hint, 23, key == "terrible" ? new Color(0.72f, 0.36f, 0.14f) : MenuTheme.Soft));

			Doc.Sheet.TryGetValue(key, out string answer);
			Control field;
			if (oneLine > 0)
			{
				var line = new LineEdit { Text = answer ?? "", MaxLength = oneLine, CustomMinimumSize = new Vector2(1030.0f, 60.0f) };
				line.TextChanged += text => Keep(key, text);
				field = line;
			}
			else
			{
				var box = new TextEdit
				{
					Text = answer ?? "", CustomMinimumSize = new Vector2(1030.0f, 104.0f),
					WrapMode = TextEdit.LineWrappingMode.Boundary,
				};
				box.TextChanged += () => Keep(key, box.Text);
				field = box;
			}
			field.AddThemeFontSizeOverride("font_size", 28);
			fields[key] = field;
			list.AddChild(field);
		}
		list.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, 60.0f) });
	}

	static Label Text(string text, int size, Color colour)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1030.0f, 0.0f) };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		return label;
	}

	void Keep(string key, string text)
	{
		Doc.Sheet[key] = text;
		Doc.Unsaved = true;
	}

	// --- Input ----------------------------------------------------------------------------------

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			if (phase == Phase.Editing) Back();
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
		Vector2 viewport = GetViewportRect().Size;
		MenuCursor cursor = pointers.Tick(viewport, mouse);
		if (cursor != null)
		{
			if (cursor.Cancel && phase == Phase.Editing)
			{
				Back();
				return;
			}
			if (cursor.Confirm) Press(cursor.Position);
		}
	}

	public override void _Process(double delta)
	{
		frames++;
		FighterIntake.CheckEvery(15.0f);

		// One frame on screen saying it is getting ready, then the work - building the PNGs takes
		// a moment, and a screen that freezes without saying why looks broken.
		if (phase == Phase.Preparing && frames > 1) StartSending();
		if (phase == Phase.Sending && sending != null && sending.IsCompleted) Finished(sending.Result);

		form.Visible = phase != Phase.Done;
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Press(Vector2 at)
	{
		if (phase == Phase.Done)
		{
			if (DoneDrawingsBox.HasPoint(at)) GameRoot.Instance.GoDrawings();
			else if (DoneMenuBox.HasPoint(at)) GameRoot.Instance.GoMainMenu();
			return;
		}
		if (phase != Phase.Editing) return;

		if (BackBox.HasPoint(at))
		{
			Back();
			return;
		}
		if (RetryBox.HasPoint(at) && FighterIntake.State == FighterIntake.Link.Offline)
		{
			FighterIntake.Check();
			return;
		}
		for (int i = 0; i < pages.Count; i++)
		{
			PageChoice choice = pages[i];
			if (choice.Layers == 0) continue;
			if (OneBox(i).HasPoint(at) && choice.EachLayer)
			{
				choice.EachLayer = false;
				SfxPlayer.Ui("ui_move");
			}
			else if (EachBox(i).HasPoint(at) && choice.Layers > 1 && !choice.EachLayer)
			{
				choice.EachLayer = true;
				SfxPlayer.Ui("ui_move");
			}
		}
		if (SendBox.HasPoint(at)) TrySend();
	}

	void Back()
	{
		if (Doc.Unsaved) Doc.Save();
		SfxPlayer.Ui("ui_back");
		GameRoot.Instance.OpenDrawing(Doc);
	}

	void Say(string text, bool error)
	{
		message = text;
		messageIsError = error;
	}

	void TrySend()
	{
		Doc.Sheet.TryGetValue("name", out string name);
		if (string.IsNullOrWhiteSpace(name))
		{
			Say("Your fighter needs a name (question 1).", true);
			form.ScrollVertical = 0;
			fields["name"].GrabFocus();
			SfxPlayer.Ui("ui_back");
			return;
		}
		if (Total == 0)
		{
			Say("There's nothing drawn yet!", true);
			return;
		}
		if (Total > FighterIntake.MaxPictures)
		{
			Say($"That's {Total} pictures, and {FighterIntake.MaxPictures} is the most. Send some pages as one picture.", true);
			return;
		}
		if (FighterIntake.State != FighterIntake.Link.Online)
		{
			FighterIntake.Check();
			Say($"Can't reach {FighterIntake.Where} right now. Your drawing is saved - send it when you're online.", true);
			return;
		}
		SfxPlayer.Ui("ui_select");
		phase = Phase.Preparing;
		frames = 0;
		Say("Getting your pictures ready...", false);
	}

	void StartSending()
	{
		var pictures = new List<(string file, byte[] png)>();
		var lines = new List<string>();
		foreach (PageChoice choice in pages)
		{
			if (choice.Count == 0) continue;
			DrawingPage page = choice.Page;
			if (choice.EachLayer)
			{
				foreach (DrawingLayer layer in page.Layers)
				{
					if (!layer.Visible || layer.IsEmpty()) continue;
					pictures.Add(($"{Slug(page.Name)}-{Slug(layer.Name)}.png", layer.ToImage().SavePngToBuffer()));
					lines.Add($"page \"{page.Name}\" ({page.KindWords}), layer \"{layer.Name}\"");
				}
			}
			else
			{
				pictures.Add(($"{Slug(page.Name)}.png", Drawing.Flatten(page).SavePngToBuffer()));
				lines.Add($"page \"{page.Name}\" ({page.KindWords}), the whole page");
			}
		}
		long total = 0;
		foreach (var (_, png) in pictures)
		{
			total += png.Length;
			if (png.Length <= FighterIntake.MaxPictureBytes) continue;
			Say("One of the pictures is too big to send. Try sending that page as one picture.", true);
			phase = Phase.Editing;
			return;
		}
		if (total > FighterIntake.MaxSendBytes)
		{
			Say("That's too much to send at once. Try sending some pages as one picture.", true);
			phase = Phase.Editing;
			return;
		}

		var answers = new List<KeyValuePair<string, string>>();
		foreach (var (key, _, _, _) in Questions)
		{
			Doc.Sheet.TryGetValue(key, out string answer);
			answer = (answer ?? "").Trim();
			if (key == "extra") answer = WithManifest(answer, lines);
			else if (answer.Length > 2000) answer = answer.Substring(0, 2000);
			answers.Add(new KeyValuePair<string, string>(key, answer));
		}

		Say("Sending...", false);
		phase = Phase.Sending;
		sending = FighterIntake.Send(answers, pictures);
	}

	/// <summary>
	/// The kid's own "anything else", then which picture is which. Each file keeps a short form of
	/// its name (photo-3-fighter-front-arm.png); this list adds what the page is for and the names
	/// exactly as written. The server keeps 2000 characters of an answer, so the kid's text gives
	/// way first.
	/// </summary>
	static string WithManifest(string extra, List<string> lines)
	{
		var manifest = new StringBuilder();
		manifest.Append($"--- Drawn in the DrawFight game (v{UpdateScreen.CurrentVersion}) ---\n");
		manifest.Append($"Every picture is a see-through PNG of the whole page ({Drawing.Width}x{Drawing.Height}), so pictures of the same page line up.\n");
		for (int i = 0; i < lines.Count; i++) manifest.Append($"photo-{i + 1}: {lines[i]}\n");
		string tail = manifest.ToString().TrimEnd();
		int room = 2000 - tail.Length - 2;
		if (extra.Length > room) extra = extra.Substring(0, Mathf.Max(0, room));
		return extra == "" ? tail : extra + "\n\n" + tail;
	}

	static string Slug(string text)
	{
		var slug = new StringBuilder();
		foreach (char c in text.ToLowerInvariant())
		{
			if (char.IsLetterOrDigit(c) && c < 128) slug.Append(c);
			else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
		}
		string result = slug.ToString().Trim('-');
		return result == "" ? "page" : result;
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
		Doc.Sent.Add(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
		Doc.Save();
		phase = Phase.Done;
		SfxPlayer.Ui("ui_select");
	}

	// --- Drawing ---------------------------------------------------------------------------------

	bool Hot(Rect2 rect) => rect.HasPoint(pointers.At(mouse));

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;

		if (phase == Phase.Done)
		{
			DrawDone(font);
			return;
		}

		MenuTheme.DrawButton(this, BackBox, "Back", 901, Hot(BackBox), fontSize: 30);
		DrawString(font, new Vector2(190.0f, 175.0f), "Send it in", HorizontalAlignment.Left, -1, 60, MenuTheme.Text);

		(string words, Color light) = FighterIntake.Describe();
		DrawCircle(new Vector2(200.0f, 238.0f), 11.0f, light);
		DrawString(font, new Vector2(222.0f, 249.0f), words, HorizontalAlignment.Left, 540.0f, 26, MenuTheme.Text);
		if (FighterIntake.State == FighterIntake.Link.Offline) MenuTheme.DrawButton(this, RetryBox, "Try again", 902, Hot(RetryBox), fontSize: 24);

		DrawString(font, new Vector2(190.0f, 314.0f), "Pictures", HorizontalAlignment.Left, -1, 34, MenuTheme.Text);
		for (int i = 0; i < pages.Count; i++) DrawPageRow(font, i);

		int total = Total;
		float y = RowTop + pages.Count * RowStep + 30.0f;
		bool over = total > FighterIntake.MaxPictures;
		string count = total == 1 ? "1 picture" : $"{total} pictures";
		DrawString(font, new Vector2(190.0f, y), $"{count} ({FighterIntake.MaxPictures} at most)", HorizontalAlignment.Left, -1, 26,
			over ? new Color(0.80f, 0.25f, 0.22f) : MenuTheme.Soft);
		if (Doc.Sent.Count > 0)
		{
			string when = Doc.Sent[^1].Length >= 10 ? Doc.Sent[^1].Substring(0, 10) : Doc.Sent[^1];
			DrawMultilineString(font, new Vector2(190.0f, y + 34.0f), $"You sent this on {when}. Sending it again sends it as it is now.",
				HorizontalAlignment.Left, 540.0f, 22, 2, MenuTheme.Soft);
		}

		bool ready = phase == Phase.Editing && FighterIntake.State == FighterIntake.Link.Online && !over && total > 0;
		string label = phase == Phase.Editing ? "Send my fighter" : "Sending...";
		MenuTheme.DrawButton(this, SendBox, label, 903, Hot(SendBox) && phase == Phase.Editing, ready, phase == Phase.Editing, 40);
		if (message != "")
		{
			DrawMultilineString(font, new Vector2(190.0f, 880.0f), message, HorizontalAlignment.Left, 560.0f, 25, 3,
				messageIsError ? new Color(0.78f, 0.24f, 0.20f) : MenuTheme.Ink);
		}
	}

	void DrawPageRow(Font font, int i)
	{
		PageChoice choice = pages[i];
		Rect2 row = Row(i);
		var thumb = new Rect2(row.Position + new Vector2(0.0f, 0.0f), new Vector2(128.0f, 96.0f));
		DrawTextureRect(choice.Thumb, thumb, false);
		DrawRect(thumb, MenuTheme.Rule, false, 2.0f);
		DrawString(font, new Vector2(row.Position.X + 140.0f, row.Position.Y + 32.0f), choice.Page.Name,
			HorizontalAlignment.Left, 400.0f, 28, MenuTheme.Text);
		if (choice.Layers == 0)
		{
			DrawString(font, new Vector2(row.Position.X + 140.0f, row.Position.Y + 78.0f), "Nothing drawn - not sent",
				HorizontalAlignment.Left, -1, 24, MenuTheme.Soft);
			return;
		}
		MenuTheme.DrawButton(this, OneBox(i), "One picture", 910 + i * 2, Hot(OneBox(i)), !choice.EachLayer, fontSize: 22);
		string each = choice.Layers > 1 ? $"Each layer ({choice.Layers})" : "Each layer";
		MenuTheme.DrawButton(this, EachBox(i), each, 911 + i * 2, Hot(EachBox(i)) && choice.Layers > 1, choice.EachLayer,
			choice.Layers > 1, 22);
	}

	void DrawDone(Font font)
	{
		Doc.Sheet.TryGetValue("name", out string name);
		MenuTheme.DrawCentredText(this, new Rect2(0.0f, 300.0f, 1920.0f, 120.0f), "Got it!", 110, MenuTheme.Text);
		MenuTheme.DrawCentredText(this, new Rect2(0.0f, 450.0f, 1920.0f, 60.0f),
			$"{name?.Trim()} is on the way into DrawFight. Thank you for drawing!", 40, MenuTheme.Text);
		MenuTheme.DrawCentredText(this, new Rect2(0.0f, 520.0f, 1920.0f, 50.0f),
			"It gets cut into pieces and put in the game, where it runs, jumps and fights.", 30, MenuTheme.Soft);
		MenuTheme.DrawButton(this, DoneDrawingsBox, "My drawings", 930, Hot(DoneDrawingsBox), fontSize: 34);
		MenuTheme.DrawButton(this, DoneMenuBox, "Main menu", 931, Hot(DoneMenuBox), fontSize: 34);
	}
}
