using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// Playing online (Eric, 2026-10-29; .ai/online.md): up to four copies of the game, each with
/// whoever is on its couch, playing as one through a room code on ecec.dev.
///
/// Every copy runs the whole session itself - menus, fights, stories - in lockstep. Each frame,
/// each copy reads its own controllers and sends what they did to the others through the relay
/// (play/server.js in the website repo); a frame is only played once every copy's controllers for
/// it are in. Every controller in the session then looks like a controller plugged into every
/// copy (NetInputSource, ControllerAssignment), and since everything in the game is decided once
/// a frame from its controllers, every copy plays the same game. What a copy reads is sent a few
/// frames ahead of when it is played (Delay), so a press usually arrives before its frame does
/// and nobody waits.
///
/// Only controllers cross the network - never a fighter, a position or a drawing. In return,
/// everything that decides what happens must be the same on every copy: randomness comes from the
/// session's seed (GameRandom), the saved game is the host's (SaveFiles), and nothing reads the
/// keyboard or mouse except through a controller. A check every two seconds (Hash) says if two
/// copies have gone out of step anyway, and ends the session rather than playing on apart.
/// </summary>
public partial class NetSession : Node
{
	public static NetSession Instance { get; private set; }

	/// <summary>Playing: the session has started, and every copy is in lockstep.</summary>
	public static bool Playing => Instance != null && Instance.phase == Phase.Playing;

	/// <summary>Connecting, in a lobby or playing.</summary>
	public static bool Active => Instance != null;

	/// <summary>The relay; DRAWFIGHT_PLAY_URL points it at a local copy for testing (.ai/online.md).</summary>
	static string Url
	{
		get
		{
			string env = OS.GetEnvironment("DRAWFIGHT_PLAY_URL");
			return string.IsNullOrEmpty(env) ? "wss://ecec.dev/api/play/ws" : env;
		}
	}

	enum Phase { Connecting, Lobby, Playing }
	Phase phase = Phase.Connecting;

	// What travels between copies, after the byte the relay puts in front saying who sent it.
	const byte Input = 1, Start = 2, Names = 3, Ping = 4, Pong = 5, Hash = 6, Bye = 7;

	WebSocketPeer socket;
	bool hosting;
	string joinCode;
	bool sentHello;

	/// <summary>The room's code, once the relay has given one.</summary>
	public string Code { get; private set; } = "";

	/// <summary>This copy's number in the room: 0 is the host.</summary>
	public int Me { get; private set; } = -1;
	public bool IsHost => Me == 0;

	/// <summary>Every copy in the room, by number - in the lobby, everyone there; playing, everyone who started.</summary>
	public readonly List<int> Members = new List<int>();

	/// <summary>Said on the online screen while connecting and in the lobby.</summary>
	public string Status { get; private set; } = "Connecting...";

	/// <summary>The frame being played, counted from the start of the session.</summary>
	public int Frame { get; private set; } = -1;

	/// <summary>How many frames ahead a copy's controllers are sent.</summary>
	public int Delay { get; private set; } = 4;

	/// <summary>Each copy's controllers, by frame, as they come in.</summary>
	readonly Dictionary<int, Dictionary<int, NetFrame>> frames = new Dictionary<int, Dictionary<int, NetFrame>>();

	/// <summary>Each copy's controllers for the frame being played.</summary>
	readonly Dictionary<int, NetFrame> current = new Dictionary<int, NetFrame>();

	/// <summary>The controllers in the session last frame, to notice one plugged in or out.</summary>
	string lastDevices = "";

	/// <summary>Names typed on the guests' computers, for the host to share with everyone.</summary>
	readonly Dictionary<int, string[]> guestNames = new Dictionary<int, string[]>();

	/// <summary>The host's measure of each guest's round trip, in milliseconds.</summary>
	readonly Dictionary<int, int> roundTrips = new Dictionary<int, int>();
	int pingFrames;

	/// <summary>Each copy's out-of-step check by frame, until every copy's is in.</summary>
	readonly Dictionary<int, Dictionary<int, uint>> hashes = new Dictionary<int, Dictionary<int, uint>>();
	const int HashEvery = 120;

