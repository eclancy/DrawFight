using System.Collections.Generic;
using Godot;

/// <summary>
/// Smash's two moments for a fighter you have to earn (Unlocks; Eric, 2026-10-23).
///
/// NEW CHALLENGER: the first time a story puts you up against him, before the scene - the page
/// goes dark, the words slam down, and he comes in as a silhouette winding up his own forward
/// smash. Who he is waits for the scene.
///
/// UNLOCKED (<see cref="Unlocked"/>): once he is beaten, after that fight - the page, him in full
/// colour cheering, his name in his own letters, and that he can be picked now. Press Start and
/// the story goes on.
/// </summary>
public partial class ChallengerScreen : Node2D
{
	public FighterData Fighter;

	/// <summary>The unlocked screen, rather than NEW CHALLENGER.</summary>
	public bool Unlocked;

	/// <summary>Where the story goes on from, once this is done.</summary>
	public System.Action Then;

	readonly List<MenuNav> navs = new List<MenuNav>();
	readonly Pose pose = new Pose();
	FighterRig rig;
	Node2D holder;
	MoveData swing;
	Label prompt;
	int frames;
	bool leaving;

	/// <summary>When the words come down, when he comes in, and when it can be moved on from.</summary>
	const int WordsAt = 20;
	const int FighterAt = 44;
	const int ReadyAt = 110;

	/// <summary>NEW CHALLENGER goes on by itself, as Smash's does; the unlock waits for Start.</summary>
	const int ChallengerFrames = 230;

	/// <summary>
	/// Night lavender, the darkest a page goes (art-direction: characters own black, so the dark
	/// behind him stays well above it - and his silhouette is the only black on the screen).
	/// </summary>
	static readonly Color Dusk = new Color(0.36f, 0.31f, 0.52f);
	static readonly Color DuskLight = new Color(0.48f, 0.42f, 0.66f);
	static readonly Color Shout = new Color(0.98f, 0.55f, 0.26f);
	static readonly Color Silhouette = Colors.Black;
	static readonly Color Gold = new Color(1.0f, 0.82f, 0.25f);

	public override void _Ready()
	{
		IInputSource first = ControllerAssignment.ForPlayers(1)[0];
		navs.Add(new MenuNav(first));
		if (first is not KeyboardInputSource) navs.Add(new MenuNav(KeyboardInputSource.Player1()));

		Vector2 size = GetViewportRect().Size;
		holder = new Node2D { Position = Unlocked ? new Vector2(size.X * 0.30f, 690.0f) : new Vector2(size.X * 0.70f, 700.0f) };
		AddChild(holder);
		rig = new FighterRig();
		holder.AddChild(rig);
		if (Fighter != null && rig.Load(Fighter))
		{
			rig.Normalise(Unlocked ? 520.0f : 600.0f, Unlocked ? 260.0f : 300.0f, Fighter.VisualScale);
			rig.ArmBackLimit = FighterRig.ArmBackMost;
			rig.SetPlanted(true);
			rig.SetStanding(true);
			rig.Robotic = Fighter.Robotic;
			rig.HandOnHip = false;
			if (Unlocked)
			{
				rig.CarryOnShoulder = false;
				if (Fighter.ShouldersProp) rig.SetPartVisible(RigBone.PropFront, false);
				rig.SetFace(FaceState.Toned(FaceState.Named("happy"), Fighter.FaceStrength));
				if (Fighter.FrontOn)
				{
					rig.SetFrontOn(true);
					FighterAnimations.FaceOn("cheer", pose);
				}
				else
				{
					pose.CopyFrom(FighterAnimations.StoryPose("cheer"));
					FighterAnimations.SpreadArms("cheer", pose, rig.NearArmLeads);
				}
			}
			else
			{
				// His own forward smash, wound up - the swing he means to hit you with.
				rig.SetFacing(-1);
				swing = Fighter.Move(MoveSlot.ForwardSmash);
				rig.CarryOnShoulder = false;
				holder.Modulate = Silhouette;
			}
		}
		holder.Visible = false;

		var layer = new CanvasLayer();
		AddChild(layer);
		prompt = MenuTheme.MakeLabel(Unlocked ? "Press Start" : "", new Vector2(size.X * 0.55f, 860.0f), 54, MenuTheme.Ink);
		prompt.Visible = false;
		layer.AddChild(prompt);

		if (Unlocked) MusicPlayer.PlayOnce(MusicPlayer.Victory);
		else MusicPlayer.Stop();
	}

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		if (frames == WordsAt) SfxPlayer.Ui(Unlocked ? "ui_ready" : "ko_blast");
		if (frames == FighterAt)
		{
			holder.Visible = true;
			if (!Unlocked) SfxPlayer.Ui("smash_charge");
		}
		PoseFighter();

		prompt.Visible = Unlocked && frames >= ReadyAt;
		prompt.Modulate = new Color(1.0f, 1.0f, 1.0f, frames % 64 < 42 ? 1.0f : 0.25f);

