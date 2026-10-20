using Godot;

/// <summary>What a name style draws round its letters, beyond the letters themselves.</summary>
public enum NameDeco
{
	None,
	/// <summary>Tongues of flame licking up off the tops of the letters (FlameFx). Flambé.</summary>
	Flames,
	/// <summary>A hazard-striped bar under the word, bolted at both ends. Lugnut.</summary>
	HazardBar,
	/// <summary>A sword laid under the word, hilt to point, with a glint running along it. EdgeLord.</summary>
	Blade,
	/// <summary>A steel strip under the word, riveted along its length. DoomBot.</summary>
	Rivets,
	/// <summary>Speed lines trailing out behind the word, and a top hat on its first letter. Triguy.</summary>
	SpeedLines,
}

/// <summary>
/// How a fighter's name is lettered - on the select screen, over the percent in a match, on a
/// story's speech bubble: every fighter's name written the way they would write it
/// (.ai/art-direction.md, "Every name in its own letters"). Drawn by <see cref="NameLettering"/>,
/// the one drawer; a style is only data, as a stage is.
///
/// Sizes and distances are fractions of the font size, so one style reads the same on a 26px
/// speech bubble and a 190px winner's banner. Every motion is counted in frames and every wobble
/// hashed from a seed, never random.
/// </summary>
[GlobalClass]
public partial class NameStyle : Resource
{
	/// <summary>The letters' colour - the first letter's, if <see cref="FillTo"/> is set.</summary>
	[Export] public Color Fill { get; set; } = MenuTheme.Text;

	/// <summary>The last letter's colour, the ones between running from Fill to it. Transparent for one colour.</summary>
	[Export] public Color FillTo { get; set; } = new Color(0.0f, 0.0f, 0.0f, 0.0f);

	/// <summary>Each letter's own colour in turn, over Fill - scrap bolted on from all over. Empty for none.</summary>
	[Export] public Color[] Patches { get; set; } = System.Array.Empty<Color>();

	/// <summary>A coloured line round every letter, inside the paper halo every name has. Transparent for none.</summary>
	[Export] public Color Edge { get; set; } = new Color(0.0f, 0.0f, 0.0f, 0.0f);

	/// <summary>How thick that line is, as a fraction of the font size.</summary>
	[Export] public float EdgeWidth { get; set; } = 0.0f;

	/// <summary>The edge gone over twice, each a little off the other, as a marker draws: how far off.</summary>
	[Export] public float Scrawl { get; set; } = 0.0f;

	/// <summary>A copy of the letters set behind them, offset by <see cref="ShadowOffset"/>. Transparent for none.</summary>
	[Export] public Color Shadow { get; set; } = new Color(0.0f, 0.0f, 0.0f, 0.0f);

	[Export] public Vector2 ShadowOffset { get; set; } = new Vector2(0.05f, 0.05f);

	/// <summary>
	/// Light on the top-left edge of each letter and shade on the bottom-right, this far (a
	/// fraction of the font size): round, or bevelled metal. Zero for flat letters.
	/// </summary>
	[Export] public float Bevel { get; set; } = 0.0f;
	[Export] public Color Highlight { get; set; } = new Color(1.0f, 1.0f, 1.0f, 0.0f);
	[Export] public Color Shade { get; set; } = new Color(0.0f, 0.0f, 0.0f, 0.0f);

	/// <summary>Leans the letters forward: how far right the top of a letter goes, per unit of its height.</summary>
	[Export] public float Slant { get; set; } = 0.0f;

	/// <summary>Letters stretched wide (over 1) or squeezed narrow (under 1).</summary>
	[Export] public float Stretch { get; set; } = 1.0f;

	/// <summary>Thickens every stroke (FontVariation.VariationEmbolden).</summary>
	[Export] public float Embolden { get; set; } = 0.0f;

	/// <summary>Extra room between letters, as a fraction of the font size.</summary>
	[Export] public float Spacing { get; set; } = 0.0f;

	/// <summary>Bigger or smaller than whatever size the name is asked for - modestly, so a layout still fits.</summary>
	[Export] public float SizeScale { get; set; } = 1.0f;

	/// <summary>All capitals.</summary>
	[Export] public bool Caps { get; set; }

