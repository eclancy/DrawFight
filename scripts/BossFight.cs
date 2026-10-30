using System.Collections.Generic;
using Godot;

/// <summary>
/// Boss fight (Eric's call, 2026-10-29; .ai/modes.md): everyone who sat down on fighter select,
/// people and CPUs together on one side, against a giant DoomBot played by the computer. He has a
/// health bar across the top of the screen instead of a percent, is never launched - small hits
/// clang off him, big ones stagger him - and is beaten when it runs out. They lose when every one
/// of them is out of lives.
///
/// The match is an ordinary MatchManager with the giant added as one more fighter on a team of
/// his own; this node is the rules on top - his health, his falls, his last stand, and the banner.
/// </summary>
public partial class BossFight : Node
{
	// --- The numbers ---------------------------------------------------------------------
	// First guesses, chosen by watching CPU fights rather than by playing them: see .ai/modes.md.

	/// <summary>How many times DoomBot's own size the giant is - his drawing, body and hits alike.</summary>
	public const float Scale = 2.0f;

	/// <summary>
	/// His health: a fight with one player, and as much again for each one more - four take on a
	/// giant with two and a half times the health of one. More players still finish him sooner,
	/// which is what having friends with you should feel like.
	/// </summary>
	const float HealthBase = 200.0f;
	const float HealthPerPlayer = 150.0f;

	/// <summary>
	/// A hit doing at least this much staggers him - most smashes, the strong specials, a charged
	/// anything. Jabs, tilts and aerials clang off his plating (their damage still counts).
	/// </summary>
	public const float StaggerDamage = 13.0f;

	/// <summary>How long a stagger holds him, and how long his armour then holds against everything.</summary>
	public const int StaggerFrames = 30;
	public const int StaggerCooldownFrames = 150;

	/// <summary>How hard a stagger pushes him back, and how much floor he must have behind him for it.</summary>
	public const float StaggerPush = 520.0f;
	public const float StaggerSlide = 140.0f;

	/// <summary>How big a stagger feels - the shake and the rumble - in knockback.</summary>
	public const float StaggerFeel = 110.0f;

	/// <summary>
	/// How hard his hits hit, against DoomBot's own: the same. Not the magnifying glass's extra power
	/// with size, which at twice the size is two thirds harder again - his reach, and everything he
	/// sends out grown with him, are the threat already. Watching CPUs, 1.15 beat four of them.
	/// </summary>
	public const float Power = 1.0f;

	/// <summary>Falling off costs him this much of his health; he comes back from above, as anyone does.</summary>
	const float FallCost = 0.08f;

	/// <summary>
	/// He has no lives to lose but this many, so a fall respawns him the usual way; his health bar
	/// is what decides it.
	/// </summary>
	public const int Lives = 99;

	/// <summary>On his last quarter of health he makes his last stand, as the story's boss does on his last life.</summary>
	public const float LastStandAt = 0.25f;

	/// <summary>
	/// How well the computer plays him (CpuInputSource.Level): one below Hard (8), which still gets
	/// his combos and grabs, because everything he does is twice the size.
	/// </summary>
	const int CpuLevel = 7;

	/// <summary>How often, in his last stand, he sends out a MiniBot - as in the story.</summary>
	const int MiniBotEvery = 220;

	/// <summary>
	/// The one stage a boss fight is on: Playground, the big stage with one unbroken field. Building
	/// Site's pit would drop a giant in again and again, and the kitchen's toaster and cookbooks are
	/// walls to someone his size; the playground's frames are what everyone else climbs to get above him.
	/// </summary>
	const string StageName = "Playground";

	/// <summary>Where everyone starts: the players spread along the left, the giant on the right.</summary>
	static readonly Vector2[] PlayerSpots =
	{
		new Vector2(-1460.0f, -420.0f), new Vector2(-1000.0f, -200.0f),
		new Vector2(-520.0f, -520.0f), new Vector2(-120.0f, -200.0f),
	};
	static readonly Vector2 BossSpot = new Vector2(1050.0f, -300.0f);

	// --- Setting it up ---------------------------------------------------------------------

	/// <summary>
	/// Turns the versus match fighter select built into a boss fight: DoomBot added after everyone
	/// who sat down, on the other side from all of them, on the fixed stage, with no items - an eraser
	/// that rubs out a life and a magnifying glass on a giant are not things a health bar can follow.
	/// </summary>
	public static void Prepare(MatchManager match)
	{
		int players = match.FighterIndices.Length;
		match.FighterIndices = Append(match.FighterIndices, StoryScript.IndexOf("doombot"));
		match.CpuPlayers = Append(match.CpuPlayers, true);
		match.Seats = Append(match.Seats ?? Count(players), players);
		match.Names = Append(match.Names ?? new string[players], null);
		match.CpuLevels = Append(match.CpuLevels ?? new int[players], CpuLevel);
		match.SeatSkins = Append(match.SeatSkins ?? new string[players], "");
		var teams = new int[players + 1];
		teams[players] = 1;
		match.Teams = teams;
		match.TeamBattle = false;
		match.BossPlayer = players;
		match.BossMode = true;
		match.Items = null;
		match.OnlyItem = "";
		for (int i = 0; i < StageCatalog.Count; i++)
		{
			if (StageCatalog.NameOf(i) == StageName) match.StageIndex = i;
		}
	}

