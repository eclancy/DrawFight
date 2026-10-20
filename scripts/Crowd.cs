using System.Collections.Generic;
using Godot;

/// <summary>
/// The audience in front of a stage, seen from behind (Eric, 2026-10-20: a cute, cheering, simple
/// drawn crowd below the Big Show, watching and cheering). Rows of heads and shoulders cut off by
/// the bottom of the page, the way a kid draws a crowd: they bounce on the beat, pump their fists,
/// wave, clap, swing glow sticks and hold up signs with a heart or a star - and on a KO or a
/// speaker blast everyone jumps with their arms up.
///
/// Built by Stage for a <see cref="PropKind.Crowd"/> prop, which says where they stand; Stage draws
/// nothing for the prop itself. A child of the stage, so it is over the stage's drawing (in front
/// of the deck's skirt) and under the fighters - nobody on the deck is ever hidden by a raised hand.
///
/// Scenery, so no ink: an outline means something real you can stand on or that hits you
/// (.ai/art-direction.md). Each person is pale coloured pencil with a soft edge in a darker shade of
/// their own colour, faded toward the paper the further back they stand, so the crowd never
/// competes with the fighters or the HUD over it. Every person - where they stand, their hair,
/// their colours, what they do with their arms - is hashed from the prop's seed, never random,
/// so a redraw is identical; only the beat and the cheering move them.
///
/// Cheap to run: each person's body and head is a node drawn once and only moved after that, and
/// what does move - the arms - is lines and one round dot texture. Circles drawn every frame cost
/// a hundred times what a line does, and sixty people's worth of them took longer than a frame.
/// </summary>
public partial class Crowd : Node2D
{
	/// <summary>The beat to bounce to, when the stage has speakers playing: the same thump the cones make.</summary>
	public SpeakerBlast Speakers;

	/// <summary>How a person's arms move. Hashed per person.</summary>
	enum Arms { Pump, BothUp, Wave, GlowStick, Sign, Clap, Down }

	enum Hair { Short, Long, Pigtails, Bun, Mohawk, Bobble, Cap, Curly }

	sealed class Person
	{
		public float X;
		public float Neck;
		public float Bottom;
		public float Scale;
		public int Seed;
		public int Delay;
		public float Hop;
		public float Sway;
		public int Side;
		public Arms Arms;
		public Hair Hair;
		public Color Skin, Shirt, HairColour, Extra, Thing;
		public bool Star;

		/// <summary>The body and head, drawn once; and the sign, for someone holding one up.</summary>
		public Node2D View;
		public Node2D Sign;

		// Where they are this frame, worked out once and used by the body and the arms alike.
		public Vector2 NeckNow;
		public int Frame;
		public float Beat;
	}

	/// <summary>Twenty frames a beat - the Big Show's punk is 184 BPM, the speakers' own beat.</summary>
	const int BeatFrames = 20;

	/// <summary>How long a cheer takes to die down.</summary>
	const int CheerFrames = 150;

	/// <summary>The highest anyone jumps, at the loudest roar, as a multiple of their size.</summary>
	const float RoarHop = 20.0f;

	/// <summary>The rows, back to front: where each stands in the prop's height, how big, how many, and how far faded into the paper.</summary>
	static readonly (float y, float scale, int count, float fade)[] Rows =
	{
		(0.36f, 1.30f, 30, 0.56f),
		(0.76f, 1.55f, 27, 0.36f),
	};

	static readonly Color Paper = new Color(0.985f, 0.978f, 0.955f);

	static readonly Color[] Skins =
	{
		new Color(0.99f, 0.87f, 0.77f), new Color(0.95f, 0.78f, 0.64f), new Color(0.84f, 0.64f, 0.49f),
		new Color(0.72f, 0.53f, 0.40f), new Color(0.98f, 0.83f, 0.70f),
	};

	static readonly Color[] Hairs =
	{
		new Color(0.60f, 0.44f, 0.32f), new Color(0.48f, 0.44f, 0.54f), new Color(0.96f, 0.85f, 0.52f),
		new Color(0.93f, 0.58f, 0.34f), new Color(0.72f, 0.58f, 0.46f), new Color(0.96f, 0.62f, 0.78f),
		new Color(0.54f, 0.72f, 0.95f), new Color(0.52f, 0.40f, 0.36f),
	};

