using System.Collections.Generic;
using Godot;

/// <summary>
/// One line of a story script, read from a fighter's <c>story.txt</c> - something said, or
/// something done. See the top of any story.txt for the whole language; .ai/story-mode.md for
/// why a scene is a match before the fight starts rather than a cutscene.
/// </summary>
public class StoryStep
{
	/// <summary>
	/// What happens: "say", "narrate", "write" (on the board), "wait", "camera", "shake",
	/// "music", "sound", "board", "include", "cast", "fight", "end" - or one of an actor's verbs
	/// (StoryScript.ActorVerbs).
	/// </summary>
	public string Verb = "";

	/// <summary>Who does it or says it: a fighter's story id, or "player"; "" for the scene itself.</summary>
	public string Who = "";

	/// <summary>How a line is said: "" (plain), shout, whisper, scared, happy, hot, slam.</summary>
	public string Mood = "";

	/// <summary>The words, for a line.</summary>
	public string Text = "";

	public string[] Args = System.Array.Empty<string>();

	/// <summary>The line ended in "&amp;": the next line starts straight away, without waiting for this one.</summary>
	public bool Together;

	/// <summary>Where it came from, for error messages: "fighters/doombot/story.txt:12".</summary>
	public string Source = "";

	public string Arg(int i) => i < Args.Length ? Args[i] : "";

	public float Number(int i, float otherwise)
	{
		string s = Arg(i);
		return s != "" && s.IsValidFloat() ? s.ToFloat() : otherwise;
	}
}

/// <summary>A named scene in a story.txt: everything under one "== name" heading.</summary>
public class StoryScene
{
	public string Name = "";
	public string Owner = "";
	public readonly List<StoryStep> Steps = new List<StoryStep>();
}

/// <summary>
/// Reads every fighter's story.txt - their opening, their challenger scenes, their DoomBot roast,
/// their ending - and checks them. A script is plain text so Eric can change a line himself; it
/// is checked at startup (RegressionChecks), so a typo in a fighter's name or a sound fails
/// loudly instead of silently skipping a line in the middle of a story.
/// </summary>
public static class StoryScript
{
	/// <summary>Everything an actor can be told to do. Each is played through the game as a controller would.</summary>
	public static readonly string[] ActorVerbs =
	{
		"walk", "run", "face", "turn", "look", "jump", "attack", "smash", "special", "taunt",
		"block", "crouch", "drop", "at", "heat", "stocks", "twirl", "roll", "hold",
	};

	public static readonly string[] Moods = { "shout", "whisper", "scared", "happy", "hot", "slam" };

	static readonly string[] SceneWords =
	{
		"wait", "camera", "shake", "music", "sound", "board", "include", "cast", "fight", "end",
	};

	static Dictionary<string, Dictionary<string, StoryScene>> scripts;

	/// <summary>A fighter's story id: their name, lower case, letters only - "flambe", "edgelord".</summary>
	public static string IdOf(FighterData data)
	{
		var id = new System.Text.StringBuilder();
		foreach (char c in data.DisplayName.ToLowerInvariant())
		{
			if (c == 'é' || c == 'è' || c == 'ê') id.Append('e');
			else if (c >= 'a' && c <= 'z') id.Append(c);
		}
		return id.ToString();
	}

