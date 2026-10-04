using System.Collections.Generic;
using Godot;

/// <summary>
/// The one place sound effects are played from. A child of <see cref="GameRoot"/>, so it
/// outlives every screen; call sites use the static helpers and never touch a player node.
///
/// Mirrors WizardSurvivors' SfxPlayer, cut down to what a two-player fighter needs:
///
///   * Nothing is allocated or loaded while the game runs. Every stream loads once in _Ready
///     and plays through a fixed pool of voices.
///   * Every file is already normalised to its place in the mix by tools/audio/build.py, so
///     everything plays at 0 dB. Balance is changed in tools/audio/sfx.py, not at call sites.
///   * Audio is not gameplay state. A missing file or a missing player is silence, never an
///     exception - but it is said once, because silent silence is the bug nobody files.
/// </summary>
public partial class SfxPlayer : Node
{
	public const string SfxBus = "SFX";
	public const string MusicBus = "Music";

	public static SfxPlayer Instance { get; private set; }

	// Enough for a busy moment - two fighters, their projectiles, a KO - with room to spare.
	const int PositionalVoices = 16;
	const int GlobalVoices = 6;

	// The same sound twice inside this window is one sound. A multi-hit or a volley landing on
	// several frames at once would otherwise stack into a single loud blat.
	const ulong RepeatGapMs = 35;

	// Fighting sounds pan with the action, but only partway: on a TV both players are sitting
	// in front of the same speakers, and a hit should not vanish into one of them.
	const float PanningStrength = 0.5f;

	readonly Dictionary<string, AudioStream> streams = new Dictionary<string, AudioStream>();
	readonly Dictionary<string, ulong> lastPlayed = new Dictionary<string, ulong>();
	readonly RandomNumberGenerator rng = new RandomNumberGenerator();
	AudioStreamPlayer2D[] positional;
	AudioStreamPlayer[] global;
	int nextPositional;
	int nextGlobal;
	static bool warnedMissing;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;   // the pause menu still clicks
		rng.Randomize();
		EnsureBuses();

		positional = new AudioStreamPlayer2D[PositionalVoices];
		for (int i = 0; i < positional.Length; i++)
		{
			positional[i] = new AudioStreamPlayer2D
			{
				Bus = SfxBus,
				MaxDistance = 100000.0f,
				PanningStrength = PanningStrength,
			};
			AddChild(positional[i]);
		}
		global = new AudioStreamPlayer[GlobalVoices];
		for (int i = 0; i < global.Length; i++)
		{
			global[i] = new AudioStreamPlayer { Bus = SfxBus };
			AddChild(global[i]);
		}

		int missing = 0;
		foreach (string name in SfxCatalog.AllNames)
		{
			AudioStream stream = Load(SfxCatalog.PathFor(name), SfxCatalog.Loops(name));
			if (stream == null) missing++;
			else streams[name] = stream;
		}
		GD.Print($"SfxPlayer: {streams.Count} sounds loaded" + (missing > 0 ? $", {missing} MISSING" : ""));
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;

