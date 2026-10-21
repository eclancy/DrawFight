# Story mode

Every fighter fighting for a place on the fridge. Planned 2026-10-11 from Eric's brief; **built**
2026-10-15 - all six stories, chapter by chapter, from opening to ending ("What has to be built"
says what is done and what is not). The scripts themselves live in each fighter's `story.txt`;
this is what they are built from. Read it before writing or changing any story.

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
5. **Their ending.** Every ending turns on the same thing (Eric, 2026-10-21): DoomBot beaten,
   the drawings the player fought come back and ask to stay on the fridge - so they can all keep
   playing, because it's fun - and the player says yes, in their own way. Whoever is in it is
   named on the chapter line (`ending with flambe circy lugnut triguy at fridge`).

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
  everyone like every other clip: cheer, slump, point, shrug, laugh, stomp, tremble, cross arms,
  wave, fist (in the air), flex, hands on hips, think, facepalm, present ("ta-da"), beckon, nod
  and bow - the moving ones move (a wave waves, a fist pumps). Every story puts them on the lines
  that want them (Eric, 2026-10-15).
- **Bodies talk too.** Whoever is talking talks with their hands, in the way the line is said
  (`Fighter.TalkMood`, `FighterAnimations.TalkPose`): gesturing and nodding along; leaning in
  and jabbing on a shout; hunched with a hand by the mouth on a whisper; arms open and bouncing
  when happy; cowering when scared; a showman's flourish on Flambe's hot lines; fists balled and
  jolting on DoomBot's slams. A pose a script names wins over it.
- **Faces show.** Every fighter is drawn so the whole face reads as they talk: Circy, Triguy
  and EdgeLord three-quarter on as drawn, Flambe and Lugnut redrawn three-quarter, turned to
  the camera, and DoomBot - drawn face on, arms out - turns to face you whenever he stands in a
  scene or celebrates, with a paper turn (`FighterData.FrontOn`, `FighterRig.SetFrontOn`). A
  fight is always side on.
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

Circy and Triguy are Elim's.

- **Circy talks like a normal kid** - in memes ("skill issue", "no cap", "huge W"), VERY VERY
  energetic, and his real hobby is walking. His catchphrases are Elim's: **"duuuuuuuuuude"**,
  **"GET READY FOR EVIL STUNTS"** (his bomb) and **"okaly dokaly"** (Elim, 2026-10-17; Eric's
  call, 2026-10-19). He used to talk only in pictures, before Elim said how he talks - the
  picture bubbles (`StoryPictures`) are still in the script language, unused.
- **Triguy always speaks in his catchphrases** - "that's, really kewl", in every mood, and its
  variations ("not kewl", "THAT'S, REALLY KEWL.") - plus his three show-off poses and his crying,
  which are his own drawings (`show taunt_surprised`).
- **Whoever answers Triguy understands exactly what he meant, and answers it** - the way you
  answer a friend, so what was said is obvious from the reply. Nobody translates: no "You're
  saying...", no narrator "(He meant...)" (Eric, 2026-10-15). Triguy's "that's, really kewl."
  at EdgeLord's sword gets "Hands off the sword."; Circy's "[bomb]" gets "Whoa. Okay. Nobody's
  touching your page." Where nobody else is in the scene, the scene around him carries it - a
  pose, a mark, the narrator setting it up beforehand.

That keeps them Elim's - nothing put in their mouths - and the scene still tells the story.

## The stories