	// This copy's own controllers, read once a frame and sent.
	KeyboardInputSource keyboard;
	readonly Dictionary<int, GamepadInputSource> pads = new Dictionary<int, GamepadInputSource>();
	readonly List<int> padOrder = new List<int>();

	int waitedFrames;
	Label waitLabel;

	/// <summary>What this copy had before the session, put back after it.</summary>
	VersusRule ownRule;
	int ownStocks, ownMinutes, ownHealth, ownStoryDifficulty;
	ItemFrequency ownFrequency;
	bool[] ownEnabled;

	/// <summary>How many computers the host waits for before starting by itself (--onlinestart=N): a test.</summary>
	static int StartWhen
	{
		get
		{
			foreach (string arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--onlinestart=")) return arg.Substring(14).ToInt();
			return int.MaxValue;
		}
	}

	/// <summary>Host a room: the code comes back from the relay and shows on the online screen.</summary>
	public static NetSession Host() => Open(true, "");

	/// <summary>Join a room by its code.</summary>
	public static NetSession Join(string code) => Open(false, code);

	static NetSession Open(bool host, string code)
	{
		Instance?.Close(null);
		var session = new NetSession { hosting = host, joinCode = code.ToUpper() };
		Instance = session;
		GameRoot.Instance.AddChild(session);
		return session;
	}

	public override void _Ready()
	{
		// Before everything else in a frame: the controllers it plays with are set here first.
		ProcessPhysicsPriority = -100000;
		ProcessMode = ProcessModeEnum.Always;
		socket = new WebSocketPeer();
		socket.InboundBufferSize = 1 << 20;
		socket.OutboundBufferSize = 1 << 20;
		Error error = socket.ConnectToUrl(Url);
		if (error != Error.Ok) Close("Could not reach ecec.dev.");

		var layer = new CanvasLayer { Layer = 30 };
		AddChild(layer);
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		layer.AddChild(root);
		waitLabel = MatchHud.MakeLabel(root, 40, MatchHud.Ink, 12);
		waitLabel.Position = new Vector2(0.0f, 1000.0f);
		waitLabel.Size = new Vector2(1920.0f, 60.0f);
		waitLabel.Text = "Waiting for the others...";
		waitLabel.Visible = false;
	}

	// --- The network -------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (socket == null) return;
		socket.Poll();
		WebSocketPeer.State state = socket.GetReadyState();
		if (state == WebSocketPeer.State.Open && !sentHello)
		{
			sentHello = true;
			string version = UpdateScreen.CurrentVersion;
			socket.SendText(hosting
				? $"{{\"op\":\"host\",\"game\":\"drawfight\",\"version\":\"{version}\"}}"
				: $"{{\"op\":\"join\",\"code\":\"{joinCode}\",\"game\":\"drawfight\",\"version\":\"{version}\"}}");
		}
		while (socket != null && socket.GetAvailablePacketCount() > 0)
		{
			byte[] packet = socket.GetPacket();
			if (socket.WasStringPacket()) OnText(Encoding.UTF8.GetString(packet));
			else OnBinary(packet);
		}
		if (socket == null) return;
		if (state == WebSocketPeer.State.Closed)
		{
			Close(phase == Phase.Playing ? "Lost the connection." : "Could not reach ecec.dev.");
			return;
		}

		if (phase == Phase.Lobby && IsHost && ++pingFrames % 30 == 0) SendPing();
		if (phase == Phase.Playing && waitedFrames > 60 * 20) Close("Lost the connection.");
		// "--onlinestart=2": the host starts once that many computers are in (a test, .ai/online.md),
		// after a second of pings so the delay is measured.
		if (phase == Phase.Lobby && IsHost && pingFrames > 90 && Members.Count >= StartWhen) StartGame();
	}