		// Let go of every sound now, while the engine is still running. Held until the process
		// ends, they are cleaned up after Godot's C# side has already shut down - which crashed
		// the game on the way out.
		foreach (AudioStream stream in streams.Values) stream.Dispose();
		streams.Clear();
	}

	/// <summary>
	/// Loads a WAV and makes sure a loop loops. The generators mark loops inside the file and
	/// Godot's importer reads that, but if an import setting ever overrides it a music track
	/// would play once and stop - so the loop is set here too rather than trusted.
	/// </summary>
	public static AudioStream Load(string path, bool loops)
	{
		if (!ResourceLoader.Exists(path))
		{
			GD.PushWarning($"SfxPlayer: missing {path}");
			return null;
		}
		var stream = ResourceLoader.Load<AudioStream>(path);
		if (loops && stream is AudioStreamWav wav && wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled)
		{
			wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
			wav.LoopBegin = 0;
			wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
		}
		return stream;
	}

	/// <summary>
	/// The bus layout is built in code, like everything else in this project: Music and SFX
	/// under Master, so each can be turned down on its own, and a limiter on SFX so a KO landing
	/// on top of a hit never clips.
	/// </summary>
	static void EnsureBuses()
	{
		foreach (string name in new[] { MusicBus, SfxBus })
		{
			if (AudioServer.GetBusIndex(name) >= 0) continue;
			AudioServer.AddBus();
			int index = AudioServer.BusCount - 1;
			AudioServer.SetBusName(index, name);
			AudioServer.SetBusSend(index, "Master");
			if (name == SfxBus)
			{
				AudioServer.AddBusEffect(index, new AudioEffectHardLimiter { CeilingDb = -1.0f });
			}
		}
	}

	public AudioStream Stream(string name) => streams.TryGetValue(name, out AudioStream s) ? s : null;

	bool CanPlay(string name, out AudioStream stream)
	{
		stream = null;
		if (string.IsNullOrEmpty(name) || !streams.TryGetValue(name, out stream)) return false;
		ulong now = Time.GetTicksMsec();
		if (lastPlayed.TryGetValue(name, out ulong last) && now - last < RepeatGapMs) return false;
		lastPlayed[name] = now;
		return true;
	}

	float Jitter(float amount) => amount <= 0.0f ? 1.0f : 1.0f + rng.RandfRange(-amount, amount);

	void PlayAtPosition(string name, Vector2 position, float pitchJitter, float pitch = 1.0f)
	{
		if (!CanPlay(name, out AudioStream stream)) return;
		// Round-robin, preferring a free voice. With throttling a steal is rare, and when it
		// happens the oldest sound is the one nearest its end.
		AudioStreamPlayer2D voice = positional[nextPositional];
		for (int i = 0; i < positional.Length; i++)
		{
			var candidate = positional[(nextPositional + i) % positional.Length];
			if (!candidate.Playing)
			{
				voice = candidate;
				nextPositional = (nextPositional + i) % positional.Length;
				break;
			}
		}
		nextPositional = (nextPositional + 1) % positional.Length;
		voice.Stream = stream;
		voice.GlobalPosition = position;
		voice.PitchScale = pitch * Jitter(pitchJitter);
		voice.Play();
	}

	void PlayFlat(string name, float pitchJitter)
	{
		if (!CanPlay(name, out AudioStream stream)) return;
		AudioStreamPlayer voice = global[nextGlobal];
		for (int i = 0; i < global.Length; i++)
		{
			var candidate = global[(nextGlobal + i) % global.Length];
			if (!candidate.Playing)
			{
				voice = candidate;
				nextGlobal = (nextGlobal + i) % global.Length;
				break;
			}
		}
		nextGlobal = (nextGlobal + 1) % global.Length;
		voice.Stream = stream;
		voice.PitchScale = Jitter(pitchJitter);
		voice.Play();
	}

	static SfxPlayer Get()
	{
		if (Instance == null && !warnedMissing)
		{
			warnedMissing = true;
			GD.PushWarning("SfxPlayer: not in the tree, sound effects are off");
		}
		return Instance;
	}

	// --- Call-site helpers -----------------------------------------------------

	/// <summary>A sound from something in the fight. Pitch wanders a little so repeats do not
	/// sound like a machine gun - variation at play time, not on disk.</summary>
	public static void At(string name, Vector2 position, float pitchJitter = 0.05f) =>
		Get()?.PlayAtPosition(name, position, pitchJitter);

	/// <summary>A sound that belongs to the screen rather than a place: menus, the countdown.</summary>
	public static void Ui(string name) => Get()?.PlayFlat(name, 0.0f);

	public static void Hit(Vector2 position, float knockback, bool blocked) =>
		Get()?.PlayAtPosition(SfxCatalog.ForHit(knockback, blocked), position, 0.06f);
}
