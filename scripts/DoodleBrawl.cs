using System.Collections.Generic;
using Godot;

/// <summary>
/// One player's item for a doodle brawl: the page they drew it on, its undo, and whether it is
/// thrown or swung. Kept for the session on GameRoot (DoodleSet) - a rematch starts from it - and
/// never saved or sent anywhere: it is not one of "Your drawings" and not in the gallery.
/// </summary>
public sealed class Doodle
{
	public readonly DrawingPage Page = new DrawingPage { Name = "Item", Kind = PageKind.Held };
	public readonly EditHistory History = new EditHistory();

	/// <summary>Throw or Swing - what they picked when they said Done.</summary>
	public ItemUse Use = ItemUse.Swing;

	/// <summary>They have picked Throw or Swing for it once, so the choice can show which, next time.</summary>
	public bool Picked;

	public Doodle()
	{
		Page.Layers.Add(new DrawingLayer("Item"));
	}

	public DrawingLayer Layer => Page.Layers[0];

	/// <summary>Nothing on the page anyone could see - a page left blank, which is skipped.</summary>
	public bool IsBlank => DoodleSet.InkBounds(Drawing.Composite(Page)) == null;
}

/// <summary>
/// Doodle brawl (Eric's call, 2026-10-29): every person playing draws one item before the fight,
/// and the fight drops only those. A drawn item is not a new kind of item: it is the catalog's
/// paintbrush (swung) or scrunched-up drawing (thrown) - their numbers, their swing, their bounce -
/// wearing the player's drawing, so balance stays the catalog's and everything items already do
/// (picking up, the held pose, swinging, throwing, the sounds) just works. See .ai/modes.md.
/// </summary>
public sealed class DoodleSet
{
	/// <summary>The catalog items a drawn item takes its numbers from.</summary>
	public const string SwingBase = "paintbrush";
	public const string ThrowBase = "paper_ball";

	/// <summary>
	/// How big a thrown drawing is, along its longest side. The scrunched-up drawing is 60, which is
	/// a fist-sized ball; a drawing that small lost what was drawn on it. 80 is the paint bucket.
	/// </summary>
	const float ThrowSize = 80.0f;

	/// <summary>The longest side of a drawn item's picture: about twice as big as it is ever shown, like the catalog's.</summary>
	const int ArtPixels = 320;

	readonly Doodle[] seats = new Doodle[GameRoot.MaxPlayers];

	/// <summary>A seat's item, started blank the first time it is asked for.</summary>
	public Doodle For(int seat) => seats[seat] ??= new Doodle();

	/// <summary>Hand every fighter one of the drawn items as the fight starts (--doodlehold): to look at them held and swung.</summary>
	public bool TestHold;

	/// <summary>The id a seat's drawn item goes by - "doodle_1" for Player 1 - so --film=item:doodle_1 can find it.</summary>
	public static string IdFor(int seat) => $"doodle_{seat + 1}";

	/// <summary>
	/// Sets a match up for a doodle brawl: only the items drawn by the people in it, often (High),
	/// whatever the item settings say. Nobody drew anything, and nothing drops.
	/// </summary>
	public void Arm(MatchManager match)
	{
		GameRoot root = GameRoot.Instance;
		var items = new List<ItemData>();
		for (int seat = 0; seat < GameRoot.MaxPlayers; seat++)
		{
			// A CPU does not draw; a seat that drew last time and is a CPU now has no item this time.
			if (root.SelectedFighters[seat] < 0 || root.CpuPlayers[seat] || seats[seat] == null) continue;
			ItemData item = Register(seat, root.SeatNames[seat]);
			if (item != null) items.Add(item);
		}
		match.Doodles = new DoodleItems(items);
		match.OnlyItem = "";
		match.Items = new ItemSettings { Frequency = items.Count > 0 ? ItemFrequency.High : ItemFrequency.Off };
		if (TestHold && items.Count > 0) match.Ready += () => HandOut(match, items);
	}

	static void HandOut(MatchManager match, List<ItemData> items)
	{
		for (int i = 0; i < match.Fighters.Count; i++)
		{
			Fighter f = match.Fighters[i];
			f.CatchItem(match.SpawnItem(items[i % items.Count], f.GlobalPosition, Vector2.Zero));
		}
	}

	/// <summary>A seat's drawing made into an item and put in the catalog by its id; null for a blank page.</summary>
	ItemData Register(int seat, string name)
	{
		Doodle doodle = seats[seat];
		bool swing = doodle.Use == ItemUse.Swing;
		ItemData from = ItemCatalog.Find(swing ? SwingBase : ThrowBase);
		var data = new ItemData
		{
			Id = IdFor(seat),
			DisplayName = (string.IsNullOrEmpty(name) ? $"P{seat + 1}" : name) + "'s drawing",
			Use = from.Use, Impact = from.Impact,
			Size = swing ? from.Size : ThrowSize,
			Swings = from.Swings, Cooldown = from.Cooldown, Uses = from.Uses,
			Duration = from.Duration, Radius = from.Radius,
			ThrowSpeed = from.ThrowSpeed, Gravity = from.Gravity, Tint = from.Tint,
			// The same moves, not copies: a drawn sword hits exactly as hard as the paintbrush.
			Swing = from.Swing, Hit = from.Hit,
		};
		ItemArt art = ArtFrom(doodle.Page, swing);
		if (art == null) return null;
		ItemCatalog.AddDrawn(data, art);
		return data;
	}

