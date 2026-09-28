using System.Collections.Generic;
using Godot;

/// <summary>
/// Hands controllers out to player slots, for menus and matches alike.
///
/// Pads go first in connection order, then keyboard schemes fill whoever is left - and the
/// keyboard schemes are handed out in order too, so the one person on a keyboard in a
/// pad-plus-keyboard game gets the player 1 keys (arrows + A/Q/W/S) rather than the number pad.
///
/// Menus and the fight share this on purpose. A pad that works in a match has to work on the
/// character select screen without a second mapping to keep in step.
/// </summary>
public static class ControllerAssignment
{
	/// <summary>The layout every pad uses. F3 swaps it; see .ai/fighting-design.md.</summary>
	public static GamepadLayout Layout = GamepadLayout.Standard();

	public static IInputSource[] ForPlayers(int count)
	{
		Godot.Collections.Array<int> pads = Input.GetConnectedJoypads();
		var sources = new IInputSource[count];
		int keyboardsUsed = 0;

		for (int i = 0; i < count; i++)
		{
			if (i < pads.Count)
			{
				sources[i] = new GamepadInputSource(pads[i], Layout);
				continue;
			}

			sources[i] = keyboardsUsed++ == 0
				? KeyboardInputSource.Player1()
				: KeyboardInputSource.Player2();
		}

		return sources;
	}

	public static string Describe(IInputSource source)
	{
		return source switch
		{
			GamepadInputSource pad => $"pad {pad.Device + 1}",
			KeyboardInputSource => "keyboard",
			CpuInputSource => "CPU",
			_ => "none",
		};
	}

	public static List<string> DescribeAll(IEnumerable<IInputSource> sources)
	{
		var labels = new List<string>();
		foreach (IInputSource source in sources) labels.Add(Describe(source));
		return labels;
	}

	/// <summary>A small preview of a stage, for the select screen. Palette plus platform shapes.</summary>
	public static void DrawStagePreview(CanvasItem canvas, StageData stage, Rect2 box)
	{
		CrayonBrush.SkyBands(canvas, box, stage.SkyTop, stage.SkyBottom, 22);

		// Fit the interesting part of the stage - the blast zone - into the preview box.
		Rect2 world = stage.BlastZone;
		float scale = Mathf.Min(box.Size.X / world.Size.X, box.Size.Y / world.Size.Y);
		Vector2 origin = box.Position + box.Size * 0.5f - world.GetCenter() * scale;

		foreach (StagePlatform platform in stage.Platforms)
		{
			var r = new Rect2(origin + platform.Rect.Position * scale, platform.Rect.Size * scale);

			// Clip to the preview box so a full-width floor or a very tall tower cannot bleed
			// out over the rest of the screen.
			Rect2 clipped = r.Intersection(box);
			if (clipped.Size.X < 1.0f || clipped.Size.Y < 1.0f) continue;

			canvas.DrawRect(clipped, platform.OneWay ? stage.PlatformCrayon : stage.GroundCrayon);
			canvas.DrawRect(clipped, stage.Ink, false, platform.OneWay ? 1.5f : 2.5f);
		}

		CrayonBrush.InkRect(canvas, box, MenuTheme.Ink, 4.0f, 17, 2.4f);
	}
}