	void OnText(string text)
	{
		var message = Json.ParseString(text).AsGodotDictionary();
		string op = message.TryGetValue("op", out Variant o) ? o.AsString() : "";
		switch (op)
		{
			case "room":
				Code = message["code"].AsString();
				Me = 0;
				Members.Clear();
				Members.Add(0);
				phase = Phase.Lobby;
				Status = "Tell your friends the code.";
				GD.Print($"NetSession: room {Code}");
				break;
			case "joined":
				Me = message["you"].AsInt32();
				Members.Clear();
				foreach (Variant id in message["members"].AsGodotArray()) Members.Add(id.AsInt32());
				Code = joinCode;
				phase = Phase.Lobby;
				Status = "Waiting for the host to start.";
				// The host shares everyone's saved names, so whoever is here can pick theirs.
				SendBytes(Names, Encoding.UTF8.GetBytes(string.Join("\n", PlayerNames.All)));
				break;
			case "member":
				int joined = message["id"].AsInt32();
				if (!Members.Contains(joined)) Members.Add(joined);
				Members.Sort();
				break;
			case "left":
				int gone = message["id"].AsInt32();
				if (phase == Phase.Playing) Close("Someone left, so the game ended.");
				else Members.Remove(gone);
				break;
			case "closed":
				Close(phase == Phase.Playing ? "The host left, so the game ended." : "The host left.");
				break;
			case "error":
				string why = message.TryGetValue("why", out Variant w) ? w.AsString() : "";
				Close(why switch
				{
					"no-room" => "There is no game with that code.",
					"version" => $"That game is on version {message["version"].AsString()} and this is {UpdateScreen.CurrentVersion}. Both need the newest version.",
					"started" => "That game has already started.",
					"full" => "That game is full.",
					"slow-down" => "Too many tries. Wait a minute and try again.",
					_ => "ecec.dev is busy. Try again in a minute.",
				});
				break;
		}
	}

	void OnBinary(byte[] packet)
	{
		if (packet.Length < 2) return;
		int from = packet[0];
		byte kind = packet[1];
		switch (kind)
		{
			case Input:
				if (packet.Length < 6) return;
				int frame = System.BitConverter.ToInt32(packet, 2);
				if (!frames.TryGetValue(from, out var theirs)) frames[from] = theirs = new Dictionary<int, NetFrame>();
				theirs[frame] = NetFrame.Read(packet, 6);
				break;
			case Start:
				if (!IsHost && phase == Phase.Lobby) Begin(Encoding.UTF8.GetString(packet, 2, packet.Length - 2));
				break;
			case Names:
				guestNames[from] = Encoding.UTF8.GetString(packet, 2, packet.Length - 2).Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
				break;
			case Ping:
				var pong = new byte[5];
				System.Array.Copy(packet, 2, pong, 1, 4);
				pong[0] = Pong;
				Send(pong);
				break;
			case Pong:
				int sent = System.BitConverter.ToInt32(packet, 2);
				int trip = (int)(Time.GetTicksMsec() & 0x7fffffff) - sent;
				// The worst of the last few: a lockstep game waits on its slowest moment, not its average.
				roundTrips[from] = Mathf.Max(trip, roundTrips.GetValueOrDefault(from) * 3 / 4);
				break;
			case Hash:
				int at = System.BitConverter.ToInt32(packet, 2);
				uint hash = System.BitConverter.ToUInt32(packet, 6);
				CheckHash(from, at, hash);
				break;
			case Bye:
				Close("Someone left, so the game ended.");
				break;
		}
	}

	void SendPing()
	{
		var ping = new byte[5];
		ping[0] = Ping;
		System.BitConverter.GetBytes((int)(Time.GetTicksMsec() & 0x7fffffff)).CopyTo(ping, 1);
		Send(ping);
	}

	void SendBytes(byte kind, byte[] body)
	{
		var message = new byte[body.Length + 1];
		message[0] = kind;
		body.CopyTo(message, 1);
		Send(message);
	}

	void Send(byte[] message)
	{
		if (socket != null && socket.GetReadyState() == WebSocketPeer.State.Open) socket.Send(message);
	}

	// --- Starting ---------------------------------------------------------------------

