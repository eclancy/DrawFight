# DoomBot - character sheet

Filled in by Eric, who drew him, on 2026-10-02. These are his answers; what they became in the
game is in `scripts/Specials.cs` (`DoomBot`), `scripts/CharacterNormals.cs` (`DoomBot`) and
`scripts/FighterData.cs` (`DoomBot`).

| Question | Answer |
|---|---|
| **Name** | DoomBot |
| **Drawn by** | Eric |
| **What is he?** | A factory robot that went rogue. |
| **Amazing at** | Strong, hard to knock over, long reach. |
| **Terrible at** | Overheats, rusty joints, big windups. |
| **Coolest move** | The furnace. His heat builds up as he fights: he gets visibly hotter and smokes, and lightly flashes at max heat. His down special releases all of that heat. At max heat, it's a powerful flame attack in all directions. |
| **Side special** | Extends his arms forward, grabs someone, pulls them to him, and then kicks them. |
| **Neutral special** | An eye laser. |
| **Up special** | Booster jets below his feet that can set people on fire. |
| **Jab** | A single strong kick with his foot. Some sparks come out. No combo. |
| **Dash attack** | Spins his arms windmill style. |
| **Up smash** | An electric shock from his antennae. |
| **Neutral air** | Makes an electricity field around him. |
| **Forward air** | Shoots a low power missile. |

The other normals were suggested and accepted: a piston punch (forward tilt), a claw snap (up
tilt), a boot sweep (down tilt), a hydraulic press (forward smash), a stomp that sends a quake
both ways (down smash), a boot kick (back air), a claw clap (up air) and a boots-first spike
(down air).

Added on 2026-10-04, at Eric's call:

- **MiniBot** replaced the boot sweep as his down tilt: he sets down a little copy of himself
  that walks at the other fighter and goes off in a small furnace burst.
- **Glowing claws:** past half heat, his normals burn a little and his claws glow.
- **Telescoping arms:** his piston punch and grab slide out on steel rods instead of stretching,
  because stretching is Circy's thing.

Two calls made along the way:

- **"Rusty joints"** means he is stuck for a moment after any swing that misses
  (`WhiffLagFrames`), not that he blocks badly.
- **The grab is a real command grab**, the second in the roster after EdgeLord's grapple (now
  his Chain Blade).
  Eric chose that over making it a blockable hook. See `.ai/character-design.md`.

Turned toward us, on 2026-10-21, at Eric's call:

- **His drawing is a side view.** The red slash with the green tip is an antenna on the side of
  his head (his ear), the round grille eye is his right eye, and the teeth run to the front edge
  of his face. His body is drawn side on too: the near arm comes out of a shoulder socket on its
  side, and the far arm comes off its front edge.
- **So his head and body are turned three-quarters toward us**, still facing right. Eric picked
  the 60/40 version from three mockups (`tools/art/doombot_front_mockup.py`). Of each box, 40% is
  his drawn side, squeezed, keeping the ear, the socket and the hatch. The other 60% is a new
  front facing us. It carries copies of his own marks: his eye as drawn, a mirrored copy of it
  at 88% size as the far eye, the brows with them, his teeth mirrored into a mouth across the
  front, and his chest controls exactly as drawn. The front is filled with his own grey pencil.
  The only new lines are the edges of the new fronts and the corner where each front meets his
  side. Built by `tools/art/cut_doombot.py`. This was his own drawing, done at his request, and is
  not a precedent for anyone else's.
