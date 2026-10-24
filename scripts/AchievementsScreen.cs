using System.Collections.Generic;
using Godot;

/// <summary>
/// Every achievement, from the main menu (Eric, 2026-10-23): what to do for each, a gold star on
/// the ones done, how far along the counted ones are, and a gold border on the hard ones. What
/// they give is not said - except that a fighter, or Mk. II, still to be earned shows as a
/// silhouette on the tile that earns him, and in full colour once it is done. B goes back.
/// </summary>
public partial class AchievementsScreen : Node2D
{
	readonly List<MenuNav> navs = new List<MenuNav>();

	const int Columns = 3;
	const float Left = 150.0f;
	const float Top = 190.0f;
	const float TileWidth = 560.0f;
	const float TileHeight = 80.0f;
	const float Gap = 9.0f;

	static readonly Color Gold = new Color(1.0f, 0.82f, 0.25f);

	Rect2 TileRect(int i) => new Rect2(Left + i % Columns * (TileWidth + 15.0f), Top + i / Columns * (TileHeight + Gap), TileWidth, TileHeight);

	public override void _Ready()
	{
		IInputSource first = ControllerAssignment.ForPlayers(1)[0];
		navs.Add(new MenuNav(first));
		if (first is not KeyboardInputSource) navs.Add(new MenuNav(KeyboardInputSource.Player1()));

		// The fighters a tile earns, standing small at its right end: a silhouette until earned.
		for (int i = 0; i < Achievements.All.Length; i++)
		{
			Achievement a = Achievements.All[i];
			FighterData earns = Earns(a);
			if (earns == null) continue;
			Rect2 tile = TileRect(i);
			var holder = new Node2D { Position = new Vector2(tile.End.X - 44.0f, tile.Position.Y + 44.0f) };
			AddChild(holder);
			var rig = new FighterRig();
			holder.AddChild(rig);
			if (!rig.Load(earns)) continue;
			rig.Normalise(48.0f, 24.0f, earns.VisualScale);
			rig.SetPlanted(true);
			rig.SetStanding(true);
			rig.CarryOnShoulder = earns.ShouldersProp;
			rig.BackArmForward = earns.IdleBackArmForward;
			rig.Play(FighterAnimations.Idle);
			rig.Advance();
			if (!Achievements.Earned(a.Id)) holder.Modulate = Colors.Black;
		}
	}

	/// <summary>The fighter a tile shows: the one it unlocks, or the one its skin turns him into.</summary>
	static FighterData Earns(Achievement a)
	{
		if (a.Fighter != "")
		{
			int index = StoryScript.IndexOf(a.Fighter);
			return index >= 0 ? FighterCatalog.Get(index) : null;
		}
		Skin skin = Skins.Find(a.Skin);
		return skin != null && skin.Becomes >= 0 ? FighterCatalog.Get(skin.Becomes) : null;
	}

	public override void _PhysicsProcess(double delta)
	{
		foreach (MenuNav nav in navs)
		{
			nav.Poll();
			if (!nav.Cancel && !nav.Start && !nav.Confirm) continue;
			SfxPlayer.Ui("ui_back");
			GameRoot.Instance.GoMainMenu();
			return;
		}
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(Left, 130.0f), "Achievements", HorizontalAlignment.Left, -1, 76, MenuTheme.Text);
		DrawString(font, new Vector2(Left + 600.0f, 130.0f), $"{Achievements.EarnedCount} of {Achievements.All.Length} done", HorizontalAlignment.Left, -1, 34, MenuTheme.Soft);
		DrawString(font, new Vector2(0.0f, 130.0f), "B: Back", HorizontalAlignment.Right, size.X - 60.0f, 30, MenuTheme.Soft);

		for (int i = 0; i < Achievements.All.Length; i++)
		{
			Achievement a = Achievements.All[i];
			Rect2 tile = TileRect(i);
			bool done = Achievements.Earned(a.Id);
			DrawRect(tile, new Color(1.0f, 1.0f, 1.0f, done ? 0.85f : 0.5f));
			CrayonBrush.InkRect(this, tile, a.Hard ? Gold.Darkened(0.15f) : MenuTheme.Rule, a.Hard ? 5.0f : 3.0f, 300 + i, 2.0f);
			Vector2 star = tile.Position + new Vector2(38.0f, TileHeight * 0.5f);
			if (done) Item.DrawStarOn(this, star, 26.0f, Gold);
			else Item.DrawStarOn(this, star, 26.0f, new Color(MenuTheme.Rule, 0.7f));

			float textRoom = TileWidth - 80.0f - (Earns(a) != null ? 70.0f : 0.0f);
			DrawString(font, tile.Position + new Vector2(72.0f, 32.0f), a.Title, HorizontalAlignment.Left, textRoom, 26, done ? MenuTheme.Text : MenuTheme.Soft);
			string under = a.Task;
			string progress = Achievements.Progress(a);
			if (progress != "") under += $"  ({progress})";
			Skin skin = Skins.Find(a.Skin);
			if (done && skin != null && skin.Becomes < 0) under = $"{a.Task}  Unlocked: {skin.Name} skin.";
			DrawMultilineString(font, tile.Position + new Vector2(72.0f, 54.0f), under, HorizontalAlignment.Left, textRoom, 17, 2, MenuTheme.Soft);
		}
	}
}
