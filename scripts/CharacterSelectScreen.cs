using Godot;

/// <summary>
/// Each player picks a fighter, sees it standing there idling, and locks in.
///
/// The preview is a live rig playing the shared idle animation, not a portrait. It costs
/// nothing extra and it means the moment a real drawing is imported, the select screen shows
/// that drawing breathing - which is the whole payoff of the project appearing before the
/// match even starts.
/// </summary>
public partial class CharacterSelectScreen : Node2D
{
	sealed class Slot
	{
		public MenuNav Nav;
		public IInputSource Source;
		public int Index;
		public bool Locked;
		public Node2D Holder;
		public FighterRig Rig;
		public Label Name;
		public Label Blurb;
		public Label Specials;
		public Label State;
		public Color Tint;
	}

	readonly Slot[] slots = new Slot[2];
	Control root;

	public override void _Ready()
	{
		IInputSource[] sources = ControllerAssignment.ForPlayers(slots.Length);

		var layer = new CanvasLayer();
		AddChild(layer);
		root = new Control { AnchorsPreset = (int)Control.LayoutPreset.FullRect };
		layer.AddChild(root);

		root.AddChild(MenuTheme.MakeLabel("Pick your fighter", new Vector2(190.0f, 74.0f), 76, MenuTheme.Text));
		root.AddChild(MenuTheme.MakeLabel(
			"left and right to choose        A to lock in        B to go back",
			new Vector2(196.0f, 176.0f), 27, MenuTheme.Soft));

		for (int i = 0; i < slots.Length; i++)
		{
			float x = 300.0f + i * 700.0f;

			var slot = new Slot
			{
				Nav = new MenuNav(sources[i]),
				Source = sources[i],
				Index = i % FighterCatalog.Count,
				Holder = new Node2D { Position = new Vector2(x + 150.0f, 500.0f) },
				Tint = i == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent,
			};
			AddChild(slot.Holder);

			root.AddChild(MenuTheme.MakeLabel($"Player {i + 1}", new Vector2(x, 262.0f), 34, slot.Tint));
			root.AddChild(MenuTheme.MakeLabel(
				ControllerAssignment.Describe(sources[i]), new Vector2(x, 306.0f), 22, MenuTheme.Soft));

			slot.Name = MenuTheme.MakeLabel("", new Vector2(x, 740.0f), 52, MenuTheme.Text);
			slot.Blurb = MenuTheme.MakeLabel("", new Vector2(x, 806.0f), 24, MenuTheme.Soft);
			slot.Specials = MenuTheme.MakeLabel("", new Vector2(x, 844.0f), 21, MenuTheme.Ink);
			slot.State = MenuTheme.MakeLabel("", new Vector2(x, 878.0f), 28, slot.Tint);
			root.AddChild(slot.Name);
			root.AddChild(slot.Blurb);
			root.AddChild(slot.Specials);
			root.AddChild(slot.State);

			slots[i] = slot;
			RebuildPreview(slot);
		}
	}

	void RebuildPreview(Slot slot)
	{
		slot.Rig?.QueueFree();

		FighterData data = FighterCatalog.Get(slot.Index);
		var rig = new FighterRig();
		slot.Holder.AddChild(rig);

		if (rig.Load(data.RigPath))
		{
			rig.Normalise(330.0f, 165.0f, data.VisualScale);
			rig.SetFacing(1);
		}

		slot.Rig = rig;
		slot.Name.Text = data.DisplayName;
		slot.Blurb.Text = FighterCatalog.BlurbOf(slot.Index);
		slot.Specials.Text = FighterCatalog.SpecialsOf(slot.Index);
	}

	public override void _PhysicsProcess(double delta)
	{
		bool everyoneReady = true;

		foreach (Slot slot in slots)
		{
			slot.Nav.Poll();

			if (!slot.Locked)
			{
				if (slot.Nav.StepX != 0)
				{
					slot.Index = ((slot.Index + slot.Nav.StepX) % FighterCatalog.Count + FighterCatalog.Count)
						% FighterCatalog.Count;
					RebuildPreview(slot);
				}

				if (slot.Nav.Confirm) slot.Locked = true;
				if (slot.Nav.Cancel)
				{
					GameRoot.Instance.GoTitle();
					return;
				}
			}
			else if (slot.Nav.Cancel)
			{
				slot.Locked = false;
			}

			slot.State.Text = slot.Locked ? "READY" : "choosing...";
			if (slot.Rig != null && slot.Rig.Loaded)
			{
				slot.Rig.Play(FighterAnimations.Idle);
				slot.Rig.Advance();
				slot.Rig.Modulate = slot.Locked ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.62f);
			}

			if (!slot.Locked) everyoneReady = false;
		}

		if (!everyoneReady) return;

		for (int i = 0; i < slots.Length; i++) GameRoot.Instance.SelectedFighters[i] = slots[i].Index;
		GameRoot.Instance.GoStageSelect();
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);

		// A panel per player, so the two halves of the screen read as two people choosing.
		for (int i = 0; i < slots.Length; i++)
		{
			var box = new Rect2(250.0f + i * 700.0f, 240.0f, 560.0f, 660.0f);
			DrawRect(box, new Color(1.0f, 1.0f, 1.0f, 0.55f));
			CrayonBrush.InkRect(this, box, slots[i].Locked ? slots[i].Tint : MenuTheme.Rule,
				slots[i].Locked ? 6.0f : 3.0f, 31 + i * 7, 2.6f);
		}
	}
}
