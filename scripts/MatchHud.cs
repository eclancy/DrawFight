using System.Collections.Generic;
using Godot;

/// <summary>
/// M1 heads-up display: percent, stocks, and which device each player is holding, plus the
/// controls reminder and the debug readout. Deliberately plain - M5 builds the real one with
/// portraits.
/// </summary>
public partial class MatchHud : CanvasLayer
{
	readonly List<Label> percentLabels = new List<Label>();
	readonly List<Label> stockLabels = new List<Label>();
	readonly List<Label> deviceLabels = new List<Label>();

	Label statusLabel;
	Label stageLabel;
	Label helpLabel;

	public void Build(List<Fighter> fighters)
	{
		var root = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);

		// Paper bands behind the readouts. Added before the labels so they render underneath.
		//
		// These are not decoration. The HUD was built against a cream stage and became
		// illegible the moment a stage had grass or brickwork under it. A stage palette is
		// free to be anything, so the HUD has to carry its own background rather than borrow
		// the stage one.
		var paper = new Color(0.97f, 0.96f, 0.93f, 0.86f);

		root.AddChild(new ColorRect
		{
			Color = paper,
			Position = new Vector2(0.0f, 762.0f),
			Size = new Vector2(1920.0f, 320.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});

		root.AddChild(new ColorRect
		{
			Color = paper,
			Position = new Vector2(0.0f, 0.0f),
			Size = new Vector2(760.0f, 156.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});

		for (int i = 0; i < fighters.Count; i++)
		{
			float x = 120.0f + i * 420.0f;

			var name = new Label
			{
				Text = fighters[i].Data.DisplayName,
				Position = new Vector2(x, 784.0f),
			};
			name.AddThemeFontSizeOverride("font_size", 26);
			name.AddThemeColorOverride("font_color", fighters[i].Data.PlaceholderColor);
			root.AddChild(name);

			// Showing the device per player is not decoration - with hot-plugging, "which
			// controller am I" is a real question and the answer can change mid-match.
			var device = new Label
			{
				Text = "",
				Position = new Vector2(x, 814.0f),
			};
			device.AddThemeFontSizeOverride("font_size", 19);
			device.AddThemeColorOverride("font_color", new Color(0.38f, 0.40f, 0.46f));
			root.AddChild(device);
			deviceLabels.Add(device);

			var percent = new Label
			{
				Text = "0%",
				Position = new Vector2(x, 836.0f),
			};
			percent.AddThemeFontSizeOverride("font_size", 76);
			root.AddChild(percent);
			percentLabels.Add(percent);

			var stocks = new Label
			{
				Text = "",
				Position = new Vector2(x, 934.0f),
			};
			stocks.AddThemeFontSizeOverride("font_size", 30);
			stocks.AddThemeColorOverride("font_color", new Color(0.16f, 0.16f, 0.20f));
			root.AddChild(stocks);
			stockLabels.Add(stocks);
		}

		statusLabel = new Label
		{
			Text = "",
			Position = new Vector2(120.0f, 60.0f),
		};
		statusLabel.AddThemeFontSizeOverride("font_size", 48);
		statusLabel.AddThemeColorOverride("font_color", new Color(0.16f, 0.16f, 0.20f));
		root.AddChild(statusLabel);

		stageLabel = new Label
		{
			Text = "",
			Position = new Vector2(120.0f, 116.0f),
		};
		stageLabel.AddThemeFontSizeOverride("font_size", 22);
		stageLabel.AddThemeColorOverride("font_color", new Color(0.44f, 0.46f, 0.52f));
		root.AddChild(stageLabel);

		helpLabel = new Label
		{
			Text = "",
			Position = new Vector2(120.0f, 972.0f),
		};
		helpLabel.AddThemeFontSizeOverride("font_size", 18);
		helpLabel.AddThemeColorOverride("font_color", new Color(0.44f, 0.46f, 0.52f));
		root.AddChild(helpLabel);
	}

	public void SetStageName(string name)
	{
		if (stageLabel != null) stageLabel.Text = name;
	}

	public void SetControllerLabels(List<string> labels, string padLayoutHelp)
	{
		for (int i = 0; i < labels.Count && i < deviceLabels.Count; i++)
		{
			deviceLabels[i].Text = labels[i];
		}

		if (helpLabel == null) return;

		// Controller only. Everyone plays on a pad, so keyboard keys and the F-key debug
		// toggles are not on screen - they still work, and README.md lists them.
		helpLabel.Text =
			"L-stick move, harder = faster   " + padLayoutHelp + "   Start restart\n"
			+ "attack while running = dash attack";
	}

	public void Refresh(List<Fighter> fighters, string status)
	{
		for (int i = 0; i < fighters.Count && i < percentLabels.Count; i++)
		{
			Fighter f = fighters[i];
			percentLabels[i].Text = $"{Mathf.FloorToInt(f.Percent)}%";

			// Percent colour ramps toward red as a fighter approaches kill range, which is the
			// fastest way to read "this one is about to die" from across the couch.
			float danger = Mathf.Clamp(f.Percent / 150.0f, 0.0f, 1.0f);
			percentLabels[i].AddThemeColorOverride("font_color",
				new Color(0.16f + danger * 0.68f, 0.16f, 0.20f));

			stockLabels[i].Text = f.Stocks > 0 ? new string('*', f.Stocks) : "OUT";
		}

		if (statusLabel != null) statusLabel.Text = status;
	}
}