	static readonly Color[] Shirts =
	{
		new Color(0.56f, 0.70f, 0.93f), new Color(0.96f, 0.62f, 0.56f), new Color(0.62f, 0.83f, 0.62f),
		new Color(0.98f, 0.84f, 0.46f), new Color(0.78f, 0.65f, 0.91f), new Color(0.55f, 0.81f, 0.85f),
		new Color(0.96f, 0.70f, 0.82f), new Color(0.72f, 0.74f, 0.80f), new Color(0.97f, 0.66f, 0.40f),
	};

	static readonly Color[] Glows =
	{
		new Color(0.52f, 0.98f, 0.60f), new Color(1.00f, 0.56f, 0.86f), new Color(0.50f, 0.88f, 1.00f),
		new Color(1.00f, 0.90f, 0.40f),
	};

	readonly List<Person> people = new List<Person>();
	Vector2[] mask = System.Array.Empty<Vector2>();

	int frames;
	float cheer;
	bool wasBlasting;

	/// <summary>
	/// Lays the crowd out across <paramref name="area"/>: two rows, the back one smaller and paler.
	/// On a paper stage their bodies stop at the bottom of the page they stand on, so they are cut
	/// off by the edge of the paper as a drawn crowd would be.
	/// </summary>
	public void Setup(StageData data, Rect2 area, int seed)
	{
		PinnedPage page = null;
		if (data.Style == StageStyle.PinnedPages)
		{
			foreach (PinnedPage p in data.Pages) if (p.Rect.HasPoint(area.GetCenter())) page = p;
		}
		float BottomAt(float x) => page != null ? Mathf.Min(area.End.Y, PageBottom(page, x) - 2.0f) : area.End.Y;

		// Everyone is cut off along the bottom: whatever is drawn is masked to above it, so a body
		// can be drawn long and jump up and down without its foot ever showing.
		ClipChildren = ClipChildrenMode.Only;
		float left = area.Position.X - 200.0f;
		float right = area.End.X + 200.0f;
		float top = area.Position.Y - 400.0f;
		mask = new[] { new Vector2(left, top), new Vector2(right, top), new Vector2(right, BottomAt(right)), new Vector2(left, BottomAt(left)) };

		int index = 0;
		for (int row = 0; row < Rows.Length; row++)
		{
			(float rowY, float scale, int count, float fade) = Rows[row];

			// Each row's arms are drawn behind that row and in front of the row behind it.
			var arms = new Node2D();
			AddChild(arms);
			int from = people.Count;
			int to = from + count;
			arms.Draw += () => DrawArmsOf(arms, from, to);
			armLayers.Add(arms);

			float step = area.Size.X / count;
			for (int i = 0; i < count; i++, index++)
			{
				int s = seed * 977 + index * 31;
				float x = area.Position.X + step * (i + 0.5f) + Noise(s, 1) * step * 0.22f;
				var person = new Person
				{
					X = x,
					Neck = area.Position.Y + area.Size.Y * rowY + Noise(s, 3) * 7.0f,
					Bottom = BottomAt(x),
					Scale = scale * (1.0f + Noise(s, 2) * 0.10f),
					Seed = s,
					// A frame or two either way of the beat, so the bounce ripples rather than marches.
					Delay = Mathf.RoundToInt(Unit(s, 4) * 3.0f),
					// Most bob; about one in three pogoes, as a punk crowd does.
					Hop = Unit(s, 5) < 0.34f ? 9.0f : 3.0f,
					Sway = 1.0f + 2.5f * Unit(s, 20),
					Side = Unit(s, 6) < 0.5f ? -1 : 1,
					Arms = PickArms(Unit(s, 7)),
					Hair = (Hair)Mathf.Min(7, (int)(Unit(s, 8) * 8.0f)),
					Skin = Fade(Pick(Skins, s, 9), fade),
					Shirt = Fade(Pick(Shirts, s, 10), fade),
					HairColour = Fade(Pick(Hairs, s, 11), fade),
					Extra = Fade(Pick(Shirts, s, 12), fade),
					Thing = Fade(Pick(Glows, s, 13), fade * 0.6f),
					Star = Unit(s, 14) < 0.5f,
				};

				person.View = new Node2D();
				AddChild(person.View);
				Person drawn = person;
				person.View.Draw += () => DrawBody(drawn.View, drawn);
				if (person.Arms == Arms.Sign)
				{
					person.Sign = new Node2D();
					AddChild(person.Sign);
					person.Sign.Draw += () => DrawSign(drawn.Sign, drawn);
				}
				people.Add(person);
			}
		}
		Place();
	}

