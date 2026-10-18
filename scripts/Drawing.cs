using System;
using System.Collections.Generic;
using Godot;

/// <summary>What a page of a drawing is for. It is sent along with the pictures, so whoever cuts
/// the fighter up knows a fireball page from a sword page without guessing. A stage's pages are the
/// stage itself, a hazard, or a part that moves.</summary>
public enum PageKind { Fighter, Shot, Held, Extra, Stage, Hazard, Mover }

/// <summary>
/// A fighter drawn in the game's own drawing screen (DrawScreen): its pages, each page's layers,
/// and the character sheet that goes with it when it is sent in.
///
/// Every page is the same size and every layer covers the whole page, the way a tablet app exports
/// layers - so the parts arrive already in place relative to one another, which is exactly what the
/// layered-art path of the importer wants (.ai/art-pipeline.md, "Path A").
///
/// Saved under user://drawings/&lt;id&gt;/: drawing.json, one PNG per layer, and a thumbnail.
/// </summary>
public sealed class Drawing
{
	public const int Width = 1400;
	public const int Height = 1050;
	public const int MaxPages = 6;
	public const int MaxLayers = 10;

	const string Root = "user://drawings";

	public string Id;
	public string Created = "";
	public string Modified = "";

	/// <summary>When it was sent in, if it has been - a drawing can be sent again after changes.</summary>
	public readonly List<string> Sent = new List<string>();

	/// <summary>The answers to the character sheet, by the intake server's field names.</summary>
	public readonly Dictionary<string, string> Sheet = new Dictionary<string, string>();

	public readonly List<DrawingPage> Pages = new List<DrawingPage>();
	public int ActivePageIndex;

	/// <summary>A stage rather than a fighter (Eric, 2026-10-18): its own pages, guide and questions, sent to the stage intake.</summary>
	public bool IsStage;

	/// <summary>Changed since it was last saved.</summary>
	public bool Unsaved;

	/// <summary>Undo, kept with the drawing rather than the screen, so going to send it and coming
	/// back does not lose it. Not saved: it is gone when the game closes.</summary>
	public readonly EditHistory History = new EditHistory();

	int nextLayer;

	public DrawingPage ActivePage => Pages[Mathf.Clamp(ActivePageIndex, 0, Pages.Count - 1)];
	public string Folder => Root + "/" + Id;

	/// <summary>What it is called: the fighter's name from the sheet, once there is one.</summary>
	public string Title => Sheet.TryGetValue("name", out string name) && !string.IsNullOrWhiteSpace(name) ? name.Trim() : Untitled(IsStage);

	static string Untitled(bool stage) => stage ? "Untitled stage" : "Untitled fighter";

	public static Drawing New(bool stage = false)
	{
		var drawing = new Drawing { Id = NewId(), Created = Now(), IsStage = stage };
		if (stage) drawing.AddPage(PageKind.Stage, "Stage");
		else drawing.AddPage(PageKind.Fighter, "Fighter");
		drawing.Unsaved = true;
		return drawing;
	}

	public DrawingLayer NewLayer(string name) => new DrawingLayer(name) { File = $"layer{nextLayer++}.png" };

	public DrawingPage AddPage(PageKind kind, string name)
	{
		var page = new DrawingPage { Name = name, Kind = kind };
		page.Layers.Add(NewLayer(kind == PageKind.Fighter ? "Drawing" : "Layer 1"));
		Pages.Add(page);
		return page;
	}

	static string Now() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

	static string NewId() => "d" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + ((int)(GD.Randi() % 4096)).ToString("x3");

	// --- Saving ---------------------------------------------------------------------------

