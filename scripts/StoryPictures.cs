using System.Collections.Generic;
using Godot;

/// <summary>
/// The little crayon doodles a speech bubble can hold - "[fridge] [=] [doombot]" - for a fighter
/// who talks in pictures (Circy, for now: see .ai/story-mode.md). They are bubble doodles in the
/// UI's own hand, never a copy of anyone's drawing: DoomBot here is a box with a red eye, which
/// is the joke - drawn by a kid, he is a fridge.
/// </summary>
public static class StoryPictures
{
	static readonly string[] Names =
	{
		"heart", "fridge", "doombot", "=", "?", "!", "star", "magnet", "bomb", "flame", "nofire", "square",
	};

	public static bool Exists(string name) => System.Array.IndexOf(Names, name) >= 0;

	/// <summary>Every [picture] named in a line.</summary>
	public static IEnumerable<string> In(string text)
	{
		int at = 0;
		while ((at = text.IndexOf('[', at)) >= 0)
		{
			int close = text.IndexOf(']', at);
			if (close < 0) yield break;
			yield return text.Substring(at + 1, close - at - 1).Trim().ToLowerInvariant();
			at = close + 1;
		}
	}

	static readonly Color Red = new Color(0.90f, 0.30f, 0.30f);
	static readonly Color Yellow = new Color(0.98f, 0.80f, 0.25f);
	static readonly Color Orange = new Color(0.98f, 0.55f, 0.20f);
	static readonly Color Steel = new Color(0.80f, 0.83f, 0.88f);

