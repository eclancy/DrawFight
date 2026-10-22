using System.Collections.Generic;
using Godot;

/// <summary>What an item move does - see <see cref="MoveData.ItemAction"/>.</summary>
public enum ItemAction { None, PickUp, Consume, Throw, Swing, Spray, Squeeze, Gust, Draw, Stamp }

/// <summary>
/// A fighter and items: picking them up, carrying them, throwing and using them, and what the
/// boosts do to him. Kept apart from the rest of Fighter, which only calls in at a few hooks.
///
/// Attack beside an item on the ground picks it up (a quick bend and grab); with one in hand,
/// attack uses it - swings it, throws it, sprays it - and the grab button always throws it, the
/// way the stick points. Specials still work with something in hand. A big hit knocks it out of
/// his hand. Eric's call, 2026-10-10.
/// </summary>
public partial class Fighter
{
	Item heldItem;
	Item pendingPickup;
	bool consumeAfterPickup;
	int grabBufferFrames;

	public Item HeldItem => heldItem;

	/// <summary>In the middle of the held item's own swing.</summary>
	public bool SwingingHeldItem => State == FighterState.Attacking && heldItem != null && currentMove == heldItem.Data.Swing;

	/// <summary>The bottom of his body box: where his feet are.</summary>
	public Vector2 Feet => GlobalPosition + new Vector2(0.0f, bodySize.Y * 0.5f);

	/// <summary>How far an item can be from his feet and still be picked up.</summary>
	const float PickUpReach = 95.0f;

	/// <summary>A hit with this much knockback knocks what he is holding out of his hand.</summary>
	const float DropItemKnockback = 70.0f;

	// --- The moves items make ------------------------------------------------------------

	static MoveData ItemMove(string name, AttackAnim anim, int startup, int active, int endlag, ItemAction action) => new MoveData
	{
		MoveName = name, Anim = anim,
		StartupFrames = startup, ActiveFrames = active, EndlagFrames = endlag,
		HitboxRadius = 0.0f, ItemAction = action, PropArt = "-",
	};

	/// <summary>A quick bend to the floor and up with it.</summary>
	static readonly MoveData PickUpMove = ItemMove("Pick Up", AttackAnim.SetDown, 4, 1, 6, ItemAction.PickUp);

	/// <summary>Held up high, and used: a boost.</summary>
	static readonly MoveData ConsumeMove = ItemMove("Use Item", AttackAnim.PointUp, 8, 1, 16, ItemAction.Consume);

	static readonly MoveData ThrowForward = ItemMove("Item Throw", AttackAnim.Slash, 5, 1, 12, ItemAction.Throw);
	static readonly MoveData ThrowUp = ItemMove("Item Throw Up", AttackAnim.Uppercut, 5, 1, 12, ItemAction.Throw);
	static readonly MoveData ThrowDown = ItemMove("Item Throw Down", AttackAnim.OverheadSlam, 5, 1, 12, ItemAction.Throw);
	static readonly MoveData ThrowBack = ItemMove("Item Throw Back", AttackAnim.BackSlash, 6, 1, 12, ItemAction.Throw);
	static readonly MoveData PlaceMove = ItemMove("Item Place", AttackAnim.SetDown, 6, 1, 8, ItemAction.Throw);

	/// <summary>The paint tube: held up and squeezed, as long as attack is held, then fired.</summary>
	static readonly MoveData SqueezeMove = ItemMove("Squeeze", AttackAnim.Aim, 8, 2, 14, ItemAction.Squeeze);

	/// <summary>
	/// The airbrush's gust: it blows for as long as attack is held and there is air in it (Eric,
	/// 2026-10-15) - the active window is only the most it can go on for.
	/// </summary>
	static readonly MoveData GustMove = ItemMove("Gust", AttackAnim.Aim, 6, 600, 10, ItemAction.Gust);

	/// <summary>A line drawn in the air with the marker.</summary>
	static readonly MoveData DrawMove = ItemMove("Draw", AttackAnim.Slash, 7, 10, 8, ItemAction.Draw);

	Vector2 throwWay;

	// --- Status from items ---------------------------------------------------------------

	int paintFrames;
	Color paintColour;
	int glueFrames;
	int cutoutFrames, cutoutImmuneFrames;
	int starFrames;
	int giantFrames;
	float giantScale = 1.0f;
	int ghostFrames;
	int ghostStartFrames;
	float healLeft;
	int healFrames;
	float healShown;
	int stampModeFrames;
	int reflectFlashFrames;
	Vector2 reflectAt;
	int boostFlashFrames;
	Color boostFlash;
	readonly List<(Vector2 at, int frame)> crumbs = new List<(Vector2, int)>();

	/// <summary>Where the rubber stamp has come down on the floor, printing its star.</summary>
	readonly List<(Vector2 at, int frame)> prints = new List<(Vector2, int)>();
	const int PrintFrames = 100;
	int springFrames;

	/// <summary>How far a paint tube has been squeezed, 0 to 1, for its swelling.</summary>
	public float ItemCharge { get; private set; }

	/// <summary>A shield held up, with attack or block (ItemUse.Shield).</summary>
	bool shieldRaised;
	public bool ShieldRaised => shieldRaised;

	/// <summary>How fast he edges along behind a shield, of his run.</summary>
	const float ShieldWalk = 0.22f;

	/// <summary>The scissors are mid-snip, open.</summary>
	public bool ItemSnipping => State == FighterState.Attacking && currentMove == heldItem?.Data.Swing && heldItem?.Data.Id == "scissors"
		&& moveFrame >= currentMove.StartupFrames - 2 && moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;

	/// <summary>Damage he deals, on top of a move's own: a giant hits harder.</summary>
	float ItemPower => giantScale > 1.01f ? 1.0f + 0.3f * (giantScale - 1.0f) / (GiantScale - 1.0f) : 1.0f;

	/// <summary>Glue underfoot: slow on his feet, and his jumps barely leave the ground.</summary>
	float Stuck => glueFrames > 0 ? 0.14f : 1.0f;
	float JumpStick => glueFrames > 0 ? 0.32f : 1.0f;

	const float GiantScale = 1.45f;
	const int GrowFrames = 24;

	// --- Input -----------------------------------------------------------------------------