	public void Save()
	{
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
		Modified = Now();

		var keep = new HashSet<string>();
		var pages = new Godot.Collections.Array();
		foreach (DrawingPage page in Pages)
		{
			var layers = new Godot.Collections.Array();
			foreach (DrawingLayer layer in page.Layers)
			{
				string path = Folder + "/" + layer.File;
				// Only what changed is written: a PNG of the whole page is not free, and autosave
				// runs while he draws.
				if (layer.Unsaved || !FileAccess.FileExists(path))
				{
					layer.ToImage().SavePng(path);
					layer.Unsaved = false;
				}
				keep.Add(layer.File);
				layers.Add(new Godot.Collections.Dictionary
				{
					["name"] = layer.Name, ["visible"] = layer.Visible, ["file"] = layer.File,
				});
			}
			pages.Add(new Godot.Collections.Dictionary
			{
				["name"] = page.Name, ["kind"] = page.Kind.ToString(), ["active"] = page.ActiveLayer, ["layers"] = layers,
			});
		}

		var sheet = new Godot.Collections.Dictionary();
		foreach (var pair in Sheet) sheet[pair.Key] = pair.Value;
		var sent = new Godot.Collections.Array();
		foreach (string when in Sent) sent.Add(when);

		var root = new Godot.Collections.Dictionary
		{
			["version"] = 1, ["kind"] = IsStage ? "stage" : "fighter", ["created"] = Created, ["modified"] = Modified, ["nextLayer"] = nextLayer,
			["activePage"] = ActivePageIndex, ["sheet"] = sheet, ["sent"] = sent, ["pages"] = pages,
		};

		// Written beside the old one and swapped in, so a crash mid-save cannot leave half a file.
		string json = Folder + "/drawing.json";
		using (FileAccess file = FileAccess.Open(json + ".tmp", FileAccess.ModeFlags.Write))
		{
			file?.StoreString(Json.Stringify(root, "\t"));
		}
		DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(json));
		DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(json + ".tmp"), ProjectSettings.GlobalizePath(json));

		SaveThumbnail();

		// Layers deleted since the last save leave their PNGs behind; tidy them away.
		using DirAccess dir = DirAccess.Open(Folder);
		if (dir != null)
		{
			foreach (string name in dir.GetFiles())
			{
				if (name.StartsWith("layer") && name.EndsWith(".png") && !keep.Contains(name)) dir.Remove(name);
			}
		}
		Unsaved = false;
	}

	void SaveThumbnail()
	{
		Image page = Flatten(Pages[0], MenuTheme.Paper);
		page.Resize(ThumbWidth, ThumbHeight, Image.Interpolation.Bilinear);
		page.SavePng(Folder + "/thumb.png");
	}

	public const int ThumbWidth = 320;
	public const int ThumbHeight = 240;

	public static Drawing Load(string id)
	{
		string folder = Root + "/" + id;
		using FileAccess file = FileAccess.Open(folder + "/drawing.json", FileAccess.ModeFlags.Read);
		if (file == null) return null;
		if (Json.ParseString(file.GetAsText()).Obj is not Godot.Collections.Dictionary root) return null;

		var drawing = new Drawing { Id = id };
		drawing.IsStage = root.TryGetValue("kind", out Variant kindOf) && kindOf.AsString() == "stage";
		drawing.Created = root.TryGetValue("created", out Variant created) ? created.AsString() : "";
		drawing.Modified = root.TryGetValue("modified", out Variant modified) ? modified.AsString() : "";
		drawing.nextLayer = root.TryGetValue("nextLayer", out Variant next) ? next.AsInt32() : 0;
		if (root.TryGetValue("sheet", out Variant sheet))
		{
			foreach (var pair in sheet.AsGodotDictionary()) drawing.Sheet[pair.Key.AsString()] = pair.Value.AsString();
		}
		if (root.TryGetValue("sent", out Variant sent))
		{
			foreach (Variant when in sent.AsGodotArray()) drawing.Sent.Add(when.AsString());
		}

		if (root.TryGetValue("pages", out Variant pages))
		{
			foreach (Variant pageValue in pages.AsGodotArray())
			{
				var pageData = pageValue.AsGodotDictionary();
				var page = new DrawingPage
				{
					Name = pageData["name"].AsString(),
					Kind = Enum.TryParse(pageData["kind"].AsString(), out PageKind kind) ? kind : PageKind.Extra,
				};
				foreach (Variant layerValue in pageData["layers"].AsGodotArray())
				{
					var layerData = layerValue.AsGodotDictionary();
					var layer = new DrawingLayer(layerData["name"].AsString())
					{
						Visible = layerData["visible"].AsBool(),
						File = layerData["file"].AsString(),
					};
					Image image = Image.LoadFromFile(folder + "/" + layer.File);
					if (image != null) layer.Load(image);
					layer.Unsaved = false;
					page.Layers.Add(layer);
				}
				if (page.Layers.Count == 0) page.Layers.Add(drawing.NewLayer("Layer 1"));
				page.ActiveLayer = Mathf.Clamp(pageData["active"].AsInt32(), 0, page.Layers.Count - 1);
				drawing.Pages.Add(page);
			}
		}
		if (drawing.Pages.Count == 0)
		{
			if (drawing.IsStage) drawing.AddPage(PageKind.Stage, "Stage");
			else drawing.AddPage(PageKind.Fighter, "Fighter");
		}
		drawing.ActivePageIndex = root.TryGetValue("activePage", out Variant active)
			? Mathf.Clamp(active.AsInt32(), 0, drawing.Pages.Count - 1) : 0;
		return drawing;
	}

	/// <summary>Every saved drawing, newest first.</summary>
	public static List<SavedDrawing> ListSaved()
	{
		var found = new List<SavedDrawing>();
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Root));
		using DirAccess dir = DirAccess.Open(Root);
		if (dir == null) return found;
		foreach (string id in dir.GetDirectories())
		{
			using FileAccess file = FileAccess.Open(Root + "/" + id + "/drawing.json", FileAccess.ModeFlags.Read);
			if (file == null) continue;
			if (Json.ParseString(file.GetAsText()).Obj is not Godot.Collections.Dictionary root) continue;
			bool stage = root.TryGetValue("kind", out Variant kindOf) && kindOf.AsString() == "stage";
			string title = Untitled(stage);
			if (root.TryGetValue("sheet", out Variant sheet) && sheet.AsGodotDictionary().TryGetValue("name", out Variant name)
				&& !string.IsNullOrWhiteSpace(name.AsString()))
			{
				title = name.AsString().Trim();
			}
			Image thumb = Image.LoadFromFile(Root + "/" + id + "/thumb.png");
			found.Add(new SavedDrawing
			{
				Id = id,
				Title = title,
				IsStage = stage,
				Modified = root.TryGetValue("modified", out Variant modified) ? modified.AsString() : "",
				Sent = root.TryGetValue("sent", out Variant sent) && sent.AsGodotArray().Count > 0,
				Thumbnail = thumb != null ? ImageTexture.CreateFromImage(thumb) : null,
			});
		}
		found.Sort((a, b) => string.CompareOrdinal(b.Modified, a.Modified));
		return found;
	}

	public static void Delete(string id)
	{
		string folder = ProjectSettings.GlobalizePath(Root + "/" + id);
		using DirAccess dir = DirAccess.Open(folder);
		if (dir == null) return;
		foreach (string name in dir.GetFiles()) dir.Remove(name);
		DirAccess.RemoveAbsolute(folder);
	}

	// --- Pictures -------------------------------------------------------------------------------

	/// <summary>A page's visible layers stacked into one picture - see-through where nothing is drawn,
	/// or on a background colour.</summary>
	public static Image Flatten(DrawingPage page, Color? background = null)
	{
		byte[] pixels = Composite(page);
		if (background is Color paper)
		{
			byte[] under = new byte[pixels.Length];
			for (int i = 0; i < under.Length; i += 4)
			{
				under[i] = (byte)(paper.R * 255.0f);
				under[i + 1] = (byte)(paper.G * 255.0f);
				under[i + 2] = (byte)(paper.B * 255.0f);
				under[i + 3] = 255;
			}
			Over(under, pixels);
			pixels = under;
		}
		return Image.CreateFromData(Width, Height, false, Image.Format.Rgba8, pixels);
	}

	/// <summary>The visible layers of a page stacked bottom to top, as straight-alpha RGBA.</summary>
	public static byte[] Composite(DrawingPage page, byte[] into = null)
	{
		byte[] result = into ?? new byte[Width * Height * 4];
		Array.Clear(result);
		foreach (DrawingLayer layer in page.Layers)
		{
			if (layer.Visible) Over(result, layer.Pixels);
		}
		return result;
	}

	/// <summary>Lays <paramref name="top"/> over <paramref name="bottom"/>, in place.</summary>
	public static void Over(byte[] bottom, byte[] top)
	{
		for (int i = 0; i < bottom.Length; i += 4)
		{
			int ta = top[i + 3];
			if (ta == 0) continue;
			int ba = bottom[i + 3];
			if (ta == 255 || ba == 0)
			{
				bottom[i] = top[i];
				bottom[i + 1] = top[i + 1];
				bottom[i + 2] = top[i + 2];
				bottom[i + 3] = (byte)ta;
				continue;
			}
			float a = ta / 255.0f;
			float b = ba / 255.0f * (1.0f - a);
			float outA = a + b;
			bottom[i] = (byte)((top[i] * a + bottom[i] * b) / outA + 0.5f);
			bottom[i + 1] = (byte)((top[i + 1] * a + bottom[i + 1] * b) / outA + 0.5f);
			bottom[i + 2] = (byte)((top[i + 2] * a + bottom[i + 2] * b) / outA + 0.5f);
			bottom[i + 3] = (byte)(outA * 255.0f + 0.5f);
		}
	}
}

