using System.Collections.Generic;
using Godot;

/// <summary>
/// "for my nephew..." - Eric's picture of his nephew at the table among his drawings, shown once:
/// the first time DoomBot's own story is finished, after its ending (Eric, 2026-10-24). It fades up
/// and drifts in, very slowly, and any button goes on. The same picture ends the gallery
/// (GalleryScreen).
/// </summary>
public partial class NephewScreen : Node2D
{
	public const string Picture = "res://assets/gallery/for_my_nephew.webp";

	/// <summary>Where to go on from it.</summary>
	public System.Action Then;

	const int FadeFrames = 150;
	/// <summary>A button held from the scene before does not skip it: it shows this long first.</summary>
	const int HoldFrames = 120;
	const int DriftFrames = 1800;

	readonly List<MenuNav> navs = new List<MenuNav>();
	Texture2D picture;
	Color paper = Colors.White;
	int frames;
	bool leaving;

	public override void _Ready()
	{
		foreach (IInputSource source in ControllerAssignment.Everyone()) navs.Add(new MenuNav(source));
		picture = GD.Load<Texture2D>(Picture);
		// Its own paper, round it, so the picture has no edge.
		Image image = picture?.GetImage();
		if (image != null) paper = image.GetPixel(4, 4);
	}

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		foreach (MenuNav nav in navs)
		{
			nav.Poll();
			if (!nav.AnyPress) continue;
			Leave();
			return;
		}
		QueueRedraw();
	}

	public override void _Input(InputEvent e)
	{
		if (ControllerAssignment.IsAnyPress(e)) Leave();
	}

	void Leave()
	{
		if (leaving || frames < HoldFrames) return;
		leaving = true;
		SfxPlayer.Ui("ui_select");
		if (Then != null) Then();
		else GameRoot.Instance.GoMainMenu();
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		DrawRect(new Rect2(Vector2.Zero, size), paper);
		if (picture == null) return;
		Vector2 art = picture.GetSize();
		float t = Mathf.Min(1.0f, frames / (float)DriftFrames);
		float scale = Mathf.Min(size.X / art.X, size.Y / art.Y) * (1.0f + 0.035f * Mathf.SmoothStep(0.0f, 1.0f, t));
		Vector2 shown = art * scale;
		float alpha = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Min(1.0f, frames / (float)FadeFrames));
		DrawTextureRect(picture, new Rect2((size - shown) * 0.5f, shown), false, new Color(1.0f, 1.0f, 1.0f, alpha));
	}
}