	/// <summary>Draws one, centred on <paramref name="c"/>, about <paramref name="s"/> across.</summary>
	public static void Draw(CanvasItem canvas, string name, Vector2 c, float s, Color ink, float alpha, int seed)
	{
		ink.A *= alpha;
		Color Fill(Color k) => new Color(k, k.A * alpha);
		float w = Mathf.Max(2.5f, s * 0.06f);
		float h = s * 0.5f;

		switch (name)
		{
			case "heart":
			{
				var pts = new List<Vector2>();
				for (int i = 0; i <= 28; i++)
				{
					float t = i / 28.0f * Mathf.Tau;
					float x = 16.0f * Mathf.Pow(Mathf.Sin(t), 3.0f);
					float y = -(13.0f * Mathf.Cos(t) - 5.0f * Mathf.Cos(2 * t) - 2.0f * Mathf.Cos(3 * t) - Mathf.Cos(4 * t));
					pts.Add(c + new Vector2(x, y + 2.0f) * (s / 36.0f));
				}
				canvas.DrawColoredPolygon(pts.ToArray(), Fill(Red));
				Loop(canvas, pts, ink, w, seed);
				break;
			}
			case "fridge":
			case "doombot":
			{
				// The same tall box both times. That is the point.
				var box = new Rect2(c.X - h * 0.62f, c.Y - h, h * 1.24f, h * 2.0f);
				canvas.DrawRect(box, Fill(Steel));
				CrayonBrush.InkRect(canvas, box, ink, w, seed, 1.6f);
				CrayonBrush.InkLine(canvas, new Vector2(box.Position.X, c.Y - h * 0.3f), new Vector2(box.End.X, c.Y - h * 0.3f), ink, w * 0.8f, seed + 7, 1.2f);
				if (name == "fridge")
				{
					CrayonBrush.InkLine(canvas, new Vector2(box.End.X - h * 0.25f, c.Y - h * 0.8f), new Vector2(box.End.X - h * 0.25f, c.Y - h * 0.45f), ink, w * 1.2f, seed + 9, 1.0f);
					CrayonBrush.InkLine(canvas, new Vector2(box.End.X - h * 0.25f, c.Y - h * 0.1f), new Vector2(box.End.X - h * 0.25f, c.Y + h * 0.5f), ink, w * 1.2f, seed + 11, 1.0f);
				}
				else
				{
					canvas.DrawCircle(new Vector2(c.X, c.Y - h * 0.62f), h * 0.16f, Fill(Red));
					// His long arm, straight down his side, exactly where a handle would be.
					CrayonBrush.InkLine(canvas, new Vector2(box.End.X + w, c.Y - h * 0.15f), new Vector2(box.End.X + w, c.Y + h * 0.7f), ink, w * 1.2f, seed + 13, 1.0f);
				}
				break;
			}
			case "=":
				CrayonBrush.InkLine(canvas, c + new Vector2(-h * 0.7f, -h * 0.25f), c + new Vector2(h * 0.7f, -h * 0.25f), ink, w * 1.6f, seed, 1.5f);
				CrayonBrush.InkLine(canvas, c + new Vector2(-h * 0.7f, h * 0.25f), c + new Vector2(h * 0.7f, h * 0.25f), ink, w * 1.6f, seed + 3, 1.5f);
				break;
			case "?":
			case "!":
				canvas.DrawString(ThemeDB.FallbackFont, c + new Vector2(-s * 0.25f, s * 0.36f), name, HorizontalAlignment.Left, -1, (int)(s * 1.0f), ink);
				break;
			case "star":
			{
				var pts = new List<Vector2>();
				for (int i = 0; i < 10; i++)
				{
					float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
					pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? h : h * 0.45f));
				}
				pts.Add(pts[0]);
				canvas.DrawColoredPolygon(pts.GetRange(0, 10).ToArray(), Fill(Yellow));
				Loop(canvas, pts, ink, w, seed);
				break;
			}
			case "magnet":
			{
				var pts = new List<Vector2>();
				for (int i = 0; i <= 16; i++)
				{
					float a = Mathf.Pi * i / 16.0f;
					pts.Add(c + new Vector2(-Mathf.Cos(a) * h * 0.6f, Mathf.Sin(a) * h * 0.6f));
				}
				pts.Insert(0, pts[0] + new Vector2(0, -h * 0.8f));
				pts.Add(pts[^1] + new Vector2(0, -h * 0.8f));
				canvas.DrawPolyline(pts.ToArray(), Fill(Red), w * 3.2f, true);
				canvas.DrawLine(pts[0], pts[0] + new Vector2(0, h * 0.35f), Fill(Steel), w * 3.2f);
				canvas.DrawLine(pts[^1], pts[^1] + new Vector2(0, h * 0.35f), Fill(Steel), w * 3.2f);
				break;
			}
			case "bomb":
			{
				Vector2 b = c + new Vector2(-h * 0.1f, h * 0.15f);
				canvas.DrawCircle(b, h * 0.7f, Fill(new Color(0.42f, 0.45f, 0.55f)));
				Loop(canvas, Circle(b, h * 0.7f, 18), ink, w, seed);
				CrayonBrush.InkLine(canvas, b + new Vector2(h * 0.45f, -h * 0.5f), b + new Vector2(h * 0.8f, -h * 0.95f), ink, w, seed + 5, 1.5f);
				canvas.DrawCircle(b + new Vector2(h * 0.85f, -h * 1.0f), h * 0.14f, Fill(Orange));
				break;
			}
			case "flame":
			case "nofire":
			{
				var pts = new List<Vector2>
				{
					c + new Vector2(0, -h), c + new Vector2(h * 0.25f, -h * 0.35f), c + new Vector2(h * 0.6f, -h * 0.5f),
					c + new Vector2(h * 0.6f, h * 0.35f), c + new Vector2(0, h * 0.85f), c + new Vector2(-h * 0.6f, h * 0.35f),
					c + new Vector2(-h * 0.5f, -h * 0.4f), c + new Vector2(-h * 0.2f, -h * 0.2f),
				};
				canvas.DrawColoredPolygon(pts.ToArray(), Fill(Orange));
				pts.Add(pts[0]);
				Loop(canvas, pts, ink, w, seed);
				if (name == "nofire")
				{
					Loop(canvas, Circle(c, h * 1.05f, 20), Fill(Red), w * 1.3f, seed + 3);
					CrayonBrush.InkLine(canvas, c + new Vector2(-h * 0.75f, -h * 0.75f), c + new Vector2(h * 0.75f, h * 0.75f), Fill(Red), w * 1.3f, seed + 4, 1.0f);
				}
				break;
			}
			case "square":
				CrayonBrush.InkRect(canvas, new Rect2(c - new Vector2(h * 0.7f, h * 0.5f), new Vector2(h * 1.4f, h * 1.4f)), ink, w, seed, 1.5f);
				// In a hard hat.
				canvas.DrawRect(new Rect2(c.X - h * 0.55f, c.Y - h * 0.95f, h * 1.1f, h * 0.45f), Fill(Yellow));
				CrayonBrush.InkLine(canvas, new Vector2(c.X - h * 0.8f, c.Y - h * 0.5f), new Vector2(c.X + h * 0.8f, c.Y - h * 0.5f), ink, w, seed + 2, 1.0f);
				break;
		}
	}

	static List<Vector2> Circle(Vector2 c, float r, int n)
	{
		var pts = new List<Vector2>();
		for (int i = 0; i <= n; i++) pts.Add(c + Vector2.FromAngle(Mathf.Tau * i / n) * r);
		return pts;
	}

	/// <summary>A closed outline, wobbled a little the way every crayon line is.</summary>
	static void Loop(CanvasItem canvas, List<Vector2> pts, Color ink, float width, int seed)
	{
		var wobbled = new Vector2[pts.Count];
		for (int i = 0; i < pts.Count; i++)
		{
			int k = i == pts.Count - 1 ? 0 : i;
			wobbled[i] = pts[i] + new Vector2(CrayonBrush.Noise(seed, k * 2), CrayonBrush.Noise(seed, k * 2 + 1)) * 1.4f;
		}
		canvas.DrawPolyline(wobbled, ink, width, true);
	}
}
