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
	/// middle down to the ground, with the cab, a counterweight and a hook.
	/// </summary>
	CraneJib,

	/// <summary>A steel beam hanging from a crane on two cables, which run up <see cref="StageData.CraneDrop"/> to the jib.</summary>
	HangingBeam,

	/// <summary>A scaffolding plank, with its poles and cross-braces drawn down to the ground.</summary>
	Scaffold,

	/// <summary>
	/// A street: a pavement along the top and a road with a dashed centre line. When the stage
	/// has <see cref="StageData.Traffic"/>, cars drive along the top of it.
	/// </summary>
	Road,
}

/// <summary>One collidable surface, plus how to draw it.</summary>
public sealed class StagePlatform
{
	public Rect2 Rect;

	/// <summary>Soft platforms can be jumped up through and dropped down through.</summary>
	public bool OneWay;

	public PlatformLook Look = PlatformLook.Ledge;

	public StagePlatform(Rect2 rect, bool oneWay, PlatformLook look)
	{
		Rect = rect;
		OneWay = oneWay;
		Look = look;
	}
}

/// <summary>Scenery. Never collidable - if a fighter can stand on it, it is a platform.</summary>
public enum PropKind { Sun, Moon, Cloud, Star, Hill, Bush, TapedDrawing, FridgeHandle, Fence, Tree, Grass, FridgeMagnet, Skyline }

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

	public const float DefaultBlastLeft = -1560.0f;
	public const float DefaultBlastRight = 1560.0f;
	public const float DefaultBlastTop = -1280.0f;
	public const float DefaultBlastBottom = 1080.0f;

	// --- Geometry ------------------------------------------------------------

	public StagePlatform[] Platforms = System.Array.Empty<StagePlatform>();
	public StageProp[] Props = System.Array.Empty<StageProp>();
	public Vector2[] SpawnPoints = { Vector2.Zero };
	public Vector2 RespawnPoint = new Vector2(0.0f, -620.0f);

	/// <summary>
	/// Cars drive along the stage's <see cref="PlatformLook.Road"/> at random and knock away
	/// anyone standing in the street (see <see cref="StreetTraffic"/>).
	/// </summary>
	public bool Traffic;

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
