using Godot;

/// <summary>
/// A fighter's name in their own letters, as a Control: what goes where a name Label went, in a
/// container or placed by hand. Sized like a Label of the same font size, so a layout built round
/// labels does not move. Redraws every frame only if its letters move.
/// </summary>
public partial class NameTag : Control
{
	string text = "";
	NameStyle style;

	public int FontSize = 40;
	public HorizontalAlignment Align = HorizontalAlignment.Left;

	/// <summary>
	/// Takes whatever width it is given and shrinks the name to fit it, rather than asking for the
	/// width the name wants - for a card or a column with a fixed width.
	/// </summary>
	public bool FitWidth;

	/// <summary>The fighter's heat, 0 to 1, for letters that glow with it - or null where it is not known.</summary>
	public System.Func<float> Heat;

	public NameTag()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>Shows this fighter's name in their letters, or nothing for null.</summary>
	public void SetFighter(FighterData data) => SetText(data?.DisplayName ?? "", data?.NameLook);

	public void SetText(string name, NameStyle look)
	{
		name ??= "";
		if (name == text && look == style) return;
		text = name;
		style = look;
		UpdateMinimumSize();
		QueueRedraw();
	}

	public override Vector2 _GetMinimumSize()
	{
		float height = ThemeDB.FallbackFont.GetHeight(FontSize);
		if (FitWidth || string.IsNullOrEmpty(text) || style == null) return new Vector2(0.0f, height);
		return new Vector2(NameLettering.Measure(text, style, FontSize).X, height);
	}

	public override void _Process(double delta)
	{
		if (style != null && (style.Moves || (style.Glows && Heat != null)) && IsVisibleInTree()) QueueRedraw();
	}

	public override void _Draw()
	{
		if (string.IsNullOrEmpty(text) || style == null) return;
		NameLettering.DrawIn(this, new Rect2(Vector2.Zero, Size), text, style, FontSize, Align, 1.0f, Heat?.Invoke() ?? 0.0f);
	}
}