	// --- The drawing as an item's art ------------------------------------------------------

	/// <summary>Ink fainter than this is invisible on the page, and does not count toward where the drawing is.</summary>
	const int Faint = 8;

	/// <summary>Where on the page anything visible is drawn, or null for a blank page.</summary>
	public static Rect2I? InkBounds(byte[] pixels)
	{
		int x0 = Drawing.Width, y0 = Drawing.Height, x1 = -1, y1 = -1;
		for (int y = 0; y < Drawing.Height; y++)
		{
			int row = y * Drawing.Width * 4;
			for (int x = 0; x < Drawing.Width; x++)
			{
				if (pixels[row + x * 4 + 3] < Faint) continue;
				if (x < x0) x0 = x;
				if (x > x1) x1 = x;
				if (y < y0) y0 = y;
				y1 = y;
			}
		}
		return x1 < 0 ? null : new Rect2I(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
	}

	/// <summary>
	/// The drawing cut out of its page, exactly as drawn - cropped to the ink and made smaller,
	/// never redrawn (rule 1) - with where it is held and where it hits. Swung, it is held near
	/// the bottom and hits with the top, as the page told them; thrown, it is held in the middle.
	/// </summary>
	public static ItemArt ArtFrom(DrawingPage page, bool swing)
	{
		byte[] pixels = Drawing.Composite(page);
		if (InkBounds(pixels) is not Rect2I ink) return null;

		Vector2 grip, head;
		if (swing)
		{
			// The fist closes over the bottom tenth, centred on whatever is drawn down there - a
			// handle need not be in the middle of the drawing.
			grip = new Vector2(InkCentreX(pixels, ink, ink.End.Y - ink.Size.Y * 0.2f, ink.End.Y), ink.End.Y - ink.Size.Y * 0.1f);
			head = new Vector2(InkCentreX(pixels, ink, ink.Position.Y, ink.Position.Y + ink.Size.Y * 0.12f), ink.Position.Y + ink.Size.Y * 0.03f);
		}
		else
		{
			grip = new Vector2(ink.Position.X + ink.Size.X * 0.5f, ink.Position.Y + ink.Size.Y * 0.6f);
			head = grip;
		}

		// A little paper round it, so filtering never clips the outermost dab.
		Rect2I crop = ink.Grow(8).Intersection(new Rect2I(0, 0, Drawing.Width, Drawing.Height));
		Image image = Image.CreateFromData(Drawing.Width, Drawing.Height, false, Image.Format.Rgba8, pixels).GetRegion(crop);
		// See-through pixels take the colour of the ink beside them, so scaling never mixes a dark
		// fringe into the edges (what DrawingLayer.Bleed does for the page).
		image.FixAlphaEdges();
		float scale = Mathf.Min(1.0f, ArtPixels / (float)Mathf.Max(crop.Size.X, crop.Size.Y));
		if (scale < 1.0f)
		{
			image.Resize(Mathf.Max(1, Mathf.RoundToInt(crop.Size.X * scale)), Mathf.Max(1, Mathf.RoundToInt(crop.Size.Y * scale)), Image.Interpolation.Lanczos);
		}
		Vector2 origin = crop.Position;
		return new ItemArt
		{
			Texture = ImageTexture.CreateFromImage(image),
			Grip = (grip - origin) * scale,
			Head = (head - origin) * scale,
			Size = new Vector2(image.GetWidth(), image.GetHeight()),
		};
	}

	/// <summary>Across the page, the middle of the ink between two heights - the bounds' middle if there is none.</summary>
	static float InkCentreX(byte[] pixels, Rect2I ink, float fromY, float toY)
	{
		double sum = 0.0, weight = 0.0;
		for (int y = Mathf.Max(ink.Position.Y, (int)fromY); y < Mathf.Min(ink.End.Y, Mathf.CeilToInt(toY) + 1); y++)
		{
			int row = y * Drawing.Width * 4;
			for (int x = ink.Position.X; x < ink.End.X; x++)
			{
				int a = pixels[row + x * 4 + 3];
				if (a < 64) continue;
				sum += x * a;
				weight += a;
			}
		}
		return weight > 0.0 ? (float)(sum / weight) : ink.Position.X + ink.Size.X * 0.5f;
	}

	// --- Test drawings (--doodletest) ----------------------------------------------------------

	/// <summary>
	/// A test, not a feature: nobody can draw by hand in a screenshot run, so --doodletest paints
	/// an item onto every seat's page through the drawing screen's own brushes - a sword and a
	/// hammer to swing, a ball and a star to throw - and registers them, so a doodle match or the
	/// parade (--film=item:doodle_1) has drawn items in it.
	/// </summary>
	/// <param name="shift">Turns the drawings round the seats: 1 gives Player 1 the ball, to watch a thrown one.</param>
	public void FillForTest(int shift = 0)
	{
		var painter = new Painter();
		Color ink = DrawScreen.Palette[0];
		for (int seat = 0; seat < GameRoot.MaxPlayers; seat++)
		{
			Doodle d = For(seat);
			System.Array.Clear(d.Layer.Pixels);
			d.Layer.TouchAll();
			switch ((seat + shift) % 4)
			{
				case 0:
					// A sword: blade, crossguard, handle and a knob on the end.
					Outline(painter, d, ink, new[] { new Vector2(660, 760), new Vector2(660, 280), new Vector2(700, 170), new Vector2(742, 280), new Vector2(742, 760) }, false);
					Outline(painter, d, ink, Box(560, 760, 842, 806), true);
					Outline(painter, d, ink, Box(676, 806, 724, 920), true);
					Outline(painter, d, ink, Ring(new Vector2(700, 950), 32, 0.0f), true);
					Fill(d, 700, 500, DrawScreen.Palette[3]);
					Fill(d, 600, 783, DrawScreen.Palette[6]);
					Fill(d, 700, 860, DrawScreen.Palette[5]);
					Fill(d, 700, 950, DrawScreen.Palette[17]);
					d.Use = ItemUse.Swing;
					break;
				case 1:
					// A spotty ball.
					Outline(painter, d, ink, Ring(new Vector2(700, 560), 190, 9.0f), true);
					Fill(d, 700, 560, DrawScreen.Palette[15]);
					for (int k = 0; k < 4; k++)
					{
						Vector2 at = new Vector2(620 + k * 55, 470 + (k % 2) * 120);
						Outline(painter, d, DrawScreen.Palette[11], Ring(at, 22, 3.0f), true);
					}
					d.Use = ItemUse.Throw;
					break;
				case 2:
					// A hammer: a big head on a long handle.
					Outline(painter, d, ink, Box(540, 240, 860, 420), true);
					Outline(painter, d, ink, Box(676, 420, 724, 940), true);
					Fill(d, 700, 330, DrawScreen.Palette[2]);
					Fill(d, 700, 700, DrawScreen.Palette[12]);
					d.Use = ItemUse.Swing;
					break;
				default:
					// A star.
					var star = new List<Vector2>();
					for (int k = 0; k < 10; k++)
					{
						float r = k % 2 == 0 ? 230.0f : 100.0f;
						star.Add(new Vector2(700, 560) + Vector2.Up.Rotated(Mathf.Tau * k / 10.0f) * r);
					}
					Outline(painter, d, ink, star.ToArray(), true);
					Fill(d, 700, 560, DrawScreen.Palette[17]);
					d.Use = ItemUse.Throw;
					break;
			}
			d.Picked = true;
			d.History.Forget(d.Page);
			Register(seat, null);
		}
	}

	static Vector2[] Box(float x0, float y0, float x1, float y1) => new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };

	static Vector2[] Ring(Vector2 centre, float radius, float wobble)
	{
		var points = new Vector2[24];
		for (int i = 0; i < points.Length; i++)
		{
			float a = Mathf.Tau * i / points.Length;
			points[i] = centre + Vector2.FromAngle(a) * (radius + wobble * Mathf.Sin(a * 5.0f));
		}
		return points;
	}

	/// <summary>One marker stroke round the points, as a hand would go - a slight shake, hashed, never random.</summary>
	static void Outline(Painter painter, Doodle d, Color colour, Vector2[] points, bool closed)
	{
		int count = closed ? points.Length + 1 : points.Length;
		painter.Begin(d.Page, d.Layer, PaintTool.Marker, colour, DrawScreen.Sizes[0][3], points[0], 1.0f);
		for (int i = 1; i < count; i++)
		{
			Vector2 from = points[i - 1], to = points[i % points.Length];
			for (int step = 1; step <= 8; step++)
			{
				Vector2 at = from.Lerp(to, step / 8.0f);
				painter.To(at + new Vector2(Mathf.Sin(i * 3.1f + step), Mathf.Cos(i * 1.7f + step * 2.0f)) * 2.0f, 1.0f);
			}
		}
		d.History.Push(painter.End());
	}

	static void Fill(Doodle d, int x, int y, Color colour) => d.History.Push(Painter.Fill(d.Page, d.Layer, new Vector2I(x, y), colour, true));
}

/// <summary>The drawn items a doodle brawl drops: each in turn, from a random one, so everyone's comes round.</summary>
public sealed class DoodleItems
{
	readonly List<ItemData> items;
	int next;

	public DoodleItems(List<ItemData> items)
	{
		this.items = items;
		next = items.Count > 0 ? new System.Random().Next(items.Count) : 0;
	}

	public int Count => items.Count;

	public ItemData Next()
	{
		if (items.Count == 0) return null;
		return items[next++ % items.Count];
	}
}