	/// <summary>
	/// The host starts: how far ahead to send, the seed, the rules, everyone's names and the host's
	/// saved game go to every guest, and the room closes to anyone else.
	/// </summary>
	public void StartGame()
	{
		if (!IsHost || phase != Phase.Lobby || Members.Count < 2) return;
		int worst = 0;
		foreach (int trip in roundTrips.Values) worst = Mathf.Max(worst, trip);
		// Half a round trip, in frames, and two to spare - so a press is usually in before its frame.
		int delay = Mathf.Clamp(Mathf.CeilToInt(worst * 0.5f / (1000.0f / 60.0f)) + 2, 3, 15);

		var names = new List<string>(PlayerNames.All);
		foreach (string[] theirs in guestNames.Values) foreach (string name in theirs) if (!names.Contains(name)) names.Add(name);
		var files = new Godot.Collections.Dictionary();
		foreach (string path in SaveFiles.SessionFiles) files[path] = SaveFiles.Own(path);
		var enabled = new Godot.Collections.Array();
		foreach (bool on in GameRoot.Instance.Items.Enabled) enabled.Add(on);
		var members = new Godot.Collections.Array();
		foreach (int id in Members) members.Add(id);

		var start = new Godot.Collections.Dictionary
		{
			["seed"] = (int)(GD.Randi() & 0x7fffffff),
			["delay"] = delay,
			["members"] = members,
			["rule"] = (int)GameOptions.Rule,
			["stocks"] = GameOptions.Stocks,
			["minutes"] = GameOptions.Minutes,
			["health"] = GameOptions.StaminaHealth,
			["story"] = GameOptions.StoryDifficulty,
			["frequency"] = (int)GameRoot.Instance.Items.Frequency,
			["enabled"] = enabled,
			["names"] = new Godot.Collections.Array(names.ConvertAll(n => (Variant)n)),
			["files"] = files,
		};
		string text = Json.Stringify(start);
		socket.SendText("{\"op\":\"lock\"}");
		SendBytes(Start, Encoding.UTF8.GetBytes(text));
		Begin(text);
	}

	/// <summary>Every copy, the same moment of the session: the same seed, rules, names and saved game.</summary>
	void Begin(string text)
	{
		var start = Json.ParseString(text).AsGodotDictionary();
		Delay = start["delay"].AsInt32();
		Members.Clear();
		foreach (Variant id in start["members"].AsGodotArray()) Members.Add(id.AsInt32());
		Members.Sort();

		ownRule = GameOptions.Rule;
		ownStocks = GameOptions.Stocks;
		ownMinutes = GameOptions.Minutes;
		ownHealth = GameOptions.StaminaHealth;
		ownStoryDifficulty = GameOptions.StoryDifficulty;
		GameOptions.Rule = (VersusRule)start["rule"].AsInt32();
		GameOptions.Stocks = start["stocks"].AsInt32();
		GameOptions.Minutes = start["minutes"].AsInt32();
		GameOptions.StaminaHealth = start["health"].AsInt32();
		GameOptions.StoryDifficulty = start["story"].AsInt32();

		ItemSettings items = GameRoot.Instance.Items;
		ownFrequency = items.Frequency;
		ownEnabled = (bool[])items.Enabled.Clone();
		items.Frequency = (ItemFrequency)start["frequency"].AsInt32();
		var enabled = start["enabled"].AsGodotArray();
		for (int i = 0; i < items.Enabled.Length && i < enabled.Count; i++) items.Enabled[i] = enabled[i].AsBool();

		PlayerNames.All.Clear();
		foreach (Variant name in start["names"].AsGodotArray()) PlayerNames.All.Add(name.AsString());

		var files = new Dictionary<string, string>();
		foreach (var pair in start["files"].AsGodotDictionary()) files[pair.Key.AsString()] = pair.Value.AsString();
		SaveFiles.Share(files);
		GameRandom.Seed(start["seed"].AsInt32());
		GameRoot.Instance.ResetChoices();

		// The first frames, before anyone's controllers can have arrived, are nobody pressing anything.
		frames.Clear();
		foreach (int id in Members)
		{
			frames[id] = new Dictionary<int, NetFrame>();
			for (int f = 0; f < Delay; f++) frames[id][f] = new NetFrame();
		}
		keyboard = KeyboardInputSource.Player1();
		Frame = -1;
		lastDevices = "";
		phase = Phase.Playing;
		Status = "Starting...";
	}

