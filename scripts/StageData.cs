using Godot;

/// <summary>
/// Which drawing treatment a stage uses. Both are from the art direction in
/// .ai/art-direction.md - the exercise book is the house style, and nature stages get the
/// colouring-book treatment instead.
/// </summary>
public enum StageStyle
{
	/// <summary>Ruled school paper with a margin rule, drawn over in blue biro and crayon.</summary>
	ExerciseBook,

	/// <summary>Plain paper, indigo outlines, crayon fill that runs past the lines.</summary>
	OutsideTheLines,

	/// <summary>
	/// A sheet of ruled paper taped to something (<see cref="StageData.PageRect"/>), with the
	/// stage drawn on the sheet and the thing behind it drawn round it - all in coloured pencil.
	/// The sky colours are the surface behind: Fridge Door's brushed metal.
	/// </summary>
	TapedPage,

	/// <summary>
	/// Several sheets of paper held up on a steel door by fridge magnets
	/// (<see cref="StageData.Pages"/>) - overlapping, each tilted a little, each its own kind of
	/// paper - with the stage drawn across them. Fridge Door.
	/// </summary>
	PinnedPages,
}

/// <summary>What a pinned page is: the paper it is, which is what is printed on it.</summary>
public enum PaperKind { Ruled, Grid, Plain }

/// <summary>One sheet of paper on a <see cref="StageStyle.PinnedPages"/> stage.</summary>
public sealed class PinnedPage
{
	public Rect2 Rect;
	/// <summary>Degrees it hangs off straight. A degree or two: stuck up by hand, not printed.</summary>
	public float Angle;
	public PaperKind Paper;
	/// <summary>How many magnets hold it up along its top edge: one in the middle, or two near the corners.</summary>
	public int Magnets;
	public int Seed;

	public PinnedPage(Rect2 rect, float angle, PaperKind paper, int magnets, int seed)
	{
		Rect = rect;
		Angle = angle;
		Paper = paper;
		Magnets = magnets;
		Seed = seed;
	}
}

/// <summary>How a platform is drawn. The collision rectangle is the same either way.</summary>
public enum PlatformLook
{
	/// <summary>A plain slab. The fallback.</summary>
	Ledge,

	/// <summary>A fridge magnet: rounded, brightly filled, with a highlight.</summary>
	Magnet,

	/// <summary>A roof slab with a house drawn beneath it, down to the ground line.</summary>
	HouseRoof,

	/// <summary>The rectangle IS the building. Windows are drawn into it.</summary>
	Building,

	/// <summary>A building, but narrow and very tall, with an aerial on top.</summary>
	Tower,

	/// <summary>
	/// The top of a tree's leaves, which is the part you stand on, with the trunk drawn down to
	/// the ground. Leaves you can drop through are the nature stage's soft platform.
	/// </summary>
	TreeTop,

	/// <summary>A slab drawn on the page in coloured pencil, for the taped-page stages.</summary>
	PencilLedge,

	/// <summary>
	/// The jib of a tower crane - the long arm you stand on. The mast is drawn from under its
	/// middle down to the ground, with the cab, a counterweight and a hook. A crane you cannot
	/// stand on is the prop <see cref="PropKind.TowerCrane"/>, drawn the same.
	/// </summary>
	CraneJib,

	/// <summary>A steel beam hanging from a crane on two cables, which run up <see cref="StageData.CraneDrop"/> to the jib.</summary>
	HangingBeam,

	/// <summary>A scaffolding plank, with its poles and cross-braces drawn down to the ground.</summary>
	Scaffold,

	// --- The city ---

	/// <summary>
	/// One landing of a fire escape, bolted to the side of the building its end butts against:
	/// a railing round it, a bracket under it to the wall, stairs up to the landing above, a drop
	/// ladder under the lowest and a ladder up over the roof's edge from the highest.
	/// </summary>
	FireEscape,

	/// <summary>A rooftop water tower: the lid is what you stand on, the tank and its legs drawn down to the roof.</summary>
	WaterTower,

	/// <summary>A billboard: the top of its frame is what you stand on, the picture and its legs drawn down to the roof.</summary>
	Billboard,

	/// <summary>
	/// A street: a pavement along the top and a road with a dashed centre line. When the stage
	/// has <see cref="StageData.Traffic"/>, cars drive along the top of it.
	/// </summary>
	Road,

	/// <summary>A concert stage's floor: wooden boards, with a pleated skirt hanging down its front.</summary>
	StageDeck,

	/// <summary>An amp on a stack of speaker cabinets standing on the ground; the top of the amp is the platform.</summary>
	AmpStack,

