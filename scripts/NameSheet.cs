using Godot;

/// <summary>
/// Every fighter's name in their own letters, laid out to be checked (--names): big, at the
/// sizes the game shows them - a select-screen card (40), the match HUD (34), a speech bubble
/// (30) - and at HUD size over four stage-like grounds, since a name has to read on whatever
/// stage it lands on. DoomBot's are shown cold to red-hot. A tool, like the parade.
/// </summary>
public partial class NameSheet : Node2D
{
	static readonly int[] Fighters = { 0, 1, 2, 3, 4, 5, FighterCatalog.StoryOnlyFrom };

	/// <summary>Grounds a HUD name lands on: paper, grass, brickwork, a lavender night sky.</summary>
	static readonly Color[] Grounds =
	{
		new Color(0.965f, 0.960f, 0.940f),
		new Color(0.56f, 0.76f, 0.42f),
		new Color(0.80f, 0.50f, 0.40f),
		new Color(0.76f, 0.72f, 0.90f),
	};

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(190.0f, 62.0f), "Names: big · card 40 · bubble 30 · HUD 34 on paper, grass, brick, night (DoomBot cold to hot)",
			HorizontalAlignment.Left, -1, 26, MenuTheme.Soft);

		const float Top = 90.0f;
		float row = (size.Y - Top - 20.0f) / Fighters.Length;
		for (int r = 0; r < Fighters.Length; r++)
		{
			FighterData data = FighterCatalog.Get(Fighters[r]);
			NameStyle look = data.NameLook;
			float y = Top + r * row;

			NameLettering.DrawIn(this, new Rect2(180.0f, y, 600.0f, row), data.DisplayName, look, 80);
			NameLettering.DrawIn(this, new Rect2(800.0f, y + 6.0f, 280.0f, row * 0.5f), data.DisplayName, look, 40, HorizontalAlignment.Center);
			NameLettering.DrawIn(this, new Rect2(800.0f, y + row * 0.5f, 280.0f, row * 0.5f - 6.0f), data.DisplayName, look, 30, HorizontalAlignment.Center);

			for (int g = 0; g < Grounds.Length; g++)
			{
				var swatch = new Rect2(1100.0f + g * 200.0f, y + 8.0f, 192.0f, row - 16.0f);
				DrawRect(swatch, Grounds[g]);
				float heat = data.HasHeat ? g / (Grounds.Length - 1.0f) : 0.0f;
				NameLettering.DrawIn(this, swatch, data.DisplayName, look, 34, HorizontalAlignment.Center, 1.0f, heat);
			}
		}
	}
}
