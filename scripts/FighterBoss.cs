using Godot;

/// <summary>
/// The giant of a boss fight (BossFight, .ai/modes.md): drawn twice his size by the same growing
/// the magnifying glass does, his damage going on a health bar instead of his percent, and not
/// launched like anyone else - and how everything a grown fighter does grows with him, which is for
/// anyone made big (the giant, the magnifying glass), not only the boss.
/// </summary>
public partial class Fighter
{
	/// <summary>The boss fight he is the giant of, or null - everyone else, always.</summary>
	public BossFight Boss { get; private set; }

	public bool IsBoss => Boss != null;

	/// <summary>
	/// How much bigger his moves reach than their data says: giant, they reach as far as he is
	/// drawn. Read by the CPU, which judges a swing by the move's own hitbox.
	/// </summary>
	public float ReachScale => giantScale;

	/// <summary>Whether a grab can take him. A giant cannot be picked up by someone a third his height.</summary>
	public bool Grabbable => !IsBoss;

	/// <summary>
	/// The size he stands at when nothing is growing him: 1, or a boss's giant size. The magnifying
	/// glass grows him from it and shrinks him back to it.
	/// </summary>
	float RestScale => IsBoss ? BossFight.Scale : 1.0f;

	// --- Everything he does grows with him -----------------------------------------------
	// Eric's call, 2026-10-29: made big, a fighter's every ability is big - his hitboxes and
	// smears (giantScale, where they are used), and also what he sends out, how far his arms and
	// chains reach, what he holds and how high he rises. One copy of a move per size, kept.

	readonly System.Collections.Generic.Dictionary<(MoveData, float, bool), MoveData> grownMoves =
		new System.Collections.Generic.Dictionary<(MoveData, float, bool), MoveData>();

	/// <summary>His size in steps of a twentieth, so growing does not make a copy every frame.</summary>
	float GrownStep => Mathf.Snapped(giantScale, 0.05f);

	MoveData Grown(MoveData move, bool hazard)
	{
		float k = GrownStep;
		if (move == null || Mathf.IsEqualApprox(k, 1.0f)) return move;
		if (!grownMoves.TryGetValue((move, k, hazard), out MoveData grown))
		{
			grown = hazard ? move.GrownHazard(k) : move.GrownBody(k);
			grownMoves[(move, k, hazard)] = grown;
		}
		return grown;
	}

	/// <summary>
	/// One of his own moves as he does it at his size (MoveData.GrownBody). Not his grab, throws
	/// or taunts, or an item's swing: those are told apart by which move they are.
	/// </summary>
	MoveData GrownOwnMove(MoveData move) =>
		System.Array.IndexOf(Data.Moves, move) >= 0 && !IsTaunt(move) ? Grown(move, false) : move;

	/// <summary>
	/// What he sends out, at his size, and how fast it goes (MoveData.GrownHazard) - for
	/// MatchManager.SpawnHazard, which everything a fighter sends out passes through.
	/// </summary>
	public MoveData GrownHazard(MoveData move, ref Vector2 velocity)
	{
		MoveData grown = Grown(move, true);
		if (grown != move) velocity *= GrownStep;
		return grown;
	}

	/// <summary>A move's shot as it would fly from him at his size, for the CPU to judge it by.</summary>
	public MoveData GrownShot(MoveData move) => Grown(move, true);

	/// <summary>Frames left before a big hit can stagger him again.</summary>
	int staggerCooldownFrames;

	/// <summary>
	/// Makes him the giant: his lives are the health bar, so a fall only brings him back. He grows
	/// to giant size through the 3-2-1 rather than appearing at it - the countdown is the reveal.
	/// </summary>
	public void MakeBoss(BossFight fight)
	{
		Boss = fight;
		Stocks = BossFight.Lives;
	}

	/// <summary>Once a frame, from the boss fight.</summary>
	public void TickBoss()
	{
		if (staggerCooldownFrames > 0) staggerCooldownFrames--;
	}

	/// <summary>
	/// A hit on the giant, after its damage has gone on his health bar (Percent, which a boss never
	/// keeps). A small hit only clangs off his plating - the damage counts, nothing else does, as with
	/// Lug's hard hat - and a big one staggers him: a moment of hitstun and a step back, never a
	/// launch, and not toward a drop. After a stagger his armour holds against everything for a
	/// while, so four people cannot keep him reeling. A block is a block, and is left to the usual
	/// rules. True when the hit is dealt with here.
	/// </summary>
	bool BossTakesHit(Fighter attacker, int attackerFacing, Vector2 contactPoint, float damage, bool blocked)
	{
		if (blocked) return false;
		hitlagFrames = Knockback.HitlagFrames(damage);
		if (damage < BossFight.StaggerDamage || staggerCooldownFrames > 0)
		{
			SfxPlayer.At("clang", contactPoint, 0.04f);
			// A hit, not a block: the spark and the sound say it landed, and the bar says it hurt.
			Match.OnHitLanded(attacker, this, contactPoint, damage * 2.0f, damage, false);
			return true;
		}

		staggerCooldownFrames = BossFight.StaggerCooldownFrames;
		currentMove = null;
		alreadyHitThisMove.Clear();
		IsBlocking = false;
		blockedStun = false;
		spinVictimFrames = 0;
		hitstunFrames = BossFight.StaggerFrames;
		State = FighterState.Hitstun;
		// Back a step the way he was hit - unless that is off the edge of what he stands on.
		float push = BossFight.StaggerPush * attackerFacing;
		var ahead = new Vector2(GlobalPosition.X + attackerFacing * (bodySize.X * 0.5f + BossFight.StaggerSlide), Feet.Y + 8.0f);
		var query = new PhysicsPointQueryParameters2D { Position = ahead, CollisionMask = GroundMask };
		if (IsOnFloor() && GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count == 0) push = 0.0f;
		Velocity = new Vector2(push, IsOnFloor() ? 0.0f : Mathf.Min(Velocity.Y, 0.0f));
		// Staggered in the air, he has his way back as anyone hit does.
		airJumpsUsed = 0;
		helpless = false;
		upSpecialUsed = false;
		usedThisAirtime.Clear();
		SfxPlayer.At("clang", contactPoint, 0.04f);
		Match.OnHitLanded(attacker, this, contactPoint, BossFight.StaggerFeel, damage, false);
		return true;
	}
}
