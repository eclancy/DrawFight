using Godot;

/// <summary>
/// A platform drawn in the air with the magic marker. It is a soft platform (layer 2) like a
/// stage's drop-through ones - jump up through it, drop down through it, stand on it - drawn as a
/// thick marker stroke with a DASHED outline, because in this game a dashed outline is what says
/// "you can drop through this" (.ai/art-direction.md). It draws itself on from one end over a few
/// frames, lasts a few seconds, blinks, and is gone.
/// </summary>
public partial class DrawnPlatform : StaticBody2D
{
	float width;
	int lifeFrames;
	int ageFrames;
	Color colour;
	int direction = 1;

	const int DrawOnFrames = 10;
	const int BlinkFrames = 60;
	const float Thickness = 16.0f;

	static readonly Color Ink = new Color(0.19f, 0.31f, 0.61f);

	public void Place(Vector2 centre, float width, int life, Color colour, int direction)
	{
		this.width = width;
		this.colour = colour;
		this.direction = direction;
		lifeFrames = life;
		GlobalPosition = centre;
		CollisionLayer = 0b10;
		CollisionMask = 0;
		AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(width, Thickness) },
			OneWayCollision = true,
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		ageFrames++;
		if (ageFrames > lifeFrames) QueueFree();
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (ageFrames > lifeFrames - BlinkFrames && ageFrames / 5 % 2 == 0) return;
		float drawn = Mathf.Min(1.0f, ageFrames / (float)DrawOnFrames);
		float half = width * 0.5f;
		// Drawn on from the end nearest him, the way the marker went.
		float from = -half * direction;
		float to = from + width * drawn * direction;
		var a = new Vector2(Mathf.Min(from, to), 0.0f);
		var b = new Vector2(Mathf.Max(from, to), 0.0f);
		Rect2 body = new Rect2(a.X, -Thickness * 0.5f, b.X - a.X, Thickness);
		CrayonBrush.CrayonFill(this, body, colour, 37);
		CrayonBrush.InkRect(this, body, Ink, 3.0f, 41, 1.8f, true);
		// The marker's own glossy streak along it.
		DrawLine(new Vector2(a.X + 6.0f, -3.0f), new Vector2(b.X - 6.0f, -3.0f), new Color(1, 1, 1, 0.35f), 3.0f, true);
	}
}
