using System.Collections.Generic;
using Godot;

/// <summary>
/// The M1 stage: one main platform, two soft platforms you can jump up through, and blast
/// zones. Built in code because white rectangles need no textures and therefore no Godot
/// import step - a fresh checkout can run this without importing anything.
///
/// M5 replaces this with StageData resources and a real stage built from a drawing.
/// </summary>
public partial class Stage : Node2D
{
	public const float MainPlatformHalfWidth = 620.0f;
	public const float PlatformThickness = 48.0f;

	/// <summary>
	/// Crossing any of these loses a stock. Deliberately generous at the sides and very
	/// generous below, so that recovering from off-stage feels possible rather than punishing.
	/// </summary>
	public Rect2 BlastZone { get; private set; } = new Rect2(
		new Vector2(-1560.0f, -1280.0f),
		new Vector2(3120.0f, 2360.0f));

	public readonly List<Rect2> Platforms = new List<Rect2>();

	public Vector2 RespawnPoint => new Vector2(0.0f, -620.0f);

	public override void _Ready()
	{
		AddPlatform(new Rect2(new Vector2(-MainPlatformHalfWidth, 0.0f),
			new Vector2(MainPlatformHalfWidth * 2.0f, PlatformThickness)), oneWay: false);

		AddPlatform(new Rect2(new Vector2(-430.0f, -250.0f), new Vector2(300.0f, 28.0f)), oneWay: true);
		AddPlatform(new Rect2(new Vector2(130.0f, -250.0f), new Vector2(300.0f, 28.0f)), oneWay: true);
		AddPlatform(new Rect2(new Vector2(-150.0f, -480.0f), new Vector2(300.0f, 28.0f)), oneWay: true);

		QueueRedraw();
	}

	void AddPlatform(Rect2 rect, bool oneWay)
	{
		Platforms.Add(rect);

		var body = new StaticBody2D
		{
			Position = rect.Position + rect.Size * 0.5f,
			CollisionLayer = 1,
			CollisionMask = 0,
		};

		body.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = rect.Size },
			// Soft platforms let you jump up through them from below, which is most of what
			// makes vertical movement on a platform-fighter stage interesting.
			OneWayCollision = oneWay,
		});

		AddChild(body);
	}

	public Vector2[] SpawnPoints => new[]
	{
		new Vector2(-340.0f, -260.0f),
		new Vector2(340.0f, -260.0f),
		new Vector2(-120.0f, -560.0f),
		new Vector2(120.0f, -560.0f),
	};

	public bool IsOutOfBounds(Vector2 point) => !BlastZone.HasPoint(point);

	public override void _Draw()
	{
		// The world is drawn on paper too. His art is dark ink on a white page, so a dark
		// stage makes every fighter disappear - the readable background is the one his
		// drawings were made on.
		foreach (Rect2 platform in Platforms)
		{
			DrawRect(platform, new Color(0.91f, 0.89f, 0.85f));
			DrawRect(platform, new Color(0.16f, 0.16f, 0.20f), false, 5.0f);
		}

		// Blast zone outline, so it is obvious during M1 tuning where the kill boundary is.
		DrawRect(BlastZone, new Color(0.82f, 0.26f, 0.30f, 0.35f), false, 4.0f);
	}
}
