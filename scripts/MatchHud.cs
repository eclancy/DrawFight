using System.Collections.Generic;
using Godot;

/// <summary>
/// The match heads-up display: each player's name, damage percent and stocks along the bottom,
/// and a big banner in the middle for the 3-2-1 countdown and the winner. Nothing else - no
/// boxes, no control reminders.
///
/// There are no paper bands behind the readouts any more. Legibility comes from the text itself:
/// every label carries a thick paper-coloured outline, so it reads on cream, grass or brickwork
/// alike. That is the lesson the bands were learned from (white-on-cream shipped invisible
/// twice), solved without covering the stage.
/// </summary>
public partial class MatchHud : CanvasLayer
{
	readonly List<Label> percentLabels = new List<Label>();
	readonly List<Label> stockLabels = new List<Label>();

	/// <summary>A heat gauge per player, or null for a fighter without heat.</summary>
	readonly List<Control> heatGauges = new List<Control>();

	Label bannerLabel;
	Label subBannerLabel;
	Label countdownLabel;
	Label debugLabel;

	static readonly Color Paper = new Color(0.98f, 0.97f, 0.94f);
	static readonly Color Ink = new Color(0.14f, 0.14f, 0.18f);

	static Label MakeLabel(Control root, int size, Color colour, int outline)
	{
		var label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		label.AddThemeColorOverride("font_outline_color", Paper);
		label.AddThemeConstantOverride("outline_size", outline);
		root.AddChild(label);
		return label;
	}

	public void Build(List<Fighter> fighters)
	{
		var root = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);

		const float Width = 1920.0f;
		const float Column = 420.0f;

		for (int i = 0; i < fighters.Count; i++)
		{
			// Players spread evenly across the bottom, each in their own column.
			float centreX = Width * (i + 1) / (fighters.Count + 1);
			float left = centreX - Column * 0.5f;

			Label name = MakeLabel(root, 34, fighters[i].Data.PlaceholderColor, 10);
			name.Text = fighters[i].Data.DisplayName;
			name.Position = new Vector2(left, 842.0f);
			name.Size = new Vector2(Column, 44.0f);

			Label percent = MakeLabel(root, 112, Ink, 18);
			percent.Text = "0%";
			percent.Position = new Vector2(left, 872.0f);
			percent.Size = new Vector2(Column, 130.0f);
			percentLabels.Add(percent);

			Label stocks = MakeLabel(root, 34, Ink, 10);
			stocks.Position = new Vector2(left, 1000.0f);
			stocks.Size = new Vector2(Column, 44.0f);
			stockLabels.Add(stocks);

			heatGauges.Add(fighters[i].Data.HasHeat ? MakeHeatGauge(root, fighters[i], centreX) : null);
		}

		bannerLabel = MakeLabel(root, 190, Ink, 26);
		bannerLabel.Position = new Vector2(0.0f, 300.0f);
		bannerLabel.Size = new Vector2(Width, 240.0f);
		bannerLabel.PivotOffset = new Vector2(Width * 0.5f, 120.0f);

		// The countdown is its own label: huge, crayon-coloured, with an ink outline and a paper
		// halo round that, so a bright yellow "1" still reads on a cream page or on grass.
		countdownLabel = MakeLabel(root, 330, Ink, 30);
		countdownLabel.AddThemeColorOverride("font_outline_color", Ink);
		countdownLabel.AddThemeColorOverride("font_shadow_color", Paper);
		countdownLabel.AddThemeConstantOverride("shadow_outline_size", 64);
		countdownLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		countdownLabel.AddThemeConstantOverride("shadow_offset_y", 0);
		countdownLabel.Position = new Vector2(0.0f, 180.0f);
		countdownLabel.Size = new Vector2(Width, 420.0f);
		countdownLabel.PivotOffset = new Vector2(Width * 0.5f, 210.0f);

		subBannerLabel = MakeLabel(root, 40, Ink, 12);
		subBannerLabel.Position = new Vector2(0.0f, 540.0f);
		subBannerLabel.Size = new Vector2(Width, 60.0f);