/// <summary>One page of a drawing: the fighter, or a fireball, or a sword. Layers bottom first.</summary>
public sealed class DrawingPage
{
	public string Name;
	public PageKind Kind;
	public readonly List<DrawingLayer> Layers = new List<DrawingLayer>();
	public int ActiveLayer;

	public DrawingLayer Active => Layers[Mathf.Clamp(ActiveLayer, 0, Layers.Count - 1)];

	/// <summary>What the page is for, in words for whoever opens the submission.</summary>
	public string KindWords => Kind switch
	{
		PageKind.Fighter => "the fighter",
		PageKind.Shot => "something they shoot or throw",
		PageKind.Held => "something they hold",
		PageKind.Stage => "the stage",
		PageKind.Hazard => "a hazard - something that hurts",
		PageKind.Mover => "a part that moves",
		_ => "something else",
	};

	public bool IsEmpty
	{
		get
		{
			foreach (DrawingLayer layer in Layers) if (layer.Visible && !layer.IsEmpty()) return false;
			return true;
		}
	}
}

/// <summary>
/// One layer: a whole page of straight-alpha RGBA pixels, kept in C# so painting never has to
/// cross into the engine per pixel, and shown as a grid of textures so a brush stroke uploads only
/// the tiles it touched rather than the whole page every frame.
/// </summary>
public sealed class DrawingLayer
{
	public const int TileSize = 256;
	static readonly int Cols = (Drawing.Width + TileSize - 1) / TileSize;
	static readonly int Rows = (Drawing.Height + TileSize - 1) / TileSize;
	static readonly byte[] scratch = new byte[TileSize * TileSize * 4];

