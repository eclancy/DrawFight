using System.Collections.Generic;
using Godot;

/// <summary>
/// One item on the stage, from the moment it drops in until it is used up. It falls in from
/// above with a trail of sparkles, lies on the floor glinting until someone picks it up, rides
/// in their hand, flies when thrown, and - for most - turns into what it does when it lands:
/// a splash of paint, a cloud of ink, a puddle, a blast of glitter, a trap, a spring, a drawing
/// that climbs out of a canvas. What it does is <see cref="ItemData"/>; this is where it does it.
///
/// Everything an item does to a fighter is a hit (<see cref="MoveData"/>) through the fighter's
/// own damage pipeline, or one of a few plain effects on him (slipping, sticking, being sprung
/// up) - so items obey blocking, the knockback formula and hitlag like any attack.
///
/// Every effect is drawn here in the art direction's terms: paint and paper colours, never near
/// black, outlined in the stage's indigo ink because an outline means a real thing. Every
/// wobble and flicker is hashed from a frame count, never random.
/// </summary>
public partial class Item : CharacterBody2D
{
	public enum Phase { Falling, Resting, Held, Thrown, Effect, Summon, Gone }

	public ItemData Data { get; private set; }
	public ItemArt Art { get; private set; }
	public Phase State { get; private set; } = Phase.Falling;

	/// <summary>Who is holding it, while it is held.</summary>
	public Fighter Holder { get; private set; }

	/// <summary>Who threw it or set it off - its hits are theirs, and its splashes spare them.</summary>
	public Fighter OwnerFighter { get; private set; }

	/// <summary>Uses left: swings, frames of spray, shots, gusts, platforms. See ItemData.Uses.</summary>
	public int UsesLeft;

	/// <summary>Uses left before it is spent (ItemData.Swings); -1 for no limit.</summary>
	public int SwingsLeft = -1;

	MatchManager match;
	int age;
	int phaseFrames;
	int restFrames;
	float spin;
	int bounces;
	bool hitSomeone;
	readonly Dictionary<Fighter, int> lastHitAt = new Dictionary<Fighter, int>();

	/// <summary>Where an effect happened, and the floor it lies on (NaN if none).</summary>
	Vector2 effectAt;
	float floorY = float.NaN;

	// The canvas's drawing.
	FighterRig figure;
	Vector2 canvasAt;
	int figureFacing = 1;
	int figureSwing;

	/// <summary>How long an item lies about before it is gone, and how long it blinks first.</summary>
	const int RestLifetime = 1200;
	const int BlinkFrames = 180;

	/// <summary>How long a glitter bomb lies about before it goes off by itself.</summary>
	const int FuseFrames = 480;

	const int OwnerGraceFrames = 12;

	static readonly Color Ink = new Color(0.19f, 0.31f, 0.61f);
	static readonly Color Paper = new Color(0.98f, 0.97f, 0.94f);

	float Radius => Data.Size * 0.3f;
	float ArtScale => Art != null ? Data.Size * (State == Phase.Held ? Extension : 1.0f) / Mathf.Max(Art.Size.X, Art.Size.Y) : 1.0f;

	// --- The metre stick measuring him up ------------------------------------------------
	// Picked up, it does not go straight into his hand: it stands on the floor beside him and
	// shoots up out of it, taller than he is, while he grows to match - then it is in his fist,
	// longer than it lay (Eric, 2026-10-15).

	/// <summary>How much longer than its lying-down size it is in his hand.</summary>
	public float Extension = 1.0f;

	/// <summary>Frames left of standing beside him, rising; 0 once it is in his hand.</summary>
	public int MeasureFrames;

	public const int MeasureTotal = 36;
	const int MeasureRise = 24;

	public void StartMeasuring()
	{
		MeasureFrames = MeasureTotal;
		Extension = 1.4f;
	}

	/// <summary>Where it stands while it measures him: on the floor at his side.</summary>
	public void StandAt(Vector2 floorPoint, int facing)
	{
		GlobalPosition = floorPoint;
		heldFacing = facing;
		QueueRedraw();
	}

	void DrawMeasuring()
	{
		if (Art == null) return;
		float s = ArtScale;
		int into = MeasureTotal - MeasureFrames;
		// Out of the floor fast, easing to a stop at full height.
		float t = Mathf.Min(1.0f, into / (float)MeasureRise);
		float shown = 1.0f - Mathf.Pow(1.0f - t, 3.0f);
		// The top of the stick comes up first, as if pushed up from under the floor.
		var source = new Rect2(0.0f, 0.0f, Art.Size.X, Art.Size.Y * shown);
		var place = new Rect2(-Art.Size.X * s * 0.5f, -Art.Size.Y * s * shown, Art.Size.X * s, Art.Size.Y * s * shown);
		DrawTextureRectRegion(Art.Texture, place, source);
		// A sparkle at its top as it reaches full height.
		if (into >= MeasureRise - 2 && into < MeasureRise + 10)
		{
			float k = (into - (MeasureRise - 2)) / 12.0f;
			DrawStar(new Vector2(0.0f, -Art.Size.Y * s - 10.0f), 22.0f * Mathf.Sin(k * Mathf.Pi), new Color(1.0f, 0.9f, 0.4f, 1.0f - k * 0.5f));
		}
	}

	public bool CanBePickedUp => State == Phase.Resting || State == Phase.Falling && age > 20;

	/// <summary>Dropped onto the stage from <paramref name="position"/>, as a new item or spilled from a pencil case.</summary>
	public void Launch(ItemData data, MatchManager match, Vector2 position, Vector2 velocity)
	{
		Data = data;
		Art = ItemCatalog.Art(data.Id);
		this.match = match;
		UsesLeft = data.Uses;
		SwingsLeft = data.Swings > 0 ? data.Swings : -1;
		GlobalPosition = position;
		Velocity = velocity;

		// Items fall onto anything that can be stood on, and never block a fighter.
		CollisionLayer = 0;
		CollisionMask = 0b11;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
		State = Phase.Falling;
	}

	/// <summary>A stripe of wet paint the roller left on the floor: only a slippery puddle.</summary>
	bool stripe;
	const float StripeHalfWidth = 40.0f;

	public void StartStripe(ItemData data, MatchManager match, Fighter owner, Vector2 floorPoint)
	{
		Data = data;
		this.match = match;
		OwnerFighter = owner;
		CollisionLayer = 0;
		CollisionMask = 0;
		stripe = true;
		GlobalPosition = floorPoint;
		effectAt = floorPoint;
		floorY = floorPoint.Y;
		State = Phase.Effect;
		phaseFrames = 0;
	}

	// --- Being carried and thrown -------------------------------------------------------

	/// <summary>The layer in its holder's arm that draws it while it is held (FighterRig.HeldLayer), if any.</summary>
	public Node2D DrawnBy;

	public void PickedUp(Fighter by)
	{
		Holder = by;
		OwnerFighter = by;
		State = Phase.Held;
		phaseFrames = 0;
		Velocity = Vector2.Zero;
	}

	/// <summary>Where it is in its holder's hand: the fist, the way the forearm points, and which way he faces.</summary>
	Vector2 heldDirection = Vector2.Up;
	int heldFacing = 1;

	public void HoldAt(Vector2 fist, Vector2 along, int facing)
	{
		GlobalPosition = fist;
		heldDirection = along.LengthSquared() > 0.01f ? along.Normalized() : Vector2.Up;
		heldFacing = facing;
		QueueRedraw();
	}

	/// <summary>
	/// Carried rather than wielded - a bucket, a bottle, a book held up as a shield: it stays
	/// upright in his fist whichever way his forearm points. Only a tool points along the arm;
	/// a paint bucket that did came out lying on its side.
	/// </summary>
	bool CarriedUpright => Holder != null && (Data.Use is ItemUse.Throw or ItemUse.Consume or ItemUse.Open
		|| Data.Use == ItemUse.Shield && !Holder.SwingingHeldItem);

	/// <summary>A rubber stamp hangs face down from the fist, whatever the arm does - ready to stamp.</summary>
	bool HangsDown => Holder != null && Data.Use == ItemUse.Stamp;

	Vector2 HeldUp => HangsDown ? Vector2.Down : CarriedUpright ? Vector2.Up : heldDirection;

	/// <summary>Where its business end is right now, held: a swing hits here.</summary>
	public Vector2 HeadGlobal()
	{
		if (Art == null) return GlobalPosition + HeldUp * Data.Size * 0.8f;
		return GlobalPosition + HeldUp * (Art.Grip.Y - Art.Head.Y) * ArtScale;
	}

