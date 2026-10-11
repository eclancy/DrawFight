/// <summary>
/// How a fighter's face is set, each 0 to 1: what FighterRig.SetFace does to the eyes, brows and
/// mouth lifted out of their drawing (tools/art/faces.py). A blink squashes an eye flat, a squint
/// halves it, a shout stretches the mouth open - the drawn marks themselves, moved, never redrawn.
/// </summary>
public struct FaceState
{
	public float Blink;
	public float Squint;
	public float Wide;
	public float Angry;
	public float Sad;
	public float Surprise;
	public float Open;
	public float Grit;
	/// <summary>Which way the eyes look, -1 (behind) to 1 (ahead), and down (+) or up (-).</summary>
	public float LookX, LookY;

	public static FaceState Lerp(FaceState a, FaceState b, float t) => new FaceState
	{
		Blink = a.Blink + (b.Blink - a.Blink) * t,
		Squint = a.Squint + (b.Squint - a.Squint) * t,
		Wide = a.Wide + (b.Wide - a.Wide) * t,
		Angry = a.Angry + (b.Angry - a.Angry) * t,
		Sad = a.Sad + (b.Sad - a.Sad) * t,
		Surprise = a.Surprise + (b.Surprise - a.Surprise) * t,
		Open = a.Open + (b.Open - a.Open) * t,
		Grit = a.Grit + (b.Grit - a.Grit) * t,
		LookX = a.LookX + (b.LookX - a.LookX) * t,
		LookY = a.LookY + (b.LookY - a.LookY) * t,
	};

	// The faces a story can ask for by name, and the ones the parade shows (--film=face).
	public static readonly FaceState Neutral = new FaceState();
	public static readonly FaceState Closed = new FaceState { Blink = 1.0f };
	public static readonly FaceState Squinting = new FaceState { Squint = 0.8f, Angry = 0.3f };
	public static readonly FaceState Angered = new FaceState { Angry = 1.0f, Squint = 0.45f, Grit = 0.8f };
	public static readonly FaceState Shouting = new FaceState { Angry = 1.0f, Squint = 0.2f, Open = 1.0f };
	public static readonly FaceState Hurt = new FaceState { Blink = 0.85f, Open = 0.85f, Sad = 0.6f };
	public static readonly FaceState Shocked = new FaceState { Wide = 1.0f, Surprise = 1.0f, Open = 0.7f };
	public static readonly FaceState Happy = new FaceState { Wide = 0.15f, Surprise = 0.3f, Open = 0.35f };
	public static readonly FaceState Sorry = new FaceState { Sad = 1.0f, LookY = 0.6f, Squint = 0.2f };
	public static readonly FaceState Smug = new FaceState { Squint = 0.55f, Angry = 0.25f, LookX = 0.5f };
	public static readonly FaceState Talking = new FaceState { Open = 0.6f };

	public static FaceState Named(string name) => name switch
	{
		"closed" => Closed,
		"squint" => Squinting,
		"angry" => Angered,
		"shout" => Shouting,
		"hurt" => Hurt,
		"shocked" => Shocked,
		"happy" => Happy,
		"sad" => Sorry,
		"smug" => Smug,
		_ => Neutral,
	};
}