		// The knockback readout, only while hitboxes are shown with F1. A tuning aid, not HUD.
		debugLabel = MakeLabel(root, 26, Ink, 8);
		debugLabel.HorizontalAlignment = HorizontalAlignment.Left;
		debugLabel.Position = new Vector2(40.0f, 30.0f);
	}

	/// <summary>
	/// The big centre text: "3", "2", "1", "FIGHT!", or the winner. <paramref name="punch"/>
	/// runs 1 to 0 over a number's time on screen, so each one lands big and settles.
	/// </summary>
	public void SetBanner(string text, string subText = "", float punch = 0.0f)
	{
		if (bannerLabel == null) return;
		bannerLabel.Text = text;
		bannerLabel.Scale = Vector2.One * (1.0f + 0.35f * punch * punch);
		subBannerLabel.Text = subText;
	}

	/// <summary>
	/// One beat of the 3-2-1-FIGHT countdown, in its own colour. <paramref name="punch"/> runs 1
	/// to 0 over the beat: it slams in oversized and settles, and <paramref name="fade"/> lets
	/// FIGHT! melt away as control is handed over. Empty text hides it.
	/// </summary>
	public void SetCountdown(string text, Color colour, float punch, float tilt, float fade = 1.0f)
	{
		if (countdownLabel == null) return;
		countdownLabel.Text = text;
		countdownLabel.AddThemeColorOverride("font_color", colour);
		countdownLabel.Scale = Vector2.One * (1.0f + 0.6f * punch * punch * punch);
		countdownLabel.Rotation = tilt * (0.4f + 0.6f * punch);
		countdownLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, fade);
	}

	/// <summary>
	/// A thermometer beside the percent for a fighter with heat: paper-backed with an ink edge,
	/// like every HUD readout, filling from orange to red. It flashes at full heat and goes
	/// white while overheated, so "he is about to blow" reads even when nobody is looking at him.
	/// </summary>
	static Control MakeHeatGauge(Control root, Fighter fighter, float centreX)
	{
		var gauge = new Control
		{
			Position = new Vector2(centreX + 118.0f, 892.0f),
			Size = new Vector2(30.0f, 96.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		root.AddChild(gauge);
		gauge.Draw += () =>
		{
			var box = new Rect2(Vector2.Zero, gauge.Size);
			gauge.DrawRect(box.Grow(5.0f), Paper);
			float h = Mathf.Clamp(fighter.HeatFraction, 0.0f, 1.0f);
			Color fill = new Color(0.98f, 0.66f, 0.22f).Lerp(new Color(0.90f, 0.22f, 0.18f), h);
			if (fighter.IsOverheated) fill = new Color(0.96f, 0.96f, 0.98f);
			else if (h >= 1.0f && (Time.GetTicksMsec() / 120) % 2 == 0) fill = new Color(1.0f, 0.88f, 0.45f);
			float height = fighter.IsOverheated ? box.Size.Y : box.Size.Y * h;
			gauge.DrawRect(new Rect2(0.0f, box.Size.Y - height, box.Size.X, height), fill);
			gauge.DrawRect(box, Ink, false, 3.0f);
		};
		return gauge;
	}

	public void Refresh(List<Fighter> fighters, string debug)
	{
		for (int i = 0; i < fighters.Count && i < percentLabels.Count; i++)
		{
			Fighter f = fighters[i];
			percentLabels[i].Text = $"{Mathf.FloorToInt(f.Percent)}%";

			// Percent colour ramps toward red as a fighter approaches kill range, which is the
			// fastest way to read "this one is about to die" from across the couch.
			float danger = Mathf.Clamp(f.Percent / 150.0f, 0.0f, 1.0f);
			percentLabels[i].AddThemeColorOverride("font_color",
				new Color(0.14f + danger * 0.72f, 0.14f, 0.18f));

			// One dot per stock left.
			stockLabels[i].Text = f.Stocks > 0
				? string.Join(" ", new string('●', f.Stocks).ToCharArray())
				: "OUT";

			heatGauges[i]?.QueueRedraw();
		}

		if (debugLabel != null) debugLabel.Text = debug;
	}
}