	/// <summary>The lowest point of its drawing, held in the fist and pointing <paramref name="along"/>.</summary>
	public float LowestHeldY(Vector2 along)
	{
		if (Art == null) return HeadGlobal().Y;
		Vector2 up = HangsDown ? Vector2.Down : CarriedUpright ? Vector2.Up : along.Normalized();
		float s = ArtScale;
		var place = new Transform2D(up.Angle() + Mathf.Pi * 0.5f, new Vector2(heldFacing * s, s), 0.0f, GlobalPosition);
		float lowest = float.NegativeInfinity;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(Art.Size.X, 0.0f), new Vector2(0.0f, Art.Size.Y), Art.Size })
		{
			lowest = Mathf.Max(lowest, (place * (corner - Art.Grip)).Y);
		}
		return lowest;
	}

	/// <summary>Out of his hand and away, as thrown - or let go of, with no speed, when he is hit.</summary>
	public void Thrown(Fighter by, Vector2 velocity)
	{
		Extension = 1.0f;
		MeasureFrames = 0;
		Holder = null;
		DrawnBy = null;
		OwnerFighter = by;
		Velocity = velocity;
		State = Phase.Thrown;
		phaseFrames = 0;
		hitSomeone = false;
		flewPast = false;
		lastHitAt.Clear();
		// Out of the fighter's hand: drawn in front of everyone again.
		GetParent()?.MoveChild(this, -1);
	}

	public void Dropped()
	{
		Extension = 1.0f;
		MeasureFrames = 0;
		Holder = null;
		DrawnBy = null;
		Velocity = new Vector2(0.0f, -300.0f);
		State = Phase.Falling;
		phaseFrames = 0;
		GetParent()?.MoveChild(this, -1);
	}

	/// <summary>Used up in his hand - a consumed boost, or a swing item worn out.</summary>
	public void UsedUp()
	{
		Holder = null;
		Poof();
	}

	/// <summary>The pencil case, unzipped: it sits open a moment, lid flipped back, and goes.</summary>
	bool opened;
	const int OpenCaseFrames = 26;

	public void SpillOpen()
	{
		opened = true;
		State = Phase.Effect;
		phaseFrames = 0;
		effectAt = GlobalPosition;
		Velocity = Vector2.Zero;
	}

	/// <summary>
	/// Caught in an airbrush's gust: lying on the floor it is picked up and carried off, and in
	/// the air it is pushed along with the wind - turned right round, it is the blower's now.
	/// </summary>
	public void Blow(Fighter by, float wind)
	{
		switch (State)
		{
			case Phase.Resting:
				State = Phase.Falling;
				phaseFrames = 0;
				Velocity = new Vector2(wind * 0.6f, -260.0f);
				break;
			case Phase.Falling:
				Velocity = new Vector2(Mathf.MoveToward(Velocity.X, wind * 0.8f, Mathf.Abs(wind) * 0.12f), Velocity.Y);
				break;
			case Phase.Thrown:
				bool against = Velocity.X * wind < 0.0f;
				Velocity = new Vector2(Mathf.MoveToward(Velocity.X, wind, Mathf.Abs(wind) * 0.12f), Velocity.Y);
				if (against && Velocity.X * wind > 0.0f)
				{
					OwnerFighter = by;
					lastHitAt.Clear();
				}
				break;
		}
	}

	/// <summary>Rubbed out by the eraser.</summary>
	public void Erase()
	{
		if (State == Phase.Held || State == Phase.Summon) return;
		Poof();
	}

	public bool CanBeErased(Fighter by) => (State == Phase.Thrown || State == Phase.Effect) && OwnerFighter != by;

	/// <summary>Gone in a little puff of paper dust.</summary>
	public void Poof()
	{
		if (State == Phase.Gone) return;
		match?.Dust(GlobalPosition, Data.Size / 120.0f, 0.0f);
		State = Phase.Gone;
		QueueFree();
	}

	// --- Ticking ----------------------------------------------------------------------

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		age++;
		phaseFrames++;

		switch (State)
		{
			case Phase.Falling: TickFalling(dt); break;
			case Phase.Resting: TickResting(dt); break;
			case Phase.Held: if (MeasureFrames > 0) MeasureFrames--; break;
			case Phase.Thrown: TickThrown(dt); break;
			case Phase.Effect: TickEffect(dt); break;
			case Phase.Summon: TickSummon(dt); break;
		}

		if (State != Phase.Gone && State != Phase.Held && match != null
			&& !match.StageBounds.Grow(300.0f).HasPoint(GlobalPosition))
		{
			State = Phase.Gone;
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	void TickFalling(float dt)
	{
		Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0.0f, 600.0f * dt), Mathf.Min(Velocity.Y + Data.Gravity * dt, 1400.0f));
		MoveAndSlide();
		if (IsOnFloor())
		{
			match?.Dust(GlobalPosition + new Vector2(0.0f, Radius), Data.Size / 160.0f, 0.0f);
			State = Phase.Resting;
			phaseFrames = 0;
			restFrames = 0;
			Velocity = Vector2.Zero;
		}
		TouchCollect();
	}

	void TickResting(float dt)
	{
		restFrames++;

		// The gold star never sits still: it hops about, the way a Super Star does.
		if (Data.TouchToCollect)
		{
			int dir = CrayonBrush.Noise(age / 40, 7) > 0.0f ? 1 : -1;
			Velocity = new Vector2(dir * 240.0f, -950.0f);
			State = Phase.Falling;
			TouchCollect();
			return;
		}

		// Still on the floor? It may have been standing on a girder that went.
		Velocity = new Vector2(0.0f, Velocity.Y + Data.Gravity * dt);
		MoveAndSlide();
		if (!IsOnFloor()) { State = Phase.Falling; return; }

		if (Data.Impact == ItemImpact.Blast && restFrames > FuseFrames)
		{
			// The fuse has burned down: it goes off by itself, and hits everyone.
			OwnerFighter = null;
			StartEffect(GlobalPosition);
			return;
		}
		if (restFrames > RestLifetime) Poof();
	}

	void TouchCollect()
	{
		if (!Data.TouchToCollect || match == null || age < 20) return;
		foreach (Fighter f in match.Fighters)
		{
			if (!f.IsInPlay || !f.BodyRect().Grow(Radius).HasPoint(GlobalPosition)) continue;
			f.Collect(this);
			return;
		}
	}

	// --- Flying -------------------------------------------------------------------------

	void TickThrown(float dt)
	{
		switch (Data.Impact)
		{
			case ItemImpact.Glide: SteerGlide(dt); break;
			case ItemImpact.Boomerang: SteerBoomerang(dt); break;
			default: Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y + Data.Gravity * dt, 1600.0f)); break;
		}

		spin += SpinRate() * Mathf.Sign(Velocity.X == 0.0f ? 1.0f : Velocity.X);

		KinematicCollision2D bump = MoveAndCollide(Velocity * dt);
		if (bump != null && State == Phase.Thrown) Bumped(bump);

		if (State == Phase.Thrown) HitFighters();

		// The crayon box pops its lid a moment out of his hand, and the crayons dart on from there.
		if (State == Phase.Thrown && Data.Impact == ItemImpact.Crayons && phaseFrames == CrayonPopFrames) StartEffect(GlobalPosition);

		if (State == Phase.Thrown && Data.Impact == ItemImpact.Glide && phaseFrames > Data.Duration) Poof();
		if (State == Phase.Thrown && Data.Impact == ItemImpact.Bounce && (phaseFrames > Data.Duration || bounces > 8)) Poof();
	}

	const int CrayonPopFrames = 9;

	float SpinRate() => Data.Impact switch
	{
		ItemImpact.Glide => 0.0f,
		ItemImpact.Boomerang => 0.5f,
		ItemImpact.Bounce => 0.28f,
		_ => 0.16f,
	};

	/// <summary>The paper airplane: steady speed, turning gently toward the nearest opponent.</summary>
	void SteerGlide(float dt)
	{
		Fighter target = NearestOpponent(GlobalPosition);
		Vector2 heading = Velocity.Normalized();
		if (target != null)
		{
			Vector2 want = (target.GlobalPosition - GlobalPosition).Normalized();
			float turn = Mathf.Clamp(heading.AngleTo(want), -1.6f * dt, 1.6f * dt);
			heading = heading.Rotated(turn);
		}
		Velocity = heading * Data.ThrowSpeed + new Vector2(0.0f, Data.Gravity * dt * phaseFrames * 0.1f);
	}

	/// <summary>The palette: out, slowing to a stop, then back to whoever threw it - who has to catch it.</summary>
	void SteerBoomerang(float dt)
	{
		int outFrames = Data.Duration / 3;
		if (phaseFrames < outFrames)
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, Data.ThrowSpeed / outFrames * 60.0f * dt);
			return;
		}
		if (phaseFrames > Data.Duration || OwnerFighter == null || !OwnerFighter.IsInPlay)
		{
			Velocity = new Vector2(Velocity.X * 0.9f, Velocity.Y + Data.Gravity * dt);
			return;
		}
		// Back to him - and past him, if he does not catch it. Catching is pressing attack (or grab)
		// as it comes in (Fighter.TryItemInput); miss, and it flies on by and drops (Eric,
		// 2026-10-11: it caught itself).
		if (flewPast)
		{
			Velocity = new Vector2(Velocity.X * 0.97f, Velocity.Y + Data.Gravity * 0.5f * dt);
			return;
		}
		Vector2 home = OwnerFighter.GlobalPosition + new Vector2(0.0f, -20.0f);
		Vector2 want = (home - GlobalPosition).Normalized() * Data.ThrowSpeed;
		Velocity = Velocity.MoveToward(want, 3200.0f * dt);
		if (GlobalPosition.DistanceTo(home) < 50.0f) flewPast = true;
	}

	bool flewPast;

	/// <summary>On its way back to whoever threw it, and close enough for them to catch.</summary>
	public bool CatchableBy(Fighter f, float reach) =>
		State == Phase.Thrown && Data.Impact == ItemImpact.Boomerang && OwnerFighter == f
		&& phaseFrames >= Data.Duration / 3 && GlobalPosition.DistanceTo(f.GlobalPosition) < reach;

	/// <summary>It ran into the stage: the floor, a wall or a ceiling.</summary>
	void Bumped(KinematicCollision2D bump)
	{
		Vector2 normal = bump.GetNormal();
		bool floor = normal.Y < -0.6f;

		switch (Data.Impact)
		{
			case ItemImpact.Bounce:
				Velocity = Velocity.Bounce(normal) * (floor ? 0.78f : 0.85f);
				if (floor && Mathf.Abs(Velocity.Y) < 260.0f) Velocity = new Vector2(Velocity.X, -520.0f);
				bounces++;
				SfxPlayer.At("item_crumple", GlobalPosition, 0.1f);
				return;
			case ItemImpact.Boomerang when !floor:
				Velocity = Velocity.Bounce(normal) * 0.6f;
				return;
			case ItemImpact.Glide:
				Poof();
				return;
		}

		if (!floor)
		{
			// Off a wall it just drops.
			Velocity = new Vector2(-Velocity.X * 0.25f, Mathf.Max(Velocity.Y, 0.0f));
			return;
		}

		switch (Data.Impact)
		{
			case ItemImpact.None:
			case ItemImpact.Boomerang:
				if (Velocity.Length() > 500.0f)
				{
					Velocity = new Vector2(Velocity.X * 0.4f, -Mathf.Abs(Velocity.Y) * 0.3f);
					return;
				}
				State = Phase.Resting;
				restFrames = 0;
				phaseFrames = 0;
				Velocity = Vector2.Zero;
				return;
			default:
				StartEffect(GlobalPosition);
				return;
		}
	}

	void HitFighters()
	{
		if (match == null) return;
		foreach (Fighter f in match.Fighters)
		{
			if (!f.IsInPlay || !f.CanBeHitByHazard || match.Allies(OwnerFighter, f)) continue;
			if (f == OwnerFighter && (phaseFrames < OwnerGraceFrames || Data.Impact == ItemImpact.Boomerang)) continue;
			if (lastHitAt.TryGetValue(f, out int when) && age - when < 40) continue;
			if (hitSomeone && Data.Impact == ItemImpact.None) continue;
			Rect2 body = f.BodyRect();
			if (!body.Grow(Radius).HasPoint(GlobalPosition)) continue;

			// A shield held up toward it sends it straight back the way it came.
			if (f.ReflectsFrom(GlobalPosition))
			{
				Velocity = new Vector2(-Velocity.X, Mathf.Min(Velocity.Y, -200.0f));
				OwnerFighter = f;
				phaseFrames = OwnerGraceFrames;
				lastHitAt.Clear();
				f.ShowReflect(GlobalPosition);
				return;
			}

			Vector2 contact = new Vector2(Mathf.Clamp(GlobalPosition.X, body.Position.X, body.End.X),
				Mathf.Clamp(GlobalPosition.Y, body.Position.Y, body.End.Y));
			int dir = Velocity.X < -1.0f ? -1 : Velocity.X > 1.0f ? 1 : (f.GlobalPosition.X >= GlobalPosition.X ? 1 : -1);
			lastHitAt[f] = age;
			hitSomeone = true;

			switch (Data.Impact)
			{
				case ItemImpact.None:
					f.ReceiveItemHit(OwnerFighter, Data.Hit, dir, contact);
					Velocity = new Vector2(-Velocity.X * 0.3f, -500.0f);
					return;
				case ItemImpact.Bounce:
				case ItemImpact.Boomerang:
				case ItemImpact.Glide:
					f.ReceiveItemHit(OwnerFighter, Data.Hit, dir, contact);
					if (Data.Impact == ItemImpact.Glide) Poof();
					return;
				case ItemImpact.Spring:
					f.ReceiveItemHit(OwnerFighter, Data.Hit, dir, contact);
					Velocity = new Vector2(-Velocity.X * 0.2f, 0.0f);
					return;
				default:
					// Burst on whoever it hit - a splash, a blast, the crayons out of the box.
					if (Data.Hit != null && Data.Impact != ItemImpact.Splash && Data.Impact != ItemImpact.Blast
						&& Data.Impact != ItemImpact.Mine)
					{
						f.ReceiveItemHit(OwnerFighter, Data.Hit, dir, contact);
					}
					StartEffect(GlobalPosition);
					// A paint balloon bursts on whoever it hits, rather than settling first.
					if (Data.Impact == ItemImpact.Mine && State == Phase.Effect) PopMine();
					return;
			}
		}
	}

	Fighter NearestOpponent(Vector2 from)
	{
		Fighter best = null;
		float bestDistance = float.MaxValue;
		foreach (Fighter f in match.Fighters)
		{
			if (f == OwnerFighter || !f.IsInPlay || match.Allies(OwnerFighter, f)) continue;
			float d = f.GlobalPosition.DistanceSquaredTo(from);
			if (d < bestDistance) { bestDistance = d; best = f; }
		}
		return best;
	}

	// --- What it does when it lands ------------------------------------------------------

	void StartEffect(Vector2 at)
	{
		// Which way it was going when it landed - the pop-up crocodile faces that way.
		popFacing = Velocity.X < -1.0f ? -1 : Velocity.X > 1.0f ? 1 : OwnerFighter?.Facing ?? 1;
		State = Phase.Effect;
		phaseFrames = 0;
		effectAt = at;
		Velocity = Vector2.Zero;
		floorY = FloorBelow(at, 420.0f);
		lastHitAt.Clear();

		switch (Data.Impact)
		{
			case ItemImpact.Splash:
				Splash(at);
				break;
			case ItemImpact.Blast:
				Blast(at);
				break;
			case ItemImpact.Crayons:
				Crayons(at);
				break;
			case ItemImpact.Ink:
				// The cloud goes over everyone, so the ones in it are hidden.
				ZIndex = 20;
				SfxPlayer.At("splash", at, 0.1f);
				break;
			case ItemImpact.Glue:
			case ItemImpact.Spill:
			case ItemImpact.Tacks:
				SfxPlayer.At(Data.Impact == ItemImpact.Tacks ? "creak" : "splash", at, 0.1f);
				if (float.IsNaN(floorY)) { Poof(); return; }
				break;
			case ItemImpact.Mine:
			case ItemImpact.Spring:
				if (float.IsNaN(floorY)) { if (Data.Impact == ItemImpact.Spring) { Poof(); return; } break; }
				GlobalPosition = new Vector2(at.X, floorY - Radius);
				if (Data.Impact == ItemImpact.Spring)
				{
					popUp = (PopUp)(Mathf.Abs((int)(CrayonBrush.Noise(age, (int)(GetInstanceId() % 9973)) * 3000.0f)) % 3);
					SfxPlayer.At("boing", at, 0.1f);
				}
				break;
		}
	}

	/// <summary>The floor under a point, within reach - NaN if there is none.</summary>
	float FloorBelow(Vector2 at, float reach)
	{
		var query = new PhysicsRayQueryParameters2D
		{
			From = at + new Vector2(0.0f, -10.0f),
			To = at + new Vector2(0.0f, reach),
			CollisionMask = 0b11,
		};
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? ((Vector2)hit["position"]).Y : float.NaN;
	}

	/// <summary>A splash of paint: everyone in reach hit and painted, and a puddle on the floor below.</summary>
	void Splash(Vector2 at)
	{
		SfxPlayer.At("splash", at, 0.05f);
		match?.Shake(18.0f);
		foreach (Fighter f in InReach(at, Data.Radius, OwnerFighter))
		{
			f.ReceiveItemHit(OwnerFighter, Data.Hit, f.GlobalPosition.X >= at.X ? 1 : -1, f.GlobalPosition);
			f.Paint(Data.Tint, 180);
		}
	}

	void Blast(Vector2 at)
	{
		// Its own burst, not the stage's orange explosion flash: glitter is the whole point.
		SfxPlayer.At("explosion", at, 0.03f);
		match?.Shake(50.0f);
		foreach (Fighter f in InReach(at, Data.Radius, null))
		{
			f.ReceiveItemHit(OwnerFighter, Data.Hit, f.GlobalPosition.X >= at.X ? 1 : -1, f.GlobalPosition);
		}
	}

	void Crayons(Vector2 at)
	{
		SfxPlayer.At("special_volley", at, 0.05f);
		if (match == null || OwnerFighter == null || Data.Shot == null) return;
		int dir = Velocity.X < 0.0f ? -1 : Velocity.X > 0.0f ? 1 : OwnerFighter.Facing;
		for (int i = 0; i < ItemCatalog.Crayons.Length; i++)
		{
			float angle = Mathf.DegToRad(-30.0f + 15.0f * i);
			Vector2 v = new Vector2(dir, 0.0f).Rotated(angle * dir) * Data.Shot.SpecialSpeed;
			MoveData crayon = Data.Shot.Scaled(new MoveScale(1, 1, 1, 1, 1, 1));
			crayon.FxTexture = ItemCatalog.Art(ItemCatalog.Crayons[i])?.Texture;
			Hazard h = match.SpawnHazard(OwnerFighter, crayon, at, v);
			h.FlipArt = dir < 0;
		}
	}

	List<Fighter> InReach(Vector2 at, float radius, Fighter spare)
	{
		var found = new List<Fighter>();
		if (match == null) return found;
		foreach (Fighter f in match.Fighters)
		{
			if (f == spare || !f.IsInPlay || !f.CanBeHitByHazard || match.Allies(spare, f)) continue;
			Rect2 body = f.BodyRect();
			Vector2 nearest = new Vector2(Mathf.Clamp(at.X, body.Position.X, body.End.X), Mathf.Clamp(at.Y, body.Position.Y, body.End.Y));
			if (nearest.DistanceTo(at) <= radius) found.Add(f);
		}
		return found;
	}

	/// <summary>The stretch of floor a puddle or a scatter of tacks lies on.</summary>
	Rect2 FloorPatch(float halfWidth) => new Rect2(effectAt.X - halfWidth, floorY - 14.0f, halfWidth * 2.0f, 28.0f);

	void TickEffect(float dt)
	{
		if (stripe)
		{
			foreach (Fighter f in OnPatch(FloorPatch(StripeHalfWidth))) if (f != OwnerFighter) f.Slip();
			if (phaseFrames > Data.Duration) { State = Phase.Gone; QueueFree(); }
			return;
		}
		if (opened)
		{
			if (phaseFrames > OpenCaseFrames) Poof();
			return;
		}
		int duration = Data.Duration;
		switch (Data.Impact)
		{
			case ItemImpact.Splash:
				if (!float.IsNaN(floorY)) foreach (Fighter f in OnPatch(FloorPatch(Data.Radius * 0.8f))) f.Slip();
				break;
			case ItemImpact.Glue:
				foreach (Fighter f in OnPatch(FloorPatch(Data.Radius))) f.Glue();
				break;
			case ItemImpact.Spill:
				foreach (Fighter f in OnPatch(FloorPatch(Data.Radius)))
				{
					if (Mathf.Abs(f.Velocity.X) < 150.0f || Cooling(f, 90)) continue;
					lastHitAt[f] = age;
					f.ReceiveItemHit(OwnerFighter, Data.Hit, f.Velocity.X >= 0.0f ? 1 : -1, f.GlobalPosition);
				}
				break;
			case ItemImpact.Tacks:
				foreach (Fighter f in OnPatch(FloorPatch(Data.Radius)))
				{
					if (Mathf.Abs(f.Velocity.X) < 60.0f || Cooling(f, 45)) continue;
					lastHitAt[f] = age;
					f.ReceiveItemHit(OwnerFighter, Data.Hit, f.Velocity.X >= 0.0f ? 1 : -1, f.GlobalPosition);
					SfxPlayer.At("creak", f.GlobalPosition, 0.1f);
				}
				break;
			case ItemImpact.Mine:
				TickMine();
				break;
			case ItemImpact.Spring:
				TickSpring();
				break;
			case ItemImpact.Blast:
				duration = GlitterFrames;
				break;
			case ItemImpact.Crayons:
				duration = 40;
				break;
		}
		if (phaseFrames > duration) Poof();
	}

	bool Cooling(Fighter f, int frames) => lastHitAt.TryGetValue(f, out int when) && age - when < frames;

	IEnumerable<Fighter> OnPatch(Rect2 patch)
	{
		if (match == null || float.IsNaN(floorY)) yield break;
		foreach (Fighter f in match.Fighters)
		{
			if (!f.IsInPlay || !f.IsOnFloor()) continue;
			Vector2 feet = f.Feet;
			if (patch.HasPoint(feet)) yield return f;
		}
	}

	/// <summary>The paint balloon: sits, wobbling, until anyone comes close - then bursts.</summary>
	void TickMine()
	{
		const int ArmFrames = 24;
		if (poppedMine)
		{
			// Spent: what is left is the puddle it splashed.
			if (!float.IsNaN(floorY)) foreach (Fighter f in OnPatch(FloorPatch(Data.Radius * 0.8f))) f.Slip();
			if (phaseFrames > 180) Poof();
			return;
		}
		if (phaseFrames < ArmFrames) return;
		bool burst = phaseFrames > Data.Duration - 2;
		foreach (Fighter f in match.Fighters)
		{
			if (!f.IsInPlay) continue;
			if (f == OwnerFighter && phaseFrames < 150) continue;
			if (f.BodyRect().Grow(50.0f).HasPoint(GlobalPosition)) burst = true;
		}
		if (burst) PopMine();
	}

	/// <summary>It is spent: a splash where it sat, and a puddle on the floor.</summary>
	void PopMine()
	{
		effectAt = GlobalPosition;
		floorY = FloorBelow(GlobalPosition, 420.0f);
		Splash(GlobalPosition);
		SfxPlayer.At("minibot_pop", GlobalPosition, 0.05f);
		phaseFrames = 0;
		poppedMine = true;
	}

	bool poppedMine;

	// --- The pop-up book ----------------------------------------------------------------
	// Opened on the floor, it is one of three pop-ups, much bigger than the book (Eric,
	// 2026-10-15): a bouncy castle that springs whoever stands on it sky high, a crocodile that
	// snaps at whoever is in front of it, or a storm cloud that zaps whoever is under it. The
	// one who threw it is spared by the crocodile and the cloud.

	enum PopUp { Castle, Crocodile, Cloud }

	PopUp popUp;
	int popFacing = 1;
	int sprungAt = -100;
	int snapAt = -100;
	int zapAt = -100;
	Vector2 zapTo;

	const int OpenFrames = 14;

	/// <summary>How big a pop-up stands, against the book's own size.</summary>
	const float PopUpGrowth = 3.6f;

	/// <summary>The crocodile snaps every so often, its jaw opening wide first.</summary>
	const int SnapEvery = 60;
	const int SnapOpening = 22;

	/// <summary>The cloud strikes this often.</summary>
	const int ZapEvery = 45;

	static readonly MoveData CrocodileSnap = new MoveData
	{
		MoveName = "Pop-Up Crocodile", Damage = 12.0f, BaseKnockback = 32.0f, KnockbackGrowth = 0.85f, LaunchAngleDegrees = 45.0f,
	};

	static readonly MoveData CloudZap = new MoveData
	{
		MoveName = "Pop-Up Storm", Damage = 5.0f, BaseKnockback = 30.0f, KnockbackGrowth = 0.3f, LaunchAngleDegrees = 80.0f,
	};

	ItemArt PopUpArt => ItemCatalog.Art(popUp switch
	{
		PopUp.Crocodile => "popup_book_croc",
		PopUp.Cloud => "popup_book_cloud",
		_ => "popup_book_open",
	});

	float PopUpScale(ItemArt art) => Data.Size * PopUpGrowth / Mathf.Max(art.Size.X, art.Size.Y);

	/// <summary>A point on the pop-up's drawing (in its PNG's pixels), in the world, standing open.</summary>
	Vector2 PopUpPoint(ItemArt art, Vector2 pixel)
	{
		float s = PopUpScale(art);
		Vector2 d = (pixel - art.Grip) * s;
		return GlobalPosition + new Vector2(0.0f, Radius) + new Vector2(d.X * popFacing, d.Y);
	}

	void TickSpring()
	{
		if (phaseFrames < OpenFrames) return;
		ItemArt art = PopUpArt;
		if (art == null) return;
		float half = art.Size.X * PopUpScale(art) * 0.5f;
		switch (popUp)
		{
			case PopUp.Castle:
			{
				// The whole castle bounces: anyone on its floor goes up.
				Rect2 floor = new Rect2(GlobalPosition.X - half * 0.85f, floorY - 90.0f, half * 1.7f, 100.0f);
				foreach (Fighter f in match.Fighters)
				{
					if (!f.IsInPlay || f.Velocity.Y < -50.0f || Cooling(f, 24)) continue;
					if (!floor.HasPoint(f.Feet)) continue;
					lastHitAt[f] = age;
					f.SpringUp(Data.Amount);
					sprungAt = phaseFrames;
					SfxPlayer.At("boing", GlobalPosition, 0.05f);
				}
				break;
			}
			case PopUp.Crocodile:
			{
				int into = (phaseFrames - OpenFrames) % SnapEvery;
				if (into != SnapEvery - 1 || phaseFrames > Data.Duration - 30) break;
				snapAt = phaseFrames;
				SfxPlayer.At("item_snip", GlobalPosition, 0.05f);
				match?.Shake(14.0f);
				Vector2 hinge = PopUpPoint(art, art.Head);
				float reach = 120.0f * PopUpScale(art);
				Rect2 jaws = new Rect2(popFacing > 0 ? hinge.X - 20.0f : hinge.X - reach, hinge.Y - 120.0f, reach + 20.0f, floorY - hinge.Y + 125.0f);
				foreach (Fighter f in match.Fighters)
				{
					if (f == OwnerFighter || match.Allies(OwnerFighter, f) || !f.IsInPlay || !f.CanBeHitByHazard || !f.BodyRect().Intersects(jaws)) continue;
					f.ReceiveItemHit(OwnerFighter, CrocodileSnap, popFacing, f.GlobalPosition);
				}
				break;
			}
			case PopUp.Cloud:
			{
				if ((phaseFrames - OpenFrames) % ZapEvery != ZapEvery - 1 || phaseFrames > Data.Duration - 30) break;
				Vector2 under = PopUpPoint(art, art.Head);
				float width = art.Size.X * PopUpScale(art) * 0.42f;
				zapAt = phaseFrames;
				zapTo = new Vector2(under.X + CrayonBrush.Noise(age, 5) * width * 0.6f, floorY);
				Rect2 below = new Rect2(under.X - width, under.Y, width * 2.0f, floorY - under.Y + 10.0f);
				foreach (Fighter f in match.Fighters)
				{
					if (f == OwnerFighter || match.Allies(OwnerFighter, f) || !f.IsInPlay || !f.CanBeHitByHazard || !f.BodyRect().Intersects(below)) continue;
					zapTo = f.GlobalPosition;
					f.ReceiveItemHit(OwnerFighter, CloudZap, f.GlobalPosition.X >= under.X ? 1 : -1, f.GlobalPosition);
					break;
				}
				SfxPlayer.At("zap", under, 0.08f);
				break;
			}
		}
	}

	// --- The canvas: a drawing comes to life -----------------------------------------------

	/// <summary>
	/// The blank canvas: it stands on the floor beside whoever picked it up, and a drawing - one
	/// of the other fighters, small - climbs out of it and fights on their side for a while,
	/// running at the nearest opponent and hitting them. It cannot be hit. Then it goes back flat.
	/// </summary>
	public void Summon(Fighter by)
	{
		OwnerFighter = by;
		Holder = null;
		State = Phase.Summon;
		phaseFrames = 0;
		GetParent()?.MoveChild(this, -1);
		canvasAt = by.Feet + new Vector2(by.Facing * 90.0f, 0.0f);
		GlobalPosition = canvasAt + new Vector2(0.0f, -60.0f);
		figureFacing = by.Facing;

		// Someone else from the roster - not the one who summoned it.
		int pick = 0;
		for (int tries = 0; tries < 8; tries++)
		{
			pick = Mathf.Abs((int)(CrayonBrush.Noise(age + tries * 31, by.PlayerIndex + 5) * 1000.0f)) % FighterCatalog.Count;
			if (FighterCatalog.Get(pick).DisplayName != by.Data.DisplayName) break;
		}
		FighterData data = FighterCatalog.Get(pick);
		figure = new FighterRig();
		AddChild(figure);
		if (figure.Load(data))
		{
			figure.Normalise(150.0f, 60.0f, 1.0f);
			figure.SetPlanted(true);
			figure.SetFacing(figureFacing);
			figure.SetSpinWidth(0.12f);
		}
		else
		{
			figure.QueueFree();
			figure = null;
		}
		SfxPlayer.At("special_build", canvasAt, 0.05f);
	}

	void TickSummon(float dt)
	{
		const int EmergeFrames = 26;
		const int FadeFrames = 24;
		int duration = Data.Duration;
		bool leaving = phaseFrames > duration - FadeFrames;

		// Gravity, and the floor - it runs along the stage like a fighter.
		Vector2 v = Velocity;
		v.Y = Mathf.Min(v.Y + 3600.0f * dt, 1500.0f);

		Fighter target = phaseFrames > EmergeFrames && !leaving ? NearestOpponent(GlobalPosition) : null;
		if (target != null)
		{
			float dx = target.GlobalPosition.X - GlobalPosition.X;
			if (Mathf.Abs(dx) > 8.0f) figureFacing = dx > 0.0f ? 1 : -1;
			bool close = Mathf.Abs(dx) < 110.0f && Mathf.Abs(target.GlobalPosition.Y - GlobalPosition.Y) < 140.0f;
			v.X = close ? 0.0f : figureFacing * 560.0f;
			if (IsOnFloor() && target.GlobalPosition.Y < GlobalPosition.Y - 160.0f && phaseFrames % 30 == 0) v.Y = -1350.0f;

			if (close && figureSwing <= 0)
			{
				figureSwing = 34;
			}
			if (figureSwing == 26 && close && target.CanBeHitByHazard)
			{
				target.ReceiveItemHit(OwnerFighter, Data.Hit, figureFacing, target.GlobalPosition);
			}
		}
		else
		{
			v.X = 0.0f;
		}
		if (figureSwing > 0) figureSwing--;
		Velocity = v;
		MoveAndSlide();

		if (figure != null)
		{
			figure.SetFacing(figureFacing);
			// Climbing out of the canvas: flat, then filling out; going back: flat again.
			float width = phaseFrames < EmergeFrames ? phaseFrames / (float)EmergeFrames
				: leaving ? (duration - phaseFrames) / (float)FadeFrames : 1.0f;
			figure.SetSpinWidth(Mathf.Max(0.12f, width));
			figure.Position = new Vector2(0.0f, 0.0f);
			if (figureSwing > 18) figure.ApplyDirect(FighterAnimations.AttackStrikePose, 0.7f);
			else
			{
				figure.Play(Mathf.Abs(Velocity.X) > 30.0f ? FighterAnimations.Run : FighterAnimations.Idle);
				figure.Advance();
			}
		}

		if (phaseFrames > duration) Poof();
	}

	// --- Drawing --------------------------------------------------------------------------

	public override void _Draw()
	{
		if (Data == null) return;
		switch (State)
		{
			case Phase.Falling:
				DrawSparkleTrail();
				DrawArtSitting(0.0f);
				break;
			case Phase.Resting:
				DrawResting();
				break;
			case Phase.Held:
				DrawHeld();
				break;
			case Phase.Thrown:
				DrawThrown();
				break;
			case Phase.Effect:
				DrawEffect();
				break;
			case Phase.Summon:
				DrawSummon();
				break;
		}
	}

	/// <summary>
	/// On the floor: upright, its bottom on the floor under the middle of its shape - except
	/// something long and thin, a pencil or a metre stick, which could never stand on its end and
	/// lies on its side instead.
	/// </summary>
	void DrawArtSitting(float tilt, float alpha = 1.0f)
	{
		if (Art == null) { DrawCircle(Vector2.Zero, Radius, new Color(Data.Tint, alpha)); return; }
		float s = ArtScale;
		if (Art.Size.Y > Art.Size.X * 2.25f)
		{
			DrawSetTransformMatrix(new Transform2D(Mathf.Pi * 0.5f + tilt * 0.3f, new Vector2(s, s), 0.0f, new Vector2(0.0f, Radius - Art.Size.X * s * 0.5f)));
			DrawTexture(Art.Texture, -Art.Size * 0.5f, new Color(1, 1, 1, alpha));
		}
		else
		{
			DrawSetTransformMatrix(new Transform2D(tilt, new Vector2(s, s), 0.0f, new Vector2(0.0f, Radius)));
			DrawTexture(Art.Texture, new Vector2(-Art.Size.X * 0.5f, -Art.Size.Y), new Color(1, 1, 1, alpha));
		}
		DrawSetTransformMatrix(Transform2D.Identity);
	}

	void DrawArtCentred(float angle, float squash = 1.0f, string piece = null, float alpha = 1.0f, bool flip = false)
	{
		ItemArt art = piece != null ? ItemCatalog.Art(piece) ?? Art : Art;
		if (art == null) { DrawCircle(Vector2.Zero, Radius, new Color(Data.Tint, alpha)); return; }
		float s = Data.Size / Mathf.Max(art.Size.X, art.Size.Y);
		DrawSetTransformMatrix(new Transform2D(angle, new Vector2(flip ? -s : s, s * squash), 0.0f, Vector2.Zero));
		DrawTexture(art.Texture, -art.Size * 0.5f, new Color(1, 1, 1, alpha));
		DrawSetTransformMatrix(Transform2D.Identity);
	}

	void DrawSparkleTrail()
	{
		if (age > 60) return;
		for (int i = 0; i < 5; i++)
		{
			float t = (age + i * 7) % 30 / 30.0f;
			Vector2 at = new Vector2(CrayonBrush.Noise(i, 3) * Radius, -Radius - 30.0f - 140.0f * t);
			DrawStar(at, 9.0f * (1.0f - t), new Color(1.0f, 0.88f, 0.35f, 1.0f - t));
		}
	}

	void DrawResting()
	{
		// Blinking out at the end of its time; a little glint now and then so it reads as a thing
		// to pick up.
		bool blink = restFrames > RestLifetime - BlinkFrames && restFrames / 6 % 2 == 0;
		if (Data.Impact == ItemImpact.Blast && restFrames > FuseFrames - 90)
		{
			// The glitter bomb's fuse fizzing, faster and faster.
			float rate = 0.2f + 0.8f * (restFrames - (FuseFrames - 90)) / 90.0f;
			if (Mathf.Sin(restFrames * rate) > 0.0f) DrawCircle(new Vector2(0.0f, -Radius * 0.6f), Radius * 1.1f, new Color(1.0f, 0.4f, 0.6f, 0.35f));
		}
		if (!blink) DrawArtSitting(0.04f * Mathf.Sin(restFrames * 0.05f));
		int cycle = restFrames % 120;
		if (cycle < 14)
		{
			float t = cycle / 14.0f;
			DrawStar(new Vector2(Radius * 0.6f, -Radius * 1.5f), 12.0f * Mathf.Sin(t * Mathf.Pi), new Color(1, 1, 1, 0.9f));
		}
	}

	void DrawHeld()
	{
		if (MeasureFrames > 0)
		{
			DrawMeasuring();
			return;
		}
		if (DrawnBy != null && IsInstanceValid(DrawnBy)) return;
		DrawHeldOn(this, Transform2D.Identity);
	}

	/// <summary>Drawn on a layer inside its holder's arm, exactly where it would draw itself.</summary>
	public void DrawHeldInto(Node2D layer)
	{
		Transform2D toLayer = layer.GlobalTransform.AffineInverse() * GlobalTransform;
		// A rig squashed flat - turning, or cut out edge-on - has no inverse worth drawing with.
		if (Mathf.Abs(layer.GlobalTransform.Determinant()) < 0.0001f) return;
		DrawHeldOn(layer, toLayer);
	}

	/// <summary>How far a shield has turned side-on, 0 face on to 1 edge on.</summary>
	float sideOn;

	void DrawHeldOn(CanvasItem canvas, Transform2D toCanvas)
	{
		if (Art == null || Holder == null) return;
		// Swollen while a paint tube is being squeezed.
		float swell = Holder.ItemCharge;
		float s = ArtScale * (1.0f + 0.25f * swell);
		float angle = HeldUp.Angle() + Mathf.Pi * 0.5f;
		// A shield held up turns side-on, edge toward the camera, so which way he is guarding reads
		// at a glance: a narrow oval out in front of his fist, his hand on its back (Eric,
		// 2026-10-11). Turning, it eases round rather than snapping.
		float width = 1.0f;
		Vector2 shift = Vector2.Zero;
		if (Data.Use == ItemUse.Shield)
		{
			sideOn = Mathf.MoveToward(sideOn, Holder.ShieldRaised ? 1.0f : 0.0f, 0.2f);
			width = Mathf.Lerp(1.0f, 0.26f, sideOn);
			shift = new Vector2(heldFacing * Art.Size.X * s * width * 0.5f * sideOn, 0.0f);
		}
		Texture2D texture = Art.Texture;
		// Held up as a shield, a book is gripped at its middle, in front of his chest - not by
		// the edge it is swung with, which held it up over his face.
		Vector2 grip = CarriedUpright && Data.Use == ItemUse.Shield ? Art.Size * 0.5f : Art.Grip;
		if (Data.Id == "pencil" && UsesLeft > 0 && UsesLeft <= 2 && ItemCatalog.Art("pencil_stub") is ItemArt stub)
		{
			texture = stub.Texture;
			grip = stub.Grip;
		}
		if (Data.Id == "scissors" && Holder.ItemSnipping && ItemCatalog.Art("scissors_open") is ItemArt open)
		{
			texture = open.Texture;
			grip = open.Grip;
		}
		DrawInFist(canvas, toCanvas, texture, grip, angle, new Vector2(heldFacing * s * width, s), shift);
	}

	/// <summary>
	/// A drawing held in a fist: its grip on the fist (the origin of <paramref name="toCanvas"/>),
	/// turned to <paramref name="angle"/> - the way the forearm points, for a tool. The parade's
	/// item film draws with this too, so what it shows is what a match shows.
	/// </summary>
	public static void DrawInFist(CanvasItem canvas, Transform2D toCanvas, Texture2D texture, Vector2 grip, float angle, Vector2 scale, Vector2 shift)
	{
		canvas.DrawSetTransformMatrix(toCanvas * new Transform2D(angle, scale, 0.0f, shift));
		canvas.DrawTexture(texture, -grip);
		canvas.DrawSetTransformMatrix(Transform2D.Identity);
	}

	void DrawThrown()
	{
		switch (Data.Impact)
		{
			case ItemImpact.Glide:
				// Nose first, and the right way up whichever way it flies: going left it is turned
				// over side to side, not rolled upside down.
				bool left = Velocity.X < 0.0f;
				DrawArtCentred(left ? Velocity.Angle() - Mathf.Pi : Velocity.Angle(), 1.0f, null, 1.0f, left);
				break;
			default:
				DrawArtCentred(spin);
				break;
		}
	}

	void DrawEffect()
	{
		int f = phaseFrames;
		Vector2 at = ToLocal(effectAt);
		float fade = Mathf.Clamp((Data.Duration - f) / 40.0f, 0.0f, 1.0f);
		if (stripe)
		{
			DrawPuddle(StripeHalfWidth, Data.Tint, fade * Mathf.Min(1.0f, f / 6.0f), true);
			return;
		}
		if (opened)
		{
			DrawOpenCase(f);
			return;
		}
		switch (Data.Impact)
		{
			case ItemImpact.Splash:
				DrawSplashBurst(at, f);
				DrawPuddle(Data.Radius * 0.8f, Data.Tint, fade, true);
				break;
			case ItemImpact.Mine:
				if (poppedMine)
				{
					DrawSplashBurst(at, f);
					DrawPuddle(Data.Radius * 0.8f, Data.Tint, Mathf.Clamp((180 - f) / 40.0f, 0.0f, 1.0f), true);
				}
				else
				{
					// Sitting, wobbling, a light blinking once it is armed.
					DrawArtSitting(0.08f * Mathf.Sin(f * 0.25f));
					if (f > 40 && f / 15 % 2 == 0) DrawCircle(new Vector2(0.0f, -Radius * 1.4f), 6.0f, new Color(1.0f, 0.4f, 0.3f));
				}
				break;
			case ItemImpact.Glue:
				DrawPuddle(Data.Radius, Data.Tint, fade, false);
				DrawGlueBubbles(fade);
				break;
			case ItemImpact.Spill:
				DrawPuddle(Data.Radius, Data.Tint, fade, true);
				DrawTippedCup(fade);
				break;
			case ItemImpact.Tacks:
				DrawTacks(fade);
				break;
			case ItemImpact.Ink:
				DrawInkCloud(at, f);
				break;
			case ItemImpact.Blast:
				DrawGlitterBlast(at, f);
				break;
			case ItemImpact.Crayons:
				DrawBoxBurst(at, f);
				break;
			case ItemImpact.Spring:
				DrawPopUpBook(f, fade);
				break;
		}
	}

	void DrawSplashBurst(Vector2 at, int f)
	{
		if (f > 24) return;
		float t = f / 24.0f;
		for (int i = 0; i < 12; i++)
		{
			float a = Mathf.Tau * i / 12.0f + CrayonBrush.Noise(i, 41) * 0.3f;
			Vector2 dir = Vector2.Right.Rotated(a);
			Vector2 p = at + dir * Data.Radius * (0.2f + 0.9f * t) + new Vector2(0.0f, 300.0f * t * t);
			float r = Data.Radius * 0.13f * (1.0f - t * 0.6f) * (0.7f + 0.3f * Mathf.Abs(CrayonBrush.Noise(i, 42)));
			// Drops come down onto the floor, not through it.
			if (!float.IsNaN(floorY)) p.Y = Mathf.Min(p.Y, ToLocal(new Vector2(0.0f, floorY)).Y - r * 0.5f);
			DrawCircle(p, r, new Color(Data.Tint, 1.0f - t * 0.5f));
		}
		DrawCircle(at, Data.Radius * 0.45f * (1.0f - t), new Color(Data.Tint, 0.9f * (1.0f - t)));
	}

	/// <summary>A puddle lying on the floor: a flat, glossy ellipse with a highlight, its edge wobbling.</summary>
	void DrawPuddle(float halfWidth, Color colour, float alpha, bool glossy)
	{
		if (float.IsNaN(floorY) || alpha <= 0.0f) return;
		Vector2 centre = ToLocal(new Vector2(effectAt.X, floorY));
		const int Points = 24;
		var outline = new Vector2[Points];
		for (int i = 0; i < Points; i++)
		{
			float a = Mathf.Tau * i / Points;
			float r = 1.0f + 0.08f * CrayonBrush.Noise(i, 77);
			outline[i] = centre + new Vector2(Mathf.Cos(a) * halfWidth * r, Mathf.Sin(a) * 11.0f * r - 2.0f);
		}
		DrawColoredPolygon(outline, new Color(colour, 0.85f * alpha));
		// An edge a shade darker, so even white glue reads on a white page.
		var edge = new Vector2[Points + 1];
		System.Array.Copy(outline, edge, Points);
		edge[Points] = outline[0];
		DrawPolyline(edge, new Color(colour.Darkened(0.3f), 0.8f * alpha), 2.5f, true);
		if (glossy) DrawLine(centre + new Vector2(-halfWidth * 0.5f, -6.0f), centre + new Vector2(-halfWidth * 0.1f, -7.0f), new Color(1, 1, 1, 0.7f * alpha), 3.0f, true);
	}

	void DrawGlueBubbles(float alpha)
	{
		if (float.IsNaN(floorY)) return;
		Vector2 centre = ToLocal(new Vector2(effectAt.X, floorY));
		for (int i = 0; i < 6; i++)
		{
			float x = CrayonBrush.Noise(i, 13) * Data.Radius * 0.8f;
			float bob = 3.0f * Mathf.Sin(phaseFrames * 0.08f + i);
			DrawCircle(centre + new Vector2(x, -10.0f - bob), 6.0f + 3.0f * Mathf.Abs(CrayonBrush.Noise(i, 14)), new Color(1, 1, 1, 0.8f * alpha));
			DrawArc(centre + new Vector2(x, -10.0f - bob), 7.0f, 0.0f, Mathf.Tau, 12, new Color(Ink, 0.4f * alpha), 1.5f);
		}
	}

	void DrawTippedCup(float alpha)
	{
		if (float.IsNaN(floorY)) return;
		Vector2 at = ToLocal(new Vector2(effectAt.X - Data.Radius * 0.85f, floorY - Radius * 0.6f));
		DrawSetTransformMatrix(new Transform2D(-1.4f, new Vector2(ArtScale, ArtScale) * 0.8f, 0.0f, at));
		if (Art != null) DrawTexture(Art.Texture, -Art.Size * 0.5f, new Color(1, 1, 1, alpha));
		DrawSetTransformMatrix(Transform2D.Identity);
	}

	void DrawTacks(float alpha)
	{
		if (float.IsNaN(floorY)) return;
		ItemArt tack = ItemCatalog.Art("tack");
		Vector2 centre = ToLocal(new Vector2(effectAt.X, floorY));
		for (int i = 0; i < 9; i++)
		{
			float x = (i / 8.0f - 0.5f) * Data.Radius * 2.0f + CrayonBrush.Noise(i, 21) * 10.0f;
			Vector2 p = centre + new Vector2(x, -8.0f);
			if (tack == null) { DrawCircle(p, 6.0f, new Color(0.9f, 0.3f, 0.3f, alpha)); continue; }
			float s = 26.0f / Mathf.Max(tack.Size.X, tack.Size.Y);
			DrawSetTransformMatrix(new Transform2D(CrayonBrush.Noise(i, 22) * 0.4f, new Vector2(s, s), 0.0f, p));
			DrawTexture(tack.Texture, -tack.Size * 0.5f, new Color(1, 1, 1, alpha));
		}
		DrawSetTransformMatrix(Transform2D.Identity);
	}

	/// <summary>A billowing cloud of blue ink - over the fighters in it, so they cannot be seen.</summary>
	/// <summary>
	/// A cloud of ink sitting on the floor and billowing up: lumpy puffs rolling slowly over a
	/// paler wash at the edge, swirls of light on top, and wisps curling off as it thins. Ink, not
	/// a ball - and dense enough in the middle to hide whoever is in it.
	/// </summary>
	void DrawInkCloud(Vector2 at, int f)
	{
		float grow = 1.0f - Mathf.Pow(1.0f - Mathf.Min(1.0f, f / 14.0f), 3.0f);
		float alpha = Mathf.Clamp((Data.Duration - f) / 50.0f, 0.0f, 1.0f);
		float r = Data.Radius;
		float floor = float.IsNaN(floorY) ? float.PositiveInfinity : ToLocal(new Vector2(0.0f, floorY)).Y;
		Vector2 centre = new Vector2(at.X, Mathf.Min(at.Y, floor - r * 0.5f * grow));
		Color ink = Data.Tint;
		Color wash = ink.Lightened(0.35f);

		// The pale wash round its edge first, then the dense puffs over it.
		for (int pass = 0; pass < 2; pass++)
		{
			int count = pass == 0 ? 16 : 11;
			for (int i = 0; i < count; i++)
			{
				float a = Mathf.Tau * i / count + f * 0.004f * (i % 2 == 0 ? 1.0f : -1.0f) + CrayonBrush.Noise(i, 63 + pass) * 0.3f;
				float reach = (pass == 0 ? 0.62f : 0.38f) * (1.0f + 0.12f * Mathf.Sin(f * 0.05f + i * 1.7f));
				float size = r * (pass == 0 ? 0.36f : 0.42f) * (0.8f + 0.25f * Mathf.Abs(CrayonBrush.Noise(i, 65 + pass))) * grow;
				Vector2 p = centre + new Vector2(Mathf.Cos(a) * 1.15f, Mathf.Sin(a) * 0.85f) * r * reach * grow;
				p.Y = Mathf.Min(p.Y, floor - size * 0.5f);
				DrawCircle(p, size, new Color(pass == 0 ? wash : ink, (pass == 0 ? 0.7f : 0.93f) * alpha));
			}
		}
		DrawCircle(centre, r * 0.5f * grow, new Color(ink, 0.95f * alpha));

		// Swirls of light on the upper puffs.
		for (int i = 0; i < 4; i++)
		{
			float a = -Mathf.Pi * (0.2f + 0.6f * i / 3.0f) + 0.15f * Mathf.Sin(f * 0.03f + i);
			Vector2 p = centre + Vector2.Right.Rotated(a) * r * 0.42f * grow;
			DrawArc(p, r * 0.12f * grow, a - 1.2f, a + 0.6f, 10, new Color(1, 1, 1, 0.2f * alpha), 5.0f, true);
		}

		// Wisps curling up off the top.
		for (int i = 0; i < 6; i++)
		{
			float t = ((f + i * 23) % 70) / 70.0f;
			Vector2 p = centre + new Vector2(CrayonBrush.Noise(i, 67) * r * 0.7f + 20.0f * Mathf.Sin(t * 6.0f + i), -r * (0.55f + 0.6f * t)) * grow;
			DrawCircle(p, r * 0.08f * (1.0f - t) + 3.0f, new Color(wash, 0.6f * (1.0f - t) * alpha));
		}
	}

	/// <summary>A flash of pink and gold, and glitter thrown out every way, drifting down twinkling.</summary>
	const int GlitterFrames = 75;

	/// <summary>
	/// A pink and gold starburst, then glitter everywhere: thrown out fast, slowing, and drifting
	/// down twinkling long after the bang - so it reads as glitter, not as a bomb.
	/// </summary>
	void DrawGlitterBlast(Vector2 at, int f)
	{
		float t = f / (float)GlitterFrames;
		Color pink = new Color(1.0f, 0.55f, 0.85f), gold = new Color(1.0f, 0.86f, 0.35f);

		// The burst: a soft pink flash and spiky rays, gone in a third of a second.
		if (f < 20)
		{
			float b = f / 20.0f;
			DrawCircle(at, Data.Radius * (0.4f + 0.5f * b), new Color(1.0f, 0.9f, 0.97f, 0.7f * (1.0f - b)));
			for (int i = 0; i < 12; i++)
			{
				float a = Mathf.Tau * i / 12.0f + 0.2f;
				float reach = Data.Radius * (0.5f + 0.9f * b) * (i % 2 == 0 ? 1.0f : 0.7f);
				Vector2 tip = at + Vector2.Right.Rotated(a) * reach;
				Vector2 side = Vector2.Right.Rotated(a + Mathf.Pi * 0.5f) * 10.0f * (1.0f - b);
				DrawColoredPolygon(new[] { at + side, tip, at - side }, new Color(i % 2 == 0 ? pink : gold, 0.85f * (1.0f - b)));
			}
		}

		Color[] colours = { pink, gold, new Color(0.6f, 0.75f, 1.0f), new Color(0.75f, 1.0f, 0.75f), new Color(1.0f, 1.0f, 1.0f) };
		for (int i = 0; i < 48; i++)
		{
			float a = Mathf.Tau * i / 48.0f + CrayonBrush.Noise(i, 91);
			float speed = 0.5f + 0.9f * Mathf.Abs(CrayonBrush.Noise(i, 92));
			// Out fast and slowing (an ease-out), then falling and swaying as it settles.
			float out1 = 1.0f - Mathf.Pow(1.0f - Mathf.Min(1.0f, t * 2.5f), 3.0f);
			float fall = Mathf.Max(0.0f, t - 0.25f);
			Vector2 p = at + Vector2.Right.Rotated(a) * Data.Radius * 1.3f * speed * out1
				+ new Vector2(14.0f * Mathf.Sin(f * 0.12f + i), 160.0f * fall * fall + 50.0f * fall);
			// Settling on the floor, not sinking through it.
			if (!float.IsNaN(floorY)) p.Y = Mathf.Min(p.Y, ToLocal(new Vector2(0.0f, floorY)).Y - 4.0f);
			float twinkle = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(f * 0.35f + i * 1.7f));
			float size = (i % 3 == 0 ? 11.0f : 7.0f) * twinkle;
			float alpha = Mathf.Clamp((1.0f - t) * 2.0f, 0.0f, 1.0f);
			DrawStar(p, size, new Color(colours[i % colours.Length], alpha));
		}
	}

	/// <summary>The pencil case open: its lid flipped up and back on its hinge, the inside showing.</summary>
	void DrawOpenCase(int f)
	{
		if (Art == null) return;
		float t = 1.0f - Mathf.Pow(1.0f - Mathf.Min(1.0f, f / 8.0f), 2.0f);
		float alpha = Mathf.Clamp((OpenCaseFrames - f) / 8.0f, 0.0f, 1.0f);
		float s = ArtScale;
		float w = Art.Size.X * s, top = Radius - Art.Size.Y * s * 0.72f;
		var inside = new Color(0.20f, 0.46f, 0.46f, alpha);
		var teal = new Color(0.36f, 0.75f, 0.72f, alpha);
		var ink = new Color(0.21f, 0.23f, 0.44f, alpha);

		DrawArtSitting(0.0f, alpha);
		// The open mouth of the case, dark inside.
		DrawSetTransform(new Vector2(0.0f, top), 0.0f, new Vector2(1.0f, 0.22f));
		DrawCircle(Vector2.Zero, w * 0.44f, inside);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
		// The lid on its hinge at the back, swung up and over.
		Vector2 hinge = new Vector2(-w * 0.46f, top);
		float angle = -t * 2.3f;
		Vector2 along = Vector2.Right.Rotated(angle) * w * 0.92f;
		Vector2 up = Vector2.Up.Rotated(angle) * 12.0f;
		var lid = new[] { hinge, hinge + along, hinge + along + up, hinge + up };
		DrawColoredPolygon(lid, teal);
		DrawPolyline(new[] { lid[0], lid[1], lid[2], lid[3], lid[0] }, ink, 3.0f, true);
		// The zip's teeth along the lid's edge.
		for (int i = 1; i < 9; i++)
		{
			Vector2 p = hinge + along * (i / 9.0f);
			DrawLine(p, p + up.Normalized() * -5.0f, ink, 2.0f, true);
		}
	}

	void DrawBoxBurst(Vector2 at, int f)
	{
		float t = Mathf.Min(1.0f, f / 18.0f);
		DrawArtCentred(0.0f, 1.0f - 0.3f * t, null, 1.0f - t);
		// The lid flipping off.
		Vector2 lid = at + new Vector2(30.0f * t, -60.0f * t + 80.0f * t * t);
		DrawRect(new Rect2(lid - new Vector2(20.0f, 6.0f), new Vector2(40.0f, 12.0f)), new Color(0.98f, 0.82f, 0.30f, 1.0f - t));
	}

	void DrawPopUpBook(int f, float fade)
	{
		ItemArt open = PopUpArt;
		if (open == null)
		{
			DrawArtSitting(0.0f, fade);
			return;
		}
		// Standing up out of the book with an overshoot, the way a pop-up page springs open.
		float t = Mathf.Min(1.0f, f / (float)OpenFrames);
		float rise = t < 1.0f ? t * (1.0f + 0.25f * Mathf.Sin(t * Mathf.Pi)) : 1.0f;
		// Springing someone: squashed for a moment, then stretched up past its height, settling.
		int since = f - sprungAt;
		float boing = since < 18 ? Mathf.Sin(since / 18.0f * Mathf.Pi * 2.0f) * (1.0f - since / 18.0f) : 0.0f;
		float height = rise * (1.0f + 0.3f * boing);
		float s = PopUpScale(open);
		var colour = new Color(1, 1, 1, fade);
		Vector2 foot = new Vector2(0.0f, Radius);
		DrawSetTransformMatrix(new Transform2D(0.0f, new Vector2(s * popFacing, s * Mathf.Max(0.15f, height)), 0.0f, foot));
		DrawTexture(open.Texture, -open.Grip, colour);

		if (popUp == PopUp.Crocodile && ItemCatalog.Art("popup_book_croc_jaw") is ItemArt jaw)
		{
			// The upper jaw on its hinge: opening wide before each snap, slammed shut on it.
			int into = (f - OpenFrames) % SnapEvery;
			float gape = f < OpenFrames ? 0.0f
				: into >= SnapEvery - SnapOpening ? (into - (SnapEvery - SnapOpening)) / (float)SnapOpening
				: f - snapAt < 6 ? 0.0f : 0.15f;
			float angle = -0.75f * gape * gape;
			Vector2 hinge = foot + new Vector2((open.Head.X - open.Grip.X) * s * popFacing, (open.Head.Y - open.Grip.Y) * s * Mathf.Max(0.15f, height));
			DrawSetTransformMatrix(new Transform2D(angle * popFacing, new Vector2(s * popFacing, s * Mathf.Max(0.15f, height)), 0.0f, hinge));
			DrawTexture(jaw.Texture, -jaw.Grip, colour);
		}
		DrawSetTransformMatrix(Transform2D.Identity);

		// The storm cloud's lightning: a jagged yellow bolt from under the cloud to what it struck.
		if (popUp == PopUp.Cloud && f - zapAt < 10 && f >= OpenFrames)
		{
			Vector2 from = ToLocal(PopUpPoint(open, open.Head));
			Vector2 to = ToLocal(zapTo);
			float a = 1.0f - (f - zapAt) / 10.0f;
			var bolt = new Vector2[7];
			for (int i = 0; i < bolt.Length; i++)
			{
				float k = i / (float)(bolt.Length - 1);
				float jag = i == 0 || i == bolt.Length - 1 ? 0.0f : (i % 2 == 0 ? 1.0f : -1.0f) * 26.0f;
				bolt[i] = from.Lerp(to, k) + new Vector2(jag, 0.0f);
			}
			DrawPolyline(bolt, new Color(Ink, 0.9f * a * fade), 14.0f, true);
			DrawPolyline(bolt, new Color(1.0f, 0.88f, 0.3f, a * fade), 8.0f, true);
		}
	}

	void DrawSummon()
	{
		// The canvas standing where it was set down, glowing while the drawing climbs out.
		Vector2 canvas = ToLocal(canvasAt);
		float alpha = Mathf.Clamp((Data.Duration - phaseFrames) / 30.0f, 0.0f, 1.0f);
		if (Art != null)
		{
			float s = ArtScale * 1.3f;
			DrawSetTransformMatrix(new Transform2D(0.0f, new Vector2(s, s), 0.0f, canvas));
			DrawTexture(Art.Texture, new Vector2(-Art.Size.X * 0.5f, -Art.Size.Y), new Color(1, 1, 1, alpha));
			DrawSetTransformMatrix(Transform2D.Identity);
		}
		if (phaseFrames < 30)
		{
			float t = phaseFrames / 30.0f;
			for (int i = 0; i < 8; i++)
			{
				float a = Mathf.Tau * i / 8.0f + t * 2.0f;
				DrawStar(canvas + new Vector2(0.0f, -70.0f) + Vector2.Right.Rotated(a) * 70.0f * t, 8.0f * (1.0f - t), new Color(1.0f, 0.9f, 0.4f, 1.0f - t));
			}
		}
		// The drawing's own little marker of whose side it is on.
		if (OwnerFighter != null && figure != null)
		{
			Color tint = OwnerFighter.SideTint;
			DrawCircle(new Vector2(0.0f, -150.0f), 9.0f, tint);
		}
	}

	public static void DrawStarOn(CanvasItem canvas, Vector2 at, float size, Color colour)
	{
		if (size <= 0.5f) return;
		var points = new Vector2[10];
		for (int i = 0; i < 10; i++)
		{
			float r = i % 2 == 0 ? size : size * 0.45f;
			points[i] = at + Vector2.Up.Rotated(Mathf.Tau * i / 10.0f) * r;
		}
		canvas.DrawColoredPolygon(points, colour);
	}

	void DrawStar(Vector2 at, float size, Color colour) => DrawStarOn(this, at, size, colour);
}