	public string Name;
	public bool Visible = true;
	public string File;
	public byte[] Pixels = new byte[Drawing.Width * Drawing.Height * 4];

	/// <summary>Changed since the drawing was last saved.</summary>
	public bool Unsaved = true;

	readonly ImageTexture[] tiles = new ImageTexture[Cols * Rows];
	readonly bool[] stale = new bool[Cols * Rows];
	readonly bool[] inked = new bool[Cols * Rows];

	public DrawingLayer(string name)
	{
		Name = name;
	}

	/// <summary>Some pixels changed: their tiles are redrawn next frame, and the layer saved next save.</summary>
	public void Touch(Rect2I area)
	{
		area = area.Intersection(new Rect2I(0, 0, Drawing.Width, Drawing.Height));
		if (area.Size.X <= 0 || area.Size.Y <= 0) return;
		Unsaved = true;
		for (int ty = area.Position.Y / TileSize; ty <= (area.End.Y - 1) / TileSize; ty++)
		{
			for (int tx = area.Position.X / TileSize; tx <= (area.End.X - 1) / TileSize; tx++) stale[ty * Cols + tx] = true;
		}
	}

	public void TouchAll() => Touch(new Rect2I(0, 0, Drawing.Width, Drawing.Height));

	public void Load(Image image)
	{
		if (image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
		if (image.GetWidth() != Drawing.Width || image.GetHeight() != Drawing.Height)
		{
			var fitted = Image.CreateEmpty(Drawing.Width, Drawing.Height, false, Image.Format.Rgba8);
			fitted.BlitRect(image, new Rect2I(0, 0, Mathf.Min(image.GetWidth(), Drawing.Width), Mathf.Min(image.GetHeight(), Drawing.Height)), Vector2I.Zero);
			image = fitted;
		}
		Pixels = image.GetData();
		TouchAll();
	}

	public Image ToImage() => Image.CreateFromData(Drawing.Width, Drawing.Height, false, Image.Format.Rgba8, Pixels);

	public bool IsEmpty()
	{
		for (int i = 3; i < Pixels.Length; i += 4) if (Pixels[i] != 0) return false;
		return true;
	}

	/// <summary>Draws the layer with its top-left at <paramref name="offset"/>, in whatever transform the canvas has.</summary>
	public void DrawOn(CanvasItem canvas, Vector2 offset, Color modulate)
	{
		Refresh();
		for (int t = 0; t < tiles.Length; t++)
		{
			if (tiles[t] == null || !inked[t]) continue;
			int tx = t % Cols, ty = t / Cols;
			Vector2 size = tiles[t].GetSize();
			canvas.DrawTextureRect(tiles[t], new Rect2(offset + new Vector2(tx * TileSize, ty * TileSize), size), false, modulate);
		}
	}

	void Refresh()
	{
		for (int t = 0; t < tiles.Length; t++)
		{
			if (!stale[t]) continue;
			stale[t] = false;
			int tx = t % Cols, ty = t / Cols;
			int x0 = tx * TileSize, y0 = ty * TileSize;
			int w = Mathf.Min(TileSize, Drawing.Width - x0), h = Mathf.Min(TileSize, Drawing.Height - y0);
			bool any = false;
			for (int row = 0; row < h; row++)
			{
				int from = ((y0 + row) * Drawing.Width + x0) * 4;
				Buffer.BlockCopy(Pixels, from, scratch, row * w * 4, w * 4);
				if (any) continue;
				for (int k = 3; k < w * 4; k += 4)
				{
					if (scratch[row * w * 4 + k] == 0) continue;
					any = true;
					break;
				}
			}
			inked[t] = any;
			if (!any && tiles[t] == null) continue;
			if (any) Bleed(w, h);
			byte[] data = scratch.Length == w * h * 4 ? scratch : scratch.AsSpan(0, w * h * 4).ToArray();
			Image image = Image.CreateFromData(w, h, false, Image.Format.Rgba8, data);
			if (tiles[t] == null) tiles[t] = ImageTexture.CreateFromImage(image);
			else tiles[t].Update(image);
		}
	}

	/// <summary>
	/// Gives each see-through pixel next to ink that ink's colour, in the copy shown on screen.
	/// A see-through pixel is stored black, and the page is drawn scaled, so the filter mixed that
	/// black into everything at an edge - every gap in the pencil's grain came out as a grey speck.
	/// </summary>
	static void Bleed(int w, int h)
	{
		int stride = w * 4;
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				int k = y * stride + x * 4;
				if (scratch[k + 3] != 0) continue;
				int from = x > 0 && scratch[k - 1] != 0 ? k - 4
					: x < w - 1 && scratch[k + 7] != 0 ? k + 4
					: y > 0 && scratch[k - stride + 3] != 0 ? k - stride
					: y < h - 1 && scratch[k + stride + 3] != 0 ? k + stride
					: -1;
				if (from < 0) continue;
				scratch[k] = scratch[from];
				scratch[k + 1] = scratch[from + 1];
				scratch[k + 2] = scratch[from + 2];
			}
		}
	}
}

