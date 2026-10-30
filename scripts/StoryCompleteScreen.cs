using System.Collections.Generic;
using Godot;

/// <summary>
/// A story finished: whoever's story it was cheering on the page, "STORY: COMPLETED" stamped down
/// beside them and its gold star popping on - then Press Start, and back to the main menu (Eric,
/// 2026-10-22). A press before the prompt only hurries it along, so a button mashed through the
/// ending does not skip the moment it was for.
/// </summary>
public partial class StoryCompleteScreen : Node2D
{
	public StoryRun Run;

	readonly List<MenuNav> navs = new List<MenuNav>();
	readonly Pose cheer = new Pose();
	FighterRig rig;
	FighterData data;
	Label pressStart;
	int frames;
	bool leaving;

	/// <summary>When the stamp comes down, the star pops on, and Press Start shows.</summary>
	const int StampAt = 40;
	const int StarAt = 80;
	const int PromptAt = 130;

	/// <summary>Frames the stamp takes to come down onto the page.</summary>
	const int StampFall = 9;

	static readonly Color StampInk = new Color(0.86f, 0.27f, 0.25f);
	static readonly Color Gold = new Color(1.0f, 0.82f, 0.25f);

	static readonly Vector2 StampCentre = new Vector2(1360.0f, 540.0f);
	static readonly Vector2 StarCentre = new Vector2(1360.0f, 770.0f);

	public override void _Ready()
	{
		// Every controller, so any of them goes on - and online, the session's own (NetSession).
		foreach (IInputSource source in ControllerAssignment.Everyone()) navs.Add(new MenuNav(source));

		int index = Run != null ? StoryScript.IndexOf(Run.PlayerId) : -1;
		data = index >= 0 ? FighterCatalog.Get(index) : null;
		if (data != null)
		{
			// Viewport coordinates: this screen has no camera. A holder carries the place, because
			// the rig positions itself inside its own parent.
			var holder = new Node2D { Position = new Vector2(560.0f, 640.0f) };
			AddChild(holder);
			rig = new FighterRig();
			holder.AddChild(rig);
			if (rig.Load(data))
			{
				rig.Normalise(480.0f, 240.0f, 1.0f);
				rig.ArmBackLimit = FighterRig.ArmBackMost;
				rig.SetPlanted(true);
				rig.SetStanding(true);
				rig.CarryOnShoulder = false;
				if (data.ShouldersProp) rig.SetPartVisible(RigBone.PropFront, false);
				rig.SetFace(FaceState.Toned(FaceState.Named("happy"), data.FaceStrength));
				if (data.FrontOn)
				{
					rig.SetFrontOn(true);
					FighterAnimations.FaceOn("cheer", cheer);
				}
				else
				{
					cheer.CopyFrom(FighterAnimations.StoryPose("cheer"));
					FighterAnimations.SpreadArms("cheer", cheer, rig.NearArmLeads);
				}
			}
		}

		var layer = new CanvasLayer();
		AddChild(layer);
		pressStart = MenuTheme.MakeLabel("Press Start", new Vector2(0.0f, 930.0f), 58, MenuTheme.Ink);
		pressStart.Size = new Vector2(GetViewportRect().Size.X, 80.0f);
		pressStart.HorizontalAlignment = HorizontalAlignment.Center;
		pressStart.Visible = false;
		layer.AddChild(pressStart);

		MusicPlayer.PlayOnce("victory");
	}

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		if (frames == StampAt) SfxPlayer.Ui("stomp");
		if (frames == StarAt) SfxPlayer.Ui("ui_ready");

		if (rig != null && rig.Loaded)
		{
			// Arms up, held as a scene holds a pose, with a little bounce.
			cheer.HipOffset = new Vector2(0.0f, -6.0f * Mathf.Abs(Mathf.Sin(frames * 0.1f)));
			rig.ApplyDirect(cheer, 0.75f);
		}