		if (!leaving && !Unlocked && frames >= ChallengerFrames) Leave();
		foreach (MenuNav nav in navs)
		{
			nav.Poll();
			if (leaving || !nav.AnyPress) continue;
			// Hurried along, never skipped before it has been seen.
			if (frames < ReadyAt) frames = Mathf.Max(frames, FighterAt + 20);
			else Leave();
		}
		QueueRedraw();
	}

	void Leave()
	{
		leaving = true;
		if (Unlocked) SfxPlayer.Ui("ui_select");
		Then?.Invoke();
	}

	/// <summary>The challenger comes in from the side mid-swing; the unlocked one cheers, bouncing.</summary>
	void PoseFighter()
	{
		if (rig == null || !rig.Loaded || !holder.Visible) return;
		int t = frames - FighterAt;
		if (Unlocked)
		{
			pose.HipOffset = new Vector2(0.0f, -6.0f * Mathf.Abs(Mathf.Sin(frames * 0.1f)));
			rig.ApplyDirect(pose, 0.75f);
			return;
		}
		Vector2 size = GetViewportRect().Size;
		float into = Mathf.Min(1.0f, t / 10.0f);
		holder.Position = new Vector2(size.X * 0.70f + (1.0f - into * (2.0f - into)) * 700.0f, 700.0f);
		if (swing == null) return;
		// Winding up as he comes in, and held there, about to swing.
		float at = Mathf.Min(swing.StartupFrames, t * 0.6f);
		FighterAnimations.SampleAttack(swing, at, pose, lunging: true);
		rig.ApplyDirect(pose, FighterAnimations.AttackBlend(swing, at));
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		if (Unlocked) DrawUnlocked(size);
		else DrawChallenger(size);
	}

	void DrawChallenger(Vector2 size)
	{
		DrawRect(new Rect2(Vector2.Zero, size), Dusk);
		// Streaks rushing past, toward where he comes in.
		for (int i = 0; i < 26; i++)
		{
			float y = 60.0f + i * 40.0f + CrayonBrush.Noise(i, 3) * 14.0f;
			float run = (frames * (18.0f + (i % 5) * 6.0f) + i * 211.0f) % (size.X + 900.0f) - 450.0f;
			float x = size.X - run;
			DrawLine(new Vector2(x, y), new Vector2(x + 260.0f + (i % 3) * 90.0f, y), new Color(DuskLight, 0.55f), 6.0f, true);
		}
		// A light behind him, so his shape reads against the dark.
		if (holder.Visible)
		{
			Vector2 at = holder.Position + new Vector2(0.0f, -250.0f);
			for (int r = 0; r < 6; r++) DrawCircle(at, 380.0f - r * 50.0f, new Color(1.0f, 0.86f, 0.62f, 0.07f + r * 0.03f));
		}
		if (frames < WordsAt) return;

		// NEW CHALLENGER!, slammed down big and settling, a little crooked.
		float t = Mathf.Min(1.0f, (frames - WordsAt) / 8.0f);
		float scale = Mathf.Lerp(2.2f, 1.0f, t * t);
		Vector2 jolt = frames - WordsAt < 10 ? new Vector2(CrayonBrush.Noise(frames, 5), CrayonBrush.Noise(frames, 6)) * 12.0f : Vector2.Zero;
		Font font = ThemeDB.FallbackFont;
		const string Words = "NEW CHALLENGER!";
		const int FontSize = 120;
		float width = font.GetStringSize(Words, HorizontalAlignment.Left, -1, FontSize).X;
		DrawSetTransform(new Vector2(size.X * 0.36f, 250.0f) + jolt, Mathf.DegToRad(-4.0f), Vector2.One * scale);
		DrawStringOutline(font, new Vector2(-width * 0.5f, 40.0f), Words, HorizontalAlignment.Left, -1, FontSize, 26, MenuTheme.Text);
		DrawString(font, new Vector2(-width * 0.5f, 40.0f), Words, HorizontalAlignment.Left, -1, FontSize, Shout);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
		if (frames >= FighterAt + 20)
		{
			DrawString(font, new Vector2(140.0f, 420.0f), "Someone new wants a fight...", HorizontalAlignment.Left, -1, 44, new Color(1.0f, 0.95f, 0.88f));
		}
	}

	void DrawUnlocked(Vector2 size)
	{
		MenuTheme.DrawPage(this, size);
		// Rays of gold behind him, turning slowly.
		Vector2 centre = holder.Position + new Vector2(0.0f, -230.0f);
		float grow = Mathf.Min(1.0f, frames / 30.0f);
		for (int i = 0; i < 14; i++)
		{
			float a = i * Mathf.Tau / 14.0f + frames * 0.004f;
			Vector2 tip1 = centre + Vector2.Right.Rotated(a - 0.09f) * 520.0f * grow;
			Vector2 tip2 = centre + Vector2.Right.Rotated(a + 0.09f) * 520.0f * grow;
			DrawColoredPolygon(new[] { centre, tip1, tip2 }, new Color(Gold, 0.22f));
		}
		if (frames < WordsAt || Fighter == null) return;

		Font font = ThemeDB.FallbackFont;
		// His name in his own letters, popping in a size too big and settling.
		float t = Mathf.Min(1.0f, (frames - WordsAt) / 10.0f);
		int nameSize = Mathf.RoundToInt(110.0f * (1.0f + 0.25f * Mathf.Sin(t * Mathf.Pi)));
		var left = new Vector2(size.X * 0.55f, 0.0f);
		NameLettering.Draw(this, left + new Vector2(0.0f, 330.0f), Fighter.DisplayName, Fighter.NameLook, nameSize);
		DrawString(font, left + new Vector2(0.0f, 450.0f), "is unlocked!", HorizontalAlignment.Left, -1, 72, MenuTheme.Text);
		if (frames >= FighterAt + 20)
		{
			DrawString(font, left + new Vector2(0.0f, 560.0f), "Pick them in Versus,", HorizontalAlignment.Left, -1, 40, MenuTheme.Soft);
			DrawString(font, left + new Vector2(0.0f, 615.0f), "and play their own story.", HorizontalAlignment.Left, -1, 40, MenuTheme.Soft);
			Item.DrawStarOn(this, left + new Vector2(-70.0f, 300.0f), 46.0f, Gold);
		}
	}
}
