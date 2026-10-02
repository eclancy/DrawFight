using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

/// <summary>
/// The first screen of a downloaded copy of the game: checks ecec.dev for a newer version and,
/// if there is one, installs it and restarts - no questions, because the people playing this
/// are kids and "Update available?" is a question they should not have to answer.
///
/// How a release is laid out is in .ai/releasing.md. In short: ecec.dev/drawfight/version.json
/// redirects to the newest GitHub Release's version.json, which names a small patch (the .pck
/// and the game's own DLL - everything a normal change touches) and the full zip (needed only
/// when the Godot engine itself changed). GitHub serves the files, so downloads cost nothing.
///
/// The running game cannot overwrite itself on Windows - its DLL and .pck are open - so the new
/// files are unpacked next to it and a small script waits for the game to close, copies them
/// over, and starts it again.
///
/// Every failure, from no internet onwards, just goes to the title screen. An update is never
/// worth not being able to play.
/// </summary>
public partial class UpdateScreen : Node2D
{
	/// <summary>The one address baked into every copy of the game. It is on ecec.dev rather than
	/// GitHub so where releases live can change without stranding old copies.</summary>
	public const string VersionUrl = "https://ecec.dev/drawfight/version.json";

	public static string CurrentVersion =>
		ProjectSettings.GetSetting("application/config/version", "0.0.0").AsString();

	static string EngineVersion
	{
		get
		{
			var info = Engine.GetVersionInfo();
			return $"{info["major"]}.{info["minor"]}.{info["patch"]}";
		}
	}

	/// <summary>Only an exported Windows build updates itself. Running from the editor or from
	/// `godot --path .` never does, and `--no-update` turns it off for testing a build.</summary>
	public static bool ShouldCheck()
	{
		if (!OS.HasFeature("template") || OS.GetName() != "Windows") return false;
		return Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-update") < 0
			&& Array.IndexOf(OS.GetCmdlineArgs(), "--no-update") < 0;
	}

	enum Phase { Checking, Downloading, Installing, Restart, Done }

	// Written by the worker thread, read by the main thread. Everything the screen needs is a
	// plain value, so there is nothing to lock.
	volatile Phase phase = Phase.Checking;
	volatile float progress;
	string newVersion = "";
	string applyScript = "";

	Label title;
	Label detail;
	int frames;

	public override void _Ready()
	{
		var root = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);

		title = MenuTheme.MakeLabel("", new Vector2(0.0f, 380.0f), 88, MenuTheme.Text);
		title.Size = new Vector2(1920.0f, 120.0f);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(title);

		detail = MenuTheme.MakeLabel("", new Vector2(0.0f, 650.0f), 40, MenuTheme.Soft);
		detail.Size = new Vector2(1920.0f, 60.0f);
		detail.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(detail);