	readonly List<Node2D> armLayers = new List<Node2D>();

	/// <summary>Something big happened: everybody up. A KO is 1; a speaker blast a little less.</summary>
	public void Cheer(float how) => cheer = Mathf.Max(cheer, Mathf.Clamp(how, 0.0f, 1.0f));

	public override void _PhysicsProcess(double delta)
	{
		frames = Speakers != null ? Speakers.Frames : frames + 1;
		cheer = Mathf.Max(0.0f, cheer - 1.0f / CheerFrames);
		bool blasting = Speakers != null && Speakers.Blasting;
		if (blasting && !wasBlasting) Cheer(0.8f);
		wasBlasting = blasting;
		Place();
		foreach (Node2D layer in armLayers) layer.QueueRedraw();
	}

	/// <summary>The mask the crowd is cut off by: everything above the bottom of the page.</summary>
	public override void _Draw()
	{
		if (mask.Length > 0) DrawColoredPolygon(mask, Colors.White);
	}

	/// <summary>Eased, so a cheer lands at once and settles slowly.</summary>
	float Roar => Mathf.Sqrt(cheer);

	/// <summary>Where everyone is this frame: up between beats, landing on each one, swaying.</summary>
	void Place()
	{
		float roar = Roar;
		foreach (Person p in people)
		{
			float s = p.Scale;
			p.Frame = frames - p.Delay;
			p.Beat = Mathf.PosMod(p.Frame, BeatFrames) / (float)BeatFrames;
			// A bob for most, a pogo for some, and everyone jumping when the crowd roars.
			float hop = Mathf.Abs(Mathf.Sin(p.Beat * Mathf.Pi)) * Mathf.Lerp(p.Hop, RoarHop, roar) * s;
			// A slow sway over two beats, for some more than others.
			float sway = Mathf.Sin((p.Frame + p.Seed % 40) * Mathf.Tau / (BeatFrames * 2)) * p.Sway * s;
			p.NeckNow = new Vector2(p.X + sway, p.Neck - hop);
			p.View.Position = p.NeckNow;
			// A sign is held up in both hands, over the middle of the head (DrawArms).
			if (p.Sign != null) p.Sign.Position = p.NeckNow + new Vector2(0.0f, 7.0f * s + SignLift(p, roar) - 14.0f * s);
		}
	}

	// --- Bodies, drawn once ----------------------------------------------------------------

	/// <summary>
	/// Shoulders and back, rounded over the top and drawn on down past the bottom of the page (the
	/// mask cuts them off there, however high they jump), and the head. In the person's own space:
	/// the base of the neck is the origin.
	/// </summary>
	void DrawBody(CanvasItem c, Person p)
	{
		float s = p.Scale;
		float w = 24.0f * s;
		float top = 10.0f * s;
		float rise = 14.0f * s;
		float bottom = p.Bottom - p.Neck + 4.0f;
		const int ArcPoints = 10;
		var torso = new Vector2[ArcPoints + 2];
		torso[0] = new Vector2(-w, bottom);
		for (int i = 0; i < ArcPoints; i++)
		{
			float a = Mathf.Pi + Mathf.Pi * i / (ArcPoints - 1);
			// A hand-drawn wobble, fixed per person and point.
			float wob = 1.0f + 0.04f * Noise(p.Seed + 40, i);
			torso[i + 1] = new Vector2(Mathf.Cos(a) * w * wob, top + Mathf.Sin(a) * rise * wob);
		}
		torso[ArcPoints + 1] = new Vector2(w, bottom);
		c.DrawColoredPolygon(torso, p.Shirt);

		// Coloured pencil: a few strokes of a darker shade across the back.
		Color shade = Shade(p.Shirt, 0.18f, 0.35f);
		for (int k = 0; k < 3; k++)
		{
			float x = -w * 0.55f + w * 0.55f * k + Noise(p.Seed + 41, k) * 3.0f;
			float y0 = top + 2.0f * s;
			c.DrawLine(new Vector2(x + 4.0f * s, y0), new Vector2(x - 4.0f * s, y0 + 26.0f * s), shade, 1.6f, true);
		}
		// The soft pencil edge, open at the bottom where the page cuts them off.
		c.DrawPolyline(torso, Shade(p.Shirt, 0.28f, 0.55f), 2.0f, true);

		float r = 17.0f * s;
		DrawHead(c, p, new Vector2(0.0f, -r * 0.9f), r);
	}

