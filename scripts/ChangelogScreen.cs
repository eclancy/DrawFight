using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// What's new: every version's changes, newest first, from Options (Eric, 2026-10-18). Read from
/// assets/changelog.txt, which tools/release/release.sh adds to with each release's notes - so
/// the game says what changed in the words the release did. The stick scrolls; B goes back.
/// </summary>
public partial class ChangelogScreen : Node2D
{
	public string OpenedBy;

	const string Path = "res://assets/changelog.txt";
	static readonly Rect2 BackBox = new Rect2(190.0f, 950.0f, 240.0f, 76.0f);

	TouchCursors touch;
	Node2D cursorLayer;
	RichTextLabel text;
	bool leaving;

	/// <summary>The versions in the file, newest first: the version, its date, and its changes.</summary>
	public static List<(string version, string date, List<string> changes)> Read()
	{
		var versions = new List<(string, string, List<string>)>();
		using FileAccess file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
		if (file == null) return versions;
		List<string> current = null;
		foreach (string raw in file.GetAsText().Split('\n'))
		{
			string line = raw.Trim();
			if (line.StartsWith("#") || line.Length == 0) continue;
			if (line.StartsWith("== "))
			{
				string[] parts = line.Substring(3).Split('|');
				current = new List<string>();
				versions.Add((parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : "", current));
				continue;
			}
			current?.Add(line);
		}
		return versions;
	}

	public override void _Ready()
	{
		var layer = new CanvasLayer { Layer = 2 };
		AddChild(layer);
		text = new RichTextLabel
		{
			Position = new Vector2(190.0f, 210.0f),
			Size = new Vector2(1540.0f, 710.0f),
			BbcodeEnabled = true,
			ScrollActive = true,
			FitContent = false,
		};
		text.AddThemeColorOverride("default_color", MenuTheme.Text);
		text.AddThemeFontSizeOverride("normal_font_size", 30);
		text.AddThemeFontSizeOverride("bold_font_size", 44);
		layer.AddChild(text);

		var sb = new StringBuilder();
		foreach ((string version, string date, List<string> changes) in Read())
		{
			bool now = version == UpdateScreen.CurrentVersion;
			sb.Append($"[b]{version}[/b]  [color=#{MenuTheme.Soft.ToHtml(false)}]{date}{(now ? "  - this one" : "")}[/color]\n");
			foreach (string change in changes) sb.Append($"  •  {change}\n");
			sb.Append('\n');
		}
		text.Text = sb.ToString();

		touch = new TouchCursors(new Vector2(960.0f, 1000.0f), OpenedBy);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in touch.Shown) cursor.Draw(canvas);
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;
		touch.Tick();
		foreach (MenuCursor cursor in touch.Shown)
		{
			if (leaving) return;
			cursor.Update(viewport);
			// Up and down on the stick scroll the list as well as moving the cursor.
			if (Mathf.Abs(cursor.Nav.Stick.Y) > 0.3f) text.GetVScrollBar().Value += cursor.Nav.Stick.Y * 22.0f;
			if (cursor.Cancel || cursor.Confirm && BackBox.HasPoint(cursor.Position))
			{
				leaving = true;
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoOptions(touch.KeyOf(cursor));
				return;
			}
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
		DrawString(ThemeDB.FallbackFont, new Vector2(190.0f, 170.0f), "What's new", HorizontalAlignment.Left, -1, 96, MenuTheme.Text);
		bool hot = false;
		foreach (MenuCursor cursor in touch.Shown) hot |= BackBox.HasPoint(cursor.Position);
		MenuTheme.DrawButton(this, BackBox, "Back", 390, hot, fontSize: 38);
	}
}
