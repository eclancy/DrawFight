using System.Collections.Generic;
using Godot;

/// <summary>
/// The drawings DoomBot took down, heaped at the foot of the fridge in the finale ("pile at X"):
/// the other fighters' own drawings - everyone but him and whoever's story it is - each on its
/// sheet of paper, and each damaged the way he took it down. Some are torn, a jagged piece gone;
/// some half rubbed out with an eraser; some with a corner bent over or the tape still on that
/// held them up.
///
/// One sheet for each of them, nobody twice, thrown down on its side or upside down. Every
/// drawing is the fighter's real rig caught in an action pose - the frame one of their attacks
/// hits, weapon in hand (Eric, 2026-10-21) - rendered once into its own
/// texture when the pile goes down - never redrawn (CLAUDE.md, rule 1). The damage only ever takes
/// away: a tear hides part of the sheet and the drawing on it behind a ragged edge, and the eraser
/// fades the drawing back into the paper. What the pile adds is paper, tape and smudge, all well
/// above the 30% value line - characters own black.
///
/// The board draws the pile every frame for the whole boss fight, so everything costly is done
/// once in <see cref="Build"/>: the textures, the torn outlines, where everything lies. Every
/// variation is hashed from the sheet's place in the pile, never rolled.
/// </summary>
public class TakenDownPile
{
	/// <summary>How many pixels of texture to a pixel of world, so the drawings stay crisp when the camera leans in.</summary>
	const int Resolution = 2;

	static readonly Color Paper = new Color(0.985f, 0.975f, 0.95f);
	static readonly Color PaperBack = new Color(0.90f, 0.89f, 0.86f);
	static readonly Color Ink = new Color(0.19f, 0.31f, 0.61f, 0.7f);
	static readonly Color Shadow = new Color(0.3f, 0.3f, 0.4f, 0.18f);
	static readonly Color Tape = new Color(0.96f, 0.93f, 0.72f, 0.78f);
	static readonly Color Graphite = new Color(0.58f, 0.56f, 0.66f, 0.16f);
	static readonly Color Crumb = new Color(0.90f, 0.70f, 0.72f);
	static readonly Color CrumbEdge = new Color(0.62f, 0.56f, 0.62f);

	enum Tear { None, Side, Corner, Bite }
	enum Rub { None, Top, Patch }

	/// <summary>
	/// The heap, bottom sheet first: where each lies against the pile's centre, how far up the
	/// heap it sits, its tilt, and what he did to it. One sheet per drawing - nobody twice - and
	/// every one thrown down anyhow: on its side, upside down (Eric, 2026-10-21). The ones at the
	/// bottom lie furthest back, so they peek out over the top of the ones in front.
	/// </summary>
	static readonly (float dx, float rise, float angle, Tear tear, Rub rub, bool curl, bool tape)[] Layout =
	{
		(-215.0f, 26.0f, 1.50f, Tear.Bite, Rub.None, false, true),
		(175.0f, 22.0f, -1.62f, Tear.None, Rub.Top, true, false),
		(-55.0f, 118.0f, 3.04f, Tear.Corner, Rub.None, false, true),
		(55.0f, 0.0f, -2.70f, Tear.Side, Rub.Patch, true, true),
	};

	/// <summary>
	/// What each one is caught doing on their sheet: an action pose, the frame one of their own
	/// attacks hits, weapon and all - never standing about (Eric, 2026-10-21).
	/// </summary>
	static readonly MoveSlot[] Action = { MoveSlot.ForwardSmash, MoveSlot.UpSmash, MoveSlot.ForwardAir, MoveSlot.DashAttack };

	class Sheet
	{
		public Vector2 Centre;
		public float Angle;
		public Vector2 Size;
		public ViewportTexture Texture;
		public Vector2[] Outline;
		public Vector2[] Shadow;
		public Vector2[] Uvs;
		public Color[] White;
		/// <summary>Runs of the outline: a cut edge of the sheet, inked, or a tear, which is not.</summary>
		public List<(Vector2[] points, bool torn)> Edges = new List<(Vector2[], bool)>();
		public Vector2[] Flap;
		public Vector2[] TapeStrip;
		public int Seed;
	}

	readonly List<Sheet> sheets = new List<Sheet>();
	readonly List<SubViewport> views = new List<SubViewport>();