		pressStart.Visible = frames >= PromptAt;
		pressStart.Modulate = new Color(1.0f, 1.0f, 1.0f, frames % 64 < 42 ? 1.0f : 0.25f);

		foreach (MenuNav nav in navs)
		{
			nav.Poll();
			if (leaving || !nav.AnyPress) continue;
			if (frames < PromptAt)
			{
				// Hurried along, not skipped: the stamp and the star still land.
				frames = Mathf.Max(frames, StarAt);
				continue;
			}
			leaving = true;
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.GoMainMenu();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;

		// Whose story, as the map heads it: the name in his own letters, the title in ink.
		if (Run != null)
		{
			string name = data != null ? data.DisplayName : Run.PlayerId;
			NameStyle look = data != null ? data.NameLook : NameStyle.Plain(MenuTheme.Text);
			string rest = string.IsNullOrEmpty(Run.Title) ? "'s story" : $": {Run.Title}";
			// Across the top of the page, as big as fits: some titles are long.
			int fontSize = 72;
			float Width() => NameLettering.Measure(name, look, fontSize).X + font.GetStringSize(rest, HorizontalAlignment.Left, -1, fontSize).X;
			while (fontSize > 36 && Width() > size.X - 360.0f) fontSize -= 4;
			float x = (size.X + 150.0f - Width()) * 0.5f;
			NameLettering.Draw(this, new Vector2(x, 180.0f), name, look, fontSize);
			DrawString(font, new Vector2(x + NameLettering.Measure(name, look, fontSize).X, 180.0f), rest, HorizontalAlignment.Left, -1, fontSize, MenuTheme.Text);
		}

		DrawStamp();

		if (frames >= StarAt)
		{
			float t = Mathf.Min(1.0f, (frames - StarAt) / 12.0f);
			// Pops past full size and settles back.
			float pop = 1.0f + 0.35f * Mathf.Sin(t * Mathf.Pi);
			Item.DrawStarOn(this, StarCentre, 90.0f * t * pop, Gold);
			if (frames - StarAt < 30)
			{
				float spread = (frames - StarAt) * 6.0f;
				float fade = 1.0f - (frames - StarAt) / 30.0f;
				for (int i = 0; i < 8; i++)
				{
					Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / 8.0f + 0.2f);
					DrawCircle(StarCentre + dir * (110.0f + spread), 7.0f * fade, new Color(Gold, fade));
				}
			}
		}
	}

	/// <summary>
	/// The rubber stamp: big and in the air, then slammed flat onto the page, a little crooked,
	/// with a jolt.
	/// </summary>
	void DrawStamp()
	{
		if (frames < StampAt - StampFall) return;
		float t = Mathf.Clamp((frames - (StampAt - StampFall)) / (float)StampFall, 0.0f, 1.0f);
		float scale = Mathf.Lerp(2.6f, 1.0f, t * t);
		float alpha = Mathf.Lerp(0.2f, 1.0f, t);
		Vector2 jolt = frames >= StampAt && frames < StampAt + 8
			? new Vector2(CrayonBrush.Noise(frames, 3), CrayonBrush.Noise(frames, 4)) * 10.0f
			: Vector2.Zero;
		var box = new Rect2(-380.0f, -96.0f, 760.0f, 192.0f);
		DrawSetTransform(StampCentre + jolt, Mathf.DegToRad(-5.0f), Vector2.One * scale);
		DrawRect(box, new Color(1.0f, 1.0f, 1.0f, 0.5f * alpha));
		CrayonBrush.InkRect(this, box, new Color(StampInk, alpha), 9.0f, 41, 2.4f);
		CrayonBrush.InkRect(this, box.Grow(-16.0f), new Color(StampInk, alpha), 4.0f, 42, 2.0f);
		MenuTheme.DrawCentredText(this, box, "STORY: COMPLETED", 66, new Color(StampInk, alpha));
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}
}