	// --- Lockstep ----------------------------------------------------------------------

	/// <summary>Holding the game still this frame, waiting for another copy's controllers.</summary>
	public static bool Waiting => Instance != null && Instance.waiting;
	bool waiting;
	bool pausedBefore;

	bool Ready(int frame)
	{
		foreach (int id in Members) if (!frames.TryGetValue(id, out var theirs) || !theirs.ContainsKey(frame)) return false;
		return true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (phase != Phase.Playing) return;
		socket?.Poll();
		while (socket != null && socket.GetAvailablePacketCount() > 0)
		{
			byte[] packet = socket.GetPacket();
			if (socket.WasStringPacket()) OnText(Encoding.UTF8.GetString(packet));
			else OnBinary(packet);
		}
		if (phase != Phase.Playing) return;

		// Not every copy's controllers for the next frame are in yet: the whole game is paused for
		// this frame - this runs first, so nothing else plays it - and goes on once they arrive.
		// (Setting the game's speed to nothing does not do it: frames still come, just with no time in them.)
		if (!Ready(Frame + 1))
		{
			if (!waiting)
			{
				waiting = true;
				pausedBefore = GetTree().Paused;
				GetTree().Paused = true;
			}
			// A moment's wait is the network; a long one is said, so nobody thinks the game froze.
			waitedFrames++;
			waitLabel.Visible = waitedFrames > 45;
			if (waitedFrames > 60 * 20) Close("Lost the connection.");
			return;
		}
		if (waiting)
		{
			waiting = false;
			GetTree().Paused = pausedBefore;
		}
		waitedFrames = 0;
		waitLabel.Visible = false;

		Frame++;
		foreach (int id in Members)
		{
			NetFrame theirs = frames[id].GetValueOrDefault(Frame) ?? new NetFrame();
			current[id] = theirs;
			frames[id].Remove(Frame - 1);
		}

		// A controller plugged in or out anywhere: every copy hears it on the same frame.
		string devices = string.Join(",", DeviceKeys());
		if (devices != lastDevices && Frame > Delay) GameRoot.Instance.SessionDevicesChanged();
		lastDevices = devices;

		// The session's first frames with everyone's controllers in: off to pick what to play.
		if (Frame == Delay) GameRoot.Instance.GoOnlineHome();

		SendOwn(Frame + Delay);
		if (Frame > Delay && Frame % HashEvery == 0) SendHash();
		if (StartWhen < int.MaxValue) Trace();
	}

	System.IO.StreamWriter trace;

	/// <summary>
	/// Testing (--onlinestart): every frame's controllers and fighters, written to
	/// .shots/net_N.log for each copy - two copies' logs, side by side, show the first frame they
	/// disagree on, and what about.
	/// </summary>
	void Trace()
	{
		trace ??= new System.IO.StreamWriter(ProjectSettings.GlobalizePath($"res://.shots/net_{Me}.log"));
		var line = new StringBuilder($"{Frame} {GameRoot.Instance.CurrentScreen?.GetType().Name}");
		foreach (int id in Members)
		{
			NetFrame f = current.GetValueOrDefault(id);
			if (f == null) continue;
			line.Append($" m{id}:{f.Present}");
			for (int slot = 0; slot < NetFrame.Slots; slot++) if (f.Has(slot)) line.Append($",{f.States[slot].Move.X:0.00}/{f.States[slot].Move.Y:0.00}");
		}
		if (GameRoot.Instance.CurrentScreen is MatchManager match)
		{
			foreach (Fighter fighter in match.Fighters)
			{
				line.Append($" | {fighter.Data.DisplayName} {fighter.GlobalPosition.X:0.000},{fighter.GlobalPosition.Y:0.000} v{fighter.Velocity.X:0.000},{fighter.Velocity.Y:0.000} {fighter.Percent:0.0}% {fighter.State} s{fighter.Stocks}");
			}
		}
		trace.WriteLine(line.ToString());
		trace.Flush();
	}

