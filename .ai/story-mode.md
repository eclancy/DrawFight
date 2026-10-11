# Story mode

Every fighter fighting for a place on the fridge. Planned 2026-10-11 from Eric's brief; **not
built yet** - this is the plan to build from, and the stories to build. Read it before writing or
changing any story.

## The premise

Every drawing in the house ends up on the fridge door, held up by a magnet - the Fridge Door
stage is that door. **The fridge is full.** And when the fridge is full, drawings start coming
down: into the Recycling Bin. So every drawing is fighting for its place on it. Some want to
keep theirs; some want the best one; one wants the whole fridge.

The twist, saved for the finale: the drawings have not been coming down because of the fights.
**DoomBot has been taking them down**, one by one, because he believes the fridge is a machine,
he is a machine, and so the fridge is his. He also looks exactly like a fridge, and is very
sensitive about it.

## The shape of a story

Pick a fighter, play their story. Each one is about six fights, with a short scene before and
after each:

1. **Their opening** - who they are and what they want from the fridge.
2. **One fight against each other fighter**, in an order that suits the story. Each opponent's
   challenger scene is theirs (how they meet a challenger); a scene written for one particular
   pair - Flambé and Lugnut, say - replaces it.
3. **A two-on-one**: Flambé and Lugnut, on a lunch break, teaming up on the player. In Flambé's
   and Lugnut's own stories another pair teams up instead.
4. **The finale: DoomBot**, on top of the fridge - the roast, the meltdown, the boss fight.
5. **Their ending.**

A lost fight is retried straight away, its scene skippable the second time. Any scene can be
skipped by holding a button. Progress is saved per fighter, and a finished story gets a gold
star (the item) on the story select screen.