	/// <summary>The roster index of a story id, or -1.</summary>
	public static int IndexOf(string id)
	{
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			if (IdOf(FighterCatalog.Get(i)) == id) return i;
		}
		return -1;
	}

	/// <summary>A fighter's story.txt sits beside their rig.json.</summary>
	static string PathOf(FighterData data) => data.RigPath.GetBaseDir().PathJoin("story.txt");

	/// <summary>One fighter's scene by name, or null if they have no such scene.</summary>
	public static StoryScene Scene(string fighterId, string name)
	{
		Load();
		return scripts.TryGetValue(fighterId, out var scenes) && scenes.TryGetValue(name, out StoryScene scene) ? scene : null;
	}

	public static bool Has(string fighterId, string name) => Scene(fighterId, name) != null;

	static void Load()
	{
		if (scripts != null) return;
		scripts = new Dictionary<string, Dictionary<string, StoryScene>>();
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			FighterData data = FighterCatalog.Get(i);
			string id = IdOf(data);
			string path = PathOf(data);
			scripts[id] = FileAccess.FileExists(path)
				? Parse(FileAccess.GetFileAsString(path), id, path.Replace("res://", ""))
				: new Dictionary<string, StoryScene>();
		}
	}

	/// <summary>Reads a story.txt into its scenes. Never throws: a line it cannot read is kept, and Check reports it.</summary>
	public static Dictionary<string, StoryScene> Parse(string text, string owner, string file)
	{
		var scenes = new Dictionary<string, StoryScene>();
		StoryScene scene = null;
		string[] lines = text.Replace("\r", "").Split('\n');
		for (int n = 0; n < lines.Length; n++)
		{
			string line = lines[n].Trim();
			if (line == "" || line.StartsWith("#")) continue;

			if (line.StartsWith("=="))
			{
				scene = new StoryScene { Name = line.Substring(2).Trim().ToLowerInvariant(), Owner = owner };
				scenes[scene.Name] = scene;
				continue;
			}

			var step = new StoryStep { Source = $"{file}:{n + 1}" };
			if (line.EndsWith(" &"))
			{
				step.Together = true;
				line = line.Substring(0, line.Length - 2).TrimEnd();
			}

			int colon = line.IndexOf(':');
			string head = colon > 0 ? line.Substring(0, colon).Trim() : "";
			if (colon > 0 && (!head.Contains(' ') || head.Contains('(') || head.EndsWith(" writes")))
			{
				// Something said: "doombot: HELLO." / "lugnut (shout): Break's over!" / "doombot writes: 1. REMOVE DRAWINGS."
				step.Text = line.Substring(colon + 1).Trim();
				step.Verb = "say";
				if (head.EndsWith(" writes"))
				{
					step.Verb = "write";
					head = head.Substring(0, head.Length - 7).Trim();
				}
				int open = head.IndexOf('(');
				if (open > 0)
				{
					step.Mood = head.Substring(open + 1).TrimEnd(')').Trim().ToLowerInvariant();
					head = head.Substring(0, open).Trim();
				}
				step.Who = head.ToLowerInvariant();
				if (step.Who == "narrator")
				{
					step.Verb = "narrate";
					step.Who = "";
				}
			}
			else
			{
				string[] words = line.ToLowerInvariant().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
				if (System.Array.IndexOf(SceneWords, words[0]) >= 0)
				{
					step.Verb = words[0];
					step.Args = words[1..];
				}
				else
				{
					step.Who = words[0];
					step.Verb = words.Length > 1 ? VerbOf(words[1]) : "";
					step.Args = words.Length > 2 ? words[2..] : System.Array.Empty<string>();
				}
			}

			if (scene == null)
			{
				scene = new StoryScene { Name = "", Owner = owner };
				scenes[""] = scene;
			}
			scene.Steps.Add(step);
		}
		return scenes;
	}

	/// <summary>"walks" is "walk", "crouches" is "crouch": a script reads as a sentence either way.</summary>
	static string VerbOf(string word)
	{
		if (System.Array.IndexOf(ActorVerbs, word) >= 0) return word;
		if (word.EndsWith("es") && System.Array.IndexOf(ActorVerbs, word[..^2]) >= 0) return word[..^2];
		if (word.EndsWith("s") && System.Array.IndexOf(ActorVerbs, word[..^1]) >= 0) return word[..^1];
		return word;
	}

	static bool IsActor(string who) => who == "player" || IndexOf(who) >= 0;

	static bool IsPlace(string word) => word.IsValidFloat() || IsActor(word) || word == "left" || word == "right";

	static readonly string[] Faces = { "normal", "closed", "squint", "angry", "shout", "hurt", "shocked", "happy", "sad", "smug" };

	/// <summary>
	/// Every problem in every story.txt: an unknown fighter, verb, face, mood, item or sound, a
	/// scene that includes one that does not exist. Run at startup by RegressionChecks.
	/// </summary>
	public static List<string> Check()
	{
		Load();
		var problems = new List<string>();
		foreach (var (owner, scenes) in scripts)
		{
			foreach (StoryScene scene in scenes.Values)
			{
				if (scene.Name == "" && scene.Steps.Count > 0) problems.Add($"{scene.Steps[0].Source}: a line before the first == scene heading");
				foreach (StoryStep step in scene.Steps) CheckStep(step, owner, problems);
			}
		}
		return problems;
	}

	static void CheckStep(StoryStep step, string owner, List<string> problems)
	{
		void Bad(string why) => problems.Add($"{step.Source}: {why}");

		switch (step.Verb)
		{
			case "narrate":
				return;
			case "say":
			case "write":
				if (!IsActor(step.Who)) Bad($"nobody called \"{step.Who}\"");
				if (step.Mood != "" && System.Array.IndexOf(Moods, step.Mood) < 0) Bad($"no mood \"{step.Mood}\" (try {string.Join(", ", Moods)})");
				foreach (string picture in StoryPictures.In(step.Text))
				{
					if (!StoryPictures.Exists(picture)) Bad($"no picture [{picture}]");
				}
				return;
			case "wait":
				if (!step.Arg(0).IsValidInt()) Bad("wait needs a number of frames");
				return;
			case "shake":
				if (!step.Arg(0).IsValidFloat()) Bad("shake needs a number");
				return;
			case "music":
				if (step.Arg(0) is not ("stop" or "battle" or "menu")) Bad("music is stop, battle or menu");
				return;
			case "sound":
				if (System.Array.IndexOf(SfxCatalog.AllNames, step.Arg(0)) < 0) Bad($"no sound \"{step.Arg(0)}\"");
				return;
			case "board":
				if (!(step.Arg(0) == "clear" || step.Arg(0) == "at" && step.Arg(1).IsValidFloat() && step.Arg(2).IsValidFloat()))
				{
					Bad("board is \"board at X Y\" or \"board clear\"");
				}
				return;
			case "camera":
				CheckCamera(step, Bad);
				return;
			case "cast":
				if (!IsActor(step.Arg(0))) Bad($"nobody called \"{step.Arg(0)}\"");
				else if (!(step.Arg(1) == "offstage" || step.Arg(1) == "at" && step.Arg(2).IsValidFloat())) Bad("cast is \"cast WHO at X\" or \"cast WHO offstage\"");
				return;
			case "include":
				CheckInclude(step, owner, Bad);
				return;
			case "fight":
			case "end":
				return;
		}

		if (!IsActor(step.Who))
		{
			Bad($"don't know \"{step.Who}\" - not a fighter, and not something a scene does");
			return;
		}

		switch (step.Verb)
		{
			case "walk":
			case "run":
				if (step.Arg(0) != "to" || !IsPlace(step.Arg(1))) Bad($"{step.Verb} is \"{step.Verb} to X\" or \"{step.Verb} to WHO\"");
				break;
			case "face":
				if (!IsPlace(step.Arg(0))) Bad("face is \"face left\", \"face right\" or \"face WHO\"");
				break;
			case "look":
				if (System.Array.IndexOf(Faces, step.Arg(0)) < 0) Bad($"no face \"{step.Arg(0)}\" (try {string.Join(", ", Faces)})");
				break;
			case "attack":
			case "smash":
			case "special":
				if (step.Arg(0) is not ("" or "forward" or "up" or "down")) Bad($"{step.Verb} goes forward, up or down");
				break;
			case "block":
			case "crouch":
				if (step.Arg(0) != "" && !step.Arg(0).IsValidInt()) Bad($"{step.Verb} takes a number of frames");
				break;
			case "drop":
				if (step.Args.Length > 0 && !(step.Arg(0) == "at" && IsPlace(step.Arg(1)))) Bad("drop is \"drop\" or \"drop at X\"");
				break;
			case "at":
				if (!IsPlace(step.Arg(0))) Bad("at is \"at X\"");
				break;
			case "heat":
			case "stocks":
				if (!step.Arg(0).IsValidFloat()) Bad($"{step.Verb} needs a number");
				break;
			case "hold":
				if (ItemCatalog.IndexOf(step.Arg(0)) < 0) Bad($"no item \"{step.Arg(0)}\"");
				break;
			case "turn":
			case "jump":
			case "taunt":
			case "twirl":
			case "roll":
				break;
			default:
				Bad($"{step.Who} can't \"{step.Verb}\" (try {string.Join(", ", ActorVerbs)})");
				break;
		}
	}

	static void CheckCamera(StoryStep step, System.Action<string> bad)
	{
		string[] a = step.Args;
		int i = 0;
		switch (step.Arg(0))
		{
			case "wide": i = 1; break;
			case "on":
				if (!IsActor(step.Arg(1))) bad($"camera on: nobody called \"{step.Arg(1)}\"");
				i = 2;
				break;
			case "two":
				if (!IsActor(step.Arg(1)) || !IsActor(step.Arg(2))) bad("camera two is \"camera two WHO WHO\"");
				i = 3;
				break;
			case "at":
				if (!step.Arg(1).IsValidFloat() || !step.Arg(2).IsValidFloat()) bad("camera at is \"camera at X Y\"");
				i = 3;
				break;
			default:
				bad("camera is wide, on WHO, two WHO WHO, or at X Y");
				return;
		}
		for (; i < a.Length; i++)
		{
			if (a[i] == "zoom" && i + 1 < a.Length && a[i + 1].IsValidFloat()) i++;
			else if (a[i] is not ("slow" or "fast" or "cut" or "whip")) bad($"camera: don't know \"{a[i]}\" (zoom N, slow, fast, cut, whip)");
		}
	}

	static void CheckInclude(StoryStep step, string owner, System.Action<string> bad)
	{
		string who = step.Arg(0), name = step.Arg(1);
		if (name == "") { bad("include is \"include WHO SCENE\""); return; }
		if (who == "player")
		{
			// Played in everyone else's story: every other fighter needs one.
			for (int i = 0; i < FighterCatalog.Count; i++)
			{
				string id = IdOf(FighterCatalog.Get(i));
				if (id != owner && !Has(id, name)) bad($"{id} has no \"== {name}\" for this to include");
			}
		}
		else if (!IsActor(who)) bad($"include: nobody called \"{who}\"");
		else if (!Has(who, name)) bad($"{who} has no \"== {name}\"");
	}
}
