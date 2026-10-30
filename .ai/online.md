# Online play

Up to four computers playing as one through a room code on ecec.dev (Eric's call, 2026-10-29).
**Read before changing `NetSession`, anything a session shares, or anything that could make two
copies of the game disagree** - which, online, is almost anything.

## How it works: the whole session in lockstep

Only controllers cross the network. Every copy of the game runs the whole session itself - menus,
fights, story scenes, the doodle screen - from the same controllers, frame by frame, and so plays
the same game.

- Each frame, each copy reads its own controllers once (its keyboard and mouse, and up to four
  pads), packs what they did into a few bytes (`NetFrame`) and sends it to the others through the
  relay. It is sent for the frame `Delay` frames ahead, so it usually arrives before that frame is
  played and nobody waits.
- A frame is only played once every copy's controllers for it are in. `NetSession` runs first in
  every frame (`ProcessPhysicsPriority`); if anyone's are missing it **pauses the game for that
  frame**, so nothing else plays it, and lets it go once they arrive. (Setting `Engine.TimeScale`
  to 0 does not do this: Godot still runs physics frames, just with no time in them - the first
  version did, and played frames before the other copy's controllers were in.) The pause menu
  runs while the game is paused, so it skips a held frame (`NetSession.Waiting`).
- Every controller in the session looks plugged into every copy: `ControllerAssignment.Devices`
  lists them all as `net:COPY:SLOT`, and `NetInputSource` plays one back - a press shows once, as
  a pad's does. Seats, cursors and fighters use them without knowing. A pad plugged in or out
  anywhere reaches every copy on the same frame (`GameRoot.SessionDevicesChanged`).
- What a copy reads is kept for itself exactly as the others read it - packed and unpacked - so a
  stick at 0.503 is 0.504 everywhere.

## What has to be the same on every copy

- **Randomness** that changes what happens comes from `GameRandom.New()`, seeded from the session
  (item drops, taunts, the CPU's pick, the doodle drops). Visual-only randomness may use anything.
- **The saved game** is the host's: unlocks, achievements (for skins) and story progress are sent
  at the start and read from memory (`SaveFiles`); nothing a session changes is written to disk.
  The rules, item settings and the host's story difficulty are the host's too, and every
  computer's saved names are shared so anyone can pick theirs. Each copy's own come back after.
- **Achievements** are earned only by a copy's own players (`Achievements.ByPlayer`), held back
  until the session ends, then earned into that computer's own save. Earning them during it would
  give one copy a skin the others lack.
- **Nothing reads the keyboard or mouse except through a controller.** Online, typing a name,
  Escape on the doodle screen, the doodle screen's own mouse events and the debug keys (all but
  F1) are off; QUIT on fighter select goes back to the session's home. The doodle screen draws
  with the drawing player's computer's mouse, which travels with their keyboard's frame.
- **Choices start the same:** a session begins with every seat, team and stage reset
  (`GameRoot.ResetChoices`), and ends the same way.

Every two seconds each copy boils the game down to a number (`StateHash`: the screen, and in a
fight every fighter's position, damage, lives and state) and the copies compare. If two ever
disagree the session ends, saying so, rather than playing on apart.

## The screens

- **Online** on the main menu (`OnlineScreen`): **Start a game** gets a five-letter code from the
  relay; **Join a game** types one in (on the letter keys, or a keyboard). The lobby shows the code
  and how many computers are in; the host presses **Start** once there are two or more. Each
  computer's own screen - the session is not in lockstep yet.
- **Playing online** (`OnlineHomeScreen`), on every copy once it starts: Versus, Boss fight,
  Doodle brawl or Story, picked by anyone. **Leave** ends it for everyone; B does nothing, so
  nobody ends it by accident. Everywhere that would go to the main menu goes here instead.
- **Stories** are played together: whoever picks plays the story's fighter, and every other
  computer gets a partner on their side in the fights, on its first controller, as the next
  fighter along who is not in the chapter (`GameRoot.AddStoryPartners`). Partners are kept out of
  the scenes, which were written for the story's own cast, and join in on FIGHT!.
- Training and targets are one-player and stay off the online home.

A session ends when anyone leaves, the host goes, the connection drops for 20 seconds, or the
copies get out of step - every copy goes back to the online screen saying which.

## The relay (ecec.dev/api/play)

`play/server.js` in the website repo (EricClancyEngineeringAndCrafts), deployed with the site;
`docs/DEPLOYMENT.md` there, section 8. One WebSocket per copy: host or join by code, then every
binary message is passed to the rest of the room with the sender's number in front. It keeps
nothing and logs nothing. Four copies a room, five-letter codes, joining closed once the host
starts, and **only the same version can join** - lockstep needs both to run exactly the same code.

## Testing it

Nobody needs a second computer: run the relay locally and two copies against it.

```
cd <website repo>/play && npm ci && node server.js
DRAWFIGHT_PLAY_URL=ws://127.0.0.1:3003/api/play/ws "$GODOT_BIN" --path . -- --online=host --onlinestart=2 --onlinetest=match --fighters=1,3,4,5 --cpu --stage=4 --shot=3000
DRAWFIGHT_PLAY_URL=ws://127.0.0.1:3003/api/play/ws "$GODOT_BIN" --path . -- --online=join:CODE --onlinestart=2 --onlinetest=match --fighters=1,3,4,5 --cpu --stage=4 --shot=3000
```

The host prints `NetSession: room CODE`. `--onlinestart=2` starts once two are in;
`--onlinetest=match` takes every copy straight into the fight the flags describe (add
`--mode=boss` for the boss fight), and `--onlinetest=story:edgelord` into that story with the other
copy a partner. Both copies must get the same flags. With `--onlinestart`, each copy writes every
frame's controllers and fighters to `.shots/net_N.log`: two logs side by side show the first frame
two copies disagree on, and what about. Tested this way (2026-10-29): a four-CPU team fight on time
with items on high for 4835 frames, a boss fight for 2877, and EdgeLord's opening and first fight
with a partner for 3689 - every frame identical.

**Not tested yet:** over the real internet, between Windows and Linux (the same build should
compute the same numbers on both; if it does not, the check every two seconds will say so), and
four copies at once.
