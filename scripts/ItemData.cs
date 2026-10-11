using System.Collections.Generic;
using Godot;

/// <summary>What pressing attack does with an item in hand. Grab always throws it.</summary>
public enum ItemUse
{
	/// <summary>Swung like a weapon: <see cref="ItemData.Swing"/>.</summary>
	Swing,

	/// <summary>Attack throws it, as grab does. A throwing item.</summary>
	Throw,

	/// <summary>Held down, it sprays: a stream of little hits until it runs dry. The spray can.</summary>
	Spray,

	/// <summary>Held down, it charges; let go, and it fires a blob that size. The paint tube.</summary>
	ChargeShot,

	/// <summary>Each press blows a gust that pushes everyone ahead away. The airbrush.</summary>
	Gust,

	/// <summary>Each press draws a platform, for a few seconds. The magic marker.</summary>
	Draw,

	/// <summary>While held, anything thrown at its front bounces back; attack bonks with it. The sketchbook.</summary>
	Shield,

	/// <summary>Takes over: he stamps, over and over, and can do nothing else for a while. The rubber stamp.</summary>
	Stamp,

	/// <summary>Used up the moment it is picked up: a boost. Never held.</summary>
	Consume,

	/// <summary>Attack beside it opens it: a container. Never held.</summary>
	Open,
}

/// <summary>What a thrown item does when it lands or hits someone.</summary>
public enum ItemImpact
{
	/// <summary>Hits, bounces off and lies there to be picked up again.</summary>
	None,
	/// <summary>Bursts in a splash of paint - everyone in it painted, and a slippery puddle below.</summary>
	Splash,
	/// <summary>Bursts open and its crayons fly out in a fan.</summary>
	Crayons,
	/// <summary>Leaves a sticky puddle that slows everyone in it.</summary>
	Glue,
	/// <summary>Bursts into a cloud of ink that hides everyone in it.</summary>
	Ink,
	/// <summary>Glides, curving toward the nearest opponent.</summary>
	Glide,
	/// <summary>Bounces along the floor and off walls.</summary>
	Bounce,
	/// <summary>Flies out and comes back to whoever threw it.</summary>
	Boomerang,
	/// <summary>Explodes.</summary>
	Blast,
	/// <summary>Sits where it lands and bursts when someone comes near.</summary>
	Mine,
	/// <summary>Scatters across the floor; anyone walking over it hops.</summary>
	Tacks,
	/// <summary>Spills on the floor; anyone running into it trips.</summary>
	Spill,
	/// <summary>Opens on the floor into a spring that launches whoever stands on it.</summary>
	Spring,
}

/// <summary>What a consumed item does to whoever picked it up.</summary>
public enum ItemBoost
{
	None,
	/// <summary>Takes damage off.</summary>
	Heal,
	/// <summary>Cannot be hit.</summary>
	Star,
	/// <summary>Drawn giant: bigger, heavier, hits harder.</summary>
	Giant,
	/// <summary>See-through: knocked about, but takes no damage.</summary>
	Ghost,
	/// <summary>A drawing climbs out of a canvas and fights on his side.</summary>
	Canvas,
}

/// <summary>
/// One item: what it looks like, how it is used, and what it does. Items are data like moves -
/// a new one is an entry in <see cref="ItemCatalog"/>, not a new class - and every hit an item
/// lands is a <see cref="MoveData"/>, so the knockback formula treats them like any other hit.
/// See .ai/fighting-design.md.
/// </summary>
public partial class ItemData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";

	/// <summary>One line for the item settings screen: what it does.</summary>
	[Export] public string Blurb { get; set; } = "";

	[Export] public ItemUse Use { get; set; } = ItemUse.Throw;
	[Export] public ItemImpact Impact { get; set; } = ItemImpact.None;
	[Export] public ItemBoost Boost { get; set; } = ItemBoost.None;

	/// <summary>How big it is drawn, in world pixels along its longest side, held or lying.</summary>
	[Export] public float Size { get; set; } = 100.0f;

	/// <summary>
	/// How much use is in it before it is used up: swings that land (the pencil sharpens down),
	/// frames of spray, shots, gusts, platforms, snips. Zero never runs out.
	/// </summary>
	[Export] public int Uses { get; set; } = 0;

	/// <summary>Frames a boost, a puddle, a cloud or a trap lasts.</summary>
	[Export] public int Duration { get; set; } = 0;

	/// <summary>How far a splash, a cloud, a blast or a puddle reaches, in world pixels.</summary>
	[Export] public float Radius { get; set; } = 0.0f;

	/// <summary>How hard it is thrown, px/s, and how fast it falls once thrown.</summary>
	[Export] public float ThrowSpeed { get; set; } = 1150.0f;
	[Export] public float Gravity { get; set; } = 2600.0f;

	/// <summary>Picked up just by touching it - the gold star, which hops about.</summary>
	[Export] public bool TouchToCollect { get; set; } = false;

	/// <summary>The paint or ink colour of whatever it splashes or sprays.</summary>
	[Export] public Color Tint { get; set; } = new Color(0.35f, 0.55f, 0.95f);

	/// <summary>A boost's size: how much damage it heals.</summary>
	[Export] public float Amount { get; set; } = 0.0f;

	/// <summary>Swung with attack - the swing items, the sketchbook's bonk and the stamp.</summary>
	public MoveData Swing;

	/// <summary>What it does to whoever it hits when thrown, splashes, explodes or trips them.</summary>
	public MoveData Hit;

	/// <summary>What it fires or flicks out - a crayon, a paint blob. Null for most.</summary>
	public MoveData Shot;
}

/// <summary>The drawing of an item, and where on it the hand goes and the business end is.</summary>
public sealed class ItemArt
{
	public Texture2D Texture;
	public Vector2 Grip;
	public Vector2 Head;
	public Vector2 Size;
}