	/// <summary>
	/// A red steel girder - a lighting rig - held up on a steel tower at each end, the towers
	/// drawn down to the ground, with spotlights hanging from it shining down. Lugnut's, in
	/// Flambé's story.
	/// </summary>
	Girder,

	// --- The playground ---

	/// <summary>A slide's deck: posts down to the ground, a ladder up its back and the slide down its front.</summary>
	SlideTower,

	/// <summary>Monkey bars - the bars seen end on - on a ladder at each end.</summary>
	MonkeyBars,

	/// <summary>A swing set's top bar, on splayed legs, with swings hanging from it.</summary>
	SwingSet,

	/// <summary>A low wooden sandbox on the ground, full of sand, a spade stuck in it. You stand on the sand.</summary>
	Sandbox,

	// --- The building site ---

	/// <summary>Dug earth: a cross-section of soil and stones, packed hard along the top.</summary>
	Dirt,

	/// <summary>A portable site office on the ground: corrugated walls, a window and a door. You stand on its roof.</summary>
	SiteCabin,

	// --- The kitchen ---

	/// <summary>A kitchen unit: the worktop along the top, which you stand on, and cupboards and drawers below it.</summary>
	Countertop,

	/// <summary>A sink between two units: the steel basin down to its floor, which you stand on, and the tap above.</summary>
	Sink,

	/// <summary>A toaster standing on the counter. You stand on its top.</summary>
	Toaster,

	/// <summary>A stack of cookbooks lying on the counter.</summary>
	Books,

	/// <summary>A wall cupboard: its top you can drop through, its doors pencilled in below.</summary>
	WallCabinet,

	/// <summary>A wall shelf on two brackets.</summary>
	Shelf,
}

/// <summary>One collidable surface, plus how to draw it.</summary>
public sealed class StagePlatform
{
	public Rect2 Rect;

	/// <summary>Soft platforms can be jumped up through and dropped down through.</summary>
	public bool OneWay;

	public PlatformLook Look = PlatformLook.Ledge;

	/// <summary>
	/// A platform that moves (Eric, 2026-10-18: the building site's cranes shift their loads back
	/// and forth): it swings from Rect by up to this much either way and back, once every
	/// TravelFrames. Zero stays put. Anyone standing on it rides along.
	/// </summary>
	public Vector2 Travel;
	public int TravelFrames = 360;

	public bool Moves => Travel != Vector2.Zero && TravelFrames > 0;

	public StagePlatform(Rect2 rect, bool oneWay, PlatformLook look)
	{
		Rect = rect;
		OneWay = oneWay;
		Look = look;
	}
}

/// <summary>
/// Scenery. Never collidable - if a fighter can stand on it, it is a platform. A
/// <see cref="Crowd"/> is an audience standing in its rectangle, rows of heads and shoulders
/// down to its foot (or the bottom of the page), cheering - drawn and moved by its own node
/// over the stage and under the fighters. A <see cref="TowerCrane"/> is a crane nobody stands
/// on, drawn as the CraneJib platform is but with no outline.
/// </summary>
public enum PropKind { Sun, Moon, Cloud, Star, Hill, Bush, TapedDrawing, FridgeHandle, Fence, Tree, Grass, FridgeMagnet, Skyline, Window, Cone, TowerCrane, Crowd }

public sealed class StageProp
{
	public PropKind Kind;
	public Rect2 Rect;
	public int Seed;

	public StageProp(PropKind kind, Rect2 rect, int seed)
	{
		Kind = kind;
		Rect = rect;
		Seed = seed;
	}
}

/// <summary>
/// Everything that makes one stage different from another. A stage is a palette and a list of
/// rectangles, not a renderer - adding one should never mean writing drawing code.
///
/// Built in code by <see cref="StageCatalog"/> for now, the same way FighterData is, while the
/// roster is small enough to read in one file.
/// </summary>
[GlobalClass]
public partial class StageData : Resource
{
	[Export] public string DisplayName { get; set; } = "Stage";
	[Export] public StageStyle Style { get; set; } = StageStyle.ExerciseBook;

	// --- Palette -------------------------------------------------------------
	// THE RULE, from .ai/art-direction.md: characters own black. Nothing here may be darker
	// than roughly 30% value except Ink, which is an outline colour and never a fill.

	[Export] public Color SkyTop { get; set; } = new Color(0.78f, 0.86f, 0.93f);
	[Export] public Color SkyBottom { get; set; } = new Color(0.97f, 0.95f, 0.90f);
	[Export] public Color RuleLine { get; set; } = new Color(0.77f, 0.84f, 0.89f);
	[Export] public Color MarginLine { get; set; } = new Color(0.91f, 0.65f, 0.71f);
	[Export] public Color Ink { get; set; } = new Color(0.19f, 0.31f, 0.61f);
	[Export] public Color GroundFill { get; set; } = new Color(0.95f, 0.93f, 0.86f);
	[Export] public Color GroundCrayon { get; set; } = new Color(0.62f, 0.77f, 0.88f);
	[Export] public Color PlatformCrayon { get; set; } = new Color(0.91f, 0.79f, 0.54f);
	[Export] public Color PropFill { get; set; } = new Color(0.93f, 0.90f, 0.83f);
	[Export] public Color PropCrayon { get; set; } = new Color(0.80f, 0.70f, 0.55f);