	/// <summary>
	/// Items before anything else he might do with a press: the grab button throws what is in his
	/// hand (or grabs, on the ground, with nothing in it); attack uses what is in his hand, or
	/// picks up what is at his feet. True if a move started.
	/// </summary>
	bool TryItemInput(InputState input)
	{
		if (stampModeFrames > 0)
		{
			// Stamping: nothing else, until it is over - and a stamp only when attack is pressed,
			// pressed down onto the floor in front of him, never swung (Eric, 2026-10-11).
			bool stamp = attackBufferFrames > 0;
			attackBufferFrames = specialBufferFrames = grabBufferFrames = 0;
			if (stamp && heldItem?.Data.Swing != null && itemCooldownFrames <= 0)
			{
				itemCooldownFrames = heldItem.Data.Cooldown;
				BeginMove(heldItem.Data.Swing, MoveSlot.ForwardTilt);
				return true;
			}
			return false;
		}

		if (grabBufferFrames > 0)
		{
			grabBufferFrames = 0;
			if (heldItem != null)
			{
				BeginItemThrow(input);
				return true;
			}
			if (State == FighterState.Grounded && Data.Grab != null && Data.Throws.Length == 4)
			{
				IsBlocking = false;
				BeginMove(Data.Grab, MoveSlot.Jab);
				Velocity = new Vector2(0.0f, Velocity.Y);
				return true;
			}
			return false;
		}

		// His own palette coming back: attack or grab as it arrives catches it.
		if (heldItem == null && (attackBufferFrames > 0 || grabBufferFrames > 0) && Match != null)
		{
			foreach (Node child in Match.GetChildren())
			{
				if (child is not Item coming || !coming.CatchableBy(this, bodySize.Y * 0.5f + 90.0f)) continue;
				attackBufferFrames = grabBufferFrames = 0;
				CatchItem(coming);
				SfxPlayer.At("item_pickup", GlobalPosition, 0.05f);
				return true;
			}
		}

		if (attackBufferFrames <= 0 || specialBufferFrames > 0) return false;

		// A shield is held up while attack is held, not swung: the press only raises it, and he
		// carries on moving (and can still use his specials).
		if (heldItem != null && heldItem.Data.Use == ItemUse.Shield)
		{
			attackBufferFrames = 0;
			return false;
		}

		if (heldItem != null)
		{
			attackBufferFrames = 0;
			// Still recovering from the last use: nothing, not a normal attack instead.
			if (itemCooldownFrames > 0 && heldItem.Data.Use != ItemUse.Throw && heldItem.Data.Use != ItemUse.Consume)
			{
				return true;
			}
			BeginItemUse(input);
			return true;
		}

		if (State == FighterState.Airborne)
		{
			// In the air, attack beside an item snatches it - no bend to the floor, it is just in
			// his hand (Eric, 2026-10-11) - and a boost goes straight in.
			Item caught = ItemWithinReach(true);
			if (caught == null) return false;
			attackBufferFrames = 0;
			pendingPickup = caught;
			FinishPickUp();
			if (consumeAfterPickup && heldItem != null)
			{
				consumeAfterPickup = false;
				BeginMove(ConsumeMove, MoveSlot.Jab);
			}
			return true;
		}

		if (State != FighterState.Grounded) return false;
		Item near = ItemWithinReach();
		if (near == null) return false;
		attackBufferFrames = 0;
		pendingPickup = near;
		BeginMove(PickUpMove, MoveSlot.Jab);
		Velocity = new Vector2(Velocity.X * 0.3f, Velocity.Y);
		return true;
	}

	/// <summary>The nearest item he could take: by his feet on the ground, anywhere about his body in the air.</summary>
	Item ItemWithinReach(bool inAir = false)
	{
		if (Match == null) return null;
		Item best = null;
		Vector2 from = inAir ? GlobalPosition + new Vector2(Facing * 20.0f, 0.0f) : Feet + new Vector2(Facing * 20.0f, -30.0f);
		float reach = inAir ? bodySize.Y * 0.5f + 70.0f : PickUpReach;
		float bestDistance = reach * reach;
		foreach (Node child in Match.GetChildren())
		{
			if (child is not Item item || !item.CanBePickedUp || item.Data.TouchToCollect) continue;
			float d = item.GlobalPosition.DistanceSquaredTo(from);
			if (d < bestDistance) { bestDistance = d; best = item; }
		}
		return best;
	}

	/// <summary>Frames before the held item can be used again (ItemData.Cooldown).</summary>
	int itemCooldownFrames;

	void BeginItemUse(InputState input)
	{
		ItemData data = heldItem.Data;
		if (data.Use is not (ItemUse.Throw or ItemUse.Consume or ItemUse.Open))
		{
			itemCooldownFrames = data.Cooldown;
			if (heldItem.SwingsLeft > 0) heldItem.SwingsLeft--;
		}
		switch (data.Use)
		{
			case ItemUse.Swing:
				BeginMove(data.Swing, MoveSlot.ForwardTilt);
				break;
			case ItemUse.Shield:
				break;
			case ItemUse.Spray:
				BeginMove(data.Swing, MoveSlot.ForwardTilt);
				break;
			case ItemUse.ChargeShot:
				ItemCharge = 0.0f;
				BeginMove(SqueezeMove, MoveSlot.ForwardTilt);
				break;
			case ItemUse.Gust:
				BeginMove(GustMove, MoveSlot.ForwardTilt);
				break;
			case ItemUse.Draw:
				BeginMove(DrawMove, MoveSlot.ForwardTilt);
				break;
			default:
				BeginItemThrow(input);
				break;
		}
	}

	void BeginItemThrow(InputState input)
	{
		float speed = heldItem.Data.ThrowSpeed;
		Vector2 stick = input.Move;
		if (stick.Y < -0.5f)
		{
			throwWay = new Vector2(Facing * 80.0f, -speed * 1.05f);
			BeginMove(ThrowUp, MoveSlot.UpTilt);
		}
		else if (stick.Y > 0.5f)
		{
			// Down on the ground sets it down gently in front: a trap, placed.
			throwWay = IsOnFloor() ? new Vector2(Facing * 90.0f, -120.0f) : new Vector2(0.0f, speed * 0.9f);
			BeginMove(IsOnFloor() ? PlaceMove : ThrowDown, MoveSlot.DownTilt);
		}
		else if (Mathf.Abs(stick.X) > 0.4f && Mathf.Sign(stick.X) != Facing)
		{
			throwWay = new Vector2(-Facing * speed * 0.85f, -speed * 0.2f);
			BeginMove(ThrowBack, MoveSlot.ForwardTilt);
		}
		else
		{
			throwWay = new Vector2(Facing * speed, -speed * 0.18f);
			BeginMove(ThrowForward, MoveSlot.ForwardTilt);
		}
	}

	// --- Item moves, frame by frame --------------------------------------------------------

	/// <summary>
	/// The frames of a move that do something with an item. Returns true when it has dealt with
	/// the whole frame (a stamp, which keeps him walking while it swings).
	/// </summary>
	bool TickItemMove(InputState input, int activeStart, int activeEnd, float dt)
	{
		MoveData move = currentMove;
		bool firstActive = moveFrame == activeStart + 1;

		switch (move.ItemAction)
		{
			case ItemAction.PickUp:
				if (firstActive) FinishPickUp();
				if (moveFrame >= move.TotalFrames && consumeAfterPickup && heldItem != null)
				{
					consumeAfterPickup = false;
					BeginMove(ConsumeMove, MoveSlot.Jab);
					return true;
				}
				return false;

			case ItemAction.Consume:
				if (firstActive && heldItem != null)
				{
					Item item = heldItem;
					heldItem = null;
					ApplyBoost(item);
				}
				return false;

			case ItemAction.Throw:
				if (firstActive && heldItem != null)
				{
					Item item = heldItem;
					heldItem = null;
					// Let go of in front of him, the way it goes - the arm may not have come all the
					// way round yet, and a throw must never start from behind his head.
					int way = Mathf.Abs(throwWay.X) > 1.0f ? Mathf.Sign(throwWay.X) : 0;
					if (way != 0 && (item.GlobalPosition.X - GlobalPosition.X) * way < bodySize.X * 0.35f)
					{
						item.GlobalPosition = GlobalPosition + new Vector2(way * bodySize.X * 0.6f, -bodySize.Y * 0.2f);
					}
					item.Thrown(this, throwWay + new Vector2(Velocity.X * 0.4f, 0.0f));
					SfxPlayer.At("swing_medium", GlobalPosition, 0.05f);
				}
				return false;

			case ItemAction.Squeeze:
				// Held at the top of the squeeze while attack is down, swelling, up to its fullest.
				if (moveFrame == activeStart && input.AttackHeld && ItemCharge < 1.0f && heldItem != null)
				{
					ItemCharge = Mathf.Min(1.0f, ItemCharge + 1.0f / 50.0f);
					moveFrame = activeStart - 1;
				}
				if (firstActive) FireBlob();
				if (!IsOnFloor()) Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y, 300.0f));
				return false;

			case ItemAction.Gust:
				if (moveFrame > activeStart && moveFrame <= activeEnd && heldItem != null)
				{
					heldItem.UsesLeft--;
					Gust();
					if (!input.AttackHeld || heldItem.UsesLeft <= 0) moveFrame = activeEnd;
				}
				return false;