		Task.Run(Work);
	}

	public override void _Process(double delta)
	{
		frames++;
		switch (phase)
		{
			case Phase.Done:
				GameRoot.Instance.GoTitle();
				return;

			case Phase.Restart:
				// The script waits for this process to end, so it has to be started first.
				OS.CreateProcess("cmd.exe", new[] { "/c", applyScript });
				GetTree().Quit();
				return;

			case Phase.Checking:
				// Most checks finish before anyone could read this; it only shows when the
				// internet is slow, and then it says why the game has not started.
				title.Text = frames > 45 ? "Checking for updates..." : "";
				detail.Text = "";
				break;

			case Phase.Downloading:
				title.Text = "Updating DrawFight!";
				detail.Text = $"Getting version {newVersion}...  {Mathf.RoundToInt(progress * 100.0f)}%";
				break;

			case Phase.Installing:
				title.Text = "Updating DrawFight!";
				detail.Text = "Nearly done - the game will restart by itself.";
				break;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
		if (phase != Phase.Downloading && phase != Phase.Installing) return;

		var bar = new Rect2(560.0f, 540.0f, 800.0f, 56.0f);
		float filled = phase == Phase.Installing ? 1.0f : Mathf.Clamp(progress, 0.0f, 1.0f);
		DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * filled, bar.Size.Y)), MenuTheme.Accent);
		CrayonBrush.InkRect(this, bar, MenuTheme.Ink, 4.0f, 41, 2.4f);
	}

	// --- The worker -----------------------------------------------------------------

	async Task Work()
	{
		try
		{
			phase = await Update() ? Phase.Restart : Phase.Done;
		}
		catch (Exception e)
		{
			GD.Print($"UpdateScreen: no update ({e.GetType().Name}: {e.Message})");
			phase = Phase.Done;
		}
	}

	/// <summary>True when a new version is unpacked and the restart script is written.</summary>
	async Task<bool> Update()
	{
		using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd($"DrawFight/{CurrentVersion}");

		// DRAWFIGHT_UPDATE_URL points a build at a test server instead - see .ai/releasing.md.
		string url = OS.GetEnvironment("DRAWFIGHT_UPDATE_URL");
		if (string.IsNullOrEmpty(url)) url = VersionUrl;
		using JsonDocument doc = JsonDocument.Parse(await http.GetStringAsync(url));
		JsonElement release = doc.RootElement;
		string latest = release.GetProperty("version").GetString() ?? "";
		if (!IsNewer(latest, CurrentVersion))
		{
			GD.Print($"UpdateScreen: {CurrentVersion} is up to date");
			return false;
		}

		string installDir = Path.GetDirectoryName(OS.GetExecutablePath());
		if (!CanWriteTo(installDir))
		{
			GD.Print($"UpdateScreen: {latest} is out, but {installDir} is not writable");
			return false;
		}

		// The patch only works on the engine it was built with; anything else takes the full zip.
		bool sameEngine = release.TryGetProperty("engine", out JsonElement engine)
			&& engine.GetString() == EngineVersion;
		url = sameEngine && release.TryGetProperty("patch", out JsonElement patch)
			? patch.GetString()
			: release.GetProperty("full").GetString();

		newVersion = latest;
		phase = Phase.Downloading;

		string work = ProjectSettings.GlobalizePath("user://update");
		if (Directory.Exists(work)) Directory.Delete(work, true);
		Directory.CreateDirectory(work);
		string zipPath = Path.Combine(work, "update.zip");
		await Download(url, zipPath);

		phase = Phase.Installing;
		string unpacked = Path.Combine(work, "files");
		ZipFile.ExtractToDirectory(zipPath, unpacked);
		File.Delete(zipPath);

		// The full zip keeps everything in a DrawFight/ folder so it unzips tidily for a person;
		// the patch does not. Either way, copy from wherever the .pck is.
		string from = FindPck(unpacked);
		if (from == null) throw new InvalidDataException("the update has no DrawFight.pck");

		applyScript = Path.Combine(work, "apply.cmd");
		File.WriteAllText(applyScript, ApplyScript(from, installDir, OS.GetExecutablePath(), OS.GetProcessId()));
		GD.Print($"UpdateScreen: {CurrentVersion} -> {latest} ready, restarting");
		return true;
	}

	async Task Download(string url, string path)
	{
		// A separate client: the five-second timeout above is for the check, not for 30 MB on a
		// slow connection.
		using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd($"DrawFight/{CurrentVersion}");
		using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		long total = response.Content.Headers.ContentLength ?? 0;

		progress = 0.0f;
		using Stream source = await response.Content.ReadAsStreamAsync();
		using FileStream target = File.Create(path);
		var buffer = new byte[81920];
		long done = 0;
		int read;
		while ((read = await source.ReadAsync(buffer)) > 0)
		{
			await target.WriteAsync(buffer.AsMemory(0, read));
			done += read;
			if (total > 0) progress = done / (float)total;
		}
	}

	static string FindPck(string dir)
	{
		if (File.Exists(Path.Combine(dir, "DrawFight.pck"))) return dir;
		foreach (string sub in Directory.GetDirectories(dir))
		{
			if (File.Exists(Path.Combine(sub, "DrawFight.pck"))) return sub;
		}
		return null;
	}

	static bool CanWriteTo(string dir)
	{
		try
		{
			string probe = Path.Combine(dir, ".update-probe");
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>"0.10.0" is newer than "0.9.2": compared number by number, not as text.</summary>
	public static bool IsNewer(string candidate, string current)
	{
		string[] a = candidate.Split('.');
		string[] b = current.Split('.');
		for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
		{
			int x = i < a.Length && int.TryParse(a[i], out int ax) ? ax : 0;
			int y = i < b.Length && int.TryParse(b[i], out int by) ? by : 0;
			if (x != y) return x > y;
		}
		return false;
	}

	/// <summary>
	/// Waits for the game to close, copies the new files over the old, and starts it again.
	/// `ping` is the sleep because `timeout` refuses to run without a console to read from.
	/// robocopy retries a locked file a few times, which covers the moment Windows takes to let
	/// go of a closed process's files.
	/// </summary>
	static string ApplyScript(string from, string to, string exe, int pid) =>
		"@echo off\r\n" +
		":wait\r\n" +
		$"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\r\n" +
		"if not errorlevel 1 (\r\n" +
		"  ping -n 2 127.0.0.1 >nul\r\n" +
		"  goto wait\r\n" +
		")\r\n" +
		$"robocopy \"{from}\" \"{to}\" /E /R:10 /W:1 /NFL /NDL /NJH /NJS /NP >nul\r\n" +
		$"start \"\" \"{exe}\"\r\n" +
		$"rmdir /s /q \"{from}\"\r\n";
}