A new fighter joins every story without anyone else's being rewritten: their challenger scenes
are what other stories play when they reach them (`.ai/character-design.md`, "Every fighter
brings a story").

## How a scene is staged

**A scene is a match before the fight starts**, not a separate cutscene engine. The real stage,
the real fighters on their real rigs, the real camera, items, effects and sounds - driven by a
script instead of controllers. When the scene ends, the fighters are already standing there:
the banner says FIGHT! and the fight starts in place. When it is won, the winner's scene plays
in the same place. Nothing is drawn new for a story; it is staged with what the game has.

- **Actors act through the game.** A script moves a fighter the way a controller does - walk
  to, run, jump, face, attack, special, taunt, block, get hit - so every move looks and sounds
  exactly as it does in a fight. Items are spawned, thrown and used; effects (dust, fire,
  steam, flashes, shake) fire as they do in a match.
- **Faces act.** Every fighter's own eyes and mouth are cut out of their drawing and move: they
  blink, glance, squint to attack, squeeze shut when hit, go wide when launched, and the mouth
  moves while they talk. Nothing is redrawn - the marks are only squashed, tilted and opened.
- **Turning is a paper turn.** A drawing turns round - or spins on the spot, or twirls for a
  flourish - by narrowing to its edge and coming back the other way, as a cut-out would; a
  drawing can also roll round in the plane of the page.
- **Emotion is posed and marked.** New shared poses, authored once in `FighterAnimations` for
  everyone like every other clip: cheer, slump, point, shrug, laugh, stomp, tremble, cross arms.
  Marks over the head: anger vein, sweat drop, sparkle, heart, "!", "?", steam, tears (Triguy
  cries his own). DoomBot shows anger the way he already shows heat: glowing, smoking,
  flashing.
- **Dialogue is crayon speech bubbles** on the speaker, typed out a letter at a time with a blip
  in that fighter's voice (pitch and timbre per fighter, generated like every sound). Text has
  moods: a **shout** grows, shakes and lands with a thump; a **whisper** is small and grey;
  **scared** wobbles; **happy** bounces; Flambé's hot lines flicker orange; DoomBot's ALL CAPS
  slam in a word at a time, red when he overheats. A **narrator** speaks in pencil on a torn
  strip of paper across the bottom of the screen.
- **Cameras direct.** Wide, a two-shot, a push in on whoever is talking, a slow pan, a whip pan
  for a gag, a shake on a slam, a zoom on a reaction. Black bars come in while a scene plays.
- **Music and sound tell it too.** A light story theme under the talking, the battle loop for
  the fights, DoomBot's own heavy theme, a victory sting, and silence where the joke needs it.
  Story sounds: a magnet clink, a fridge hum, paper rustling, a smoke alarm, a steam whistle.
  Generated like every other sound (`.ai/audio-direction.md`).

### The two kids' fighters

Circy and Triguy are Elim's. **Elim never said how they talk.** Circy's taunt has no line
because Elim did not give him one (a comment in `FighterData.Circy`), and Triguy's sheet gives a
catchphrase and three poses. Building on that, the stories below are written - for now - with:

- **Circy speaking in pictures.** His bubbles hold little drawings - a fridge, a heart, a magnet,
  a star, a question mark, a bomb (his own) - and everyone else gets them slightly wrong.
- **Triguy saying very little** - his catchphrase, "that's, really kewl", in every mood, and the
  odd variation ("not kewl") - plus his three show-off poses and his crying.

That is Eric's call to change (see the open questions), and their stories get rewritten from
Elim's own words when he answers the new story question on the form.

## The stories

What follows is each fighter's motive, voice, fights and the key lines of each scene. The full
scripts are written when the scenes are built; these are what they are built from.

### Flambé - The Headliner

A hotshot punk rocker who is somehow also a French chef. Confident, a smirk, all kicks and fire.
Talks in rock and kitchen: "Order up." "Let's crank it to eleven." "Too hot for you!"

**Wants:** the middle of the fridge. Eye level. The headliner spot. "Every show needs a
headliner. This kitchen is my stage."

- **Opening.** On the middle page, an air guitar (his taunt). He flicks his lighter and the
  flame roars up. "Let's turn up the heat."
- **Fights:** Triguy, Circy, EdgeLord, Lugnut, then EdgeLord and Triguy together, then DoomBot.
- **Lugnut (his best friend).** On the **Big Show** stage - a concert stage drawn on a page of
  the fridge, with a steel girder across the top. *Clang, clang.* Flambé arrives for his show to
  find Lugnut up on that girder with a wrench. "Lug. Buddy. What are you doing to my stage." -
  "Lighting rig. Your stage wasn't up to code." - "It's ROCK AND ROLL. Nothing is up to code!" -
  "Then nobody plays till it passes inspection." The flame on his head flares: "**You're
  cancelling my SHOW?!**" Lugnut tightens his hard hat. "...Break's over, I guess." They fight on
  the stage, the girder overhead. After, Lugnut looks at the dents. "...Okay. It passed." -
  "Inspection?" - "Stress test." - "So I can play?" - "With pyro, even." A fist bump, and Fire Rain
  off the girder like fireworks.
- **The two-on-one.** EdgeLord has recruited Triguy: "Speed and edge, kid. We're unstoppable."
  Triguy: "that's, really kewl." Flambé: "Order up."
- **Ending.** The Big Show, on the middle of the fridge, Lugnut's lights on the girder.
  Flambé headlines, Fire Rain for fireworks. Lugnut, in the crowd: "ENCORE!" - "I don't do
  encores." Beat. "...Okay. One encore."

### Lugnut - Room for Everybody

A big construction worker with a big heart. Practical, steady, a union man. "Break's over!"
"Hard hats on." "This fridge isn't up to code."

**Wants:** nobody in the bin. He doesn't want to come down, and he doesn't want anyone else to
either. He has measured the fridge, and if everyone is moved round and stuck on properly -
every drawing, every magnet - there is room for all of them. But everyone is too busy fighting
to listen, so he has to stop them first.

- **Opening.** Measuring the fridge door with the Metre Stick. "Fridge is full. Folks are
  getting knocked off left and right. But if everybody scooches over, there's room for all of
  us." Fighting sounds off screen. "...Right after I stop all that." He cracks his knuckles.
  "Break's over!"
- **Fights:** Circy, Triguy, EdgeLord, Flambé, then Circy and Triguy together, then DoomBot.
- **Flambé (his best friend).** Smoke. Sizzling. Flambé is making crème brûlée on the fridge with
  his lighter. *BEEP BEEP BEEP* - the smoke alarm. A voice from the kitchen (the narrator strip,
  shaking): "**IF THAT ALARM GOES OFF ONE MORE TIME, THOSE DRAWINGS ARE COMING DOWN.**" Lugnut:
  "Buddy. Pal. Put the lighter down." - "It's crème brûlée! It NEEDS fire!" - "It needs to not
  get us all recycled." - "Mon ami. You do not rush a dessert." Lugnut rolls his shoulders.
  "Then I'm on overtime." After, they sit together and Flambé hands it over. Lugnut tries it.
  "...Okay. That's actually really good." - "I KNOW."
- **The two-on-one.** Circy and Triguy, side by side. "A circle and a triangle." Circy holds up a
  picture of a square in a hard hat. "...Is that me? I'm not a square. I'm a rectangle at most."
  Triguy: "that's, really kewl." Lugnut: "Thank you."
- **Ending.** Every drawing back on the fridge, moved round and stuck on straight, a magnet on
  each - room for everybody, even a little spot for a MiniBot. Lugnut, wiping his hands: "Built
  to code." Flambé: "Why am I behind DoomBot?" - "Structural reasons."

### EdgeLord - A Cut Above

Swords everywhere, a dagger on a chain, too cool to care - and he cares a lot. "Try to keep up."
He talks in edge puns: on the cutting edge, a cut above, look sharp, point taken, you're dull.

**Wants:** to be the coolest drawing on the fridge, and doesn't care who he has to CUT OUT OF
THE PICTURE to get there.

- **Opening.** Silhouetted on top of the freezer door, wind from an Airbrush blowing his cape.
  "This fridge has too many drawings. And not enough... edge." The narrator: "EdgeLord. The
  coolest drawing on the fridge. (He said so.)" - "Anyone who isn't cool gets cut out of the
  picture." He draws a pair of Safety Scissors with a flourish. Looks at them. "...Round tips."
  Throws them over his shoulder. "I'll use my swords."
- **Fights:** Triguy, Circy, Lugnut, Flambé, then Flambé and Lugnut together, then DoomBot.
- **Triguy.** Triguy zooms in and strikes a pose. "You're fast. I'm faster." - "that's, really
  kewl." - "It IS really cool. Stop saying it like that." After, from the floor: "that's...
  really kewl." - "Try to keep up."
- **Circy.** "A circle. You don't even have edges." Circy holds up a picture of a bomb. "...Was
  that a threat?" Circy holds up a bomb with a heart on it. "I genuinely cannot tell."
- **Lugnut.** "A hard hat? Against swords?" He taps it. "Safety first." Beat. "...Respect."
- **Flambé.** "Nice mohawk. Is it... on fire?" - "Always." - "Cool. I mean - hot. Whatever." -
  "Too hot for you?" - "Nothing is too hot for - OW." (the mohawk)
- **The two-on-one.** Lugnut: "Heard you've been cutting people out of the picture." Flambé: "So
  we're putting you back in it." Lugnut: "Face first." EdgeLord: "Two against one. Finally, a
  challenge worthy of my edge."
- **Ending.** Alone on top of the fridge, posing, cape blowing. The camera pulls back: nobody is
  watching. "...Cool." Long pause. From below, Flambé: "Dude. Come sit with us." - "Only because
  it's cooler over there." He hides a smile behind a sword. Narrator: "Still the coolest. Now
  with company. A cut above."

### Circy - First on the Fridge

Elim's. Speaks in pictures. Grows tall, shrinks small, falls slowly, can't jump, falls over
easily, fires a laser and drops a bomb.

**Wants** (ours until Elim says): to stay. He was the first drawing ever put on this fridge -
his page is the oldest, its corners curled - and a new page has just been pinned half over him.

- **Opening.** The camera finds the oldest page. Narrator: "Circy was the first drawing ever
  put on this fridge." A page slides over his. A picture bubble: the fridge, and a heart. He
  stretches tall (his Stretch). Narrator: "He wasn't going anywhere."
- **Fights:** Triguy, EdgeLord, Flambé, Lugnut, then Flambé and Lugnut together, then DoomBot.
- **The running gag:** everyone reads his pictures wrong. Flambé, shown a flame crossed out: "No
  fire? NO FIRE? That's my whole thing!" Lugnut, shown a magnet: "You want my magnet? Pal, that's
  company property."
- **Ending.** A Gold Star Sticker drifts down and sticks to his page. A picture bubble: one
  enormous heart. Narrator: "First on the fridge. Still on the fridge."

### Triguy - Can't Stand Still

Elim's. Says "that's, really kewl." Reeeally fast, terrible at standing still, cries a slippery
puddle, bounces on a trampoline, throws his top hat.

**Wants** (ours until Elim says): to stay on the fridge at all. He got put up yesterday and has
slid off it eleven times since.

- **Opening.** Zooming about, skidding, sliding straight off the page. He cries (the puddle),
  bounces back up on his trampoline, and skids off again. Then he sees everyone fighting for
  spots. The surprised face. "that's, really kewl."
- **Fights:** Circy, EdgeLord, Lugnut, Flambé, then Flambé and Lugnut together, then DoomBot.
- **The running gag:** every "that's, really kewl" means something different, and everyone
  answers it as if it were a whole sentence. EdgeLord: "Say it one more time." - "that's, really
  kewl." - "...I walked into that."
- **Ending.** A Glue Bottle. Triguy, glued to his page, finally standing still. He wriggles. "that's...
  really kewl." Then, very loudly: "THAT'S, REALLY KEWL."

### DoomBot - PROTOCOL: CLEAN FRIDGE

A rogue factory robot, built big. Strong, slow, rusty, always overheating. Speaks in a level
robot ALL CAPS - until he is upset, and then it shakes, grows and goes red.

**Wants:** the whole fridge. "THE FRIDGE IS A MACHINE. DOOMBOT IS A MACHINE. MACHINES STICK
TOGETHER." Beat. "LIKE MAGNETS." Beat. "HA. HA. HA."

His own story is the villain's side:

- **Opening.** A factory hum. A red eye flickers on beside the fridge. "SCANNING." His Eye Laser
  sweeps the door. "TARGET: FRIDGE. STATUS: COVERED IN... DRAWINGS. UNACCEPTABLE."
- **Fights:** Triguy, Circy, EdgeLord, Lugnut - then Flambé arrives to help his friend, and it
  is the two of them - then the finale.
- **After every win:** "YOU HAVE BEEN... PROCESSED." Beat. "YOU ARE ON THE FLOOR NOW."
- **His finale: DoomBot Mk. II.** A shinier DoomBot (the same rig, a cooler, newer tint) rolls in.
  "GREETINGS, OLD MODEL." - "OLD?" - "THE FACTORY HAS SENT A NEWER, BETTER DOOMBOT. THE FRIDGE
  BELONGS TO THE NEWEST MACHINE." - "I WAS MADE ON TUESDAY." - "I WAS MADE ON WEDNESDAY." A very
  long pause. Steam. "...NO." His own fridge joke, said to the only one it fits: "YOU LOOK LIKE A
  FRIDGE." - "SO DO YOU." Both of them start to steam. "...THIS CONVERSATION IS OVER."
- **Ending.** DoomBot alone on an empty fridge. "VICTORY." The fridge hums. "...IT IS VERY
  QUIET." *Clink* - a magnet. Someone has put up a new drawing: a crayon drawing of DoomBot. "...
  SOMEONE. DREW. ME?" He glows (that is a blush). "WARNING. CORE TEMPERATURE RISING. FROM AN
  UNKNOWN CAUSE." One by one, he puts the others back up. "THE FRIDGE IS... A LITTLE BIT MINE."

### Flambé and Lugnut, on everyone else's road

Everyone else meets them one at a time, as their challenger scenes - then, later, together:

- **Flambé alone.** Cooking, of course. "You're just in time. ...To get roasted."
- **Lugnut alone.** On a break, a sandwich in his hand. He sighs, puts it down carefully. "Break's
  over."
- **Together: the lunch break.** They're sharing lunch on top of a girder. The player walks up.
  Flambé and Lugnut look at each other, then the player. Lugnut: "You hungry?" Flambé: "For
  a fight." Lugnut: "...I was gonna offer half a sandwich." They team up.

## The finale: DoomBot

Every story but DoomBot's own ends the same way, with each fighter's own lines.

- **The reveal.** The music cuts out. The fridge is nearly bare - just the player's page left. The
  narrator: "One by one, the drawings had been coming down. And not because of the fights."
  The camera tilts up to the freezer door. A red eye opens. Heavy footsteps, each one a shake.
  DoomBot lands on rocket boots.
- **He explains the plan** - properly, the way a villain does. A stack of taken-down drawings is
  piled behind him. "GREETINGS, SMALL DRAWING. YOU HAVE MET MY... DISTRACTION." He points his
  claw at the pile. "EVERY NIGHT, WHILE YOU FOUGHT EACH OTHER, I REMOVED ONE DRAWING. YOU BLAMED
  EACH OTHER. NOBODY SUSPECTED... THE ROBOT." His Eye Laser draws the plan on the fridge, one
  line at a time:

  > 1. REMOVE DRAWINGS.
  > 2. REMOVE MAGNETS.
  > 3. FRIDGE.

  "THE FRIDGE IS A MACHINE. I AM A MACHINE. WHEN THE LAST DRAWING COMES DOWN, THE FRIDGE WILL BE
  RETURNED TO FACTORY SETTINGS. SHINY. CLEAN. **MINE.**"
- **The roast.** Every fighter makes the same joke - he looks like the fridge, and that doesn't
  make it his - in their own words:
  - **EdgeLord:** "Looking like the fridge doesn't make it yours. You're not cool, DoomBot."
    Beat. "You're just cold."
  - **Flambé:** "The fridge is not yours, mon ami - even if you do look so alike that I almost
    grabbed your handle to put the butter away." DoomBot's long arm does hang straight down his
    side, exactly like the handle on a fridge door. **"THAT. IS. AN. ARM."**
  - **Lugnut:** "Pal, I've installed plenty of fridges. You look like one, sure. Doesn't make it
    yours." He holds up the Metre Stick and squints. "...And you're not even level."
  - **Circy** holds up a picture: a fridge, an equals sign, DoomBot. DoomBot: "...IS THAT. AN
    EQUALS SIGN." Beat. **"I AM NOT A FRIDGE."**
  - **Triguy** makes his surprised face, looking from DoomBot to the fridge and back - a whip
    pan each way. DoomBot: "DO NOT. MAKE. THAT FACE." Triguy: "not kewl."
- **The meltdown.** A steam whistle. His heat climbs to full, he glows, he smokes, and each word
  slams in on its own, shaking the screen: **"I. DO. NOT. LOOK. LIKE. THE. FRIDGE."** The camera
  pans slowly from DoomBot to the fridge beside him and back. They are the same shape.
  DoomBot's theme hits. FIGHT!
- **The fight is the joke.** Mocked, he overheats: **he starts the fight at full heat** - his
  Furnace Blast loaded, his claws glowing - and that is also when his overheat stalls and rusty
  joints leave him most open. He has more stocks than the player. On his last one: "CORE
  TEMPERATURE CRITICAL," he flashes red, MiniBots come out, the music doubles in speed.
- **When he falls.** He cools, with a long hiss of steam. Quietly, in lower case for the first
  time: "...i just wanted to be on the fridge." And the player's fighter answers in their own
  way - Lugnut builds him a spot, EdgeLord shrugs ("Fine. Behind me."), Flambé offers him dessert,
  Circy holds up a picture of DoomBot with a heart, Triguy says the line, softly - before their
  ending.

## What has to be built

In order, each step usable before the next:

1. **The scene runner.** A script drives a match: actors through a scripted input source (the
   same seam as the CPU), the camera through direction overrides on `GameCamera`, speech bubbles
   and the narrator on an overlay, items and effects through the calls the match already has,
   music cues. Skip and retry. One scene end to end first: the DoomBot finale.
2. **The script format.** One plain-text script per fighter, `fighters/<name>/story.txt`, beside
   their sheet: their opening, challenger scenes, pair scenes, roast and ending. Readable enough
   for Eric to change a line himself. Every script is checked at startup by `RegressionChecks`
   (an unknown fighter, item, pose or sound fails loudly), the way up-specials are.
3. **Feeling.** Faces (eyes and mouths cut from every drawing, in every fight too) and the paper
   turn - built first, 2026-10-11 - then the emotion poses and head marks; text moods and
   per-fighter voices; story sounds; three new music pieces (story, DoomBot, victory sting) -
   read `.ai/audio-direction.md`. The Big Show stage, for Flambé's story, is built and in the
   stage list.
4. **Two-on-one.** Three fighters in a match, on teams: no hitting a teammate, the CPU picks only
   opponents, the HUD and camera take three, a team is out when both are. This is a piece of M6
   (three and four players), done early.
5. **CPU difficulty,** so a story ramps from gentle to the boss. Boss rules for DoomBot: more
   stocks, starting heat, the last-stock phase, MiniBots.
6. **The story screens.** Main menu Story → pick a fighter (with gold stars for finished
   stories) → the fridge as a chapter map, a magnet per fight → play. Progress saved.
7. **Write all six stories,** then a full play through each, watched, not just built.

Checking it: `--story=<fighter>` boots into a fighter's story, `--story=<fighter>:<n>` into one
scene; scenes screenshot like matches (`--shot=N`). Pacing and jokes have to be watched -
a build verifies none of it.

## Open questions

- **Circy and Triguy.** Elim never said how they talk. Written for now in pictures and a few
  words, and Elim asked the new story question for both, so their real stories are his. The
  alternative is that they talk like everyone else, in lines written for them from his sheets.
- **DoomBot Mk. II.** His own story needs a final boss who isn't him. Recommended: a newer
  model, the same drawing in a new tint - nothing drawn that Eric didn't.
- **The voice from the kitchen** in Lugnut's story is a grown-up, unseen, on the narrator's
  strip. Fine for the tone?