	/// <summary>This copy's controllers, read now, for the frame Delay frames on - sent, and kept for itself.</summary>
	void SendOwn(int frame)
	{
		var own = new NetFrame();
		own.Set(0, keyboard.Poll());
		Vector2 mouse = GetViewport().GetMousePosition();
		own.Pointer = mouse;
		own.Mouse = (byte)((Godot.Input.IsMouseButtonPressed(MouseButton.Left) ? 1 : 0) | (Godot.Input.IsMouseButtonPressed(MouseButton.Right) ? 2 : 0));

		var connected = new List<int>(Godot.Input.GetConnectedJoypads());
		connected.Sort();
		padOrder.Clear();
		for (int i = 0; i < connected.Count && i < NetFrame.Slots - 1; i++)
		{
			int pad = connected[i];
			padOrder.Add(pad);
			if (!pads.TryGetValue(pad, out GamepadInputSource source)) pads[pad] = source = new GamepadInputSource(pad, ControllerAssignment.Layout);
			own.Set(i + 1, source.Poll());
		}

		byte[] body = own.Write();
		// Kept as the others will read it - packed and unpacked - so every copy plays the same numbers.
		frames[Me][frame] = NetFrame.Read(body, 0);
		var message = new byte[body.Length + 5];
		message[0] = Input;
		System.BitConverter.GetBytes(frame).CopyTo(message, 1);
		body.CopyTo(message, 5);
		Send(message);
	}

	/// <summary>Every controller in the session, as ControllerAssignment names them: each copy's pads, then its keyboard.</summary>
	public List<string> DeviceKeys()
	{
		var keys = new List<string>();
		foreach (int id in Members)
		{
			if (!current.TryGetValue(id, out NetFrame theirs)) continue;
			for (int slot = 1; slot < NetFrame.Slots; slot++) if (theirs.Has(slot)) keys.Add($"net:{id}:{slot}");
			if (theirs.Has(0)) keys.Add($"net:{id}:0");
		}
		return keys;
	}

	public bool HasDevice(int machine, int slot) => current.TryGetValue(machine, out NetFrame theirs) && theirs.Has(slot);

	/// <summary>What a controller is doing this frame.</summary>
	public InputState StateOf(int machine, int slot) =>
		current.TryGetValue(machine, out NetFrame theirs) && theirs.Has(slot) ? theirs.States[slot] : InputState.None;

	/// <summary>A copy's mouse this frame: where it points, and whether its left button is down.</summary>
	public (Vector2 at, bool down) MouseOf(int machine) =>
		current.TryGetValue(machine, out NetFrame theirs) ? (theirs.Pointer, (theirs.Mouse & 1) != 0) : (Vector2.Zero, false);

	/// <summary>One of this copy's own pads, to rumble.</summary>
	public IHapticInputSource OwnPad(int slot) =>
		slot >= 1 && slot - 1 < padOrder.Count && pads.TryGetValue(padOrder[slot - 1], out GamepadInputSource pad) ? pad : null;

	// --- Staying in step ---------------------------------------------------------------

	/// <summary>
	/// What the game looks like now, boiled down to a number: which screen, and in a fight, where
	/// everyone is and how hurt. Two copies that disagree have gone out of step.
	/// </summary>
	uint StateHash()
	{
		uint hash = 2166136261;
		void Mix(long value)
		{
			for (int i = 0; i < 8; i++)
			{
				hash ^= (uint)(value & 0xff);
				hash *= 16777619;
				value >>= 8;
			}
		}
		Node screen = GameRoot.Instance.CurrentScreen;
		foreach (char c in screen?.GetType().Name ?? "") Mix(c);
		if (screen is MatchManager match)
		{
			foreach (Fighter f in match.Fighters)
			{
				Mix(Mathf.RoundToInt(f.GlobalPosition.X * 4.0f));
				Mix(Mathf.RoundToInt(f.GlobalPosition.Y * 4.0f));
				Mix(Mathf.RoundToInt(f.Percent * 10.0f));
				Mix(f.Stocks);
				Mix((int)f.State);
			}
		}
		return hash;
	}

