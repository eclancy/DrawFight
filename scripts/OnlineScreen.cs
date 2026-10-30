using System.Collections.Generic;
using Godot;

/// <summary>
/// Playing online, from the main menu (Eric, 2026-10-29; .ai/online.md): start a game and get a
/// code to give your friends, or join one by typing a friend's code. Then a lobby - the code, and
/// how many computers are in - until the host presses Start, and every copy goes on together to
/// what to play (OnlineHomeScreen). A session that ends comes back here, saying why.
///
/// This screen is each computer's own, read from its own controllers: the session only plays in
/// lockstep once it has started (NetSession).
/// </summary>
public partial class OnlineScreen : Node2D
{
	/// <summary>Why the last session ended, or null.</summary>
	public string Message;

	enum Step { Choose, Code, Lobby }
	Step step = Step.Choose;

	/// <summary>The code being typed.</summary>
	string code = "";
	const int CodeLength = 5;

	/// <summary>The letters a code is made of: no I or O, which read as 1 and 0 (play/server.js).</summary>
	const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

	static readonly Rect2 HostBox = new Rect2(560.0f, 400.0f, 800.0f, 150.0f);
	static readonly Rect2 JoinBox = new Rect2(560.0f, 610.0f, 800.0f, 150.0f);
	static readonly Rect2 StartBox = new Rect2(710.0f, 800.0f, 500.0f, 120.0f);
	const int KeyColumns = 8;

	TouchCursors touch;
	Node2D cursorLayer;
	bool leaving;
	int frame;

