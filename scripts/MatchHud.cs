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
	Label helpLabel;

	public void Build(List<Fighter> fighters)
	{
		var root = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);

		for (int i = 0; i < fighters.Count; i++)
		{
			float x = 120.0f + i * 420.0f;

			var name = new Label
			{
				Text = fighters[i].Data.DisplayName,
				Position = new Vector2(x, 838.0f),
			};
			name.AddThemeFontSizeOverride("font_size", 26);
			name.AddThemeColorOverride("font_color", fighters[i].Data.PlaceholderColor);
			root.AddChild(name);

			// Showing the device per player is not decoration - with hot-plugging, "which
			// controller am I" is a real question and the answer can change mid-match.
			var device = new Label
			{
				Text = "",
				Position = new Vector2(x, 868.0f),
			};
			device.AddThemeFontSizeOverride("font_size", 19);
			device.AddThemeColorOverride("font_color", new Color(0.38f, 0.40f, 0.46f));
			root.AddChild(device);
			deviceLabels.Add(device);

			var percent = new Label
			{
				Text = "0%",
				Position = new Vector2(x, 890.0f),
			};
			percent.AddThemeFontSizeOverride("font_size", 76);
			root.AddChild(percent);
			percentLabels.Add(percent);

			var stocks = new Label
			{
				Text = "",
				Position = new Vector2(x, 985.0f),
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

		helpLabel = new Label
		{
			Text = "",
			Position = new Vector2(120.0f, 992.0f),
		};
		helpLabel.AddThemeFontSizeOverride("font_size", 19);
		helpLabel.AddThemeColorOverride("font_color", new Color(0.44f, 0.46f, 0.52f));
		root.AddChild(helpLabel);
	}

	public void SetControllerLabels(List<string> labels, string padLayoutHelp)
	{
		for (int i = 0; i < labels.Count && i < deviceLabels.Count; i++)
		{
			deviceLabels[i].Text = labels[i];
		}

		if (helpLabel == null) return;

		helpLabel.Text =
			"pad   L-stick move   " + padLayoutHelp + "   Start restart\n"
			+ "keys  P1 WASD / G jump H attack J special F block      "
			+ "P2 arrows / Num1 jump Num2 attack Num3 special Num0 block\n"
			+ "F1 hitboxes   F2 slow-motion   F3 swap pad layout   R restart   Esc quit";
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