	void SendHash()
	{
		uint hash = StateHash();
		var message = new byte[9];
		message[0] = Hash;
		System.BitConverter.GetBytes(Frame).CopyTo(message, 1);
		System.BitConverter.GetBytes(hash).CopyTo(message, 5);
		Send(message);
		CheckHash(Me, Frame, hash);
	}

	void CheckHash(int from, int frame, uint hash)
	{
		if (!hashes.TryGetValue(frame, out var at)) hashes[frame] = at = new Dictionary<int, uint>();
		at[from] = hash;
		if (at.Count < Members.Count) return;
		hashes.Remove(frame);
		if (StartWhen < int.MaxValue) GD.Print($"NetSession: frame {frame} checked across {at.Count} copies (delay {Delay})");
		foreach (uint other in at.Values)
		{
			if (other == at.GetValueOrDefault(Me)) continue;
			GD.PrintErr($"NetSession: out of step at frame {frame}");
			Close("The games got out of step, so this one ended. Sorry!");
			return;
		}
	}

	// --- Ending ------------------------------------------------------------------------

	/// <summary>Leaving on purpose: the others are told, and everyone goes back.</summary>
	public void Leave()
	{
		Send(new[] { Bye });
		Close(null);
	}

	/// <summary>
	/// The session over, for whatever reason: everything this copy had before is put back, the
	/// achievements its players earned are earned for real, and it goes to the online screen with
	/// <paramref name="message"/> (or the main menu, with none).
	/// </summary>
	public void Close(string message)
	{
		if (Instance != this) return;
		Instance = null;
		bool wasPlaying = phase == Phase.Playing;
		WebSocketPeer closing = socket;
		socket = null;
		closing?.Close();
		if (wasPlaying)
		{
			Engine.TimeScale = 1.0f;
			GetTree().Paused = false;
			GameOptions.Rule = ownRule;
			GameOptions.Stocks = ownStocks;
			GameOptions.Minutes = ownMinutes;
			GameOptions.StaminaHealth = ownHealth;
			GameOptions.StoryDifficulty = ownStoryDifficulty;
			GameRoot.Instance.Items.Frequency = ownFrequency;
			System.Array.Copy(ownEnabled, GameRoot.Instance.Items.Enabled, ownEnabled.Length);
			PlayerNames.Load();
			GameRandom.Unseed();
			SaveFiles.Unshare();
			GameRoot.Instance.ResetChoices();
		}
		QueueFree();
		if (message != null || wasPlaying) GameRoot.Instance.GoOnline(message);
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}
}

/// <summary>One copy's controllers for one frame: its keyboard and mouse (slot 0) and up to four pads.</summary>
public sealed class NetFrame
{
	public const int Slots = 5;
	public readonly InputState[] States = new InputState[Slots];

	/// <summary>Which slots have a controller in them, a bit each.</summary>
	public byte Present;

	/// <summary>The keyboard's mouse: where it points, and its buttons (1 left, 2 right).</summary>
	public Vector2 Pointer;
	public byte Mouse;

	public bool Has(int slot) => (Present & (1 << slot)) != 0;

	public void Set(int slot, InputState state)
	{
		States[slot] = state;
		Present |= (byte)(1 << slot);
	}

