# DuskFight — agent instructions

A local-multiplayer platform fighter (Super Smash Bros. rules) where every fighter is a
drawing made by a kid, cut into parts and puppeted on a shared skeleton.

**`Dusk` is the kid's online handle** — the project is named after him. Never propose renaming
it.

Godot 4.5.1 Mono (.NET 9), C#, 2D. **M0, M1 and most of M2 are built** — the fight runs, Xbox
pads work, and two rigged cutout puppets play the shared animation library. What M2 still owes
is a rig built from a **real drawing**; the two in `fighters/` are generated stick figures, and
`fighters/README.md` says when to delete them. M3 is the import tool.

Everything is built in code, with no `.tscn` beyond a two-line `scenes/Match.tscn` that attaches
`MatchManager`. Do not treat "no scenes" as a convention to preserve — it is just what has not
needed one yet.

## Design docs — read before designing

`.ai/` is the design source of truth. Read the relevant doc before any design, balance, or
content decision. Do not restate its contents here.

- `.ai/project-overview.md` — the pitch, the five pillars, what is in and out of scope
- `.ai/fighting-design.md` — the combat contract: match rules, the knockback formula, the
  frame-based timing rule, game feel, movement, controls. **Read before touching movement,
  hitboxes, knockback, or any move's numbers.**
- `.ai/art-pipeline.md` — the shared skeleton, how a photo becomes a rigged fighter, the
  import tool. **Read before touching the rig, the importer, or anything under `fighters/`.**
- `.ai/character-design.md` — how a kid's description becomes a balanced moveset, the special
  archetype library, the weakness rule. **Read before designing any character.**
- `.ai/roadmap.md` — milestones and what is deliberately deferred

`for-nephew/` is written **for a 10-12 year old**, not for us. Keep the tone direct and never
talk down. Those two documents are on the critical path: nothing past M2 can be built until a
drawing exists.

The drawing guide also exists as a published Artifact — <https://claude.ai/artifact/DmdTZS491FFJs28Cj8g5qL>
— which is the copy actually sent to kids. **Change one and change the other**, or the version
they read drifts from the version we maintain.

## The three rules that are not negotiable

1. **Never redraw, clean up, smooth, or "fix" his linework.** Fill in what is missing; never
   replace what is there. This is the entire point of the project.
2. **Every fighter rigs to the same skeleton**, so the animation library is authored once and
   inherited by everyone. Do not generalise the bone hierarchy — see the non-humanoid escape
   hatch in `.ai/art-pipeline.md` instead.
3. **Every duration is an integer frame count at a fixed 60 Hz `_PhysicsProcess`**, never a
   float in seconds. Retrofitting this means rewriting every move.

## Build & run

- **`dotnet build DuskFight.sln`** — the default verification loop. Catches essentially all C#
  errors, no Godot needed.
- **`"$GODOT_BIN" --path .`** — run the game. `$GODOT_BIN` is set in `.claude/settings.json`;
  if it is unset or missing, stop and say so rather than guessing a path.
- **`"$GODOT_BIN" --path . -- --parade --shot=70`** — lay out every fighter in every animation,
  side by side and large, and write a screenshot to `.shots/`. **This is how rig and animation
  changes get verified.** A wrong pivot or a flipped rotation sign is obvious here and invisible
  in a match. `--shot=N` works on the match too; `F12` grabs a frame while playing.
- **`python tools/art/stickfigures.py`** — regenerate the two generated test fighters.
- **`"$GODOT_BIN" --headless --path . --quit-after 120`** — boot the match headless and read
  the `RegressionChecks:` calibration table it prints. This is how you find out what percent a
  move KOs at without picking up a controller. **Read it before and after changing any
  knockback number** — launch distance scales with the square of knockback, so a change that
  looks small in the move data is not small on the stage. See `.ai/fighting-design.md`.

**`dotnet build` does not validate `.tscn` wiring, resource paths, signal connections, or
anything about the rig.** When you have only run a build, say so explicitly and list the
in-Godot checks that remain. Animation and game feel cannot be verified by a build at all —
they have to be looked at.

## Conventions

Mirrors the sibling project `WizardSurvivors` (`../WizardSurvivors`) deliberately. When
something is unstated here, default to however that project does it.

- `scripts/` and `scenes/` are **flat** — no subdirectories. `.tres` resources live at the
  **repo root**.
- **Tabs** for indentation, LF line endings.
- Node scripts are `public partial class X : GodotType`. Tunables are
  `[Export] public T Name { get; set; }`.
- Signals: `[Signal] public delegate void FooEventHandler(...)`, connected with
  `new Callable(this, nameof(Handler))`, disconnected in `_ExitTree()`.
- Content is **data-driven through `.tres` Resources** (`FighterData`, `MoveData`,
  `StageData`), following the `SpellData` pattern in the sibling project. New content is
  authored as resources, not as new classes. A new special-move *archetype* script is the rare
  exception and should be reusable by more than one fighter.
- Comments explain rationale rather than mechanics.

## Environment gotchas

- **Only Python 2.7 with PIL is installed** — no python3, no numpy, no ImageMagick. This is
  why the import tool is a C# `EditorPlugin` and not a script under `tools/`. Do not plan
  pipeline work that needs Python 3.
- **Do not put backticks or apostrophes inside a Bash heredoc on this machine.** The shell
  substitutes and mis-parses them even inside a quoted delimiter, silently mangling Markdown
  and code. Use the Write/Edit tools for any file containing them.
- `.godot/` is generated and gitignored. A fresh clone needs
  `"$GODOT_BIN" --headless --path . --import` before scenes will load; `Unrecognized UID` and
  `Failed loading resource` before that has happened mean "not imported yet", not "broken".
- Source photographs of drawings live under `fighters/<name>/source/` and are the master copy.
  Sliced part PNGs are generated — re-cut from the source, never hand-edit a part.
- No CI, no test framework, no `dotnet test`. Do not invent one without asking.

## Don't

- Redraw or "improve" his art. (Rule 1. It bears repeating.)
- Author durations in seconds.
- Create subdirectories under `scripts/` or `scenes/`.
- Add new root-level status `.md` files. Durable notes go in `.ai/`.
- Give a fighter an up-special that does not provide real vertical recovery.
- Ship a move with both high base knockback and high knockback growth.
- Add universal grabs or throws, or leave hooks for them. **Settled and out**, not deferred —
  see `.ai/fighting-design.md`.
- Build a Smash-style shield. Blocking **reduces** damage and knockback; it never negates them.
  There is no shield health, no shield break, and no shield poking, and the cost of blocking is
  paid in chip damage raising your percent.
- Accept or request a drawing that is not in **side view facing right**.
- Route player input through Godot's `InputMap`. Both input sources **poll devices directly**,
  because every buffer window in the game is counted in frames from a button's rising edge and
  polling makes that edge unambiguous. New devices implement `IInputSource`; nothing in
  `Fighter` should learn what kind of device is driving it.
- Apply a per-axis stick deadzone. It must be radial — see `.ai/fighting-design.md`.
- Add anything light-coloured to the world or the HUD. **The stage is paper**: the background is
  cream and everything on it is dark ink, because his art is dark marker on a white page and
  vanishes on a dark stage. White-on-cream has already been shipped invisible twice.
- Use `Skeleton2D`/`Bone2D` for the rig. Cutout parts are rigid and rotate about a joint; the
  rig is a `Node2D` tree with `Centered = false` sprites offset by `-pivot`. See
  `.ai/art-pipeline.md`.
- Call game feel or animation verified when you have only run `dotnet build`.
