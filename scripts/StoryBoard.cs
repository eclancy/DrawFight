using System.Collections.Generic;
using Godot;

/// <summary>
/// Writing on the fridge door itself, a line at a time - DoomBot burning his plan onto it with
/// his eye laser ("doombot writes: 1. REMOVE DRAWINGS."). In the world, behind the fighters, so
/// it is on the door and the camera moves over it like everything else.
/// </summary>
public partial class StoryBoard : Node2D
{
	class Written
	{
		public string Text;
		public float Shown;
		public int Hold;
		public Fighter Writer;
		public bool Finished;
	}

	readonly List<Written> lines = new List<Written>();
	Vector2 anchor;
	float fade = 1.0f;
	bool clearing;
	int frames;

	public bool Skipping;

	const int FontSize = 72;
	const float LineGap = 96.0f;

	static readonly Color Burn = new Color(0.90f, 0.24f, 0.20f);
	static readonly Color Glow = new Color(1.0f, 0.55f, 0.35f);

	public void At(Vector2 world)
	{
		anchor = world;
		lines.Clear();
		fade = 1.0f;
		clearing = false;
	}

	public void Clear() => clearing = true;

	/// <summary>Starts the next line; done once it is written and has sat a moment.</summary>
	public object Write(Fighter writer, string text)
	{
		var line = new Written { Text = text, Writer = writer };
		lines.Add(line);
		return line;
	}

	public bool IsFinished(object line) => line is Written w && w.Finished;

	public void Hurry(object line)
	{
		if (line is not Written w) return;
		if (w.Shown < w.Text.Length) w.Shown = w.Text.Length;
		else w.Finished = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		if (clearing)
		{
			fade = Mathf.MoveToward(fade, 0.0f, 1.0f / 20.0f);
			if (fade <= 0.0f) lines.Clear();
		}

		foreach (Written line in lines)
		{
			if (line.Finished) continue;
			if (Skipping)
			{
				line.Shown = line.Text.Length;
				line.Finished = true;
				continue;
			}
			if (line.Shown < line.Text.Length)
			{
				int before = (int)line.Shown;
				line.Shown = Mathf.Min(line.Text.Length, line.Shown + 0.45f);
				if ((int)line.Shown != before && before % 3 == 0) SfxPlayer.At("zap", WorldOf(line, before), 0.08f);
				if (line.Shown >= line.Text.Length) line.Hold = 50;
			}
			else if (--line.Hold <= 0)
			{
				line.Finished = true;
			}
		}
		QueueRedraw();
	}

	Vector2 WorldOf(Written line, int letter)
	{
		Font font = ThemeDB.FallbackFont;
		int row = lines.IndexOf(line);
		float x = 0.0f;
		for (int i = 0; i < letter && i < line.Text.Length; i++) x += font.GetCharSize(line.Text[i], FontSize).X;
		return anchor + new Vector2(x, row * LineGap);
	}

	public override void _Draw()
	{
		Font font = ThemeDB.FallbackFont;
		for (int row = 0; row < lines.Count; row++)
		{
			Written line = lines[row];
			float x = 0.0f;
			int shown = (int)line.Shown;
			for (int i = 0; i < shown; i++)
			{
				string ch = line.Text[i].ToString();
				// Burnt in by hand, so no two letters sit quite level.
				Vector2 at = anchor + new Vector2(x, row * LineGap + CrayonBrush.Noise(row * 31 + 5, i) * 4.0f);
				bool fresh = i >= shown - 2 && shown < line.Text.Length;
				DrawChar(font, at, ch, FontSize, new Color(fresh ? Glow : Burn, fade));
				x += font.GetCharSize(line.Text[i], FontSize).X;
			}

			// The laser, from his eye to the letter going on.
			if (shown < line.Text.Length && line.Writer != null && IsInstanceValid(line.Writer))
			{
				Vector2 eye = line.Writer.BeamOrigin();
				Vector2 tip = anchor + new Vector2(x, row * LineGap - FontSize * 0.3f);
				tip += new Vector2(CrayonBrush.Noise(frames, 1), CrayonBrush.Noise(frames, 2)) * 5.0f;
				DrawLine(eye, tip, new Color(Glow, 0.35f), 16.0f, true);
				DrawLine(eye, tip, new Color(Burn, 0.9f), 5.0f, true);
				DrawCircle(tip, 11.0f + Mathf.Abs(CrayonBrush.Noise(frames, 3)) * 5.0f, new Color(Glow, 0.8f));
			}
		}
	}
}
