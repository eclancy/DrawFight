using System.Collections.Generic;
using Godot;

/// <summary>
/// Start in the middle of a match: everything stops, and a small page offers Resume or going
/// back to pick fighters. Any player can steer it - it listens to every human's controller, the
/// same ones they fight with, so there is nothing to map.
///
/// It reads those controllers directly rather than through Godot's input events, which also
/// swallows the button that closes it: the fighter's own controller has seen that press by the
/// time play resumes, so choosing Resume with A does not come out as a jump.
///
/// The match pauses through the scene tree, so fighters, hazards and platforms all freeze
/// without any of them having to know about pausing. This node alone keeps running.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
	readonly List<MenuNav> navs = new List<MenuNav>();
	System.Action onResume;
	System.Action onQuit;

	static readonly string[] Options = { "Resume", "Pick fighters" };
	int selected;

	Label[] optionLabels;
	Node2D page;

	public void Open(IEnumerable<IInputSource> humans, System.Action resume, System.Action quit)
	{
		foreach (IInputSource source in humans) navs.Add(new MenuNav(source));
		onResume = resume;
		onQuit = quit;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Layer = 20;

		page = new Node2D();
		AddChild(page);
		page.Draw += DrawPage;

		var root = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);

		var title = MenuTheme.MakeLabel("Paused", new Vector2(0.0f, 330.0f), 96, MenuTheme.Text);
		title.Size = new Vector2(1920.0f, 120.0f);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(title);

		optionLabels = new Label[Options.Length];
		for (int i = 0; i < Options.Length; i++)
		{
			Label label = MenuTheme.MakeLabel(Options[i], new Vector2(0.0f, 500.0f + i * 110.0f), 64, MenuTheme.Text);
			label.Size = new Vector2(1920.0f, 90.0f);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			root.AddChild(label);
			optionLabels[i] = label;
		}
		Refresh();
	}

	public override void _Process(double delta)
	{
		foreach (MenuNav nav in navs)
		{
			nav.Poll();

			if (nav.StepY != 0)
			{
				selected = (selected + nav.StepY + Options.Length) % Options.Length;
				SfxPlayer.Ui("ui_move");
				Refresh();
			}

			// Start again, or B, is always "carry on".
			if (nav.Start || nav.Cancel)
			{
				SfxPlayer.Ui("ui_back");
				onResume?.Invoke();
				return;
			}

			if (nav.Confirm)
			{
				SfxPlayer.Ui("ui_select");
				if (selected == 0) onResume?.Invoke();
				else onQuit?.Invoke();
				return;
			}
		}
	}

	void Refresh()
	{
		for (int i = 0; i < optionLabels.Length; i++)
		{
			optionLabels[i].AddThemeColorOverride("font_color", i == selected ? MenuTheme.Accent : MenuTheme.Soft);
			optionLabels[i].Text = i == selected ? $"> {Options[i]} <" : Options[i];
		}
	}

	/// <summary>The match dimmed behind, and a sheet of paper in front with the choices on it.</summary>
	void DrawPage()
	{
		page.DrawRect(new Rect2(0.0f, 0.0f, 1920.0f, 1080.0f), new Color(0.93f, 0.92f, 0.89f, 0.55f));

		var sheet = new Rect2(660.0f, 290.0f, 600.0f, 480.0f);
		page.DrawRect(sheet, MenuTheme.Paper);
		for (float y = sheet.Position.Y + 60.0f; y < sheet.End.Y - 10.0f; y += 54.0f)
		{
			CrayonBrush.InkLine(page, new Vector2(sheet.Position.X + 12.0f, y), new Vector2(sheet.End.X - 12.0f, y),
				MenuTheme.Rule, 2.0f, Mathf.RoundToInt(y), 1.2f);
		}
		CrayonBrush.InkRect(page, sheet, MenuTheme.Ink, 4.0f, 77, 2.4f);

		// A strip of tape holding it up.
		page.DrawRect(new Rect2(sheet.GetCenter().X - 60.0f, sheet.Position.Y - 18.0f, 120.0f, 36.0f),
			new Color(0.96f, 0.93f, 0.72f, 0.85f));
	}
}