	/// <summary>Every letter in a cell of the same width, like a terminal.</summary>
	[Export] public bool Mono { get; set; }

	/// <summary>How far each letter bobs up and down, as a fraction of the font size, each on its own beat.</summary>
	[Export] public float Bob { get; set; } = 0.0f;

	/// <summary>How many frames one bob takes.</summary>
	[Export] public int BobFrames { get; set; } = 60;

	/// <summary>How far, in degrees, each letter rocks side to side as it bobs.</summary>
	[Export] public float Rock { get; set; } = 0.0f;

	/// <summary>Each letter set crooked by up to this many degrees, and up to <see cref="Drop"/> off the line - fixed, hashed per letter.</summary>
	[Export] public float Askew { get; set; } = 0.0f;
	[Export] public float Drop { get; set; } = 0.0f;

	/// <summary>Now and then a letter judders this far (a fraction of the font size), every so many frames.</summary>
	[Export] public float Judder { get; set; } = 0.0f;
	[Export] public int JudderFrames { get; set; } = 6;

	/// <summary>The letters' brightness flickers, by up to this much, as firelight does.</summary>
	[Export] public float Flicker { get; set; } = 0.0f;

	/// <summary>Glows hotter as his heat rises, where it is known - in a match, or a story he is fighting in.</summary>
	[Export] public bool Glows { get; set; }

	[Export] public NameDeco Deco { get; set; } = NameDeco.None;
	[Export] public Color DecoColor { get; set; } = Colors.White;
	[Export] public Color DecoColorTwo { get; set; } = Colors.White;

	/// <summary>Whether anything about it moves, so whatever shows it has to redraw every frame.</summary>
	public bool Moves => Bob > 0.0f || Judder > 0.0f || Flicker > 0.0f || Deco == NameDeco.Flames
		|| Deco == NameDeco.Blade || Deco == NameDeco.SpeedLines;

	/// <summary>
	/// A fighter with no style of their own: their colour, in the paper halo every name has. What
	/// every name looked like before they had their own.
	/// </summary>
	public static NameStyle Plain(Color colour) => new NameStyle { Fill = colour };

	// The roster's letters. Every colour stays light enough to keep to the 30% line outside a
	// character (.ai/art-direction.md): the edges are deep colours, not black.

	/// <summary>Flambé: hot orange running to yellow, leaning into it, with flames licking off the tops.</summary>
	public static NameStyle Flambe() => new NameStyle
	{
		Fill = new Color(1.0f, 0.50f, 0.12f),
		FillTo = new Color(1.0f, 0.78f, 0.20f),
		Edge = new Color(0.66f, 0.16f, 0.08f),
		EdgeWidth = 0.14f,
		Bevel = 0.025f,
		Highlight = new Color(1.0f, 0.93f, 0.62f),
		Shade = new Color(0.86f, 0.30f, 0.08f),
		Slant = 0.14f,
		Embolden = 0.5f,
		Flicker = 0.07f,
		Deco = NameDeco.Flames,
	};

	/// <summary>Lugnut: chunky wide capitals in hi-vis yellow, a hard shadow, on a hazard-striped bar.</summary>
	public static NameStyle Lugnut() => new NameStyle
	{
		Fill = new Color(1.0f, 0.78f, 0.14f),
		Edge = new Color(0.52f, 0.30f, 0.08f),
		EdgeWidth = 0.17f,
		Shadow = new Color(0.62f, 0.40f, 0.16f, 0.55f),
		ShadowOffset = new Vector2(0.06f, 0.06f),
		Stretch = 1.12f,
		Embolden = 1.4f,
		Spacing = 0.03f,
		SizeScale = 0.94f,
		Caps = true,
		Deco = NameDeco.HazardBar,
		DecoColor = new Color(1.0f, 0.80f, 0.16f),
		DecoColorTwo = new Color(0.84f, 0.84f, 0.82f),
	};

