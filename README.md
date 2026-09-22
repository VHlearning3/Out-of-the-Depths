# Out of the Depths

First-person underwater puzzle / survival demo (Unity 6, URP). A mermaid wakes in a shipwreck, has to eat to survive, and fights her way out.

Design doc: [GDD (Google Docs)](https://docs.google.com/document/d/13_2u-vNOkEtTL4YWukK6gxh_4NgNRiDHsvID02H33mQ/edit?usp=sharing)

Inside Unity, click **`Assets/_START HERE`** for the same guide in the Inspector.

---

## Scenes (`Assets/Scenes`)

| Scene | What it is |
|---|---|
| `MainMenu` | Start screen. New Game / Settings / Credits / Quit, over a looping background video (`VideoBackground` under the Canvas: drop an .mp4 or .webm onto its `Clip`; see `Assets/Art/Video`). First scene in the build. |
| `Main_Scene` | The real level: the ship greybox built from the GDD floor plans (see *Ship greybox* below). |
| `TestArena` | Labelled test zones around a spawn pad: movement course (slalom, low tunnel, vertical shaft, ramp), fish + food (one dead fish respawns), combat pen, hazard lane ending in a far checkpoint, pickup shelf, puzzle zone (pedestal / seaweed / two locked doors), doors demo (swing door, closes-behind door), a deck with a hatch and a basement, and the team mascot framed on the wall behind the spawn pad, and the chase corridor (GDD step 9) behind the door in the north wall. **Use this to try things.** Not included in builds. Regenerate it any time with **Tools → Out of the Depths → Rebuild Test Arena** — it keeps Player/HUD/admin panel and rebuilds the rest from the placeholder prefabs. |

Open a scene and press Play. `MainMenu` → New Game loads `Main_Scene`.

## Controls

| Input | Does |
|---|---|
| Mouse | Look. Swimming forward follows where you look, so looking up/down changes depth. |
| W A S D | Swim (slow to get going, glides when you let go: `Swim Controller → Acceleration / Drag`) |
| Space / Ctrl | Swim straight up / down |
| Shift | Sprint |
| 1–5 / mouse wheel | Select an inventory slot |
| E | Interact (eat a dead fish, pick up an item, activate a checkpoint, later: doors, chests) |
| Left mouse | Slash — only once the dagger (or trident) is in your inventory |
| Inspecting a pickup (it hangs in front of you) | Hold left mouse and move it to turn the item, scroll to zoom in and out, **E** to take it. Movement still works; the hotbar wheel and slashing wait |
| E held (about a quarter second) on a pickup | Grab and go: takes the item without the inspect. Items you have already inspected this session skip it by themselves (`Inspect Only First Time`), and the admin panel has a **Quick pickups** switch that skips it for everything |
| **Esc** | Pause menu: freezes the game and frees the mouse. Pages down the left: **Map** (a framed map area, texture assignable), **Settings** (resolution, fullscreen, v-sync, master volume, mouse sensitivity), **Keybindings** (click a key, press a new one), **Admin**; under them Resume, Main menu and Quit. Pages are whatever scripts implement `IPauseMenuPage` |
| **0** | Pause menu straight onto the Admin page (spawn fish, god mode, colours, time scale, quick pickups) |

---

## For artists — swapping placeholders

Everything grey/white and blocky is a placeholder. The rule everywhere: **replace the visual, keep the logic object.**

### Fish
Prefabs in `Assets/Prefabs/Placeholders/` (`Fish_Wanderer`, `Pufferfish`, `WallFish`, `DeadFish`).
Each fish is a **root object** (all the scripts + collider) with a child called **`Visual`** (just a mesh).

1. Open the prefab.
2. Delete or hide the `Visual` sphere, drop your fish model in as a child of the root.
3. Point the model's nose down **+Z** (blue arrow) — the fish swims along its forward axis.
4. On the root, `Fish Controller → Visual` should reference your new model (it's what flips belly-up on death).
5. Resize the root's collider to fit.

Colour: the root has a **Renderer Tint** component — pick any colour, no new material needed. One shared `Fish.mat` covers all fish.

**Death:** a killed fish flips belly-up and drifts upward (still edible) until it reaches a **`Dead Fish Barrier`** — a trigger box you stretch across the water surface or a room's top — then fades out and respawns like an eaten fish (`Edible Fish → Respawn Time`; 0 = gone for good). No barrier in reach? `Fish Controller → Drift Timeout` fades it anyway. Put one barrier per room / over the whole level.

**Collisions:** moving fish (wanderers, pufferfish, dead fish) have *trigger* colliders, so they never push or block the player — slashing, eating and the barrier all use overlap queries and still work. The wall fish keeps a solid collider because it is meant to block. Fish steer around walls and the player, and push apart from each other so packs don't overlap (`Fish Wander → Other fish`).

**Spawning from windows / holes:** Create Empty in the room → add **`Fish Spawner`** → set `Fish Prefab` and `Count` (**`Count` is the pack size** when `Join School` is set). `Start Mode` decides when they come: **Scene Start**, **Player Enters Trigger** (add a Box Collider to the spawner, tick *Is Trigger*, size it over the room — the fish come in when the player swims into it, and with `Despawn When Player Leaves` the live ones swim back out through the nearest window when the player leaves and return when they come back; TestArena's closed fish room works this way), or **Manual** (call `Activate` / `Deactivate` from events — e.g. a **`Player Area Trigger`** volume anywhere, which has `On Player Enter / Exit` events you can wire to anything). Then drop **`FishWindow_Placeholder`** (`Prefabs/Placeholders`) on the room-side face of the wall, blue arrow pointing into the room, **as a child of the spawner** — the spawner uses every Fish Window under it automatically, so **Ctrl+D** the window for more. **It cuts its own hole**: on a box wall (any scaled cube, i.e. all the greybox walls) the wall behind is replaced by pieces around a `Hole Size` opening and the hole is lined with a frame through the wall's full depth (`Sleeve Thickness / Color`) — automatically when you press Play, or permanently via the **Cut hole in the wall behind** button on the component / **Tools → Out of the Depths → Cut Holes For All Fish Windows** (undoable). A modelled hull mesh can't be cut that way; model the hole into it and the window just sits over it. Each window owns its entry path (`Fish Window → Start Depth / Start Drop / Exit Distance / scatter`, drawn in the Scene view): the fish appears behind and below the opening — deep under the sill outside, out of any sightline — rises into view outside the window, comes through it, fans out into the room, then wanders around the spawner or joins the pack. When one is eaten or fades at the barrier, its replacement comes in through a window again after the prefab's `Edible Fish → Respawn Time`. Swap the rim meshes on the prefab freely; the hole itself is modelled into the hull. (`Fish School → Fish Prefab / Spawn Count` is the other way to size a pack: those appear at the pack's centre at start instead of swimming in.)

**Packs:** Create Empty → add **`Fish School`** → drag fish prefabs under it (or set `Fish Prefab` + `Spawn Count`). The pack's centre wanders around that object (`Wander Radius`, `Speed`) and every fish holds a slot within `Spread` of it, so they all swim the same route. A killed fish drops out and rejoins when it respawns. Fish steer around walls and the player and never push into them (`Fish Wander → Walls`); the pufferfish keeps this while chasing.

### Pickups (bone / stone fragments)
Any pickup whose item has no World Model shows the placeholder: a small plaque with one of the seven dog photos on it, picked at random at start (`Pickup Item → Placeholder Materials`, the materials in `Art/Materials/DogPhotos`; the `Visual` child of `Pickup_Placeholder` is a thin portrait box). Items with a World Model (the two keys) show their real mesh instead. Swap the plaque for a real placeholder mesh on the prefab whenever you like.
`Pickup_*` prefabs follow the same shape: a **root** with `Pickup Item` + a collider, and a child **`Visual`** that is only a mesh.

1. Delete or hide the placeholder `Visual`, drop your model in as a child of the root.
2. Resize the root's collider to fit. `Pickup Item → Item` says which item it is (an asset from `Assets/Items`).

**Bone key pieces.** `Item_BoneKeyFragment` has `bone_key_piece1` as its World Model and pieces 2 and 3 as **World Model Variants**; each pickup says which look it shows with `Pickup Item → Model Variant` (0 = World Model, 1 and 2 = the variants), and the builders give the three fragments one piece each. `Item_BoneKey` (the tied key) shows `bone_key_full`. Any item can have variants the same way.

**Easier: give the item its model.** Select the item asset in `Assets/Items` and set **World Model** to your .fbx from `Art/Models` — every pickup of that item then shows it (auto-fitted to `Pickup Item → Model Size`, centred, and with `World Model Scale / Rotation` on the item for tweaks). It swaps in when you press Play; to see it in the editor too, run **Tools → Out of the Depths → Apply Item Models To Pickups** (bakes it into the prefabs). The gold key (`gold_key.fbx`) and rune key (`rune_key.fbx`) are wired to the first room key and the symbol key automatically, and each gets a hotbar icon rendered from the model; add a line to `KnownModels` in `Scripts/Editor/ItemModelTools.cs` for new ones, or just set the field by hand.

Two components on the root make it readable, and both work on any interactable (dead fish, chests...) — just add them next to the interactable's script:

- **`Interactable Highlight`** — the meshes light up while the player looks at the object (tint + brightness, fades in/out, slow pulse). Works with any material and keeps `Renderer Tint`. If the material has **Emission** enabled it also glows — colours are on the component.
- **`Interactable Indicator`** — spawns the sparkle particle prefab (`Particle_Placeholder` in `Prefabs/Placeholders`), sized to whatever meshes are under the root. Keep the prefab's idle rate low (a few particles/second); the component flares it up with a burst when looked at. Restyle that one prefab and every pickup and dead fish updates.

### Hands / dagger (the hand-drawn animation)
Under `Player → CameraPivot → Hands`. Right now `Placeholder Hand Animator` just swings a stick.
When the ENA-style hand frames are ready: put them on a camera-facing quad under `Hands`, write/attach a hand animator that implements `IHandAnimator` (one method, `PlaySlash()`), and drag it into `Player → Slash Attack → Hand Animator`. Nothing else changes.
`Slash Attack → Hit Delay` sets when the damage lands after the click — match it to your impact frame.
`Slash Attack → Weapon Visual` (the dagger placeholder) is hidden until the player owns a weapon item; the swing only works with the dagger or trident in the inventory (`Require Weapon` off = always armed, for testing).

### Checkpoint plate
The `RespawnPlate` prefab is a complete checkpoint: the checkpoint pillar model (`Art/Models/environment/checkpoint`, the pillar in `RespawnPlate.mat` and the book on top in `Checkpoint.mat`, its UV texture with emission on so it glows through the paint; the book (two halves hinged on its spine, each its own model file) hovers facing you, the hinge measured from the halves at start (`Measure Book` on the component; off = the numbers typed on it); activating the pillar lights it up from the bottom, a glowing ring and its light climbing to the cap while the book swings shut and flies up into the water, then it flares; the book comes back when another checkpoint takes over) as the `Visual` under a root with the trigger collider and the `Checkpoint` script — drop one anywhere and it works. The player activates it by looking at it and pressing **E** (swimming over it does nothing): the pillar pops, its glow and point light flare and sparkles burst, and while it is active it breathes light. You respawn where you stood when you activated it (never inside the pillar); set `Respawn Point` on the component to choose a spot instead. Keep a spawn checkpoint beside the spawn point, not on it. To change the model, drop a new .obj or .fbx into that folder and run **Tools → Out of the Depths → Use Checkpoint Model** (both scene builders run it too); the `Checkpoint` component only needs a collider. Active/inactive glow colours and the prompt text are on the component.

### Murals
Every mural in the ship and in the chase corridor is a `Mural` object (the builders make them with `MuralAt`): an empty root that faces into the room and a `Picture` quad under it. Nothing is on them yet. To put a picture on one, select it and drop the texture on **Picture** on its `Mural` component: it shows at once, fitted inside the mural's area without stretching (`Keep Aspect` off = stretched to fill), glowing faintly so it reads in the water (`Glow`). Empty = the plain placeholder colour. All murals share `Art/Materials/Mural.mat` and each shows its own picture on top of it, so a picture is one drag and no new material.

### Doors
`Door_Placeholder` (`Prefabs/Placeholders`, created by the arena builder) is the same shape as everything else: the **root** is the hinge and carries a doorway trigger, the `Door` script and `Interactable Highlight`; the child **`Visual`** is a 2 × 3 panel with its collider. Swap the Visual for the real door mesh, keep the root.

- `Motion`: **Slide** (moves the visual by `Slide Offset`, default straight up) or **Swing** (rotates it `Swing Angle` degrees around the root — so put the root at the door's edge).
- **E** opens/closes it, unless `Locked`. Locked doors are opened from events: `Item Socket → On Filled → Door.Open` (a key, the pedestal), or `Unlock` to let the player open it themselves.
- `Close Behind Player`: the door shuts by itself when the player walks away from the doorway. With `Lock Behind` on it only does so once the player has gone through (to the side the root's blue arrow points at) and then locks for good — the GDD's first room; with it off it just closes and can be opened again from either side (the arena's fish room).
- Swing doors push open **away from whoever opens them** (`Swing Away From Player`), so they never swing into the player's face.
- Feel: `Open Curve` / `Close Curve` shape the move (default: an eased push open, a gravity-style drop shut). `Bounce`, `Bounces` and `Bounce Time` make it rebound a little when it lands shut; `Open Settle` is the small overshoot when it hits the open stop. Set them to 0 for a dead stop.
- Sounds (`Door → Sounds`, real CC0 recordings of wooden doors pre-wired, swap any): `Open Sound` is one whole recording of a creaky wooden door opening, played as it opens; closing plays `Unlatch Sound` (a creak) as it starts to swing and `Close Stop Sound` (a heavy wooden thud) at the exact frame it lands, again quieter on each bounce, so the thud always matches the landing. `Close Sound` (a whole closing recording, with `Close Sound Lands At` to time the door to its thud), `Groan Sound`, `Move Loop` and `Open Stop Sound` are extra layers, empty by default on the wooden set; the metal set is still in `Sound/Doors` as spares. `Locked Sound` is the rattle when you try it locked. `Pitch` (0.95), `Reverb` (Off by default, since natural recordings sound best dry) and `Muffle Cutoff` treat all of it. `Hearing Range` is how far it carries. Events: `On Opened`, `On Closed`.
- **Trapdoor / hatch**: `Trapdoor_Placeholder` is the same `Door` lying flat — `Swing Axis` (0,0,1) so the lid tilts up around its edge, `Through Axis` down, prompt "open hatch". Swap its Visual the same way. Today it opens with a plain **E**; when the GDD's button-mash comes, that mechanic will just call `Open()` on it.

### Double door (the symbol room)
`DoubleDoor_Placeholder` (`Prefabs/Placeholders`, made by the builders): the **root** is the doorway (a trigger over the opening, the `Double Door` script, the highlight); **`Leaf_Left`** and **`Leaf_Right`** under it are the hinges at the outer edges, each with a **`Visual`** panel (2.4 × 4 m) to swap for the real art; **`Lock`** on the right leaf is the plate the key goes into (the `Item Socket` sits on it, so it swings with the door). The frame round it is built by the scene builders (`SpawnDoubleDoor`). Both leaves swing away from whoever opens them; `Swing Angle`, `Duration` and the three sounds are on the root. The middle room's north door is one, opened by the bone key.

### Puzzle board (stone tablet, runes)
The GDD's drag-and-click puzzle: a row of slots along the top and the tiles on offer below; drag a tile into a slot or click it to send it to the next free one, click a placed tile to take it back; the moment every slot holds the right tile it is solved. **What it looks like comes from a `Puzzle` asset** in `Assets/Puzzles` (`Puzzle_StoneTablet`, `Puzzle_Runes`; **Tools → Out of the Depths → Create Puzzle Assets** makes them with placeholder pictures in `Art/UI/Puzzle`): the board, slot and tile-frame sprites, every tile's picture (or a text label), the words, the size, the tiles and the solution order. Drop the real sprites onto the asset or replace the PNGs. In the world a **`Puzzle Station`** (the pedestal in the stone room, the code lock in the symbol room; the arena has both) opens the board on E: `Required Item / Amount` is what you must be carrying to start (the three stone fragments), taken when solved if `Consume`; `On Solved` fires once (the pedestal opens the box room door, the code lock unlocks the rune door). The admin page can solve them all.

### Sparkle + highlight on fish and pickups
Every prefab whose root is interactable (`Fish_Wanderer`, `Pufferfish`, `WallFish`, `DeadFish`, `Pickup_*`) carries `Interactable Highlight` and `Interactable Indicator`; a live fish only starts sparkling once it is dead and edible. If you add a new interactable prefab, run **Tools → Out of the Depths → Add Indicators To Interactable Prefabs** and it gets both, wired to `Particle_Placeholder`.

### Inventory, items and puzzle sockets
Straight from the GDD: collected items go into a **5-slot hotbar** (bottom-right), selected with **1–5 or the mouse wheel**. `Collectible` items (pearls, shells) don't take a slot — they're counted top-right.

- **Items** are assets in `Assets/Items` (`Item_StoneFragment`, `Item_BoneKeyFragment`, `Item_BoneKey`, `Item_SymbolKey`, `Item_FirstRoomKey`, `Item_Dagger`, `Item_Trident`, `Item_Pearl`, `Item_Shell`). New one: **Assets → Create → Out of the Depths → Item**. Give it an **Icon** sprite and the hotbar shows it instead of the name.
- **Pickup**: any object with `Pickup Item` (+ collider) and an Item set. The dagger is one: picking it up unlocks slashing (any `Weapon`-category item does).
- **Socket** (`Item Socket` on any object with a collider) is the one script for every "use an item here" spot:
  - pedestal — `Required Item` stone fragment, `Required Amount` 3, `Consume Items` on. You have to be **holding** the fragment (select it with 1-5 / wheel) and press E; every fragment you carry then goes in one after another. The prompt says what to do: "hold the stone fragment to place it" while it is in your inventory but not in your hand, "needs stone fragment (0/3)" while you have none. `Require Held` off = anywhere in the inventory will do
  - lock / door — required item bone key, amount 1
  - seaweed — required item bone key fragment ×3, `Reward Item` bone key (turns the pieces into the key). The seaweed itself is a `Seaweed` clump of flat cartoon leaves (tapered, round-tipped, waving side to side and front to back with a soft flutter along the edges, painted from a deep base colour to a bright tip) that sway slowly together, part around the player as they swim through (dragged along by their wake, springing back after), and swoop when the key is made; count, shape, paint, sway and swoops are on the component and change live in the Inspector. `Custom Model` takes a real model instead (scaled to Height, stood on the ground, painted the same and bent the same way from the base up), or drop an .obj/.fbx into `Assets/Art/Models/environment/seaweed` and, with Tools → Out of the Depths → Seaweed Uses Dropped-In Model ticked, Rebuild Test Arena uses it (the note in that folder lists free models)
  - `Placed Visuals` are the pieces as they should end up (place them on the pedestal); each one glides from your hand to its spot on a gentle arc, turns into its resting pose, grows to full size and settles with a small swell as it lands (`Place Duration`, `Place Arc Height`, `Place Spins` for a tumble, `Between Pieces`)
  - Pickups bob and spin (`Pickup Item → Idle motion`) and pop when taken (`Collect Duration`).
- **HUD**: `Inventory` under `HUD` (`Inventory UI`). Missing in a scene? **Tools → Out of the Depths → Add Inventory HUD To Open Scene** adds it, the `Player Inventory` component, and points `Slash Attack → Weapon Visual` at the dagger placeholder.

`TestArena`'s puzzle zone has all three socket types working end to end.

### Sounds
Drop a clip into the field, done:

| Sound | Where |
|---|---|
| Swing / hit | `Player → Slash Attack → Swing Sound / Hit Sound` |
| Eat | `EdibleFish → Eat Sound` (per fish prefab) |
| Enemy hit / death | `Damageable → Hit Sound / Death Sound` |
| Pufferfish bite | `Fish Aggression → Attack Sound` |
| Low hunger warning | `Player → Hunger System → Warning Sound` — plays **once** when hunger drops to `Warning Threshold` (25%); re-arms after eating back above it. Stand-in: a generated stomach gurgle |
| Low health warning | `Player → Health System → Warning Sound` — runs while health ≤ `Warning Threshold` (30%), repeats every `Warning Interval` seconds (0 = loop), stops above |
| Checkpoint | `Checkpoint → Activate Sound` |
| Door | `Door → Unlatch / Move Loop / Open Stop / Close Stop / Locked Sound` (see Doors above; the prefabs carry real CC0 metal-hatch clips) |
| Pick up | `Pickup_Placeholder → Pickup Item → Pickup Sound` (the team jingle in `Sound/Items`, pre-wired on the prefab). On pickup the item snaps up in front of your eyes, with its name and its `Description` (on the item asset) shown above the pictogram in a fixed showcase pose and hangs there while a pictogram shows what to do (`InspectHintUI`: mouse with the left button lit = turn, wheel lit = zoom, E key = take; generated icons, or add an Inspect Hint UI to the HUD with your own sprites). Hold the left mouse button to turn it, scroll to zoom (it glides), press E to take it (`Wait For Interact` off = taken by itself after `Hold Seconds`), with a soft chime (`Take Sound`, CC0, pre-wired) as it goes. Swimming and looking around stop, the reticle hides, the mouse cursor appears, the item gets its own key and fill light plus the white outline, and everything behind it blurs meanwhile (`Freeze While Inspecting`, `Lock Look While Inspecting`, `Show Cursor While Inspecting`, `Hide Reticle While Inspecting`, `Blur Background`; the blur is `InspectFocus`, a URP depth of field, tunable by adding an Inspect Focus to the scene). Then it eases into your torso with a small jolt. Knobs: `Grab / Hold / Absorb Seconds`, `Hold Distance`, `Hold Height`, `Hold Scale`, `Hold Turn Speed`, `Hold Bob Amount / Speed`, `Zoom Step`, `Zoom Range`, `Zoom Smoothing`, `Inspect Sensitivity`, `Absorb Shake`, `Collect Offset` |
| Press E on a prop | `Sound On Interact` on any object with a collider: prompt, clips (random one, random pitch), cooldown, a wiggle, and an `On Interacted` event. The mascot photo in TestArena uses it to bark |
| Ocean ambience | `Ambience → Ocean Ambience` (both scenes) — `Bed Loop` is the quiet always-on background (a CC0 underwater loop from Freesound), `Stingers` are the random far-off sounds of the deep: a whale moan, a distant boom, a lone sonar ping, the pressure-groan of ice and low rumbles (real CC0 recordings; add or swap any clips). `Bed Volume`, `Stinger Volume`, `Min / Max Interval` set how loud and how often; `Low Pass Cutoff` + `Reverb` make stingers sound muffled and distant. `PlayStinger()` can be wired to any event for a scripted scare |
| Panic | `Player → Player Panic` — `Breathing Loop` (underwater breathing, faster and louder with panic) and `Heartbeat` (one beat, repeated at `Heart Rate` beats per minute). Panic rises with a chase, a bite or low health and calms over `Calm Seconds`; it also makes the camera sway heavier, adds a tremble, a little adrenaline speed and turns the ocean ambience down |
| Swimming | `Player → Swim Audio` — `Movement Loop` swells with your speed and fades when you stop (a CC0 hydrophone recording; an alternate take sits next to it in `Assets/Sound/Player`), `Stroke Clips` play in rhythm while you move (`Stroke Interval` = seconds between strokes at top speed). A clip longer than `Stroke Slice Seconds` plays a random faded slice each time, so a long hydrophone recording gives endless different strokes |

`Checkpoint chime.wav`, `Hit impact.wav` and `Hunger stomach gurgle.wav` in `Assets/Sound/SFX Sound effects` are generated stand-ins — replace them. The ambience and swim clips are real recordings from Freesound, all CC0; `Assets/Sound/CREDITS.md` lists them.

### Credits
The main menu Credits page comes from `Assets/Resources/Credits.asset` (**Tools → Out of the Depths → Update Credits** makes it and adds a line for every sound under `Assets/Sound`, every font in `Resources/Fonts` and every model under `Art/Models` it does not list yet; the table in `Assets/Sound/CREDITS.md` supplies titles, authors and sources). On the page each team name is a row that opens to show what they did (`Team` on the asset: name, a `Photo` beside the name, the words, and `Pictures` of their work shown under them; a name with nothing written yet opens to "work here") and the assets fold out per kind. Lines already on the asset are never overwritten, so edit the words there.

### UI

**Font.** The game font is the one file in `Assets/Resources/Fonts` (`AldotheApache.ttf`). Everything shows it: the HUD, the signs and the prompts are built with it, the pause menu uses it unless the theme asset names another, and while playing every scene switches any text still on Unity's built-in font as it loads. To swap it, put one other `.ttf` / `.otf` in that folder instead and run **Tools → Out of the Depths → Use Game Font (All Scenes)** once, so the scenes and prefabs say so too (the builders do the open scene on every rebuild; *Use Game Font (Open Scene)* does the open scene and the prefabs).

`HUD` in each gameplay scene follows the GDD mock-up (**Tools → Out of the Depths → Apply GDD HUD Layout** arranges it; safe to re-run):

| Where | Object | Notes |
|---|---|---|
| top-left | `HungerBar` | a vertical gauge: `Fill` is an Image set to *Filled / Vertical*. Drop the **fish sprite** into `Background` and `Fill` and it fills the fish from the tail up — no script change |
| under it | `HealthBar` | small horizontal bar |
| top-right | `Collectibles`, `SettingsButton` | "18/50" = pearls/shells collected out of all in the scene (`Collectible Counter UI`); the gear is a placeholder `Button` with nothing wired yet |
| bottom-centre | `InteractPromptRoot` | keycap **E** + the prompt text ("eat", "pick up dagger"…), hidden when nothing is targeted |
| bottom-right | `Inventory` | the hotbar slots |
| centre | `Reticle` | |
| full screen | `DamageFlash`, `FoodFlash`, `DeathScreen` | |

Restyle anything; the scripts only need the references that are already wired. Everything uses `UI_White` (a plain white square) so it works without art.

### Lighting / water look
`Underwater Lighting` (on `Ambience`; **Tools → Out of the Depths → Underwater Look (Open Scene)** sets it all up, the builders too): the murk (fog colour and density), the ambient colours, the sun (found by itself; colour, intensity, shimmer), **caustics** (a drifting web of light on whatever the sun reaches: size, strength, drift), no sky (the camera clears to the fog colour), and **motes** drifting round the camera while playing (count, area, size, drift; `Art/Materials/WaterMotes.mat`). Live-updates in the editor. The colour grade on top is a `Volume` on the same object with `Assets/Settings/UnderwaterProfile.asset` (teal tint, vignette, bloom: made once, tune it there); post-processing is switched on for the main camera. The ship also gets a dim cool point light per room and four along the basement (`Lights` under the ship), since the roof keeps the sun out.

---

## Ship greybox (Main_Scene)

**Tools → Out of the Depths → Rebuild Ship Greybox (Main_Scene)** builds the whole ship from the GDD floor plans at 1 map pixel = 5 cm (58 × 58.5 m, 5 m decks, the basement 6 m down) and wires the GDD's lock-and-key order with the systems that exist. It clears the old level first (`ROOMS`, `Placeholder`, loose doors / fish / plates at the root, the previous `SHIP`) and keeps `GAMEPLAY`, `HUD`, `LIGHTING`, `MANAGERS` and `Ambience`. Everything it makes lives under **`SHIP`**, one group per room (`Room_1_Spawn` … `Column_Trident`, plus `Decks`, `Hull`, `Windows` and one `Roof` slab). Untick `Roof` in the Hierarchy to look in from above. Every floor carries a 1 m grid with a heavier line every 5 m (`Grid Floor`, generated, lined up worldwide), and each room is tinted as on the map: **1 cyan, 2 red, 3 orange, 5 green, 6 blue, 7 yellow**.

Flow, as the map is drawn: **1** spawn in the net → the key is in the drawer in the corner (E slides it out) → the one-way door east is the only way out → **2** middle room (stone fragment, symbol 2, the bone-key seaweed), doors west, east and north → **3** fish room (west): dagger, hack the wall of fish, open the hatch → **4** basement (symbol key, stone fragment, bone fragment) → up the second hatch into **5** the chest room, which has no doors at all: the symbol key opens the chest (stone + bone fragment), then back down through the basement → **6** stone room (east of the middle room): three fragments on the pedestal open the door south → **7** box room, the whole south strip up against room 1's wall (no door between them): pressure plates (swim onto one for now) open the closet with the last bone fragment → the seaweed ties the bone key → **8** symbol room (north of the middle room): the bone key opens its door; the rune door (code lock later, E for now) starts the **chase** → **9** hallway (the nook under it, through the hallway's door, has symbol 3 and a checkpoint): two fragments into the tablet unlock the door east → the column: trident (E for now), rubble seals the corridor, fish to fight, the one-way exit.

Greybox rules: it is generated, so tune numbers in `ShipGreyboxBuilder.cs` (every room is a few lines of walls, doors and props with metre coordinates) and rebuild, or hand-edit under `SHIP` and stop rebuilding. Doors are `Door_Placeholder` instances, pickups `Pickup_Placeholder`, fish the fish prefabs, and every window is a `FishWindow_Placeholder` with a real hole through the hull, so swapping art in a prefab updates the ship too. Only the fish room's windows have a `Fish Spawner` (its `Count` is the pack size); to have fish come in anywhere else, add a Fish Spawner and make those windows its children. The ship has no signs (the arena keeps its zone signs). The pause menu's Map page shows the GDD floor plan cropped to the hull (`Art/UI/ShipMap_Deck.png`, and `ShipMap_Basement.png` while you are below deck), lined up with the ship so the dot is where you are.

## Chase sequence (GDD step 9)

Three **chase pufferfish** hunt the player until **rubble** seals the corridor behind them. `TestArena` has the whole thing behind the door in the north wall (activate the checkpoint in front of it first). Every piece is a plain component you place by hand, so a level can be tuned freely; the only wiring is dragging references into Inspector fields.

1. **The pack:** Create Empty where they wait (a vent, a hole in the hull) → add **`Chase Sequence`** → drag three `ChasePufferfish_Placeholder` (`Prefabs/Placeholders`) under it, placed inside the hole **facing the way out** (blue arrow; each swims `Emerge Distance` straight out before hunting), and **untick their checkbox** (inactive). Optional reveal: put a `Grate` object over the hole (any object; on start it is blown off, tips over and drops to the floor) and set `Look Pull Seconds` (the camera is drawn toward it for that long; the mouse can still fight it). **Reveal cutscene:** first the hush (`Hush Seconds`, 1.6 s): the ocean falls silent, the lights stutter, `Hush Sound` groans from the vent and the view is drawn slowly toward it (`Hush Look Rate`); then the grate bursts and the camera zooms in on the first pursuer while time slows for `Cutscene Seconds` (1.5 s), then eases back to the normal view and hands control back; `Cutscene Time Scale`, `Cutscene Zoom Fov` and `Zoom In / Out Seconds` on the Chase Sequence tune it, and 0 seconds turns it off. `Hide Interact Prompt` hides the Press E prompt during the cutscene (default), for the whole chase, or never; E itself keeps working. **Start condition:** with `Start When Seen` on (the default) the trigger only arms the chase; it starts the moment the player is looking down the corridor at the vent with a clear view of the whole vent opening (`Seen Angle`, `Seen Needs Clear Line`, and sight lines `Seen Spread` metres to either side and `Seen Spread Vertical` above and below it), so a wall edge still half-covering the corridor keeps it waiting. `Force Start Distance` and `Force Start After` are safety nets that start it anyway. **Fear** (same component): `Tension Loop` is a drone that runs for the whole chase and swells as the pack closes in, `Near Sound` is heard from the nearest hunter whenever it is within `Near Distance` (a slice of a hydrophone recording of something big moving past, pitched low), `Shake At Max Danger` makes the camera tremble and `Fov Boost` widens the view as panic sets in. Sounds and the danger distances are on the component. The Scene view shows the pack's start points and lines to whatever starts and ends it.
2. **Start it:** drag a **`Player Area Trigger`** (a trigger box just inside the final door) into `Start Trigger`, or the final door itself into `Start Door` (it starts when the door opens). Anything else can call `Begin` through a UnityEvent.
3. **End it:** Create Empty → add **`Rubble Fall`** → rock cubes under it **where they should land** (they are lifted out of sight at start) and a child with a Box Collider over the whole pile as the `Blocker`. Drag the trident pickup into the rubble's `Drop On Pickup` (or a trigger into `Drop On Trigger`), and drag the rubble into the sequence's `End Rubble`: the pack turns tail and fades when it comes down.
4. **HUD:** **Tools → Out of the Depths → Add Chase Danger HUD To Open Scene** adds the danger indicator: a soft red vignette that creeps in from the screen edges and beats like a heart, faster and harder the closer the pack is, plus a small chevron at the screen edge pointing at the nearest hunter while it is off screen. Colour, reach, beat rate and the chevron are on `Chase Danger UI`; drop your own sprites into `Vignette Sprite` / `Marker Sprite` to replace the generated ones.
5. **Testing:** in Play mode use the admin panel's **Chase** section (Start / End / Reset) and **Go to**, or right-click the `Chase Sequence` / `Rubble Fall` component header for *Begin chase*, *End chase*, *Reset chase* and *Drop*.

How they differ from the normal pufferfish (`Chase Pufferfish` component): they never lose you — they follow your exact route (breadcrumbs from `Player Trail`, added to the player automatically) through doors and round corners; they speed up when far behind (`Catch Up Boost`) and ease off right behind you (`Close Speed Factor`), so sprinting keeps them back and stopping to solve something lets them catch up; they puff up, lunge and bite with a shove (`Knockback`), and **`Hits To Kill` = 3** bites from full health. The dagger does nothing to them. Speed lives on the prefab (`Speed`, `Catch Up Boost`, `Lunge Speed`), so one change tunes every chase. Dying resets the chase: the pack goes back to its hole and comes again a couple of seconds after the respawn (if the player is still near; otherwise the trigger starts it again).

## Folder map

```
Assets/
  _START HERE.asset       this guide, inside Unity
  Art/
    FBX 3D mallit/        models
    Materials/            Fish.mat, RespawnPlate.mat, FloorColors/
    Prefabs/Placeholders/ all placeholder prefabs (spawnable from the admin panel)
    Textures/             DogPhoto.jpg (the mascot, framed behind TestArena's spawn pad), UI/UI_White.png
  Items/                  one ItemDefinition asset per collectable (Tools → Out of the Depths → Create GDD Items)
  Scenes/                 MainMenu, Main_Scene, TestArena
  Scripts/                see below
  Sound/                  SFX Sound effects/, Ambience/ (ocean bed + stingers), Player/ (swim sounds); CREDITS.md lists the CC0 clips
  Settings/               URP render settings
```

## Scripts (`Assets/Scripts`)

Every script starts with a one-line comment saying what it does. By folder:

- **Player/** — `SwimController` (movement + camera feel), `SwimAudio` (water-rush loop + strokes that follow your speed), `PlayerPanic` (panic 0..1 from chases, bites and low health: breathing, heartbeat, heavier sway, tremble, adrenaline; the HUD vignette beats in time with it), `InspectFocus` (the depth-of-field blur behind an inspected pickup), `InspectLight` (key + fill lights on the camera for an inspected pickup), `HungerSystem`, `HealthSystem`, `DamageManager`, `DeathManager` (respawn), `PlayerInteractor` (E prompt), `PlayerInventory` (5-slot hotbar, 1-5 / wheel), `PlayerBody` (the "is this collider the player?" check for triggers), `PlayerTrail` (breadcrumbs the chase pack follows)
- **Combat/** — `SlashAttack` (needs a weapon item in the inventory), `Damageable` (enemy HP), `IDamageable`, `IHandAnimator` + `PlaceholderHandAnimator`
- **Creatures/** — `FishController` (alive → dead → drift up → fade → respawn), `DeadFishBarrier` (where dead fish fade), `FishWander` (solo wander or pack slot, separation), `FishSchool` (a pack's shared route), `FishSpawner` (fish swim in through windows/holes), `FishWindow` (one window's entry path; the prefab), `FishSteering` (wall avoidance), `FishAggression` (pufferfish chase/bite), `ChasePufferfish` (the chase-sequence hunter: follows your route, lunges, 3 bites kill), `ChaseSequence` (runs the chase: Begin / End, restarts after death), `RendererTint`
- **Interaction/** — `IInteractable`, `EdibleFish`, `PickupItem`, `Door` (slide/swing, lock, closes behind), `IInteractTargetListener` (react to being looked at), `InteractableHighlight` (lights up on look), `OutlineHull` (the white outline on look; built by PlayerInteractor from the shader in Resources/Shaders), `InteractableIndicator` (sparkle), `SoundOnInteract` (E = a sound + wiggle: the barking mascot), `HazardDamage`, `Checkpoint`
- **Items/** — `ItemDefinition` (one asset per collectable), `ItemSocket` (pedestal / lock / crafting spot that takes items from the inventory)
- **UI/** — `StatBarUI` (bar or any Filled sprite), `ScreenFlash`, `HitMarker`, `InventoryUI` (hotbar), `CollectibleCounterUI`, `MainMenuController`, `MenuBackgroundVideo` (a looping video or still image behind the main menu, dimmed, swappable in the Inspector), `PauseMenu` (Esc; a sidebar of `IPauseMenuPage` pages plus Resume, Main menu, Quit), `MapPage`, `SettingsPage`, `KeybindingsPage` (the built-in pages), `MenuGUI` (the buttons, rows, switches and key caps a page draws with, so it matches and the hover tick works), `AdminPanel` (the Admin page), `ChaseDangerUI` (danger vignette + chevron while the chase pack is near), `InspectHintUI` (the mouse / wheel / E pictogram while a pickup is inspected)
- **Environment/** — `UnderwaterLighting`, `OceanAmbience` (background loop + random creepy stingers), `ProximityLabel` (3D signs that face the player and fade in nearby; the arena zone labels), `WallCutter` (cuts a hole through a box wall for a Fish Window), `PlayerAreaTrigger` (trigger volume with player enter/exit events), `RubbleFall` (rocks that drop on Drop() and seal a corridor), `Seaweed` (flat cartoon leaves painted base-to-tip that sway slowly in a shared current, part around the player and swoop on demand, or any prefab/model dropped into Custom Model fitted, painted and bent the same way; the puzzle seaweed), `GridFloor` (metre grid on greybox floors)
- **Utility/** — `Ease` (easing curves used by every hand-rolled animation: doors, placing pieces, fades, pops)
- **Editor/** — the *Tools → Out of the Depths* menu: `TestArenaBuilder`, `ShipGreyboxBuilder` (the ship in Main_Scene), `CheckpointModelTools` (the checkpoint model onto the plate prefab), `InteractablePrefabTools` (indicators on prefabs), `ItemTools` (GDD items, inventory HUD), `HudLayoutTools` (GDD HUD layout), `ChaseTools` (chase pufferfish prefab, danger HUD); editor-only

How the pieces connect: `SlashAttack` → `Damageable` → (fish) `FishController` enables `EdibleFish` → `PlayerInteractor` → `HungerSystem` → `HealthSystem` → `DeathManager` → `Checkpoint`. Items: `PlayerInteractor` → `PickupItem` → `PlayerInventory` → `ItemSocket` (pedestal / lock / crafting) → `onFilled` opens a door, enables a chest, etc.

## Pause menu (Esc) and the Admin page (0)

`PauseMenu` (on the Player) is an IMGUI menu that freezes time, frees the mouse and switches off movement, interaction and attacking while it is up. Everything about how it looks, reads and sounds lives in one asset, **`Assets/Settings/PauseMenuTheme.asset`** (`Theme` on the component; **Tools → Out of the Depths → Create Pause Menu Theme** makes and assigns it, the builders too): colours, panel size and corners, sidebar side, font and every font size, the title and button labels, the resume hint, page renames / hiding / order (`Pages`: the page name as its script reports it), fade and rise timings, hover and open / close sounds. Edit it and the open menu updates, even in Play mode. To work on it without playing, tick `Preview In Editor` on the Pause Menu (or **Tools → Out of the Depths → Preview Pause Menu**, or the button at the top of the theme asset): the Game view shows the menu open, the sidebar switches pages, the pages are look-only. New pages are still any script that implements `IPauseMenuPage`. Down its left side is one entry per script in the scene that implements `IPauseMenuPage` (sorted by `Order`), with Resume, Main menu (loads `Main Menu Scene`, hidden if that scene is not in Build Settings) and Quit (asks first) underneath; the open page fills the right side. The built-in pages are components on the Player too (**Tools → Out of the Depths → Add Pause Menu Pages** adds any that are missing, the builders too; a scene without them gets them at runtime): **Map** (`MapPage`: a framed area that shows `Map Texture`, a painted map or a Render Texture from a top-down camera, with the player as a dot once `World Min` / `World Max` are set to the world x and z of its corners; nothing assigned = a placeholder that holds the space), **Settings** (`SettingsPage`: what the page shows is its `Rows` list in the Inspector, top to bottom: headings and notes, the built-in settings (resolution, fullscreen, v-sync, quality, master volume, mouse sensitivity, a reset button) and rows of your own, a switch / slider / stepper / button with a label, a default, an optional `Save Key` and a UnityEvent that fires when it is used; reorder, rename, remove or add rows there, *Put back the default rows* restores the shipped page; volume, sensitivity, v-sync, quality and every row with a save key are kept in PlayerPrefs and put back on start, and the reset button returns all of them to their defaults), **Keybindings** (`KeybindingsPage`: the keyboard and mouse bindings of the Player action map as key caps, click one and press the new key, Escape cancels, Reset all; overrides are kept in PlayerPrefs and loaded on start; which actions show and their labels are an Inspector list), **Credits** (`CreditsPage`: the same credits as the main menu, each name and each kind of asset folding open) and **Admin**. Pages draw with `MenuGUI` (buttons, toggles, `Heading`, `SwitchRow`, `SliderRow`, `StepperRow`, `ButtonRow`, `KeyCap`) so they match and the hover tick knows what the mouse is over. The Admin page: an IMGUI page that works in any scene without a canvas. Sections: **Player** (live health and hunger bars; **God mode** = no damage from anything, starving included; **No hunger drain**; **Quick pickups** = no inspect, items fly straight in; fill / hurt / kill / starve / teleport to the active checkpoint), **Give** (every item in `Assets/Items`, `x3` for stackables, clear inventory), **Spawn in front of you** (the prefabs in `Spawnables`), **Go to** (teleport in front of any sign or onto any checkpoint in the scene), **Chase** (start / end / reset the Chase Sequence, live danger), **World** (time scale with presets, unlock + open or close all doors, drop all rubble, kill all fish, fish colour). *Rebuild Test Arena* fills `Items` and `Spawnables`; in another scene set them on the `AdminPanel` component.

## Conventions

- `*_Placeholder` in a name = temporary art, replace it.
- Tunable numbers live in the Inspector, not in code (`Hunger System → Depletion Rate`, `Slash Attack → Damage`, ...).
- Test in `TestArena`, keep `Main_Scene` for the real level.