	/// <summary>The back of a head: mostly hair, ears either side, the nape showing under short hair.</summary>
	static void DrawHead(CanvasItem c, Person p, Vector2 at, float r)
	{
		Color edge = Shade(p.HairColour, 0.25f, 0.55f);
		switch (p.Hair)
		{
			case Hair.Long:
				c.DrawRect(new Rect2(at.X - r * 0.95f, at.Y, r * 1.9f, r * 1.55f), p.HairColour);
				c.DrawCircle(at + new Vector2(0.0f, r * 1.55f), r * 0.95f, p.HairColour);
				break;
			case Hair.Curly:
				for (int k = 0; k < 6; k++)
				{
					float a = Mathf.Tau * k / 6.0f + 0.3f;
					c.DrawCircle(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.9f) * r * 0.62f, r * 0.62f, p.HairColour);
				}
				break;
		}

		// Ears, and the head itself.
		c.DrawCircle(at + new Vector2(-r * 0.97f, r * 0.12f), r * 0.24f, p.Skin);
		c.DrawCircle(at + new Vector2(r * 0.97f, r * 0.12f), r * 0.24f, p.Skin);
		c.DrawCircle(at, r, p.Skin);

		switch (p.Hair)
		{
			case Hair.Pigtails:
				c.DrawCircle(at + new Vector2(-r * 1.08f, r * 0.30f), r * 0.42f, p.HairColour);
				c.DrawCircle(at + new Vector2(r * 1.08f, r * 0.30f), r * 0.42f, p.HairColour);
				c.DrawCircle(at + new Vector2(-r * 0.86f, r * 0.05f), r * 0.14f, p.Extra);
				c.DrawCircle(at + new Vector2(r * 0.86f, r * 0.05f), r * 0.14f, p.Extra);
				break;
			case Hair.Bun:
				c.DrawCircle(at + new Vector2(0.0f, -r * 1.02f), r * 0.44f, p.HairColour);
				break;
		}

		// The hair over the back of the head: lifted a little, so the nape shows under it.
		float lift = p.Hair == Hair.Long || p.Hair == Hair.Curly ? 0.02f : 0.16f;
		Vector2 crown = at + new Vector2(0.0f, -r * lift);
		c.DrawCircle(crown, r * 1.02f, p.HairColour);
		c.DrawArc(crown, r * 1.02f, 0.0f, Mathf.Tau, 24, edge, 1.6f, true);
		// A couple of strands falling from the crown, in pencil - down the back of the head, so
		// nobody reads as a face turned round to us.
		float lean = 0.12f * Noise(p.Seed, 30);
		c.DrawPolyline(new[] { crown + new Vector2((-0.08f + lean) * r, -0.92f * r), crown + new Vector2(-0.42f * r, -0.25f * r), crown + new Vector2(-0.5f * r, 0.35f * r) }, edge, 1.4f, true);
		c.DrawPolyline(new[] { crown + new Vector2((0.1f + lean) * r, -0.9f * r), crown + new Vector2(0.36f * r, -0.2f * r), crown + new Vector2(0.44f * r, 0.4f * r) }, edge, 1.4f, true);

