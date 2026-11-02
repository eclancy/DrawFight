using Godot;

/// <summary>
/// The picture a bug report can send: the last frame of the last match, kept in memory only and
/// never written to disk. "Report a bug" opens from the main menu, so a picture taken then would
/// only ever show the main menu - instead the match leaves one behind as it is left (GameRoot.Show)
/// or paused (MatchManager.OnStartPressed, taken before the pause page goes up). A fresh launch
/// has none, and the bug report then offers none (SuggestScreen).
/// </summary>
public static class BugSnapshot
{
	/// <summary>The longest side it is kept at, and sent at.</summary>
	public const int MaxSide = 1600;

	public const float JpegQuality = 0.85f;

	/// <summary>The picture, already scaled down; null until a match has been played.</summary>
	public static Image Picture { get; private set; }

	/// <summary>What it is, as the bug report says it: "From your last match".</summary>
	public static string Caption { get; private set; } = "";

	public static bool Has => Picture != null;

	/// <summary>Keeps what <paramref name="viewport"/> shows now - the frame drawn last.</summary>
	public static void Take(Viewport viewport, string caption)
	{
		Image image = viewport.GetTexture()?.GetImage();
		if (image == null || image.IsEmpty()) return;
		int longest = Mathf.Max(image.GetWidth(), image.GetHeight());
		if (longest > MaxSide)
		{
			float scale = MaxSide / (float)longest;
			image.Resize(Mathf.RoundToInt(image.GetWidth() * scale), Mathf.RoundToInt(image.GetHeight() * scale),
				Image.Interpolation.Bilinear);
		}
		Picture = image;
		Caption = caption;
	}

	/// <summary>The picture as the JPEG that is sent, or null when there is none.</summary>
	public static byte[] Jpeg() => Picture?.SaveJpgToBuffer(JpegQuality);
}