	/// <summary>Lit windows, chalk marks, anything that should read as a warm highlight.</summary>
	[Export] public Color Accent { get; set; } = new Color(0.97f, 0.83f, 0.42f);

	/// <summary>Sun or moon.</summary>
	[Export] public Color Celestial { get; set; } = new Color(0.97f, 0.88f, 0.55f);

	/// <summary>Night stages light their windows and draw a moon instead of a sun.</summary>
	[Export] public bool Night { get; set; } = false;

	// --- Bounds --------------------------------------------------------------

	[Export] public Rect2 BlastZone { get; set; } = new Rect2(
		new Vector2(DefaultBlastLeft, DefaultBlastTop),
		new Vector2(DefaultBlastRight - DefaultBlastLeft, DefaultBlastBottom - DefaultBlastTop));

	/// <summary>
	/// When false there is no way to lose a stock downward: the only KOs are off the sides and
	/// the top. Set on stages whose ground runs the full width of the play area.
	/// </summary>
	[Export] public bool HasBottomBlastZone { get; set; } = true;

	/// <summary>
	/// How far out the camera may pull back, as a zoom. A big stage made for four players lets it go
	/// further, or two fighters at its far ends could not both be on screen; nowhere goes so far that
	/// a fighter is a speck.
	/// </summary>
	[Export] public float CameraMinZoom { get; set; } = 0.42f;

	/// <summary>Made for three or four players (Eric, 2026-10-16). The stage select says so.</summary>
	[Export] public bool Large { get; set; }

	public const float DefaultBlastLeft = -1560.0f;
	public const float DefaultBlastRight = 1560.0f;
	public const float DefaultBlastTop = -1280.0f;
	public const float DefaultBlastBottom = 1080.0f;

	// --- Geometry ------------------------------------------------------------

	public StagePlatform[] Platforms = System.Array.Empty<StagePlatform>();
	public StageProp[] Props = System.Array.Empty<StageProp>();

	/// <summary>Scenery drawn on the pages rather than behind them, on a paper stage: the kitchen's window.</summary>
	public StageProp[] OnPage = System.Array.Empty<StageProp>();
	public Vector2[] SpawnPoints = { Vector2.Zero };
	public Vector2 RespawnPoint = new Vector2(0.0f, -620.0f);

	/// <summary>
	/// Cars drive along the stage's <see cref="PlatformLook.Road"/> at random and knock away
	/// anyone standing in the street (see <see cref="StreetTraffic"/>).
	/// </summary>
	public bool Traffic;

	/// <summary>
	/// The Big Show's speakers play loud: the cones thump on the beat and now and then one stack
	/// swells and blasts whoever is near it back (SpeakerBlast).
	/// </summary>
	public bool Speakers;

	/// <summary>
	/// The battle music this stage fights to (assets/music, made by tools/audio/music.py): every
	/// stage has its own (Eric, 2026-10-18) - guitar rock at the Big Show, industrial on the Building
	/// Site. A story's boss fights to his own theme instead.
	/// </summary>
	public string Music = "battle";

	/// <summary>How far a <see cref="PlatformLook.HangingBeam"/> hangs below the crane jib it hangs from.</summary>
	public const float CraneDrop = 220.0f;

	/// <summary>The sheet of paper the stage is drawn on, for a <see cref="StageStyle.TapedPage"/> stage.</summary>
	public Rect2 PageRect;

	/// <summary>The sheets on a <see cref="StageStyle.PinnedPages"/> stage, back to front.</summary>
	public PinnedPage[] Pages = System.Array.Empty<PinnedPage>();

	/// <summary>Where the drawn ground sits, for props that need to stand on it.</summary>
	public float GroundLine = 0.0f;

	/// <summary>
	/// True when a point is past a blast zone this stage actually has. A stage without a
	/// bottom blast zone simply never reports one, however far a fighter falls.
	/// </summary>
	public bool IsOutOfBounds(Vector2 point)
	{
		if (point.X < BlastZone.Position.X || point.X > BlastZone.End.X) return true;
		if (point.Y < BlastZone.Position.Y) return true;
		if (HasBottomBlastZone && point.Y > BlastZone.End.Y) return true;
		return false;
	}
}