		switch (p.Hair)
		{
			case Hair.Mohawk:
				// A punk's crest, in a loud colour - this is a punk show.
				for (int k = -1; k <= 1; k++)
				{
					Vector2 baseAt = crown + new Vector2(k * r * 0.34f, -r * 0.82f);
					c.DrawColoredPolygon(new[]
					{
						baseAt + new Vector2(-r * 0.2f, 0.0f), baseAt + new Vector2(r * 0.2f, 0.0f),
						baseAt + new Vector2(k * r * 0.12f, -r * (0.75f - 0.15f * Mathf.Abs(k))),
					}, p.Extra);
				}
				break;
			case Hair.Bobble:
				c.DrawArc(crown + new Vector2(0.0f, r * 0.1f), r * 0.98f, Mathf.Pi + 0.15f, Mathf.Tau - 0.15f, 12, p.Extra, r * 0.55f, true);
				c.DrawCircle(crown + new Vector2(0.0f, -r * 1.1f), r * 0.3f, Paper.Lerp(p.Extra, 0.4f));
				break;
			case Hair.Cap:
				// Worn backwards: the dome, and the peak pointing out at us.
				c.DrawArc(crown + new Vector2(0.0f, r * 0.15f), r * 0.92f, Mathf.Pi + 0.2f, Mathf.Tau - 0.2f, 12, p.Extra, r * 0.6f, true);
				c.DrawRect(new Rect2(crown.X - r * 0.55f, crown.Y - r * 0.08f, r * 1.1f, r * 0.26f), Shade(p.Extra, 0.12f, 1.0f));
				break;
		}
	}

	/// <summary>A little card with a heart or a star on it, in pencil, centred on the node.</summary>
	static void DrawSign(CanvasItem c, Person p)
	{
		float s = p.Scale;
		var card = new Rect2(-24.0f * s, -16.0f * s, 48.0f * s, 32.0f * s);
		c.DrawRect(card, Paper.Lerp(p.Extra, 0.25f));
		CrayonBrush.InkRect(c, card, Shade(p.Extra, 0.25f, 0.6f), 1.6f, p.Seed + 50, 0.8f);
		float u = 9.0f * s;
		if (p.Star)
		{
			var star = new Vector2[10];
			for (int k = 0; k < 10; k++)
			{
				float a = -Mathf.Pi * 0.5f + Mathf.Pi * k / 5.0f;
				star[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (k % 2 == 0 ? 1.0f : 0.45f) * u * 1.15f;
			}
			c.DrawColoredPolygon(star, new Color(0.98f, 0.78f, 0.30f));
		}
		else
		{
			var heart = new Color(0.94f, 0.46f, 0.52f);
			c.DrawCircle(new Vector2(-u * 0.48f, -u * 0.25f), u * 0.55f, heart);
			c.DrawCircle(new Vector2(u * 0.48f, -u * 0.25f), u * 0.55f, heart);
			c.DrawColoredPolygon(new[] { new Vector2(-u, -u * 0.05f), new Vector2(u, -u * 0.05f), new Vector2(0.0f, u) }, heart);
		}
	}

	// --- Arms, every frame -----------------------------------------------------------------

	void DrawArmsOf(CanvasItem c, int from, int to)
	{
		float roar = Roar;
		for (int i = from; i < to; i++) DrawArms(c, people[i], roar);
	}

	/// <summary>
	/// Two arms from the shoulders to wherever this person's hands are this frame, and whatever
	/// they are holding up. A roar pulls every pair of hands up into a V.
	/// </summary>
	void DrawArms(CanvasItem c, Person p, float roar)
	{
		float s = p.Scale;
		float w = 24.0f * s;
		Vector2 neck = p.NeckNow;
		var shoulderL = neck + new Vector2(-w * 0.72f, 7.0f * s);
		var shoulderR = neck + new Vector2(w * 0.72f, 7.0f * s);
		float reach = 50.0f * s;
		float beat = p.Beat;
		// Where each hand is, relative to its shoulder, before a roar.
		Vector2 handL = Vector2.Zero, handR = Vector2.Zero;
		bool showL = false, showR = false;
		// Two beats, side to side and back.
		float swing = Mathf.Sin(Mathf.Tau * Mathf.PosMod(p.Frame, BeatFrames * 2) / (BeatFrames * 2));
		float punch = Punch(beat);

		switch (p.Arms)
		{
			case Arms.Pump:
				OneHand(p, new Vector2(p.Side * 14.0f * s, -reach * 0.35f).Lerp(new Vector2(p.Side * 7.0f * s, -reach * 0.95f), punch), ref handL, ref handR, ref showL, ref showR);
				break;
			case Arms.BothUp:
				handL = new Vector2(-14.0f * s + swing * 6.0f * s, -reach * 0.9f);
				handR = new Vector2(14.0f * s + swing * 6.0f * s, -reach * 0.9f);
				showL = showR = true;
				break;
			case Arms.Wave:
				OneHand(p, new Vector2(p.Side * 6.0f * s + swing * 20.0f * s, -reach * 0.88f + Mathf.Abs(swing) * 6.0f * s), ref handL, ref handR, ref showL, ref showR);
				break;
			case Arms.GlowStick:
				OneHand(p, new Vector2(swing * 16.0f * s, -reach * 0.85f), ref handL, ref handR, ref showL, ref showR);
				break;
			case Arms.Sign:
			{
				float lift = SignLift(p, roar);
				handL = new Vector2(w * 0.72f - 18.0f * s, lift);
				handR = new Vector2(-w * 0.72f + 18.0f * s, lift);
				showL = showR = true;
				break;
			}
			case Arms.Clap:
			{
				// Hands meet over the head on the beat.
				float apart = (3.0f + 13.0f * Mathf.Sin(beat * Mathf.Pi)) * s;
				handL = new Vector2(w * 0.72f - apart, -reach * 0.92f);
				handR = new Vector2(-w * 0.72f + apart, -reach * 0.92f);
				showL = showR = true;
				break;
			}
		}

		// The roar: both hands up and out in a V, whatever they were doing (a sign is held up
		// higher still, in SignLift).
		if (roar > 0.0f && p.Arms != Arms.Sign)
		{
			handL = (showL ? handL : new Vector2(-8.0f * s, -reach * 0.2f)).Lerp(new Vector2(-14.0f * s, -reach * 0.97f), roar);
			handR = (showR ? handR : new Vector2(8.0f * s, -reach * 0.2f)).Lerp(new Vector2(14.0f * s, -reach * 0.97f), roar);
			showL = showR = true;
		}

		Vector2 atL = shoulderL + handL;
		Vector2 atR = shoulderR + handR;
		if (showL) atL = DrawArm(c, p, shoulderL, atL, -1);
		if (showR) atR = DrawArm(c, p, shoulderR, atR, 1);

		if (p.Arms == Arms.GlowStick)
		{
			bool left = p.Side < 0;
			Vector2 hand = left ? atL : atR;
			Vector2 shoulder = left ? shoulderL : shoulderR;
			// Points on along the forearm, tipping with the swing.
			Vector2 dir = (hand - shoulder).Normalized().Rotated(swing * 0.35f);
			Vector2 tip = hand + dir * 22.0f * s;
			Vector2 butt = hand - dir * 6.0f * s;
			c.DrawLine(butt, tip, new Color(p.Thing, 0.28f), 13.0f * s, true);
			c.DrawLine(butt, tip, p.Thing, 5.0f * s, true);
			c.DrawLine(butt.Lerp(tip, 0.3f), tip - dir * 2.0f * s, new Color(1, 1, 1, 0.7f), 1.6f * s, true);
		}

		if (showL) Dot(c, atL, 5.2f * s, p.Skin);
		if (showR) Dot(c, atR, 5.2f * s, p.Skin);
	}

	/// <summary>A punch up on the beat that falls away until the next one.</summary>
	static float Punch(float beat) => beat < 0.15f ? beat / 0.15f : 1.0f - (beat - 0.15f) / 0.85f;

	/// <summary>How high above the shoulders a sign is held: bobbing on the beat, higher in a roar.</summary>
	static float SignLift(Person p, float roar) => -50.0f * p.Scale * 0.8f - (Punch(p.Beat) * 4.0f + roar * 6.0f) * p.Scale;

	/// <summary>One hand up, on the side this person favours; the other stays down out of sight.</summary>
	static void OneHand(Person p, Vector2 hand, ref Vector2 handL, ref Vector2 handR, ref bool showL, ref bool showR)
	{
		if (p.Side < 0) { handL = hand; showL = true; }
		else { handR = hand; showR = true; }
	}

	/// <summary>
	/// One arm, upper arm in the sleeve and forearm bare, its elbow bent out to <paramref name="side"/>.
	/// Returns where the hand ended up - a hand cannot reach further than the arm is long.
	/// </summary>
	static Vector2 DrawArm(CanvasItem c, Person p, Vector2 shoulder, Vector2 hand, int side)
	{
		float s = p.Scale;
		float upper = 25.0f * s;
		float lower = 25.0f * s;
		Vector2 d = hand - shoulder;
		float full = d.Length();
		if (full < 0.01f) return hand;
		float len = Mathf.Min(full, upper + lower - 0.01f);
		Vector2 dir = d / full;
		hand = shoulder + dir * len;
		// The elbow, where the two lengths meet, bent outward.
		float along = (upper * upper - lower * lower + len * len) / (2.0f * len);
		float outward = Mathf.Sqrt(Mathf.Max(0.0f, upper * upper - along * along));
		var normal = new Vector2(-dir.Y, dir.X);
		if (normal.X * side < 0.0f) normal = -normal;
		Vector2 elbow = shoulder + dir * along + normal * outward;

		c.DrawLine(elbow, hand, p.Skin, 7.5f * s, true);
		Dot(c, elbow, 3.75f * s, p.Skin);
		c.DrawLine(shoulder, shoulder.Lerp(elbow, 0.7f), p.Shirt, 10.0f * s, true);
		Dot(c, shoulder, 5.0f * s, p.Shirt);
		return hand;
	}

	// --- A round dot, cheaply ----------------------------------------------------------------

	static ImageTexture dot;

	/// <summary>
	/// A soft-edged white disc, tinted to whatever round thing is wanted - a hand, an elbow. One
	/// textured rectangle costs about what a line does; a drawn circle costs a hundred times that.
	/// </summary>
	static void Dot(CanvasItem c, Vector2 at, float radius, Color colour)
	{
		if (dot == null)
		{
			const int Size = 64;
			Image image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
			float half = Size * 0.5f;
			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float dist = new Vector2(x + 0.5f - half, y + 0.5f - half).Length();
					image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(half - 1.0f - dist, 0.0f, 1.0f)));
				}
			}
			dot = ImageTexture.CreateFromImage(image);
		}
		c.DrawTextureRect(dot, new Rect2(at - Vector2.One * radius, Vector2.One * radius * 2.0f), false, colour);
	}

	// --- Hashing and colour ------------------------------------------------------------------

	static float Noise(int seed, int index) => CrayonBrush.Noise(seed, index);

	/// <summary>Hashed 0..1.</summary>
	static float Unit(int seed, int index) => 0.5f + 0.5f * Mathf.Clamp(CrayonBrush.Noise(seed, index), -1.0f, 1.0f) * 0.999f;

	static Color Pick(Color[] from, int seed, int index) => from[Mathf.Min(from.Length - 1, (int)(Unit(seed, index) * from.Length))];

	/// <summary>Further back is paler: faded into the paper, which is how depth is drawn without ink.</summary>
	static Color Fade(Color c, float fade) => c.Lerp(Paper, fade);

	static Color Shade(Color c, float darker, float alpha) => new Color(c.Darkened(darker), alpha);

	static Arms PickArms(float u)
	{
		if (u < 0.24f) return Arms.Pump;
		if (u < 0.34f) return Arms.BothUp;
		if (u < 0.49f) return Arms.Wave;
		if (u < 0.63f) return Arms.GlowStick;
		if (u < 0.75f) return Arms.Sign;
		if (u < 0.88f) return Arms.Clap;
		return Arms.Down;
	}

	/// <summary>Where the bottom edge of a tilted page is, at <paramref name="x"/>.</summary>
	static float PageBottom(PinnedPage page, float x)
	{
		float angle = Mathf.DegToRad(page.Angle);
		Vector2 centre = page.Rect.GetCenter();
		float half = page.Rect.Size.Y * 0.5f;
		// Points along the bottom edge are centre + R(angle) * (u, half); solve for the one at x.
		float u = (x - centre.X + half * Mathf.Sin(angle)) / Mathf.Cos(angle);
		return centre.Y + u * Mathf.Sin(angle) + half * Mathf.Cos(angle);
	}
}
