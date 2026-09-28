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
public enum PropKind { Sun, Moon, Cloud, Star, Hill, Bush, TapedDrawing, FridgeHandle, Fence }

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