			case ItemAction.Draw:
				if (firstActive && heldItem != null)
				{
					heldItem.UsesLeft--;
					DrawPlatform(input);
				}
				return false;
		}

		// The held item's own swings.
		if (heldItem == null) return false;
		ItemData data = heldItem.Data;
		if (move != data.Swing) return false;

		if (data.Use == ItemUse.Stamp) return TickStamp(input, activeStart, activeEnd, dt);

		if (data.Use == ItemUse.Spray)
		{
			// Sprays as long as attack is held and there is paint left.
			if (moveFrame > activeStart && moveFrame <= activeEnd)
			{
				heldItem.UsesLeft--;
				if (!input.AttackHeld || heldItem.UsesLeft <= 0) moveFrame = activeEnd;
				if (moveFrame % 9 == 0) SfxPlayer.At("item_spray", GlobalPosition, 0.1f);
			}
			return false;
		}

		if (firstActive && data.Id == "paintbrush") FlickPaint();
		if (data.Id == "eraser" && moveFrame > activeStart && moveFrame <= activeEnd) EraseAround(heldItem.HeadGlobal(), 70.0f);
		if (data.Id == "eraser" && moveFrame == 1) SfxPlayer.At("special_glint", GlobalPosition, 0.0f);
		// Slammed down onto the floor and missed: a thud the whole stage feels, and a puff of dust.
		if (data.Id == "eraser" && moveFrame == activeEnd)
		{
			SfxPlayer.At("stomp", GlobalPosition, 0.05f);
			Match?.Shake(16.0f);
			if (IsOnFloor()) Match?.Dust(new Vector2(heldItem.HeadGlobal().X, Feet.Y), 1.0f, 0.0f);
		}
		if (data.Id == "scissors" && firstActive) SfxPlayer.At("item_snip", GlobalPosition, 0.05f);
		return false;
	}

	/// <summary>
	/// The rubber stamp: it stamps, over and over, while he walks and jumps about with it - a
	/// Smash hammer. Nothing else until the time is up, and then it is gone.
	/// </summary>
	bool TickStamp(InputState input, int activeStart, int activeEnd, float dt)
	{
		bool active = moveFrame > activeStart && moveFrame <= activeEnd;
		if (active) QueryHits();
		// The thump and the print as the stamp meets the floor: the last of the active frames, once
		// it is all the way down.
		if (moveFrame == activeEnd)
		{
			SfxPlayer.At("stomp", GlobalPosition, 0.05f);
			if (IsOnFloor())
			{
				Match?.Shake(10.0f);
				prints.Add((new Vector2(heldItem.HeadGlobal().X, Feet.Y), fxFrames));
			}
		}

		float walk = Mathf.Abs(input.Move.X) > MoveDeadzone ? input.Move.X : 0.0f;
		if (IsOnFloor())
		{
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, walk * RunSpeed * 0.55f, GroundAcceleration * dt), Velocity.Y);
			if (jumpBufferFrames > 0)
			{
				jumpBufferFrames = 0;
				Velocity = new Vector2(Velocity.X, -Data.JumpForce * Tuning.JumpScale * JumpStick);
			}
		}
		else
		{
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, walk * Data.AirSpeed * 0.7f, Data.AirAcceleration * dt), Velocity.Y);
		}
		if (Mathf.Abs(walk) > 0.4f) Facing = walk > 0.0f ? 1 : -1;

		if (moveFrame >= currentMove.TotalFrames)
		{
			alreadyHitThisMove.Clear();
			EndMove();
			if (stampModeFrames <= 0)
			{
				heldItem?.UsedUp();
				heldItem = null;
			}
		}
		return true;
	}

	void FinishPickUp()
	{
		Item item = pendingPickup;
		pendingPickup = null;
		if (item == null || !IsInstanceValid(item) || !item.CanBePickedUp || heldItem != null) return;

		if (item.Data.Use == ItemUse.Open)
		{
			OpenPencilCase(item);
			return;
		}

		heldItem = item;
		item.PickedUp(this);
		// Drawn behind him, so his fist closes over its handle.
		Match?.MoveChild(item, GetIndex());
		SfxPlayer.At("item_pickup", GlobalPosition, 0.05f);
		if (item.Data.Id == "metre_stick") MeasureUp(item);

		switch (item.Data.Use)
		{
			case ItemUse.Consume:
				consumeAfterPickup = true;
				break;
			case ItemUse.Stamp:
				stampModeFrames = item.Data.Duration;
				break;
		}
	}

	/// <summary>The pencil case: it unzips and spills two or three other items out onto the floor.</summary>
	void OpenPencilCase(Item box)
	{
		if (Match == null) return;
		SfxPlayer.At("item_pickup", box.GlobalPosition, 0.05f);
		int count = 2 + (Mathf.Abs((int)(CrayonBrush.Noise(fxFrames, PlayerIndex) * 100.0f)) % 2);
		for (int i = 0; i < count; i++)
		{
			ItemData pick = Match.RandomItem(i + fxFrames, true);
			if (pick == null) break;
			var v = new Vector2((i - (count - 1) * 0.5f) * 260.0f, -900.0f - 120.0f * i);
			Match.SpawnItem(pick, box.GlobalPosition + new Vector2(0.0f, -20.0f), v);
		}
		box.SpillOpen();
	}

	// --- Using -------------------------------------------------------------------------------

	void FlickPaint()
	{
		ItemData data = heldItem.Data;
		if (Match == null || data.Shot == null) return;
		// Off the bristles - unless the swing has not brought them round in front of him yet, in
		// which case from just in front of his chest, never from behind his head.
		Vector2 from = heldItem.HeadGlobal();
		Vector2 chest = GlobalPosition + new Vector2(Facing * bodySize.X * 0.6f, -bodySize.Y * 0.15f);
		if ((from.X - chest.X) * Facing < 0.0f) from = chest;
		for (int i = -1; i <= 1; i++)
		{
			Vector2 v = new Vector2(Facing, 0.0f).Rotated(Mathf.DegToRad(i * 10.0f - 8.0f) * Facing) * data.Shot.SpecialSpeed;
			Match.SpawnHazard(this, data.Shot, from, v);
		}
	}

	void FireBlob()
	{
		if (heldItem == null || Match == null || heldItem.Data.Shot == null) return;
		MoveData shot = heldItem.Data.Shot.Scaled(new MoveScale(1, 1, 1.0f + 2.5f * ItemCharge, 1, 1, 1));
		shot.FxRadius = heldItem.Data.Shot.FxRadius * (1.0f + 1.4f * ItemCharge);
		Match.SpawnHazard(this, shot, heldItem.HeadGlobal(), new Vector2(Facing * shot.SpecialSpeed, 0.0f));
		heldItem.UsesLeft--;
		ItemCharge = 0.0f;
		SfxPlayer.At("special_shot", GlobalPosition, 0.05f);
	}

	/// <summary>
	/// The airbrush's gust: everyone ahead pushed away, harder the closer they are - and anything
	/// thrown, lying about or shot in its way blown along with it. A shot blown right round is
	/// his now, as off a shield (Eric, 2026-10-15).
	/// </summary>
	void Gust()
	{
		if (Match == null || heldItem == null) return;
		float reach = heldItem.Data.Radius;
		Vector2 nozzle = heldItem.HeadGlobal();
		float Strength(Vector2 at, float height)
		{
			float dx = (at.X - nozzle.X) * Facing;
			if (dx < -20.0f || dx > reach || Mathf.Abs(at.Y - nozzle.Y) > height) return 0.0f;
			return 1.0f - Mathf.Clamp(dx / reach, 0.0f, 1.0f) * 0.6f;
		}
		foreach (Fighter f in Match.Fighters)
		{
			if (f == this || !f.IsInPlay) continue;
			float strength = Strength(f.GlobalPosition, 140.0f);
			if (strength > 0.0f) f.ApplyWind(new Vector2(Facing * 950.0f * strength, 0.0f));
		}
		foreach (Node child in Match.GetChildren())
		{
			if (child is Item item && item != heldItem)
			{
				float strength = Strength(item.GlobalPosition, 160.0f);
				if (strength > 0.0f) item.Blow(this, Facing * 1100.0f * strength);
			}
			else if (child is Hazard hazard && hazard.CanBeBlown)
			{
				float strength = Strength(hazard.GlobalPosition, 160.0f);
				if (strength > 0.0f) hazard.Blow(this, Facing * 1300.0f * strength);
			}
		}
		if (moveFrame % 8 == 0) SfxPlayer.At("item_gust", GlobalPosition, 0.1f);
	}

	/// <summary>
	/// The marker draws a platform: under his feet in the air or holding down, otherwise a step
	/// up in front of him.
	/// </summary>
	void DrawPlatform(InputState input)
	{
		if (Match == null || heldItem == null) return;
		ItemData data = heldItem.Data;
		bool under = !IsOnFloor() || input.Move.Y > 0.5f;
		Vector2 centre = under ? Feet + new Vector2(Facing * 30.0f, 14.0f) : Feet + new Vector2(Facing * 170.0f, -110.0f);
		var platform = new DrawnPlatform();
		Match.AddChild(platform);
		platform.Place(centre, data.Radius, data.Duration, data.Tint, Facing);
		SfxPlayer.At("special_build", centre, 0.05f);
	}

	/// <summary>The eraser rubs out anything thrown or set down near it that is not his own.</summary>
	void EraseAround(Vector2 at, float radius)
	{
		if (Match == null) return;
		foreach (Node child in Match.GetChildren())
		{
			if (child is Hazard hazard && !hazard.IsExpiring && hazard.OwnerFighter != this && hazard.GlobalPosition.DistanceTo(at) < radius + 60.0f)
			{
				crumbs.Add((hazard.GlobalPosition, fxFrames));
				hazard.Remove();
			}
			else if (child is Item item && item.CanBeErased(this) && item.GlobalPosition.DistanceTo(at) < radius + 80.0f)
			{
				crumbs.Add((item.GlobalPosition, fxFrames));
				item.Erase();
			}
		}
	}

	/// <summary>A swing with an item landed: anything that item does to whoever it hits.</summary>
	void OnItemSwingHit(Fighter other, bool firstHit)
	{
		if (heldItem == null || currentMove != heldItem.Data.Swing) return;
		ItemData data = heldItem.Data;
		if (data.Id == "scissors") other.CutOut();
		if (data.Use == ItemUse.Spray) other.Paint(data.Tint, 120);
		if (data.Id == "stamp") other.StampMark();
		// The eraser is spent on whoever it rubs out: it leaves his hand to scrub them off the
		// page (DrawEraserScrub), and the rest of the swing hits no one else. A swing that misses
		// keeps it (Eric, 2026-10-20).
		if (data.Swing.Erases && other.IsBeingErased)
		{
			heldItem.UsedUp();
			heldItem = null;
			moveFrame = Mathf.Max(moveFrame, currentMove.StartupFrames + currentMove.ActiveFrames);
			return;
		}
		// A swing that lands wears it down - once a swing, however many it hits.
		if (data.Uses > 0 && data.Use == ItemUse.Swing && heldItem.UsesLeft > 0 && firstHit) heldItem.UsesLeft--;
	}

	// --- Boosts --------------------------------------------------------------------------------

	/// <summary>Picked up by touching it - the gold star.</summary>
	public void Collect(Item item)
	{
		if (heldItem == item) heldItem = null;
		ApplyBoost(item);
	}

	void ApplyBoost(Item item)
	{
		ItemData data = item.Data;
		boostFlashFrames = 18;
		switch (data.Boost)
		{
			case ItemBoost.Heal:
				healLeft = data.Amount;
				healShown = Mathf.Min(data.Amount, Percent);
				healFrames = 70;
				boostFlash = new Color(1.0f, 1.0f, 1.0f);
				SfxPlayer.At("item_heal", GlobalPosition, 0.05f);
				break;
			case ItemBoost.Star:
				starFrames = data.Duration;
				boostFlash = new Color(1.0f, 0.86f, 0.3f);
				SfxPlayer.At("special_glint", GlobalPosition, 0.05f);
				break;
			case ItemBoost.Giant:
				giantFrames = data.Duration;
				boostFlash = new Color(0.7f, 0.9f, 1.0f);
				SfxPlayer.At("special_resize", GlobalPosition, 0.05f);
				break;
			case ItemBoost.Ghost:
				ghostFrames = data.Duration;
				ghostStartFrames = data.Duration;
				boostFlash = new Color(0.9f, 0.95f, 1.0f);
				SfxPlayer.At("special_blink", GlobalPosition, 0.05f);
				break;
			case ItemBoost.Canvas:
				item.Summon(this);
				return;
		}
		item.UsedUp();
	}

	// --- Being on the receiving end ----------------------------------------------------------

	/// <summary>A hit from an item, thrown or set off - knocked the way it was going.</summary>
	public void ReceiveItemHit(Fighter owner, MoveData hit, int direction, Vector2 contact)
	{
		if (hit == null || !CanBeHit) return;
		ReceiveHitFrom(null, direction, hit, contact, owner != null ? owner.ItemPower : 1.0f);
	}

	public void Paint(Color colour, int frames)
	{
		paintColour = colour;
		paintFrames = Mathf.Max(paintFrames, frames);
	}

	/// <summary>Stuck in glue - and still stuck for a little after stepping out of it.</summary>
	public void Glue() => glueFrames = Mathf.Max(glueFrames, GlueLingerFrames);

	const int GlueLingerFrames = 30;

	/// <summary>Sprung up off a pop-up book.</summary>
	public void SpringUp(float speed)
	{
		if (State == FighterState.Grounded) State = FighterState.Airborne;
		Velocity = new Vector2(Velocity.X, -speed * Tuning.JumpScale);
		airJumpsUsed = 0;
		springFrames = 20;
	}

	/// <summary>Cut out by the scissors: flat as paper, fluttering, stunned for a moment.</summary>
	public void CutOut()
	{
		if (cutoutImmuneFrames > 0) return;
		cutoutFrames = 55;
		cutoutImmuneFrames = 150;
		if (State == FighterState.Hitstun) hitstunFrames = Mathf.Max(hitstunFrames, 50);
		Velocity *= 0.3f;
	}

	int stampMarkFrames;
	public void StampMark() => stampMarkFrames = 90;

	// --- Rubbed out --------------------------------------------------------------------------
	// Touched by the eraser's swing he is erased off the page where he stands: frozen, the
	// eraser scrubs back and forth across him from his head down, and everything it passes over
	// is gone - his drawing really is rubbed out (EraseShader) - and then so is the stock
	// (Eric, 2026-10-15).

	int erasedFrames;
	const int EraseFrames = 84;
	const int EraseStartle = 14;
	const int EraseRub = 62;

	public bool IsBeingErased => erasedFrames > 0;

	static Shader eraseShader;

	/// <summary>
	/// Every part of the rig cut away above a ragged line that sweeps from his head to his feet,
	/// with streaks of half-rubbed drawing just below it, the way a real eraser leaves a page.
	/// </summary>
	const string EraseShaderCode = @"