/// <summary>Something done to a drawing that can be undone.</summary>
public abstract class Edit
{
	public DrawingPage Page;
	public abstract void Undo();
	public abstract void Redo();
	public virtual long Bytes => 64;
}

/// <summary>Pixels changed in one rectangle of one layer: what was there before, and after.</summary>
public sealed class PixelEdit : Edit
{
	readonly DrawingLayer layer;
	readonly Rect2I rect;
	readonly byte[] before, after;

	public PixelEdit(DrawingPage page, DrawingLayer layer, Rect2I rect, byte[] before, byte[] after)
	{
		Page = page;
		this.layer = layer;
		this.rect = rect;
		this.before = before;
		this.after = after;
	}

	public override long Bytes => before.Length + after.Length;
	public override void Undo() => Paste(before);
	public override void Redo() => Paste(after);

	void Paste(byte[] data)
	{
		Put(layer.Pixels, rect, data);
		layer.Touch(rect);
	}

	/// <summary>A copy of one rectangle of a page of pixels.</summary>
	public static byte[] Cut(byte[] pixels, Rect2I rect)
	{
		var cut = new byte[rect.Size.X * rect.Size.Y * 4];
		for (int row = 0; row < rect.Size.Y; row++)
		{
			Buffer.BlockCopy(pixels, ((rect.Position.Y + row) * Drawing.Width + rect.Position.X) * 4, cut, row * rect.Size.X * 4, rect.Size.X * 4);
		}
		return cut;
	}