	public byte[] Write()
	{
		var bytes = new List<byte> { Present };
		for (int slot = 0; slot < Slots; slot++)
		{
			if (!Has(slot)) continue;
			InputState s = States[slot];
			bytes.Add((byte)(sbyte)Mathf.Clamp(Mathf.RoundToInt(s.Move.X * 127.0f), -127, 127));
			bytes.Add((byte)(sbyte)Mathf.Clamp(Mathf.RoundToInt(s.Move.Y * 127.0f), -127, 127));
			int b = (s.MoveFromDpad ? 1 : 0) | (s.JumpPressed ? 2 : 0) | (s.JumpHeld ? 4 : 0) | (s.AttackPressed ? 8 : 0)
				| (s.AttackHeld ? 16 : 0) | (s.SpecialPressed ? 32 : 0) | (s.SpecialHeld ? 64 : 0) | (s.BlockHeld ? 128 : 0)
				| (s.StartPressed ? 256 : 0) | (s.TauntPressed ? 512 : 0) | (s.GrabPressed ? 1024 : 0);
			bytes.Add((byte)(b & 0xff));
			bytes.Add((byte)(b >> 8));
		}
		short x = (short)Mathf.Clamp(Mathf.RoundToInt(Pointer.X), short.MinValue, short.MaxValue);
		short y = (short)Mathf.Clamp(Mathf.RoundToInt(Pointer.Y), short.MinValue, short.MaxValue);
		bytes.AddRange(System.BitConverter.GetBytes(x));
		bytes.AddRange(System.BitConverter.GetBytes(y));
		bytes.Add(Mouse);
		return bytes.ToArray();
	}

	public static NetFrame Read(byte[] bytes, int at)
	{
		var frame = new NetFrame { Present = bytes[at++] };
		for (int slot = 0; slot < Slots; slot++)
		{
			if (!frame.Has(slot)) continue;
			float mx = (sbyte)bytes[at++] / 127.0f;
			float my = (sbyte)bytes[at++] / 127.0f;
			int b = bytes[at] | bytes[at + 1] << 8;
			at += 2;
			frame.States[slot] = new InputState
			{
				Move = new Vector2(mx, my),
				MoveFromDpad = (b & 1) != 0,
				JumpPressed = (b & 2) != 0,
				JumpHeld = (b & 4) != 0,
				AttackPressed = (b & 8) != 0,
				AttackHeld = (b & 16) != 0,
				SpecialPressed = (b & 32) != 0,
				SpecialHeld = (b & 64) != 0,
				BlockHeld = (b & 128) != 0,
				StartPressed = (b & 256) != 0,
				TauntPressed = (b & 512) != 0,
				GrabPressed = (b & 1024) != 0,
			};
		}
		frame.Pointer = new Vector2(System.BitConverter.ToInt16(bytes, at), System.BitConverter.ToInt16(bytes, at + 2));
		frame.Mouse = bytes[at + 4];
		return frame;
	}
}

/// <summary>
/// A controller somewhere in an online session - on this computer or another - as every copy sees
/// it: what it did this frame, from NetSession. Nothing that reads one can tell it from a pad.
/// </summary>
public sealed class NetInputSource : IInputSource, IHapticInputSource
{
	public readonly int Machine;
	public readonly int Slot;
	int polledFrame = int.MinValue;

	public NetInputSource(int machine, int slot)
	{
		Machine = machine;
		Slot = slot;
	}

	/// <summary>On this computer: its player earns achievements here, and its pad rumbles here.</summary>
	public bool IsMine => NetSession.Instance != null && NetSession.Instance.Me == Machine;

	/// <summary>
	/// What it did this frame. A press shows once: not on the first read - a button already down
	/// then was pressed for the screen before, as a pad does it - and not on a second read in the
	/// same frame.
	/// </summary>
	public InputState Poll()
	{
		NetSession session = NetSession.Instance;
		if (session == null) return InputState.None;
		InputState state = session.StateOf(Machine, Slot);
		bool fresh = polledFrame != int.MinValue && polledFrame != session.Frame;
		polledFrame = session.Frame;
		if (fresh) return state;
		state.JumpPressed = state.AttackPressed = state.SpecialPressed = state.StartPressed = state.TauntPressed = state.GrabPressed = false;
		return state;
	}

	public void Rumble(float strength, float seconds)
	{
		if (IsMine) NetSession.Instance.OwnPad(Slot)?.Rumble(strength, seconds);
	}
}