	public override void _Ready()
	{
		touch = new TouchCursors(new Vector2(960.0f, 960.0f), GameRoot.Instance.MenuReturnKey);
		GameRoot.Instance.MenuReturnKey = null;
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in touch.Shown) cursor.Draw(canvas);
		});
		if (NetSession.Active && !NetSession.Playing) step = Step.Lobby;
	}

	/// <summary>A key on the letter grid: the letters, then DEL and JOIN.</summary>
	static Rect2 KeyBox(int i)
	{
		const float W = 120.0f, H = 90.0f, Gap = 14.0f;
		float left = (1920.0f - (KeyColumns * W + (KeyColumns - 1) * Gap)) * 0.5f;
		return new Rect2(left + i % KeyColumns * (W + Gap), 470.0f + i / KeyColumns * (H + Gap), W, H);
	}

	static string KeyText(int i) => i < Letters.Length ? Letters[i].ToString() : i == Letters.Length ? "DEL" : "JOIN";
	static int KeyCount => Letters.Length + 2;

	public override void _PhysicsProcess(double delta)
	{
		frame++;
		// The session has started: from here every copy plays it together, and this screen waits.
		if (NetSession.Playing) { QueueRedraw(); return; }
		if (step == Step.Lobby && !NetSession.Active) step = Step.Choose;

		Vector2 viewport = GetViewportRect().Size;
		touch.Tick();
		foreach (MenuCursor cursor in touch.Shown)
		{
			if (leaving) break;
			cursor.Update(viewport);
			if (cursor.Cancel || cursor.Confirm && MenuTheme.BackBox.HasPoint(cursor.Position))
			{
				Back();
				break;
			}
			if (cursor.Confirm) Press(cursor.Position);
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	/// <summary>Typing a code on a keyboard, as well as picking letters.</summary>
	public override void _UnhandledInput(InputEvent e)
	{
		if (step != Step.Code || e is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode == Key.Backspace) Type("DEL");
		else if (key.Keycode is Key.Enter or Key.KpEnter) Type("JOIN");
		else if (key.Unicode > 0)
		{
			string letter = ((char)key.Unicode).ToString().ToUpper();
			if (Letters.Contains(letter)) Type(letter);
		}
	}

	void Press(Vector2 at)
	{
		switch (step)
		{
			case Step.Choose:
				if (HostBox.HasPoint(at))
				{
					SfxPlayer.Ui("ui_select");
					Message = null;
					NetSession.Host();
					step = Step.Lobby;
				}
				else if (JoinBox.HasPoint(at))
				{
					SfxPlayer.Ui("ui_select");
					Message = null;
					code = "";
					step = Step.Code;
				}
				break;
			case Step.Code:
				for (int i = 0; i < KeyCount; i++) if (KeyBox(i).HasPoint(at)) Type(KeyText(i));
				break;
			case Step.Lobby:
				NetSession session = NetSession.Instance;
				if (StartBox.HasPoint(at) && session != null && session.IsHost)
				{
					if (session.Members.Count < 2) { SfxPlayer.Ui("ui_back"); return; }
					SfxPlayer.Ui("ui_ready");
					session.StartGame();
				}
				break;
		}
	}

	void Type(string key)
	{
		if (key == "DEL")
		{
			if (code.Length > 0) code = code.Substring(0, code.Length - 1);
			SfxPlayer.Ui("ui_back");
			return;
		}
		if (key == "JOIN")
		{
			if (code.Length < CodeLength) { SfxPlayer.Ui("ui_back"); return; }
			SfxPlayer.Ui("ui_select");
			NetSession.Join(code);
			step = Step.Lobby;
			return;
		}
		if (code.Length >= CodeLength) return;
		code += key;
		SfxPlayer.Ui("ui_move");
	}

	void Back()
	{
		SfxPlayer.Ui("ui_back");
		switch (step)
		{
			case Step.Code:
				step = Step.Choose;
				break;
			case Step.Lobby:
				NetSession.Instance?.Close(null);
				step = Step.Choose;
				break;
			default:
				leaving = true;
				GameRoot.Instance.GoMainMenu();
				break;
		}
	}

	bool Pointed(Rect2 box)
	{
		foreach (MenuCursor cursor in touch.Shown) if (box.HasPoint(cursor.Position)) return true;
		return false;
	}

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(190.0f, 190.0f), "Play online", HorizontalAlignment.Left, -1, 76, MenuTheme.Text);
		bool started = NetSession.Playing;
		if (!started) MenuTheme.DrawButton(this, MenuTheme.BackBox, step == Step.Lobby ? "Leave" : "Back", 801, Pointed(MenuTheme.BackBox), fontSize: 32);

		if (!string.IsNullOrEmpty(Message))
		{
			DrawMultilineString(font, new Vector2(190.0f, 290.0f), Message, HorizontalAlignment.Center, 1540.0f, 36, 2, new Color(0.80f, 0.25f, 0.22f));
		}

		switch (step)
		{
			case Step.Choose:
				MenuTheme.DrawButton(this, HostBox, "", 901, Pointed(HostBox));
				MenuTheme.DrawCentredText(this, new Rect2(HostBox.Position, new Vector2(HostBox.Size.X, 95.0f)), "Start a game", 52, MenuTheme.Text);
				MenuTheme.DrawCentredText(this, new Rect2(HostBox.Position + new Vector2(0.0f, 85.0f), new Vector2(HostBox.Size.X, 50.0f)), "Get a code to give your friends.", 28, MenuTheme.Soft);
				MenuTheme.DrawButton(this, JoinBox, "", 902, Pointed(JoinBox));
				MenuTheme.DrawCentredText(this, new Rect2(JoinBox.Position, new Vector2(JoinBox.Size.X, 95.0f)), "Join a game", 52, MenuTheme.Text);
				MenuTheme.DrawCentredText(this, new Rect2(JoinBox.Position + new Vector2(0.0f, 85.0f), new Vector2(JoinBox.Size.X, 50.0f)), "Type in a friend's code.", 28, MenuTheme.Soft);
				break;

			case Step.Code:
				MenuTheme.DrawCentredText(this, new Rect2(0.0f, 300.0f, 1920.0f, 50.0f), "Type your friend's code:", 36, MenuTheme.Soft);
				for (int i = 0; i < CodeLength; i++)
				{
					var box = new Rect2(760.0f + i * 84.0f, 350.0f, 72.0f, 96.0f);
					DrawRect(box, Colors.White);
					CrayonBrush.InkRect(this, box, MenuTheme.Ink, 3.0f, 910 + i, 2.0f);
					if (i < code.Length) MenuTheme.DrawCentredText(this, box, code[i].ToString(), 64, MenuTheme.Text);
				}
				var keys = new List<MenuTheme.ButtonSpec>();
				for (int i = 0; i < KeyCount; i++)
				{
					Rect2 box = KeyBox(i);
					keys.Add(new MenuTheme.ButtonSpec(box, KeyText(i), 920 + i, Pointed(box), KeyText(i) == "JOIN" && code.Length == CodeLength, i < Letters.Length ? 44 : 30, null));
				}
				MenuTheme.DrawButtons(this, keys);
				break;

			case Step.Lobby:
				NetSession session = NetSession.Instance;
				if (session == null) break;
				if (started)
				{
					MenuTheme.DrawCentredText(this, new Rect2(0.0f, 480.0f, 1920.0f, 100.0f), "Starting...", 64, MenuTheme.Text);
					break;
				}
				if (session.Code == "")
				{
					MenuTheme.DrawCentredText(this, new Rect2(0.0f, 480.0f, 1920.0f, 100.0f), "Connecting" + new string('.', frame / 20 % 4), 56, MenuTheme.Soft);
					break;
				}
				MenuTheme.DrawCentredText(this, new Rect2(0.0f, 330.0f, 1920.0f, 50.0f), "The code is", 36, MenuTheme.Soft);
				MenuTheme.DrawCentredText(this, new Rect2(0.0f, 380.0f, 1920.0f, 180.0f), session.Code, 150, MenuTheme.Text);
				MenuTheme.DrawCentredText(this, new Rect2(0.0f, 580.0f, 1920.0f, 50.0f), session.Status, 36, MenuTheme.Soft);
				int computers = session.Members.Count;
				MenuTheme.DrawCentredText(this, new Rect2(0.0f, 650.0f, 1920.0f, 60.0f),
					computers == 1 ? "Just this computer so far." : $"{computers} computers are in.", 40, MenuTheme.Text);
				if (session.IsHost)
				{
					MenuTheme.DrawButton(this, StartBox, "Start", 903, Pointed(StartBox), enabled: computers >= 2, fontSize: 52);
				}
				break;
		}
	}
}