	/// <summary>
	/// Lays the pile down at <paramref name="x"/> on the floor: renders every sheet's drawing into
	/// a texture under <paramref name="owner"/> and works out its outline. Everyone on the roster
	/// but DoomBot and <paramref name="playerId"/> is in it.
	/// </summary>
	public void Build(Node owner, string playerId, float x, float floor)
	{
		var others = new List<FighterData>();
		int player = StoryScript.IndexOf(playerId);
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			FighterData data = FighterCatalog.Get(i);
			if (i == player || StoryScript.IdOf(data) == "doombot") continue;
			others.Add(data);
		}
		if (others.Count == 0) return;

		for (int i = 0; i < Layout.Length && i < others.Count; i++)
		{
			var lay = Layout[i];
			int seed = 900 + 37 * i;
			float w = 176.0f + 26.0f * Mathf.Abs(CrayonBrush.Noise(seed, 1));
			var size = new Vector2(w, w * 0.76f);
			float angle = lay.angle + CrayonBrush.Noise(seed, 2) * 0.04f;
			// Tipped on its side a sheet stands taller: rest its lowest corner on the floor.
			float halfHigh = Mathf.Abs(size.X * 0.5f * Mathf.Sin(angle)) + Mathf.Abs(size.Y * 0.5f * Mathf.Cos(angle));
			var sheet = new Sheet
			{
				Size = size,
				Seed = seed,
				Angle = angle,
				Centre = new Vector2(x + lay.dx + CrayonBrush.Noise(seed, 3) * 10.0f, floor - halfHigh * 0.8f - lay.rise),
			};
			FighterData drawing = others[i];
			// Drawn a little off-centre, the way nobody centres a drawing - and on the sheets at the
			// sides of the heap, off towards the side that shows.
			float off = Mathf.Abs(lay.dx) > 100.0f ? Mathf.Sign(lay.dx) * 0.1f : 0.06f * CrayonBrush.Noise(seed, 5);
			sheet.Texture = Render(owner, drawing, drawing.Move(Action[i % Action.Length]), size, off, lay.rub, seed);
			Shape(sheet, lay.tear, lay.curl, lay.tape, lay.dx);
			sheets.Add(sheet);
		}
	}

	public void Free()
	{
		foreach (SubViewport view in views) if (GodotObject.IsInstanceValid(view)) view.QueueFree();
		views.Clear();
		sheets.Clear();
	}

	// --- The drawing on the sheet, rendered once ---------------------------------------------

	/// <summary>
	/// The sheet as it lies flat: paper, the fighter's own drawing standing on it as he was drawn,
	/// facing right, and wherever the eraser went over it. Rendered once and never again.
	/// </summary>
	ViewportTexture Render(Node owner, FighterData data, MoveData action, Vector2 size, float off, Rub rub, int seed)
	{
		var view = new SubViewport
		{
			Size = new Vector2I(Mathf.CeilToInt(size.X * Resolution), Mathf.CeilToInt(size.Y * Resolution)),
			TransparentBg = true,
			Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
		};
		owner.AddChild(view);
		views.Add(view);

		var root = new Node2D { Scale = Vector2.One * Resolution };
		view.AddChild(root);
		var paper = new Node2D();
		root.AddChild(paper);
		paper.Draw += () => paper.DrawRect(new Rect2(Vector2.Zero, size), Paper);

		var holder = new Node2D();
		root.AddChild(holder);
		var rig = new FighterRig();
		holder.AddChild(rig);
		Rect2 body = new Rect2(size * 0.25f, size * 0.5f);
		if (rig.Load(data))
		{
			rig.Normalise(230.0f, 115.0f, data.VisualScale);
			rig.SetFacing(1);
			rig.Robotic = data.Robotic;
			rig.ArmBackLimit = data.ArmReachBack;
			rig.CarryOnShoulder = data.ShouldersProp;
			rig.HandOnHip = data.HandOnHip;
			rig.SetPlanted(true);
			rig.SetStanding(true);
			if (action != null)
			{
				// Caught mid-attack, at the frame it hits, as the attack parade shows it: the
				// weapon the move draws in his hand, reaching as far as a match lets him.
				var pose = new Pose();
				FighterAnimations.SampleAttack(action, FighterAnimations.ShowFrame(action), pose, drama: data.AnimationDrama);
				rig.HandOnHip = false;
				rig.CarryOnShoulder = data.ShouldersProp && action.CarryOnShoulder;
				rig.ArmBackLimit = FighterRig.ArmBackMost;
				rig.ShowProp(action.PropArt ?? "");
				for (int i = 0; i < 60; i++) rig.ApplyDirect(pose, 0.75f);
			}
			else
			{
				for (int i = 0; i < 12; i++) rig.PoseAt(FighterAnimations.Idle, 0.0f, 1.0f);
			}
			Rect2 bounds = rig.BodyBounds(holder);
			if (bounds.Size.X >= 1.0f && bounds.Size.Y >= 1.0f)
			{
				float scale = Mathf.Min(size.X * 0.7f / bounds.Size.X, size.Y * 0.84f / bounds.Size.Y);
				var at = new Vector2(size.X * (0.5f + off), size.Y * 0.93f);
				holder.Scale = Vector2.One * scale;
				holder.Position = new Vector2(at.X - bounds.GetCenter().X * scale, at.Y - bounds.End.Y * scale);
				body = new Rect2(holder.Position + bounds.Position * scale, bounds.Size * scale);
			}
		}

		if (rub != Rub.None)
		{
			var rubbed = new Node2D();
			root.AddChild(rubbed);
			Rect2 over = body;
			rubbed.Draw += () => RubOut(rubbed, over, rub, seed);
		}
		return view.GetTexture();
	}

	/// <summary>
	/// The eraser gone over the drawing: back-and-forth scrubbing in the paper's own colour, so
	/// the drawing fades into the page in streaks - more gone where the strokes cross, a ghost of
	/// it left where they only grazed - with a grey smear of graphite dragged along and the little
	/// rolled crumbs an eraser leaves.
	/// </summary>
	static void RubOut(CanvasItem canvas, Rect2 body, Rub rub, int seed)
	{
		Vector2 centre;
		Vector2 radii;
		float angle = CrayonBrush.Noise(seed, 20) * 0.35f;
		if (rub == Rub.Top)
		{
			// From the head down, as far as he got: the top half of him nearly gone.
			centre = new Vector2(body.GetCenter().X, body.Position.Y + body.Size.Y * 0.18f);
			radii = new Vector2(body.Size.X * 0.75f + 16.0f, body.Size.Y * 0.36f);
		}
		else
		{
			centre = body.Position + body.Size * new Vector2(0.5f + 0.15f * CrayonBrush.Noise(seed, 21), 0.5f + 0.12f * CrayonBrush.Noise(seed, 22));
			radii = new Vector2(body.Size.X * 0.42f + 10.0f, body.Size.Y * 0.24f);
		}

		for (int pass = 0; pass < 3; pass++)
		{
			Vector2[] scrub = Scrub(centre, radii * (1.0f - 0.12f * pass), angle + pass * 0.12f, seed + 31 * pass);
			canvas.DrawPolyline(scrub, new Color(Paper, 0.42f), 11.0f, true);
		}
		// Graphite dragged along by the eraser.
		Vector2[] smear = Scrub(centre + new Vector2(0.0f, radii.Y * 0.2f), radii * 0.8f, angle, seed + 7);
		canvas.DrawPolyline(smear, Graphite, 5.0f, true);

		for (int i = 0; i < 7; i++)
		{
			float a = Mathf.Tau * (i / 7.0f + 0.1f * CrayonBrush.Noise(seed, 40 + i));
			float reach = 1.0f + 0.25f * Mathf.Abs(CrayonBrush.Noise(seed, 50 + i));
			Vector2 at = centre + new Vector2(Mathf.Cos(a) * radii.X, Mathf.Sin(a) * radii.Y) * reach;
			Vector2 along = Vector2.Right.Rotated(CrayonBrush.Noise(seed, 60 + i) * 3.0f) * 3.5f;
			canvas.DrawLine(at - along, at + along, CrumbEdge, 4.2f, true);
			canvas.DrawLine(at - along, at + along, Crumb, 2.6f, true);
		}
	}

	/// <summary>A hand scrubbing an eraser to and fro across an oval, working its way down it.</summary>
	static Vector2[] Scrub(Vector2 centre, Vector2 radii, float angle, int seed)
	{
		var points = new List<Vector2>();
		int rows = Mathf.Max(4, Mathf.RoundToInt(radii.Y * 2.0f / 4.5f));
		for (int k = 0; k <= rows; k++)
		{
			float y = -1.0f + 2.0f * k / rows;
			float half = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - y * y)) * (0.75f + 0.25f * Mathf.Abs(CrayonBrush.Noise(seed, k)));
			float side = k % 2 == 0 ? -1.0f : 1.0f;
			var p = new Vector2(side * half * radii.X, y * radii.Y + CrayonBrush.Noise(seed, 100 + k) * 2.0f);
			points.Add(centre + p.Rotated(angle));
		}
		return points.ToArray();
	}

	// --- What happened to the sheet --------------------------------------------------------

	/// <summary>
	/// The sheet's outline - its corners, with a tear run in jagged where a piece came away - and
	/// the bent corner and the tape, all in the sheet's own space, its top left at the origin.
	/// </summary>
	void Shape(Sheet sheet, Tear tear, bool curl, bool tape, float dx)
	{
		Vector2 s = sheet.Size;
		int seed = sheet.Seed;
		// Corners clockwise from the top left, each with whether the run to the next is a tear.
		var corners = new List<(Vector2 at, bool torn)>
		{
			(Vector2.Zero, false), (new Vector2(s.X, 0.0f), false), (s, false), (new Vector2(0.0f, s.Y), false),
		};
		// Which top corner he tore at, hashed, so it is the same every time; a strip comes off
		// the end that sticks out of the heap, where it shows.
		int which = Mathf.Abs(Mathf.RoundToInt(CrayonBrush.Noise(seed, 70) * 1000.0f)) % 2;
		switch (tear)
		{
			case Tear.Side:
			{
				// A strip off one end: the tear runs top to bottom, wandering.
				bool right = dx != 0.0f ? dx > 0.0f : which == 1;
				float top = s.X * (right ? 0.76f : 0.24f) + CrayonBrush.Noise(seed, 71) * 10.0f;
				float bottom = s.X * (right ? 0.86f : 0.14f) + CrayonBrush.Noise(seed, 72) * 10.0f;
				corners = right
					? new List<(Vector2, bool)> { (Vector2.Zero, false), (new Vector2(top, 0.0f), true), (new Vector2(bottom, s.Y), false), (new Vector2(0.0f, s.Y), false) }
					: new List<(Vector2, bool)> { (new Vector2(top, 0.0f), false), (new Vector2(s.X, 0.0f), false), (s, false), (new Vector2(bottom, s.Y), true) };
				break;
			}
			case Tear.Corner:
			{
				// Big enough to take a piece of the drawing with it.
				float along = s.X * (0.64f + 0.06f * Mathf.Abs(CrayonBrush.Noise(seed, 73)));
				float down = s.Y * (0.64f + 0.1f * Mathf.Abs(CrayonBrush.Noise(seed, 74)));
				corners = CutCorner(corners, which, along, down, true);
				break;
			}
			case Tear.Bite:
			{
				// A piece ripped out of the top edge.
				float a = s.X * (0.45f + 0.1f * CrayonBrush.Noise(seed, 75));
				float b = a + s.X * 0.32f;
				float deep = s.Y * 0.34f;
				corners = new List<(Vector2, bool)>
				{
					(Vector2.Zero, false), (new Vector2(a, 0.0f), true), (new Vector2(a + 0.35f * (b - a), deep), true),
					(new Vector2(b, deep * 0.55f), true), (new Vector2(b + 6.0f, 0.0f), false), (new Vector2(s.X, 0.0f), false),
					(s, false), (new Vector2(0.0f, s.Y), false),
				};
				break;
			}
		}

		if (curl)
		{
			// A corner he did not tear, bent over where it was caught: across from the tear if
			// there is one, and one still on the sheet.
			int index = -1;
			foreach (int turn in new[] { 2, 1, 3, 0 })
			{
				Vector2 at = CornerOf(s, (which + turn) % 4);
				index = corners.FindIndex(c => c.at == at);
				if (index >= 0) break;
			}
			if (index >= 0)
			{
				Vector2 corner = corners[index].at;
				Vector2 before = corners[(index + corners.Count - 1) % corners.Count].at;
				Vector2 after = corners[(index + 1) % corners.Count].at;
				float fold = s.X * (0.16f + 0.05f * Mathf.Abs(CrayonBrush.Noise(seed, 76)));
				Vector2 f1 = corner + (before - corner).Normalized() * fold;
				Vector2 f2 = corner + (after - corner).Normalized() * fold * 0.85f;
				bool onward = corners[index].torn;
				corners[index] = (f1, false);
				corners.Insert(index + 1, (f2, onward));
				// The corner folded over the crease - not quite flat, so it reads as a curl.
				Vector2 mid = (f1 + f2) * 0.5f;
				Vector2 across = (f2 - f1).Normalized();
				Vector2 mirrored = f1 + across * 2.0f * (corner - f1).Dot(across) - (corner - f1);
				sheet.Flap = new[] { f1, mid.Lerp(mirrored, 0.78f), f2 };
			}
		}

		// The outline, its tears run out into a ragged edge.
		var outline = new List<Vector2>();
		for (int i = 0; i < corners.Count; i++)
		{
			(Vector2 from, bool torn) = corners[i];
			Vector2 to = corners[(i + 1) % corners.Count].at;
			Vector2[] run = torn ? Ragged(from, to, seed + 80 + i) : new[] { from, to };
			sheet.Edges.Add((run, torn));
			for (int k = 0; k < run.Length - 1; k++) outline.Add(run[k]);
		}
		if (Geometry2D.TriangulatePolygon(outline.ToArray()).Length == 0)
		{
			// A tear that crossed itself would draw nothing at all: keep the whole sheet instead.
			outline = new List<Vector2> { Vector2.Zero, new Vector2(s.X, 0.0f), s, new Vector2(0.0f, s.Y) };
			sheet.Edges.Clear();
			for (int i = 0; i < 4; i++) sheet.Edges.Add((new[] { outline[i], outline[(i + 1) % 4] }, false));
			sheet.Flap = null;
		}
		sheet.Outline = new Vector2[outline.Count];
		sheet.Uvs = new Vector2[outline.Count];
		sheet.White = new Color[outline.Count];
		for (int i = 0; i < outline.Count; i++)
		{
			sheet.Outline[i] = outline[i] - s * 0.5f;
			sheet.Uvs[i] = outline[i] / s;
			sheet.White[i] = Colors.White;
		}
		if (sheet.Flap != null) for (int i = 0; i < sheet.Flap.Length; i++) sheet.Flap[i] -= s * 0.5f;
		foreach ((Vector2[] run, bool _) in sheet.Edges) for (int i = 0; i < run.Length; i++) run[i] -= s * 0.5f;
		sheet.Shadow = new Vector2[sheet.Outline.Length];
		for (int i = 0; i < sheet.Shadow.Length; i++) sheet.Shadow[i] = sheet.Outline[i] + new Vector2(6.0f, 8.0f);

		if (tape)
		{
			// The tape that held it up, still on it: across the top edge, torn off the door at
			// its far end. Put where there is still paper under it.
			float[] tries = { 0.22f, 0.5f, 0.78f, 0.35f, 0.65f };
			foreach (float t in tries)
			{
				var on = new Vector2(s.X * (t + 0.04f * CrayonBrush.Noise(seed, 90)), 6.0f);
				if (!Geometry2D.IsPointInPolygon(on, outline.ToArray())) continue;
				if (sheet.Flap != null && Geometry2D.IsPointInPolygon(on - s * 0.5f, sheet.Flap)) continue;
				float tilt = CrayonBrush.Noise(seed, 91) * 0.35f;
				sheet.TapeStrip = TapeAt(on - s * 0.5f, tilt, seed);
				break;
			}
		}
	}

	static Vector2 CornerOf(Vector2 s, int corner) => corner switch
	{
		0 => Vector2.Zero,
		1 => new Vector2(s.X, 0.0f),
		2 => s,
		_ => new Vector2(0.0f, s.Y),
	};

	/// <summary>The four corners with one cut off - straight, or torn.</summary>
	static List<(Vector2 at, bool torn)> CutCorner(List<(Vector2 at, bool torn)> corners, int which, float along, float down, bool torn)
	{
		Vector2 c = corners[which].at;
		Vector2 before = corners[(which + 3) % 4].at;
		Vector2 after = corners[(which + 1) % 4].at;
		// Along the top or bottom edge by "along", up or down the side by "down".
		bool horizontalFirst = which % 2 == 1;
		Vector2 p1 = c + (before - c).Normalized() * (horizontalFirst ? along : down);
		Vector2 p2 = c + (after - c).Normalized() * (horizontalFirst ? down : along);
		var result = new List<(Vector2, bool)>();
		for (int i = 0; i < 4; i++)
		{
			if (i == which)
			{
				result.Add((p1, torn));
				result.Add((p2, false));
			}
			else result.Add(corners[i]);
		}
		return result;
	}

	/// <summary>
	/// A tear from one point to another: never a straight cut - it wanders, and its edge is
	/// fibrous, nicked every few pixels. It only ever moves forward along the line, so it can
	/// never cross itself.
	/// </summary>
	static Vector2[] Ragged(Vector2 from, Vector2 to, int seed)
	{
		float length = from.DistanceTo(to);
		int steps = Mathf.Max(3, Mathf.RoundToInt(length / 5.0f));
		Vector2 normal = (to - from).Normalized().Orthogonal();
		float wander = Mathf.Min(9.0f, length * 0.08f);
		float phase = CrayonBrush.Noise(seed, 0) * 3.0f;
		var points = new Vector2[steps + 1];
		for (int i = 0; i <= steps; i++)
		{
			float t = i / (float)steps;
			float taper = Mathf.Sin(t * Mathf.Pi);
			float push = (Mathf.Sin(t * 7.0f + phase) * wander + CrayonBrush.Noise(seed, i + 1) * 2.6f) * taper;
			points[i] = from.Lerp(to, t) + normal * push;
		}
		return points;
	}

	/// <summary>A strip of tape, square at the end still on the paper and ragged where it tore off the door.</summary>
	static Vector2[] TapeAt(Vector2 centre, float tilt, int seed)
	{
		const float Length = 52.0f, Width = 18.0f;
		var points = new List<Vector2>
		{
			new Vector2(-Length * 0.5f, Width * 0.5f + 4.0f),
			new Vector2(-Length * 0.5f, -Width * 0.5f + 4.0f),
		};
		// The torn end: a zigzag across the strip.
		for (int k = 0; k <= 5; k++)
		{
			float y = -Width * 0.5f + Width * k / 5.0f + 4.0f;
			float x = Length * 0.5f - 3.0f + (k % 2 == 0 ? 0.0f : -5.0f) + CrayonBrush.Noise(seed, 95 + k) * 2.0f;
			points.Add(new Vector2(x, y));
		}
		var strip = new Vector2[points.Count];
		for (int i = 0; i < points.Count; i++) strip[i] = centre + points[i].Rotated(tilt) - new Vector2(0.0f, Width * 0.5f);
		return strip;
	}

	// --- On the floor -------------------------------------------------------------------------

	/// <summary>The heap, bottom sheet first: shadow, the sheet and its drawing cut to its outline, its edges, the bent corner, the tape.</summary>
	public void Draw(CanvasItem canvas)
	{
		foreach (Sheet sheet in sheets)
		{
			canvas.DrawSetTransform(sheet.Centre, sheet.Angle, Vector2.One);
			canvas.DrawColoredPolygon(sheet.Shadow, Shadow);
			// Paper first, in case the drawing has not been rendered yet on its first frame.
			canvas.DrawColoredPolygon(sheet.Outline, Paper);
			canvas.DrawPolygon(sheet.Outline, sheet.White, sheet.Uvs, sheet.Texture);

			int n = 0;
			foreach ((Vector2[] run, bool torn) in sheet.Edges)
			{
				n++;
				if (run.Length < 2) continue;
				if (torn)
				{
					// A tear has no ink on it: just the paper's fibres, a hair of shade along them.
					canvas.DrawPolyline(run, new Color(0.66f, 0.66f, 0.76f, 0.6f), 4.0f, true);
					canvas.DrawPolyline(run, new Color(1.0f, 1.0f, 1.0f, 1.0f), 2.4f, true);
				}
				else
				{
					CrayonBrush.InkLine(canvas, run[0], run[^1], Ink, 2.5f, sheet.Seed + 10 * n, 1.4f);
				}
			}

			if (sheet.Flap != null)
			{
				Vector2[] f = sheet.Flap;
				canvas.DrawColoredPolygon(new[] { f[0], f[1] + new Vector2(3.0f, 5.0f), f[2] }, new Color(Shadow, 0.22f));
				canvas.DrawColoredPolygon(f, PaperBack);
				CrayonBrush.InkLine(canvas, f[0], f[1], Ink, 2.2f, sheet.Seed + 201, 1.0f);
				CrayonBrush.InkLine(canvas, f[1], f[2], Ink, 2.2f, sheet.Seed + 202, 1.0f);
				canvas.DrawLine(f[0], f[2], new Color(0.75f, 0.74f, 0.80f, 0.8f), 1.6f, true);
			}

			if (sheet.TapeStrip != null) canvas.DrawColoredPolygon(sheet.TapeStrip, Tape);
		}
		canvas.DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}
}
