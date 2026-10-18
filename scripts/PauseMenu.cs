using System.Collections.Generic;
using Godot;

/// <summary>
/// Start in the middle of a match: everything stops, and a small page offers Resume, Pick
/// fighters or Main menu (Eric, 2026-10-18) - in a story, the story's map instead of picking
/// fighters. Any player can steer it - it listens to every human's controller, the
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

	/// <summary>What it offers: Resume first, then wherever the match can be left for.</summary>
	(string label, System.Action act)[] options;
	int selected;

	Label[] optionLabels;
	Node2D page;

	public void Open(IEnumerable<IInputSource> humans, System.Action resume, params (string label, System.Action act)[] leave)
	{
		foreach (IInputSource source in humans) navs.Add(new MenuNav(source));
		onResume = resume;
		options = new (string, System.Action)[leave.Length + 1];
		options[0] = ("Resume", resume);
		for (int i = 0; i < leave.Length; i++) options[i + 1] = leave[i];
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

		var title = MenuTheme.MakeLabel("Paused", new Vector2(0.0f, 280.0f), 96, MenuTheme.Text);
		title.Size = new Vector2(1920.0f, 120.0f);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(title);

		optionLabels = new Label[options.Length];
		for (int i = 0; i < options.Length; i++)
		{
			Label label = MenuTheme.MakeLabel(options[i].label, new Vector2(0.0f, 440.0f + i * 100.0f), 60, MenuTheme.Text);
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
				selected = (selected + nav.StepY + options.Length) % options.Length;
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
				options[selected].act?.Invoke();
				return;
			}
		}
	}

	void Refresh()
	{
		for (int i = 0; i < optionLabels.Length; i++)
		{
			optionLabels[i].AddThemeColorOverride("font_color", i == selected ? MenuTheme.Accent : MenuTheme.Soft);
			optionLabels[i].Text = i == selected ? $"> {options[i].label} <" : options[i].label;
		}
	}

	/// <summary>The match dimmed behind, and a sheet of paper in front with the choices on it.</summary>
	void DrawPage()
	{
		page.DrawRect(new Rect2(0.0f, 0.0f, 1920.0f, 1080.0f), new Color(0.93f, 0.92f, 0.89f, 0.55f));

		var sheet = new Rect2(640.0f, 250.0f, 640.0f, 120.0f + options.Length * 100.0f + 100.0f);
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