	public static void Put(byte[] pixels, Rect2I rect, byte[] data)
	{
		for (int row = 0; row < rect.Size.Y; row++)
		{
			Buffer.BlockCopy(data, row * rect.Size.X * 4, pixels, ((rect.Position.Y + row) * Drawing.Width + rect.Position.X) * 4, rect.Size.X * 4);
		}
	}
}

/// <summary>A layer added to a page, or taken off it.</summary>
public sealed class LayerEdit : Edit
{
	readonly DrawingLayer layer;
	readonly int index;
	readonly bool added;

	public LayerEdit(DrawingPage page, DrawingLayer layer, int index, bool added)
	{
		Page = page;
		this.layer = layer;
		this.index = index;
		this.added = added;
	}

	public override long Bytes => added ? 64 : layer.Pixels.Length;
	public override void Undo() => Apply(!added);
	public override void Redo() => Apply(added);

	void Apply(bool add)
	{
		if (add)
		{
			Page.Layers.Insert(Mathf.Clamp(index, 0, Page.Layers.Count), layer);
			Page.ActiveLayer = Page.Layers.IndexOf(layer);
		}
		else
		{
			Page.Layers.Remove(layer);
			Page.ActiveLayer = Mathf.Clamp(index - 1, 0, Page.Layers.Count - 1);
		}
		layer.TouchAll();
	}
}

/// <summary>A layer moved up or down the stack.</summary>
public sealed class LayerOrderEdit : Edit
{
	readonly int from, to;

	public LayerOrderEdit(DrawingPage page, int from, int to)
	{
		Page = page;
		this.from = from;
		this.to = to;
	}

	public override void Undo() => Move(to, from);
	public override void Redo() => Move(from, to);

	void Move(int a, int b)
	{
		DrawingLayer layer = Page.Layers[a];
		Page.Layers.RemoveAt(a);
		Page.Layers.Insert(b, layer);
		Page.ActiveLayer = b;
	}
}

/// <summary>A layer pressed down into the one under it.</summary>
public sealed class MergeEdit : Edit
{
	readonly LayerEdit removed;
	readonly PixelEdit lower;

	public MergeEdit(DrawingPage page, LayerEdit removed, PixelEdit lower)
	{
		Page = page;
		this.removed = removed;
		this.lower = lower;
	}

	public override long Bytes => removed.Bytes + lower.Bytes;

	public override void Undo()
	{
		lower.Undo();
		removed.Undo();
	}

	public override void Redo()
	{
		lower.Redo();
		removed.Redo();
	}
}

/// <summary>Undo and redo, with a cap on how much memory the history may hold.</summary>
public sealed class EditHistory
{
	const int MaxEdits = 60;
	const long MaxBytes = 300L * 1024 * 1024;

	readonly List<Edit> undo = new List<Edit>();
	readonly List<Edit> redo = new List<Edit>();

	public bool CanUndo => undo.Count > 0;
	public bool CanRedo => redo.Count > 0;

	public void Push(Edit edit)
	{
		if (edit == null) return;
		undo.Add(edit);
		redo.Clear();
		long bytes = 0;
		foreach (Edit e in undo) bytes += e.Bytes;
		while (undo.Count > 1 && (undo.Count > MaxEdits || bytes > MaxBytes))
		{
			bytes -= undo[0].Bytes;
			undo.RemoveAt(0);
		}
	}

	/// <summary>Undoes the last edit and returns it (so the screen can show its page), or null.</summary>
	public Edit Undo()
	{
		if (undo.Count == 0) return null;
		Edit edit = undo[^1];
		undo.RemoveAt(undo.Count - 1);
		edit.Undo();
		redo.Add(edit);
		return edit;
	}

	public Edit Redo()
	{
		if (redo.Count == 0) return null;
		Edit edit = redo[^1];
		redo.RemoveAt(redo.Count - 1);
		edit.Redo();
		undo.Add(edit);
		return edit;
	}

	/// <summary>A page was thrown away: nothing that happened on it can be undone any more.</summary>
	public void Forget(DrawingPage page)
	{
		undo.RemoveAll(e => e.Page == page);
		redo.RemoveAll(e => e.Page == page);
	}
}

/// <summary>A saved drawing as the drawings list shows it, without loading every layer.</summary>
public sealed class SavedDrawing
{
	public string Id;
	public string Title;
	public string Modified;
	public bool Sent;
	public bool IsStage;
	public Texture2D Thumbnail;
}