	static T[] Append<T>(T[] array, T value)
	{
		var list = new List<T>(array) { value };
		return list.ToArray();
	}

	static int[] Count(int n)
	{
		var seats = new int[n];
		for (int i = 0; i < n; i++) seats[i] = i;
		return seats;
	}

	MatchManager match;
	Fighter boss;
	BossHealthBar bar;
	StoryOverlay overlay;
	StoryOverlay.Line line;

	/// <summary>Everyone fighting him, in order - the HUD's columns.</summary>
	public List<Fighter> Players { get; private set; }

	public Fighter Giant => boss;
	public float Health { get; private set; }
	public float MaxHealth { get; private set; }

	/// <summary>His health gone: the blast, and out.</summary>
	public bool Defeated { get; private set; }

	public bool LastStand { get; private set; }

	int lastStocks;
	int lastStandFrames;

	public void Setup(MatchManager owner)
	{
		match = owner;
		boss = match.Fighters[match.BossPlayer];
		boss.MakeBoss(this);
		lastStocks = boss.Stocks;
		Players = match.Fighters.FindAll(f => f != boss);
		MaxHealth = HealthBase + HealthPerPlayer * (Players.Count - 1);
		Health = MaxHealth;

		for (int i = 0; i < Players.Count; i++)
		{
			Players[i].GlobalPosition = PlayerSpots[i % PlayerSpots.Length];
			Players[i].Face(1);
		}
		boss.GlobalPosition = BossSpot;
		boss.Face(-1);

		bar = new BossHealthBar { Fight = this };
		AddChild(bar);
		overlay = new StoryOverlay { Match = match };
		AddChild(overlay);
	}

	/// <summary>Damage on him, from anything (Fighter.Percent): off the bar.</summary>
	public void Hurt(float amount)
	{
		if (Defeated || amount <= 0.0f) return;
		Health = Mathf.Max(0.0f, Health - amount);
		bar?.Hit(amount);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (boss == null || Defeated) return;
		boss.TickBoss();
		if (line != null && line.Finished)
		{
			overlay.Close(line);
			line = null;
		}

		// Fallen off: back he comes the usual way, and it costs him.
		if (boss.Stocks < lastStocks)
		{
			Hurt(MaxHealth * FallCost);
			if (LastStand) boss.SetHeat(Heat.Max);
		}
		lastStocks = boss.Stocks;

		if (Health <= 0.0f)
		{
			Beaten();
			return;
		}
		TickLastStand();
	}

	/// <summary>
	/// His health gone: a blast where he stands and he is out, which ends the fight the usual way -
	/// the slow motion, everyone left celebrating.
	/// </summary>
	void Beaten()
	{
		Defeated = true;
		boss.Raging = false;
		if (line != null) overlay.Close(line);
		Vector2 at = boss.BodyRect().GetCenter();
		boss.Stocks = 1;
		match.OnExplosion(at);
		match.ErasedOut(boss);
	}

	/// <summary>
	/// On his last quarter he goes critical, as the story's boss does on his last life: he says so,
	/// flashes red, runs at full heat, sends a MiniBot after someone every few seconds, and the
	/// music speeds up.
	/// </summary>
	void TickLastStand()
	{
		if (!LastStand)
		{
			if (Health > MaxHealth * LastStandAt || !boss.IsInPlay || match.InputLocked) return;
			LastStand = true;
			boss.Raging = true;
			boss.SetHeat(Heat.Max);
			line = overlay.Say(boss, "slam", "CORE TEMPERATURE CRITICAL.");
			SfxPlayer.Ui("steam");
			MusicPlayer.SetSpeed(1.25f);
			return;
		}
		if (!boss.IsInPlay || ++lastStandFrames % MiniBotEvery != 0) return;

		// A MiniBot set down at his feet, marching at whoever is nearest - grown, as everything he sends out is.
		MoveData bot = boss.Data.Move(MoveSlot.DownTilt);
		if (bot == null || bot.Special != SpecialKind.Walker) return;
		Fighter target = null;
		float best = float.MaxValue;
		foreach (Fighter f in Players)
		{
			if (!f.IsInPlay) continue;
			float d = Mathf.Abs(f.GlobalPosition.X - boss.GlobalPosition.X);
			if (d < best) { best = d; target = f; }
		}
		if (target == null) return;
		int way = target.GlobalPosition.X >= boss.GlobalPosition.X ? 1 : -1;
		Vector2 at = boss.Feet + new Vector2(way * boss.BodyRect().Size.X * 0.6f, -bot.FxArtSize * boss.ReachScale * 0.5f);
		match.SpawnHazard(boss, bot, at, new Vector2(way * bot.SpecialSpeed, 0.0f));
		SfxPlayer.At("minibot_pop", at, 0.05f);
	}

	/// <summary>Everyone fighting him is out of lives.</summary>
	public bool PlayersOut => Players != null && Players.TrueForAll(f => f.Stocks <= 0);

	/// <summary>The banner once it is over: DoomBot's win in his own letters, or theirs.</summary>
	public void ShowResult(MatchHud hud, Fighter victor)
	{
		const string Next = "Press Start to pick fighters";
		if (victor == boss) hud.SetWinner(boss.Data, Next);
		else if (victor != null) hud.SetBanner($"You beat {boss.Data.DisplayName}!", Next);
		else hud.SetBanner("Draw!", Next);
	}
}