shader_type canvas_item;
uniform float edge = -1.0;
uniform float top = 0.0;
uniform float height = 1.0;
varying vec2 world;
void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float y = (world.y - top) / height + 0.025 * sin(world.x * 0.09) + 0.015 * sin(world.x * 0.31 + 1.3);
	float streak = 0.5 + 0.5 * sin(world.y * 0.55);
	float faint = smoothstep(edge, edge + 0.07 + 0.05 * streak, y);
	c.a *= faint;
	COLOR = c;
}";

	void BeginErased()
	{
		if (erasedFrames > 0 || rig == null) return;
		erasedFrames = EraseFrames;
		Velocity = Vector2.Zero;
		ReleaseGrabbed();
		DropHeldItem();
		Expression = "shocked";
		eraseShader ??= new Shader { Code = EraseShaderCode };
		rig.Material = new ShaderMaterial { Shader = eraseShader };
		UseParentMaterialBelow(rig, true);
		SfxPlayer.At("special_blink", GlobalPosition, 0.0f);
		Match?.Shake(30.0f);
	}

	static void UseParentMaterialBelow(Node node, bool on)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is CanvasItem item) item.UseParentMaterial = on;
			UseParentMaterialBelow(child, on);
		}
	}

	/// <summary>How far down him the rubbing has got, 0 at his head to 1 past his feet.</summary>
	float EraseProgress => erasedFrames <= 0 ? 0.0f
		: Mathf.Clamp((EraseFrames - EraseStartle - erasedFrames) / (float)EraseRub, 0.0f, 1.0f);

	/// <summary>The top and height of what there is of him to rub out, in the world.</summary>
	(float top, float height) EraseSpan()
	{
		Rect2 body = BodyRect();
		return (body.Position.Y - bodySize.Y * 0.15f, body.Size.Y + bodySize.Y * 0.3f);
	}

	void TickErased()
	{
		erasedFrames--;
		Velocity = Vector2.Zero;
		(float top, float height) = EraseSpan();
		if (rig?.Material is ShaderMaterial erase)
		{
			erase.SetShaderParameter("edge", EraseProgress * 1.12f - 0.06f);
			erase.SetShaderParameter("top", top);
			erase.SetShaderParameter("height", height);
		}
		int into = EraseFrames - erasedFrames;
		if (into > EraseStartle && into % 7 == 0)
		{
			SfxPlayer.At("item_crumple", GlobalPosition, 0.15f);
			crumbs.Add((new Vector2(GlobalPosition.X + CrayonBrush.Noise(into, 3) * bodySize.X * 0.6f, top + height * EraseProgress), fxFrames));
		}
		if (erasedFrames > 0) return;

		// All gone: so is the stock.
		rig.Material = null;
		UseParentMaterialBelow(rig, false);
		Expression = "";
		Match?.ErasedOut(this);
	}

	/// <summary>The eraser scrubbing back and forth across him at the line it has rubbed down to.</summary>
	void DrawEraserScrub(CanvasItem canvas)
	{
		int into = EraseFrames - erasedFrames;
		if (into <= EraseStartle - 4 || ItemCatalog.Art("eraser") is not ItemArt art) return;
		(float top, float height) = EraseSpan();
		Vector2 local = canvas is Node2D n ? n.ToLocal(new Vector2(GlobalPosition.X, top + height * EraseProgress)) : Vector2.Zero;
		float sweep = Mathf.Sin(into * 0.55f) * bodySize.X * 0.55f;
		float s = 120.0f / Mathf.Max(art.Size.X, art.Size.Y);
		canvas.DrawSetTransformMatrix(new Transform2D(0.35f + 0.15f * Mathf.Sin(into * 0.55f), new Vector2(s, s), 0.0f, local + new Vector2(sweep, -30.0f)));
		canvas.DrawTexture(art.Texture, -art.Size * 0.5f);
		canvas.DrawSetTransformMatrix(Transform2D.Identity);
	}

	/// <summary>
	/// A gust pushing him. A shove straight along the ground, past his own footing - a push on his
	/// speed alone is eaten by his friction and he barely slides - and harder in the air, with
	/// nothing to brace on. Blocking halves it. It does not stop at an edge.
	/// </summary>
	public void ApplyWind(Vector2 push)
	{
		if (hitlagFrames > 0 || !IsInPlay) return;
		float give = IsOnFloor() ? 0.55f : 0.8f;
		if (IsBlocking) give *= 0.5f;
		MoveAndCollide(new Vector2(push.X * give / Engine.PhysicsTicksPerSecond, 0.0f));
		if (!IsOnFloor()) Velocity = new Vector2(Mathf.Lerp(Velocity.X, push.X, 0.1f), Velocity.Y);
	}

	/// <summary>Holding a shield up toward where a shot is coming from: it bounces back.</summary>
	public bool ReflectsFrom(Vector2 from)
	{
		if (!shieldRaised || heldItem == null || heldItem.Data.Use != ItemUse.Shield) return false;
		if (State == FighterState.Hitstun) return false;
		return Mathf.Sign(from.X - GlobalPosition.X) == Facing;
	}

	public void ShowReflect(Vector2 at)
	{
		reflectFlashFrames = 12;
		reflectAt = at;
		SfxPlayer.At("clang", at, 0.05f);
	}

	/// <summary>His thrown palette comes back to him: caught, if his hand is free.</summary>
	public bool CatchItem(Item item)
	{
		if (heldItem != null || State == FighterState.Hitstun) return false;
		heldItem = item;
		item.PickedUp(this);
		Match?.MoveChild(item, GetIndex());
		if (item.Data.Use == ItemUse.Stamp) stampModeFrames = item.Data.Duration;
		return true;
	}

	/// <summary>
	/// The metre stick, picked up off the floor (not handed to him by a story): it shoots up
	/// beside him, taller than he is, and he grows to measure up - giant for its Duration, as the magnifying glass makes him. It is in his hand
	/// once it has finished rising, and cannot be swung before.
	/// </summary>
	void MeasureUp(Item stick)
	{
		stick.StartMeasuring();
		giantFrames = Mathf.Max(giantFrames, stick.Data.Duration);
		itemCooldownFrames = Mathf.Max(itemCooldownFrames, Item.MeasureTotal);
		boostFlashFrames = 18;
		boostFlash = new Color(0.98f, 0.86f, 0.55f);
		SfxPlayer.At("special_resize", GlobalPosition, 0.05f);
	}

	/// <summary>Knocked out of his hand - or, KO'd, gone.</summary>
	/// <summary>
	/// Knocked out of his hand - or, KO'd, gone. Something made to be thrown flies off the way he
	/// was hit, as if he had thrown it, and does whatever it does when it lands: a paint bucket
	/// knocked loose still splashes (Eric, 2026-10-11). Anything else just drops.
	/// </summary>
	void DropHeldItem(bool remove = false, Vector2 knockedAway = default)
	{
		if (heldItem == null) return;
		Item item = heldItem;
		heldItem = null;
		if (stampModeFrames > 0 || remove) item.Poof();
		else if (item.Data.Use == ItemUse.Throw) item.Thrown(this, knockedAway * 0.7f + new Vector2(0.0f, -250.0f));
		else item.Dropped();
		stampModeFrames = 0;
	}

	void ClearItemStatus()
	{
		DropHeldItem(true);
		pendingPickup = null;
		consumeAfterPickup = false;
		paintFrames = glueFrames = cutoutFrames = starFrames = ghostFrames = 0;
		healLeft = 0.0f;
		healFrames = 0;
		giantFrames = 0;
		if (giantScale != 1.0f) SetGiantScale(1.0f);
	}

	// --- Each frame -----------------------------------------------------------------------------

	void TickItemStatus(InputState input)
	{
		if (input.GrabPressed) grabBufferFrames = Tuning.AttackBufferFrames;
		else if (grabBufferFrames > 0) grabBufferFrames--;

		if (paintFrames > 0)
		{
			paintFrames--;
			Slip();
		}
		if (glueFrames > 0)
		{
			glueFrames--;
			// Stuck fast: whatever was carrying him along the floor is soaked up by the glue.
			if (IsOnFloor()) Velocity = new Vector2(Velocity.X * 0.8f, Velocity.Y);
		}
		if (cutoutFrames > 0) cutoutFrames--;
		if (cutoutImmuneFrames > 0) cutoutImmuneFrames--;
		if (starFrames > 0) starFrames--;
		if (ghostFrames > 0) ghostFrames--;
		if (stampMarkFrames > 0) stampMarkFrames--;
		if (reflectFlashFrames > 0) reflectFlashFrames--;
		if (boostFlashFrames > 0) boostFlashFrames--;
		if (springFrames > 0) springFrames--;
		if (stampModeFrames > 0)
		{
			stampModeFrames--;
			// Time up between stamps: the stamp is gone.
			if (stampModeFrames == 0 && heldItem?.Data.Use == ItemUse.Stamp && State != FighterState.Attacking)
			{
				heldItem.UsedUp();
				heldItem = null;
			}
		}
		if (itemCooldownFrames > 0) itemCooldownFrames--;
		crumbs.RemoveAll(c => fxFrames - c.frame > 24);
		if (heldLayer != null && IsInstanceValid(heldLayer)) heldLayer.QueueRedraw();
		prints.RemoveAll(p => fxFrames - p.frame > PrintFrames);

		if (healLeft > 0.0f)
		{
			float step = Mathf.Min(healLeft, 1.0f);
			healLeft -= step;
			Percent = Mathf.Max(0.0f, Percent - step);
		}
		if (healFrames > 0) healFrames--;

		// Growing and shrinking: the boost's first and last frames.
		if (giantFrames > 0)
		{
			giantFrames--;
			float target = giantFrames > GrowFrames ? GiantScale : 1.0f;
			float step = (GiantScale - 1.0f) / GrowFrames;
			float next = Mathf.MoveToward(giantScale, target, step);
			if (!Mathf.IsEqualApprox(next, giantScale)) SetGiantScale(next);
		}
		else if (giantScale != 1.0f)
		{
			SetGiantScale(Mathf.MoveToward(giantScale, 1.0f, (GiantScale - 1.0f) / GrowFrames));
		}

		// A swing item worn right down, or used as many times as it can be, is gone.
		if (heldItem != null && State != FighterState.Attacking
			&& (heldItem.Data.Uses > 0 && heldItem.UsesLeft <= 0 || heldItem.SwingsLeft == 0))
		{
			heldItem.UsedUp();
			heldItem = null;
		}
		if (heldItem != null && !IsInstanceValid(heldItem)) heldItem = null;

		// Rolling the paint roller along the floor leaves a wet stripe behind.
		if (heldItem != null && heldItem.Data.Id == "roller" && IsOnFloor() && Mathf.Abs(Velocity.X) > 300.0f
			&& heldItem.UsesLeft > 0 && fxFrames % 7 == 0 && Match != null)
		{
			heldItem.UsesLeft -= 7;
			Match.SpawnStripe(this, heldItem.Data, Feet - new Vector2(Facing * 40.0f, 0.0f));
		}
	}

	/// <summary>
	/// Grows or shrinks him about his feet: drawn bigger, his body box bigger, his hits reaching
	/// further - the magnifying glass.
	/// </summary>
	void SetGiantScale(float scale)
	{
		giantScale = scale;
		if (rig != null && rig.Loaded) rig.Normalise(Data.BodySize.Y * 1.18f * scale, Data.BodySize.Y * 0.5f * scale, Data.VisualScale);
		SetSizeLevel(sizeLevel);
	}

	/// <summary>Where his hand is and which way it points, and the item put there.</summary>
	void PlaceHeldItem()
	{
		if (heldItem == null || rig == null || !rig.Loaded) return;
		(Vector2 fist, Vector2 along)? hand = rig.FrontHandGlobal();
		if (hand == null) return;
		(Vector2 fist, Vector2 along) = hand.Value;

		// Still measuring him up: standing on the floor at his side, not in his hand yet.
		if (heldItem.MeasureFrames > 0)
		{
			heldItem.DrawnBy = null;
			heldItem.StandAt(new Vector2(GlobalPosition.X + Facing * (bodySize.X * 0.5f + 60.0f), Feet.Y), Facing);
			return;
		}

		heldItem.HoldAt(fist, along, Facing);

		// Drawn inside his arm, in front of his body and behind his hand. The layer is part of his
		// own rig, so it goes when he does and the connection with it.
		Node2D layer = rig.HeldLayer();
		if (layer != null && layer != heldLayer)
		{
			heldLayer = layer;
			layer.Connect(CanvasItem.SignalName.Draw, new Callable(this, nameof(DrawHeldLayer)));
		}
		heldItem.DrawnBy = layer;

		// A shield is held from behind: it goes in front of his fist and him, not under the hand.
		if (heldItem.Data.Use == ItemUse.Shield)
		{
			heldItem.DrawnBy = null;
			if (Match != null && heldItem.GetIndex() < GetIndex()) Match.MoveChild(heldItem, -1);
		}

		// A stamp hangs face down and is pressed down onto the floor by its pose.
		if (heldItem.Data.Use == ItemUse.Stamp) return;

		// Swung low on the ground, it comes down onto the floor and scrapes along it rather than
		// through it: tipped up until no corner of the drawing is below the floor. Every corner,
		// not the end of the handle - a stamp's block is wide, and its edge sank in.
		float floor = Feet.Y + 4.0f;
		if (!IsOnFloor() || fist.Y >= floor || heldItem.LowestHeldY(along) <= floor) return;
		float side = Mathf.Abs(along.X) > 0.05f ? Mathf.Sign(along.X) : Facing;
		float below = Mathf.Atan2(along.Y, Mathf.Abs(along.X));
		Vector2 way = along;
		for (int i = 0; i < 30 && heldItem.LowestHeldY(way) > floor; i++)
		{
			below -= 0.06f;
			way = new Vector2(side * Mathf.Cos(below), Mathf.Sin(below));
		}
		heldItem.HoldAt(fist, way, Facing);
	}

	Node2D heldLayer;

	void DrawHeldLayer()
	{
		if (heldItem != null && IsInstanceValid(heldItem) && heldItem.DrawnBy == heldLayer) heldItem.DrawHeldInto(heldLayer);
	}

	/// <summary>
	/// Not swinging it, he holds it up in front of him, not trailing along the floor - and a shield
	/// held up goes out in front of his chest, between him and whoever he faces.
	/// </summary>
	void HoldItemPose()
	{
		if (heldItem == null || rig == null || !rig.Loaded) return;
		if (State == FighterState.Attacking || State == FighterState.LedgeHang || State == FighterState.Dodging) return;
		// A pose struck in a scene puts the arms where it wants them - Flambe playing his guitar.
		if (!string.IsNullOrEmpty(StoryPose)) return;
		HoldItemOut(rig, Facing, shieldRaised);
	}

	/// <summary>The front fist held out in front with something in it (HoldItemPose) - the parade's item film holds it the same way.</summary>
	public static void HoldItemOut(FighterRig rig, int facing, bool shield)
	{
		Vector2? shoulder = rig.JointGlobal(RigBone.ArmFrontUpper);
		float length = rig.ArmLength(RigBone.ArmFrontUpper, RigBone.ArmFrontLower);
		if (shoulder == null || length <= 0.0f) return;
		Vector2 hold = shield ? new Vector2(facing * length * 0.8f, length * 0.15f) : new Vector2(facing * length * 0.62f, length * 0.42f);
		rig.ReachArm(RigBone.ArmFrontUpper, RigBone.ArmFrontLower, shoulder.Value + hold, shield ? 1.0f : 0.85f);
	}

	/// <summary>Shaking under the eraser, straining to hold it up (FighterAnimations.HeaveRoll).</summary>
	float HeaveRoll() => State == FighterState.Attacking ? FighterAnimations.HeaveRoll(currentMove, moveFrame, PlayerIndex) : 0.0f;

	/// <summary>Cut out: a paper doll turning over and over as it flutters, edge-on and then flat.</summary>
	float CutoutWidth() => cutoutFrames > 0 ? 0.1f + 0.9f * Mathf.Abs(Mathf.Cos(cutoutFrames * 0.16f)) : 1.0f;
	float CutoutFlutter() => cutoutFrames > 0 ? 0.3f * Mathf.Sin(fxFrames * 0.35f) : 0.0f;

	/// <summary>The rig's tint from items: a star's shimmer, a ghost's see-through, paint.</summary>
	Color ItemTint(Color tint)
	{
		if (starFrames > 0)
		{
			float hue = (fxFrames * 0.03f) % 1.0f;
			Color shimmer = Color.FromHsv(hue, 0.35f, 1.0f);
			tint = tint * new Color(1.15f, 1.1f, 0.9f).Lerp(shimmer * 1.3f, 0.5f + 0.5f * Mathf.Sin(fxFrames * 0.4f));
			if (starFrames < 90 && starFrames / 5 % 2 == 0) tint = Colors.White;
		}
		if (ghostFrames > 0)
		{
			bool ending = ghostFrames < 90 && ghostFrames / 5 % 2 == 0;
			tint.A = ending ? 0.8f : 0.36f;
		}
		if (paintFrames > 0) tint = tint.Lerp(tint * paintColour.Lightened(0.35f), Mathf.Min(1.0f, paintFrames / 60.0f) * 0.5f);
		return tint;
	}

	// --- Drawing ---------------------------------------------------------------------------------

	/// <summary>Behind him: a star's glow.</summary>
	void DrawItemAuras()
	{
		if (starFrames <= 0) return;
		float r = Mathf.Max(bodySize.X, bodySize.Y) * 0.7f;
		for (int i = 0; i < 3; i++)
		{
			float pulse = 0.5f + 0.5f * Mathf.Sin(fxFrames * 0.25f + i);
			DrawCircle(Vector2.Zero, r * (0.8f + 0.15f * i), new Color(1.0f, 0.86f, 0.35f, 0.12f * pulse));
		}
	}

	/// <summary>In front of him: everything items show happening to him.</summary>
	void DrawItemOverlay(CanvasItem canvas)
	{
		DrawItemUses(canvas);
		if (erasedFrames > 0) DrawEraserScrub(canvas);
		// The eraser's wind-up has no glow round it (Eric, 2026-10-20): his body heaving it up and
		// shaking under it is the warning (AttackAnim.EraserHeave).
		Vector2 top = new Vector2(0.0f, -bodySize.Y * 0.5f);
		Vector2 feet = new Vector2(0.0f, bodySize.Y * 0.5f);

		// A burst of light as a boost goes in.
		if (boostFlashFrames > 0)
		{
			float t = 1.0f - boostFlashFrames / 18.0f;
			canvas.DrawArc(Vector2.Zero, bodySize.Y * (0.4f + 0.6f * t), 0.0f, Mathf.Tau, 32, new Color(boostFlash, 0.8f * (1.0f - t)), 8.0f, true);
		}

		// The star: sparkles going round him.
		if (starFrames > 0)
		{
			for (int i = 0; i < 6; i++)
			{
				float a = fxFrames * 0.12f + Mathf.Tau * i / 6.0f;
				Vector2 p = new Vector2(Mathf.Cos(a) * bodySize.X * 0.8f, Mathf.Sin(a) * bodySize.Y * 0.45f);
				Item.DrawStarOn(canvas, p, 7.0f + 3.0f * Mathf.Sin(fxFrames * 0.5f + i), new Color(1.0f, 0.9f, 0.4f));
			}
		}

		// White-Out: three white strokes painted down over him, then sparkles rising and the
		// damage taken away shown going up off him.
		if (healFrames > 0)
		{
			int f = 70 - healFrames;
			for (int s = 0; s < 3; s++)
			{
				float drawn = Mathf.Clamp((f - s * 5) / 8.0f, 0.0f, 1.0f);
				float fade = Mathf.Clamp((60 - f) / 20.0f, 0.0f, 1.0f);
				if (drawn <= 0.0f) continue;
				float x = (s - 1) * bodySize.X * 0.3f;
				Vector2 from = new Vector2(x - 10.0f, top.Y + 10.0f);
				Vector2 to = from.Lerp(new Vector2(x + 10.0f, feet.Y - 20.0f), drawn);
				canvas.DrawLine(from, to, new Color(1.0f, 1.0f, 1.0f, 0.85f * fade), 14.0f, true);
			}
			for (int i = 0; i < 7; i++)
			{
				float t = ((f * 1.5f + i * 13) % 50) / 50.0f;
				Vector2 p = new Vector2(CrayonBrush.Noise(i, 31) * bodySize.X * 0.6f, feet.Y - (bodySize.Y + 40.0f) * t);
				DrawPlus(canvas, p, 7.0f * (1.0f - t), new Color(0.45f, 0.9f, 0.55f, 1.0f - t));
			}
			float rise = f / 70.0f;
			string text = $"-{Mathf.RoundToInt(healShown)}%";
			Vector2 at = top + new Vector2(-28.0f, -30.0f - 50.0f * rise);
			Color ink = new Color(0.3f, 0.65f, 0.4f, 1.0f - rise * rise);
			for (int o = 0; o < 8; o++)
			{
				Vector2 offset = Vector2.Right.Rotated(Mathf.Tau * o / 8.0f) * 3.0f;
				canvas.DrawString(ThemeDB.FallbackFont, at + offset, text, HorizontalAlignment.Left, -1, 34, new Color(1, 1, 1, ink.A));
			}
			canvas.DrawString(ThemeDB.FallbackFont, at, text, HorizontalAlignment.Left, -1, 34, ink);
		}

		// Tracing paper: a sheet floats down over him as he goes see-through.
		if (ghostFrames > 0 && ghostStartFrames - ghostFrames < 22)
		{
			float t = (ghostStartFrames - ghostFrames) / 22.0f;
			Vector2 size = new Vector2(bodySize.X * 1.4f, bodySize.Y * 1.15f);
			Rect2 sheet = new Rect2(new Vector2(-size.X * 0.5f, top.Y - 80.0f * (1.0f - t) - 10.0f), size);
			canvas.DrawRect(sheet, new Color(0.88f, 0.92f, 0.98f, 0.55f * (1.0f - t * 0.7f)));
			CrayonBrush.InkRect(canvas, sheet, new Color(0.6f, 0.68f, 0.82f, 0.7f * (1.0f - t)), 2.0f, 5);
		}

		// Paint dripping off him.
		if (paintFrames > 0)
		{
			float alpha = Mathf.Min(1.0f, paintFrames / 40.0f);
			for (int i = 0; i < 5; i++)
			{
				float x = CrayonBrush.Noise(i, 51) * bodySize.X * 0.45f;
				float t = ((fxFrames + i * 11) % 40) / 40.0f;
				Vector2 p = new Vector2(x, top.Y + bodySize.Y * (0.25f + 0.7f * t));
				canvas.DrawCircle(p, 5.0f + 2.0f * (1.0f - t), new Color(paintColour, alpha));
				canvas.DrawLine(p - new Vector2(0.0f, 14.0f), p, new Color(paintColour, alpha * 0.8f), 4.0f, true);
			}
		}

		// Glue: white strands stuck to his feet.
		if (glueFrames > 0)
		{
			for (int i = 0; i < 4; i++)
			{
				float x = (i - 1.5f) * bodySize.X * 0.18f;
				float stretch = 10.0f + 6.0f * Mathf.Sin(fxFrames * 0.2f + i);
				canvas.DrawLine(feet + new Vector2(x, -stretch), feet + new Vector2(x + 3.0f, 6.0f), new Color(1, 1, 1, 0.85f), 4.0f, true);
			}
		}

		// Cut out: a dashed cutting line round him while he flutters.
		if (cutoutFrames > 0)
		{
			Rect2 r = new Rect2(-bodySize * 0.55f, bodySize * 1.1f);
			CrayonBrush.InkRect(canvas, r, new Color(0.19f, 0.31f, 0.61f, 0.7f), 3.0f, 9, 2.0f, true);
		}

		// The stamp's mark: a gold star printed on him.
		if (stampMarkFrames > 0)
		{
			float a = Mathf.Min(1.0f, stampMarkFrames / 30.0f);
			Item.DrawStarOn(canvas, new Vector2(0.0f, -bodySize.Y * 0.05f), 22.0f, new Color(0.95f, 0.35f, 0.3f, 0.85f * a));
		}

		// The shield bouncing something back, or taking a hit: a flash where it struck the foil.
		if (reflectFlashFrames > 0)
		{
			Vector2 at = canvas is Node2D n ? n.ToLocal(reflectAt) : Vector2.Zero;
			float t = 1.0f - reflectFlashFrames / 12.0f;
			canvas.DrawArc(at, 20.0f + 40.0f * t, 0.0f, Mathf.Tau, 20, new Color(0.6f, 0.85f, 1.0f, 1.0f - t), 5.0f, true);
		}

		// Eraser crumbs where something was rubbed out.
		foreach ((Vector2 at, int frame) in crumbs)
		{
			Vector2 local = canvas is Node2D n ? n.ToLocal(at) : Vector2.Zero;
			float t = (fxFrames - frame) / 24.0f;
			for (int i = 0; i < 7; i++)
			{
				Vector2 p = local + new Vector2(CrayonBrush.Noise(i, 71) * 30.0f * (1.0f + t), -20.0f * t + 60.0f * t * t + CrayonBrush.Noise(i, 72) * 12.0f);
				canvas.DrawRect(new Rect2(p, new Vector2(6.0f, 4.0f)), new Color(0.95f, 0.62f, 0.72f, 1.0f - t));
			}
		}

		// The stamp's star printed on the floor, flat, with a splash of ink as it comes down.
		foreach ((Vector2 at, int frame) in prints)
		{
			Vector2 local = canvas is Node2D n ? n.ToLocal(at) : Vector2.Zero;
			int age = fxFrames - frame;
			float fade = Mathf.Clamp((PrintFrames - age) / 30.0f, 0.0f, 1.0f);
			canvas.DrawSetTransform(local, 0.0f, new Vector2(1.0f, 0.28f));
			Item.DrawStarOn(canvas, Vector2.Zero, 40.0f, new Color(0.86f, 0.3f, 0.32f, 0.85f * fade));
			canvas.DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
			if (age >= 12) continue;
			float t = age / 12.0f;
			for (int i = 0; i < 6; i++)
			{
				float side = (i - 2.5f) / 2.5f;
				Vector2 p = local + new Vector2(side * 70.0f * t, -50.0f * t * (1.0f - Mathf.Abs(side) * 0.5f) + 40.0f * t * t);
				canvas.DrawCircle(p, 6.0f * (1.0f - t) + 2.0f, new Color(0.86f, 0.3f, 0.32f, 1.0f - t));
			}
		}

		// The airbrush's gust and the spray can's mist, out of the nozzle.
		if (heldItem != null && State == FighterState.Attacking && canvas is Node2D node)
		{
			Vector2 nozzle = node.ToLocal(heldItem.HeadGlobal());
			bool active = moveFrame > currentMove.StartupFrames && moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;

			// The paint tube: a bead of paint swelling at the nozzle as it is squeezed, trembling
			// once it is as big as it gets.
			if (currentMove.ItemAction == ItemAction.Squeeze && heldItem.Data.Shot != null && moveFrame <= currentMove.StartupFrames + 1)
			{
				Color paint = heldItem.Data.Shot.FxColor;
				float r = 5.0f + 24.0f * ItemCharge;
				Vector2 shake = ItemCharge >= 1.0f ? new Vector2(CrayonBrush.Noise(fxFrames, 83), CrayonBrush.Noise(fxFrames, 84)) * 3.0f : Vector2.Zero;
				Vector2 bead = nozzle + new Vector2(Facing * r * 0.7f, 0.0f) + shake;
				canvas.DrawCircle(bead, r + 3.0f, paint.Darkened(0.3f));
				canvas.DrawCircle(bead, r, paint);
				canvas.DrawCircle(bead + new Vector2(-Facing * r * 0.3f, -r * 0.35f), r * 0.28f, new Color(1, 1, 1, 0.75f));
			}
			if (active && currentMove.ItemAction == ItemAction.Gust)
			{
				// Comic wind lines streaming out, white edged in blue so they read on any sky,
				// each curling up at its tail.
				Color edge = heldItem.Data.Tint.Darkened(0.35f);
				for (int i = 0; i < 8; i++)
				{
					float t = ((fxFrames * 3 + i * 13) % 40) / 40.0f;
					float y = (i - 3.5f) * 20.0f;
					float alpha = Mathf.Min(1.0f, (1.0f - t) * 1.6f);
					Vector2 a = nozzle + new Vector2(Facing * heldItem.Data.Radius * t, y * (0.4f + t));
					Vector2 b = a + new Vector2(Facing * 80.0f, 0.0f);
					Vector2 curl = b + new Vector2(Facing * 14.0f, -12.0f);
					canvas.DrawPolyline(new[] { a, b, curl }, new Color(edge, 0.8f * alpha), 8.0f, true);
					canvas.DrawPolyline(new[] { a, b, curl }, new Color(1, 1, 1, alpha), 3.5f, true);
				}
			}
			if (active && heldItem.Data.Use == ItemUse.Spray && currentMove == heldItem.Data.Swing)
			{
				for (int i = 0; i < 28; i++)
				{
					float t = ((fxFrames * 2 + i * 5) % 24) / 24.0f;
					float spread = CrayonBrush.Noise(i, 81) * 0.4f;
					Vector2 dir = new Vector2(Facing, 0.0f).Rotated(spread);
					canvas.DrawCircle(nozzle + dir * 200.0f * t, 5.0f + 11.0f * t, new Color(heldItem.Data.Tint, 0.8f * (1.0f - t * t)));
				}
			}
		}
	}

	/// <summary>
	/// Holding someone: four little arrows round him, the ways he can throw them, so it is plain
	/// the stick picks one. They shrink as the hold runs out.
	/// </summary>
	void DrawThrowArrows(CanvasItem canvas)
	{
		float left = grabHoldFrames / (float)GrabHoldFrames;
		float pulse = 0.85f + 0.15f * Mathf.Sin(fxFrames * 0.4f);
		Vector2 centre = new Vector2(0.0f, -bodySize.Y * 0.1f);
		float radius = bodySize.Y * 0.95f;
		Color ink = new Color(0.19f, 0.31f, 0.61f, 0.5f + 0.5f * left);
		foreach (Vector2 way in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
		{
			Vector2 tip = centre + way * radius * pulse;
			Vector2 back = tip - way * 26.0f;
			Vector2 side = way.Orthogonal() * 14.0f;
			canvas.DrawColoredPolygon(new[] { tip, back + side, back - side }, new Color(1, 1, 1, 0.9f));
			canvas.DrawPolyline(new[] { tip, back + side, back - side, tip }, ink, 3.0f, true);
		}
	}

	/// <summary>
	/// Over his head while he holds something with a limited number of uses: a dot for each one
	/// left, pale while it is still cooling down from the last - so how much is left, and when it
	/// is ready again, can be seen at a glance.
	/// </summary>
	void DrawItemUses(CanvasItem canvas)
	{
		if (heldItem == null || State == FighterState.Eliminated) return;
		int left = heldItem.SwingsLeft >= 0 ? heldItem.SwingsLeft
			: heldItem.Data.Uses > 0 && heldItem.Data.Uses <= 10 ? heldItem.UsesLeft : -1;
		if (left < 0) return;
		bool ready = itemCooldownFrames <= 0;
		const float Gap = 14.0f;
		var start = new Vector2(-(left - 1) * Gap * 0.5f, -bodySize.Y * 0.5f - 34.0f);
		for (int i = 0; i < left; i++)
		{
			Vector2 at = start + new Vector2(i * Gap, 0.0f);
			canvas.DrawCircle(at, 6.5f, new Color(0.19f, 0.31f, 0.61f, ready ? 0.9f : 0.35f));
			canvas.DrawCircle(at, 4.0f, ready ? heldItem.Data.Tint.Lerp(Colors.White, 0.2f) : new Color(1, 1, 1, 0.6f));
		}
	}

	static void DrawPlus(CanvasItem canvas, Vector2 at, float size, Color colour)
	{
		if (size < 0.5f) return;
		canvas.DrawLine(at - new Vector2(size, 0.0f), at + new Vector2(size, 0.0f), colour, size * 0.6f, true);
		canvas.DrawLine(at - new Vector2(0.0f, size), at + new Vector2(0.0f, size), colour, size * 0.6f, true);
	}
}
