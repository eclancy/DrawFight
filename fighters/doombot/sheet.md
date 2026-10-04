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