What follows is each fighter's motive, voice, fights and the key lines of each scene - what the
scripts were built from. The scripts themselves (`fighters/<name>/story.txt`: Flambé's is in
`swift/`, Lugnut's in `lug/`) are the truth where the two differ, and add the scenes this does
not list: each fighter's challenger and defeated scenes, and the scenes after the pair fights.

### Flambé - The Headliner

A hotshot punk rocker who is somehow also a French chef. Confident, a smirk, all kicks and fire.
Talks in rock and kitchen: "Order up." "Let's crank it to eleven." "Too hot for you!"

**Wants:** the middle of the fridge. Eye level. The headliner spot. "Every show needs a
headliner. This kitchen is my stage."

- **Opening.** On the middle page, a fist pumped in the air (his first taunt - a scene's
  `taunt` is always a fighter's first, so it plays the same every time). He flicks his lighter and the
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
- **The two-on-one.** Circy and Triguy, side by side. "A circle and a triangle." Circy: "And you're
  a square, bro! Shape squad!" "Ha. That's me, all right. ...Pal, I'm a rectangle at most."
  Triguy: "that's, really kewl." Lugnut: "Thank you."
- **Ending.** Every drawing back on the fridge, moved round and stuck on straight, a magnet on
  each - room for everybody, even a little spot for a MiniBot. Lugnut, wiping his hands: "Built
  to code." Flambé: "Why am I behind DoomBot?" - "Structural reasons."

### EdgeLord - A Cut Above

Swords everywhere, a dagger on a chain, too cool to care - and he cares a lot. "Try to keep up."
He talks in edge puns: on the cutting edge, a cut above, look sharp, point taken, you're dull.

**Wants:** to be the coolest drawing on the fridge, and doesn't care who he has to CUT OUT OF
THE PICTURE to get there.

- **Opening.** Silhouetted on top of the freezer door, wind from an Airbrush blowing past him (he has no cape).
  "This fridge has too many drawings. And not enough... edge." The narrator: "EdgeLord. The
  coolest drawing on the fridge. (He said so.)" - "Anyone who isn't cool gets cut out of the
  picture." He draws a pair of Safety Scissors with a flourish. Looks at them. "...Round tips."
  Throws them over his shoulder. "I'll use my swords."
- **Fights:** Triguy, Circy, Lugnut, Flambé, then Flambé and Lugnut together, then DoomBot.
- **Triguy.** Triguy zooms in and strikes a pose. "You may be fast, but my swords
  are faster." - "that's, really kewl." - "It IS really cool. Stop saying it like that." After, from the floor: "that's...
  really kewl." - "Try to keep up."
- **Circy.** "A circle. You don't even have edges." - "Bro, I don't NEED edges. I'm built
  different." - "...You're built round. You are a ball." - "Exactly. Ball is life." After: "GG, bro.
  You're kinda goated, no cap." - "...Then yes. I am goated. No cap." Memes that make sense in the
  conversation, with EdgeLord the straight man who doesn't get them (Eric, 2026-10-21).
- **Lugnut.** "A hard hat? Against swords?" He taps it. "Safety first." Beat. "...Respect."
- **Flambé: the haircut** (Eric, 2026-10-21). "Nice mohawk. Is it... on fire?" - "Always. The
  hair of a true artist." - "Looks a little long on top. Lucky for you, I've got swords." - "Non.
  NON. Nobody touches the hair." - "Just a little off the top." After: "Tell me the hair is still
  there." - "I only took a little off the top." - "...Mon dieu. It is FABULOUS." Flambé's own
  story plays it from his side, and he keeps his hair.
- **The two-on-one.** Lugnut: "The guy who cut my break short." Flambé: "And gave me a haircut I
  did not order." Lugnut: "You picked us off one at a time. So we're switching to the buddy
  system." Flambé: "Two chefs, one recipe. You, well done." EdgeLord: "Two against one. Finally,
  a challenge worthy of my edge."
- **Ending.** On top of the fridge, posing: "The sharpest drawing on the fridge. Obviously." One by
  one the others drop in and ask to stay - Circy: "Duuude! Can we stay on the fridge too?"
  Lugnut: "Fighting you was the most fun I've had all week, pal." Flambé: "Without us, who would
  you even fight?" Triguy: "that's, really kewl." - "...Fair point. Fine. Everybody stays.
  Somebody has to keep losing to me." Narrator: "Still the coolest. Now with company. A cut
  above."

### Circy - First on the Fridge

Elim's. Talks in memes, very loudly. Grows tall, shrinks small, falls slowly, can't jump, falls over
easily, fires a laser and drops a bomb.

**Wants** (ours until Elim says): to stay. He was the first drawing ever put on this fridge -
his page is the oldest, its corners curled - and a new page has just been pinned half over him.

- **Opening.** The camera finds the oldest page. Narrator: "Circy was the first drawing ever
  put on this fridge." A page slides over his. "DUUUUUUUUUUDE. That's MY spot!" He stretches
  tall (his Stretch). Narrator: "He wasn't going anywhere."
- **Fights:** Triguy, EdgeLord, Flambé, Lugnut, then Flambé and Lugnut together, then DoomBot.
- **The running gag:** every fight starts with "GET READY FOR EVIL STUNTS!", and everyone takes
  it seriously. He tells Triguy to slow down, because walking is elite. Flambé, told "no fire
  near the fridge, not after last time": "...That's fair. That's actually very fair." Lugnut,
  asked for a magnet so strong he never comes down again: "Pal, I've got just the one."
- **Ending.** A Gold Star Sticker drifts down and sticks to his page. "DUUUUUUUUUUDE. A GOLD
  STAR!" Narrator: "First on the fridge. Still on the fridge." Circy: "Okaly dokaly. Going for a
  walk."

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
- **His finale: DoomBot Mk. II.** DoomBot upgraded - the same rig, with scrap armour bolted on
  (a saw-blade crest, exhaust stacks, spikes, a II on his belly plate) - rolls in and does to
  him exactly what he did to the drawings, starting with his own opening: "SCANNING. TARGET:
  FRIDGE. STATUS: COVERED IN... DOOMBOT. UNACCEPTABLE." - "...THAT IS MY LINE." Mk. II shows
  off his upgrades ("NEW SAW BLADE. NEW EXHAUST PIPES. NEW SPIKES." - "...I HAVE SOME
  BUTTONS."), thanks him for taking the old drawings down, and says he is taking down the old
  robot. DoomBot, quietly: "...SO THIS IS HOW IT FEELS." Then the steam: "NOBODY. TAKES.
  DOOMBOT. DOWN." Beaten, Mk. II: "...i only did what you did." - "...I KNOW." The scene has a
  reason for Mk. II to be there and gives DoomBot the feeling his ending turns on (Eric asked for
  that, 2026-10-15, over a first draft whose "the fridge belongs to the newest machine" came from
  nowhere).
- **Ending.** DoomBot alone on an empty fridge. "...VICTORY." The fridge hums. "...IT IS VERY
  QUIET." *Clink* - a magnet. Someone has put up a new drawing: a crayon drawing of DoomBot. "...
  SOMEONE. DREW. ME?" He glows (that is a blush). "WARNING. CORE TEMPERATURE RISING. FROM AN
  UNKNOWN CAUSE." One by one, he puts the others back up. "THE FRIDGE IS... A LITTLE BIT MINE."

### Flambé and Lugnut, on everyone else's road

Everyone else meets them one at a time, as their challenger scenes - then, later, together:

- **Flambé alone.** Cooking, of course. "You're just in time. ...To get roasted."
- **Lugnut alone.** On a break, a sandwich in his hand. He sighs, puts it down carefully. "Break's
  over."
- **Together: the lunch break.** They're sharing lunch on top of a girder. The player walks up -
  the one who already beat each of them alone - and they team up on it, each in his own trade
  (Eric, 2026-10-21): Lugnut: "Picked us off one at a time, huh? Rookie move. On my site,
  nobody works alone." Flambé: "And in my kitchen, every dish needs two cooks." Lugnut: "Buddy
  system." Every two-on-one (and DoomBot's) says the same thing in its own words.

## The finale: DoomBot

Every story but DoomBot's own ends the same way, with each fighter's own lines.

- **The reveal.** The music cuts out. The fridge is nearly bare - just the player's page left. The
  narrator: "One by one, the drawings had been coming down. And not because of the fights."
  The camera tilts up to the freezer door. A red eye opens. Heavy footsteps, each one a shake.
  DoomBot lands on rocket boots.
- **He explains the plan** - properly, the way a villain does. A stack of taken-down drawings is
  piled behind him, and they are the other fighters' own drawings - everyone's but his and
  yours - partly torn or rubbed out (Eric, 2026-10-21; `TakenDownPile`, below). "GREETINGS, SMALL DRAWING. YOU HAVE MET MY... DISTRACTION." He points his
  claw at the pile. "EVERY NIGHT, WHILE YOU FOUGHT EACH OTHER, I REMOVED ONE DRAWING. YOU BLAMED
  EACH OTHER. NOBODY SUSPECTED... THE ROBOT."
- **He thanks you** (Eric, 2026-10-21) - every drawing you beat on the way here, he took down,
  so you did his work for him. Each story's own "== thanks", in its own terms: EdgeLord, "THANK
  YOU FOR CUTTING OUT THE OTHER DRAWINGS" ("...I was cutting them out for YOU?"); Flambé,
  roasting them ("I was your... sous-chef?"); Lugnut, clearing the site ("That was NOT the job");
  Circy, the evil stunts ("Bro. I got played?!"); Triguy, running them off ("not kewl"). Then:
  "NOW ONE DRAWING STANDS BETWEEN ME AND MY DESTINY. THE ONE TRUE RULER. OF THE FRIDGE." His Eye
  Laser draws the plan on the fridge, one line at a time:

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
  - **Circy:** "Bro. You literally look like the fridge. Same shape. No cap." DoomBot: "...NO.
    CAP?" Beat. **"I AM NOT A FRIDGE."**
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
  Circy says "Same, bro. Same.", Triguy says the line, softly - before their
  ending.

## What has to be built

In order, each step usable before the next:

1. **The scene runner** - **built, 2026-10-15.** `StoryDirector` drives a match: actors through
   `ScriptedInput` (the same `IInputSource` seam as a pad or the CPU), the camera through
   `GameCamera.FrameOn`/`CutTo`, bubbles, the narrator strip and the bars on `StoryOverlay`, and
   DoomBot's laser-written plan on `StoryBoard` (on the fridge, behind the fighters). A story
   match opens on its scene instead of the 3-2-1; `fight` hands the fighters back (the player to
   their pad, everyone else to the CPU) and the fight starts on FIGHT!, in place. Won, the scene
   after plays where they stand (a KO'd loser is put back, `Fighter.ReviveForScene`); lost, the
   chapter starts again. **A** hurries a line - the first press finishes its typing and it
   still gets its full reading time; only a single press after it has stood half a second sends
   it on, an earlier press restarts that wait, and A does nothing for the first three quarters of
   a second of a scene - so mashing A, or presses carried over from the fight, never throw
   words away unread (Elim, 2026-10-21: a hurried line used to close the next frame); **holding B** skips the scene by running it at
   eight times speed under a sheet of paper, so everyone still ends up where the scene leaves
   them. The DoomBot finale and the scene after it play end to end for all five fighters.
2. **The script format** - **built, 2026-10-15.** One plain-text script per fighter,
   `story.txt` beside their `rig.json` (Flambé's is `fighters/swift/`, Lugnut's `fighters/lug/`):
   their opening, challenger scenes, pair scenes, roast and ending, each under an `== name`
   heading. The whole language is written out at the top of `fighters/doombot/story.txt`, which
   also holds the shared finale (`== finale`, `== beaten`); each fighter's own file has their
   `== roast` and `== answer`, pulled in with `include player roast`. Every line waits until it is
   done; a line ending in `&` lets the next start alongside it. `StoryScript.Check` reads every
   script at startup (`RegressionChecks: stories`): an unknown fighter, verb, face, mood,
   picture, item or sound, or an include with nothing to include, fails loudly. The scripts ship
   in the build through the export preset's include filter (`fighters/*/story.txt`).
   Talking is a blip per few letters, the one generated `voice` sound pitched per fighter
   (`FighterData.VoicePitch`). The text moods are in: shout (a burst, shaking, a thump), whisper
   (small, grey, a dotted bubble), scared (wobbling letters), happy (bouncing), hot (flickering
   orange) and slam (a word at a time, each with a clang and a shake, red when he is hot). So are
   picture bubbles (`StoryPictures`: crayon doodles in the UI's own hand).
   **Chapters** - **built, 2026-10-15.** Each story.txt opens with a `== chapters` list, one
   chapter a line (the grammar is at the top of `fighters/doombot/story.txt`; `StoryChapters`
   reads it). A fight's scene before is the player's own `== vs WHO`, else that fighter's
   `== challenger` (`== team` for two); its scene after is the player's `== beat WHO`, else their
   `== won`, else that fighter's `== defeated`. `StoryScript.Check` reads every chapter list too:
   a chapter with no scene to start it fails at startup.
3. **Feeling** - **built, 2026-10-15.** Faces and the paper turn (2026-10-11); the shared story
   poses (`pose cheer`, slump, point, shrug, laugh, stomp, tremble, crossarms, wave, fist,
   flex, hips, think, facepalm, present, beckon, nod, bow - `FighterAnimations.StoryPose`),
   talking with their hands (2026-10-15), a fighter's own whole drawings (`show taunt_surprised`), and
   marks over the head (`mark anger`, sweat, sparkle, heart, !, ?, steam, tears - `StoryOverlay`);
   text moods and per-fighter voices; story sounds (magnet clink, fridge hum, smoke alarm, steam
   whistle) and three new music pieces (story, DoomBot's theme, the victory sting) -
   `.ai/audio-direction.md`. The reveal is staged: `stage bare` takes the other drawings off the
   door, `dark on` dims it to night lavender, `eye at` opens his red eye up in the dark, `pile at`
   heaps the taken-down drawings behind him, and `page at` pins up the drawing of DoomBot in his
   ending (`StoryBoard`). The pile (`TakenDownPile`, Eric, 2026-10-21) is four sheets of
   paper, one for each of the other fighters - everyone but DoomBot and the player, nobody twice
   - each thrown down anyhow, on its side or upside down, and each carrying that fighter's real
   rig caught in an action pose: the frame one of their own attacks hits, weapon in hand (Eric,
   2026-10-21). Each rig is rendered once into its own texture when the
   pile goes down (it stays through the boss fight, so nothing costly happens per frame), and
   then damaged the way he took it down, only ever by taking away (rule 1): torn - the sheet and
   the drawing cut to a ragged outline with a piece missing, its torn edge white paper fibre,
   not ink - or half rubbed out, the eraser's scrubbing in the paper's own colour fading the
   drawing in streaks, with a graphite smear and eraser crumbs; and a bent-over corner or the
   tape still on it that held it up. Every placement and tear is a fixed table plus hashed
   wobble, never random. It is drawn on the board, behind the fighters. `table at` sets a lunch table, and `pose sit` / `pose eat` sit someone
   at it on a stool (`FighterRig.Sitting`) - Flambe and Lugnut at lunch, before every fight
   against the two of them (Eric, 2026-10-16). A scene never walks anyone off a drop
   (`MatchManager.WalkableTo`), and no car comes down City Rooftops' street while one plays.
4. **Two-on-one** - **built, 2026-10-15.** `MatchManager.Teams`: teammates cannot hit, grab,
   splash or trap each other, a CPU only goes after the other side, and a side is out when
   everyone on it is. The HUD and camera already take any number. A piece of M6, done early.
5. **CPU difficulty** - **built, 2026-10-15.** `CpuInputSource.Level`, 1 to 9: reaction time,
   how often it hesitates, blocks, swings and closes in. Level 4 is the versus CPU, unchanged. A
   story's fights climb from 2 to 5 down the chapter list, and the boss plays at 6 - on
   Medium. **Easy, Medium or Hard** is picked on the story's map (left and right;
   `GameOptions.StoryDifficulty`, saved, the same for every story; Eric, 2026-10-19): Easy plays
   three levels down and gives the player a fourth life, Hard two levels up. A two-on-one is a
   level down and each of the two has 2 lives (1 on Easy, 3 on Hard) - with 3 each it was six
   lives against three, and Elim found it too hard. A boss keeps his scene's `stocks`. **Boss rules**
   (`MatchManager.BossPlayer`, `StoryDirector.TickBoss`): more stocks and full heat from the
   scene; his own theme on FIGHT!; on his last stock "CORE TEMPERATURE CRITICAL", he flashes red,
   runs hot, sends a MiniBot after you every few seconds, and the music speeds up. DoomBot Mk. II
   (`FighterData.DoomBotMk2`, story id `mk2`) is DoomBot wearing the outfit `mk2` - scrap armour
   drawn by `tools/art/mk2_armour.py` and hung on DoomBot's own rig - fielded only by a story
   (`FighterCatalog.StoryOnlyFrom`). **Items** drop in every story fight, always on Low,
   whatever versus is set to, from the items switched on in the item settings
   (`GameRoot.StoryItems`; Eric, 2026-10-19) - enough to season a chapter, never to decide it.
6. **The story screens** - **built, 2026-10-15.** Main menu Story → "Whose story?" (a gold star
   on every story finished) → the story's map (`StoryMapScreen`: the fridge door, a magnet per
   chapter, a star on each played, the next one glowing) → play. After every chapter the story
   comes back to its map. Progress is saved per fighter in `user://story.cfg`; a finished story
   starts again from the top.
7. **All six stories are written** (2026-10-15). **Not yet:** a full play through each, watched
   and listened to - pacing, the jokes, the CPU levels and the boss fight all need playing.

Checking it: `--story=<fighter>` boots into a fighter's story, `--story=<fighter>:<n>` into its
chapter n (from 0), and `--story=<fighter>:<n>:after` into the scene after that chapter's fight;
`--storyselect` opens "Whose story?". `--shots=60,400,900` takes a screenshot at each of those
frames, then quits - a scene is checked by looking at it all the way through. Pacing and jokes
have to be watched; a build verifies none of it.

What the finale still cannot show: the fridge door is the stage, so "the camera pans from
DoomBot to the fridge beside him - the same shape" pans to the door's handle, not a whole fridge.

## Decided (Eric, 2026-10-15)

- **Triguy always talks in catchphrases,** and whoever answers says what he meant (see "The two
  kids' fighters"). Circy talked in pictures until Elim told us how he talks (2026-10-19).
- **DoomBot Mk. II** is the final boss of DoomBot's own story: a newer model, Eric's drawing
  with scrap armour bolted on - mismatched upgrades, drawn as separate pieces, so not a line of
  DoomBot is redrawn and DoomBot himself wears none of it (Eric, 2026-10-15).
- **The voice from the kitchen** in Lugnut's story is a grown-up, unseen, shouting on the
  narrator's strip (`narrator (shout):`).
- **EdgeLord's colouring is final.** Eric is not colouring him himself.