	/// <summary>Circy: round, fat yellow letters in a scrawled marker line, each bobbing on its own beat.</summary>
	public static NameStyle Circy() => new NameStyle
	{
		Fill = new Color(1.0f, 0.86f, 0.26f),
		Edge = new Color(0.50f, 0.32f, 0.04f),
		EdgeWidth = 0.20f,
		Scrawl = 0.03f,
		Bevel = 0.03f,
		Highlight = new Color(1.0f, 0.97f, 0.76f),
		Shade = new Color(0.94f, 0.62f, 0.10f),
		Embolden = 1.0f,
		Spacing = 0.06f,
		SizeScale = 1.04f,
		Bob = 0.07f,
		BobFrames = 54,
		Rock = 6.0f,
	};

	/// <summary>EdgeLord: sharp red italics, narrow and leaning hard, over a sword with a glint running down it.</summary>
	public static NameStyle EdgeLord() => new NameStyle
	{
		Fill = new Color(0.92f, 0.16f, 0.18f),
		FillTo = new Color(0.76f, 0.08f, 0.16f),
		Edge = new Color(0.44f, 0.04f, 0.10f),
		EdgeWidth = 0.11f,
		Bevel = 0.02f,
		Highlight = new Color(1.0f, 0.66f, 0.62f),
		Shade = new Color(0.52f, 0.04f, 0.10f),
		Slant = 0.36f,
		Stretch = 0.92f,
		Embolden = 0.35f,
		Spacing = 0.02f,
		Deco = NameDeco.Blade,
		DecoColor = new Color(0.84f, 0.86f, 0.92f),
		DecoColorTwo = new Color(0.50f, 0.52f, 0.62f),
	};

	/// <summary>DoomBot: bevelled steel capitals in fixed cells, rust round the edges, on a riveted strip - glowing red as he heats up.</summary>
	public static NameStyle DoomBot() => new NameStyle
	{
		Fill = new Color(0.66f, 0.68f, 0.73f),
		Edge = new Color(0.48f, 0.17f, 0.12f),
		EdgeWidth = 0.18f,
		Bevel = 0.03f,
		Highlight = new Color(0.95f, 0.96f, 0.99f),
		Shade = new Color(0.40f, 0.42f, 0.48f),
		Embolden = 0.9f,
		Caps = true,
		Mono = true,
		SizeScale = 0.92f,
		Judder = 0.03f,
		JudderFrames = 8,
		Glows = true,
		Deco = NameDeco.Rivets,
		DecoColor = new Color(0.70f, 0.72f, 0.77f),
		DecoColorTwo = new Color(0.42f, 0.44f, 0.50f),
	};

	/// <summary>DoomBot Mk. II: DoomBot's letters in newer blue steel, every one a different bit of scrap bolted on crooked.</summary>
	public static NameStyle DoomBotMk2()
	{
		NameStyle style = DoomBot();
		style.Fill = new Color(0.70f, 0.78f, 0.92f);
		style.Patches = new[]
		{
			new Color(0.70f, 0.78f, 0.92f),
			new Color(0.84f, 0.60f, 0.44f),
			new Color(0.72f, 0.76f, 0.64f),
			new Color(0.82f, 0.84f, 0.88f),
			new Color(0.64f, 0.71f, 0.84f),
		};
		style.Edge = new Color(0.22f, 0.34f, 0.62f);
		style.Askew = 5.0f;
		style.Drop = 0.04f;
		style.DecoColor = new Color(0.72f, 0.78f, 0.88f);
		style.DecoColorTwo = new Color(0.40f, 0.46f, 0.60f);
		return style;
	}

	/// <summary>Triguy: white letters edged in Elim's blue, leaning right forward, speed lines behind and a top hat on.</summary>
	public static NameStyle Triguy() => new NameStyle
	{
		Fill = new Color(1.0f, 1.0f, 1.0f),
		Edge = new Color(0.10f, 0.34f, 0.92f),
		EdgeWidth = 0.15f,
		Shadow = new Color(0.62f, 0.76f, 1.0f, 0.8f),
		ShadowOffset = new Vector2(-0.06f, 0.0f),
		Slant = 0.30f,
		Embolden = 0.6f,
		Spacing = 0.01f,
		Deco = NameDeco.SpeedLines,
		DecoColor = new Color(0.40f, 0.62f, 1.0f),
		DecoColorTwo = new Color(0.28f, 0.28f, 0.36f),
	};
}
