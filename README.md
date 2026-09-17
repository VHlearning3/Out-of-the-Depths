# Out of the Depths

First-person underwater puzzle / survival demo (Unity 6, URP). A mermaid wakes in a shipwreck, has to eat to survive, and fights her way out.

Design doc: [GDD (Google Docs)](https://docs.google.com/document/d/13_2u-vNOkEtTL4YWukK6gxh_4NgNRiDHsvID02H33mQ/edit?usp=sharing)

Inside Unity, click **`Assets/_START HERE`** for the same guide in the Inspector.

---

## Scenes (`Assets/Scenes`)

| Scene | What it is |
|---|---|
| `MainMenu` | Start screen. New Game / Settings / Credits / Quit. First scene in the build. |
| `Main_Scene` | The real level. |
| `TestArena` | Labelled test zones around a spawn pad: movement course (slalom, low tunnel, vertical shaft, ramp), fish + food (one dead fish respawns), combat pen, hazard lane ending in a far checkpoint, pickup shelf, puzzle zone (pedestal / seaweed / two locked doors), doors demo (swing door, closes-behind door). **Use this to try things.** Not included in builds. Regenerate it any time with **Tools → Out of the Depths → Rebuild Test Arena** — it keeps Player/HUD/admin panel and rebuilds the rest from the placeholder prefabs. |

Open a scene and press Play. `MainMenu` → New Game loads `Main_Scene`.

## Controls

| Input | Does |
|---|---|
| Mouse | Look. Looking up/down while swimming forward changes depth. |
| W A S D | Swim |
| Shift | Sprint |
| 1–5 / mouse wheel | Select an inventory slot |
| E | Interact (eat a dead fish, pick up an item, activate a checkpoint, later: doors, chests) |
| Left mouse | Slash — only once the dagger (or trident) is in your inventory |
| **0** | Admin panel (spawn fish, god mode, colours, time scale) |

---

## For artists — swapping placeholders

Everything grey/white and blocky is a placeholder. The rule everywhere: **replace the visual, keep the logic object.**

### Fish
Prefabs in `Assets/Art/Prefabs/Placeholders/` (`Fish_Wanderer`, `Pufferfish`, `WallFish`, `DeadFish`).
Each fish is a **root object** (all the scripts + collider) with a child called **`Visual`** (just a mesh).

1. Open the prefab.
2. Delete or hide the `Visual` sphere, drop your fish model in as a child of the root.
3. Point the model's nose down **+Z** (blue arrow) — the fish swims along its forward axis.
4. On the root, `Fish Controller → Visual` should reference your new model (it's what flips belly-up on death).
5. Resize the root's collider to fit.

Colour: the root has a **Renderer Tint** component — pick any colour, no new material needed. One shared `Fish.mat` covers all fish.

**Packs:** Create Empty → add **`Fish School`** → drag fish prefabs under it (or set `Fish Prefab` + `Spawn Count`). The pack's centre wanders around that object (`Wander Radius`, `Speed`) and every fish holds a slot within `Spread` of it, so they all swim the same route. A killed fish drops out and rejoins when it respawns. Fish steer around walls and the player and never push into them (`Fish Wander → Walls`); the pufferfish keeps this while chasing.

### Pickups (bone / stone fragments)
`Pickup_*` prefabs follow the same shape: a **root** with `Pickup Item` + a collider, and a child **`Visual`** that is only a mesh.

1. Delete or hide the placeholder `Visual`, drop your model in as a child of the root.
2. Resize the root's collider to fit. `Pickup Item → Item` says which item it is (an asset from `Assets/Items`).

Two components on the root make it readable, and both work on any interactable (dead fish, chests...) — just add them next to the interactable's script:

- **`Interactable Highlight`** — the meshes light up while the player looks at the object (tint + brightness, fades in/out, slow pulse). Works with any material and keeps `Renderer Tint`. If the material has **Emission** enabled it also glows — colours are on the component.
- **`Interactable Indicator`** — spawns the sparkle particle prefab (`Particle_Placeholder` in `Art/Prefabs/Placeholders`), sized to whatever meshes are under the root. Keep the prefab's idle rate low (a few particles/second); the component flares it up with a burst when looked at. Restyle that one prefab and every pickup and dead fish updates.

### Hands / dagger (the hand-drawn animation)
Under `Player → CameraPivot → Hands`. Right now `Placeholder Hand Animator` just swings a stick.
When the ENA-style hand frames are ready: put them on a camera-facing quad under `Hands`, write/attach a hand animator that implements `IHandAnimator` (one method, `PlaySlash()`), and drag it into `Player → Slash Attack → Hand Animator`. Nothing else changes.
`Slash Attack → Hit Delay` sets when the damage lands after the click — match it to your impact frame.
`Slash Attack → Weapon Visual` (the dagger placeholder) is hidden until the player owns a weapon item; the swing only works with the dagger or trident in the inventory (`Require Weapon` off = always armed, for testing).

### Checkpoint plate
The `RespawnPlate` prefab is a complete checkpoint: squashed cylinder + `RespawnPlate.mat` (emissive cyan) + a trigger collider + the `Checkpoint` script — drop one anywhere and it works. The player activates it by looking at it and pressing **E** (swimming over it does nothing). Swap the mesh freely; the `Checkpoint` component only needs a collider. Active/inactive glow colours and the prompt text are on the component.

### Doors
`Door_Placeholder` (`Art/Prefabs/Placeholders`, created by the arena builder) is the same shape as everything else: the **root** is the hinge and carries a doorway trigger, the `Door` script and `Interactable Highlight`; the child **`Visual`** is a 2 × 3 panel with its collider. Swap the Visual for the real door mesh, keep the root.

- `Motion`: **Slide** (moves the visual by `Slide Offset`, default straight up) or **Swing** (rotates it `Swing Angle` degrees around the root — so put the root at the door's edge).
- **E** opens/closes it, unless `Locked`. Locked doors are opened from events: `Item Socket → On Filled → Door.Open` (a key, the pedestal), or `Unlock` to let the player open it themselves.
- `Close Behind Player`: once the player passes through (to the side the root's blue arrow points at), it closes and locks for good — the GDD's first room.
- Sounds: `Open Sound` / `Close Sound` (the SFX door clips are pre-wired). Events: `On Opened`, `On Closed`.

### Sparkle + highlight on fish and pickups
Every prefab whose root is interactable (`Fish_Wanderer`, `Pufferfish`, `WallFish`, `DeadFish`, `Pickup_*`) carries `Interactable Highlight` and `Interactable Indicator`; a live fish only starts sparkling once it is dead and edible. If you add a new interactable prefab, run **Tools → Out of the Depths → Add Indicators To Interactable Prefabs** and it gets both, wired to `Particle_Placeholder`.

### Inventory, items and puzzle sockets
Straight from the GDD: collected items go into a **5-slot hotbar** (bottom-right), selected with **1–5 or the mouse wheel**. `Collectible` items (pearls, shells) don't take a slot — they're counted top-right.

- **Items** are assets in `Assets/Items` (`Item_StoneFragment`, `Item_BoneKeyFragment`, `Item_BoneKey`, `Item_SymbolKey`, `Item_FirstRoomKey`, `Item_Dagger`, `Item_Trident`, `Item_Pearl`, `Item_Shell`). New one: **Assets → Create → Out of the Depths → Item**. Give it an **Icon** sprite and the hotbar shows it instead of the name.
- **Pickup**: any object with `Pickup Item` (+ collider) and an Item set. The dagger is one: picking it up unlocks slashing (any `Weapon`-category item does).
- **Socket** (`Item Socket` on any object with a collider) is the one script for every "use an item here" spot:
  - pedestal — `Required Item` stone fragment, `Required Amount` 3, `Consume Items` on; fragments go in automatically when you press E with them on you
  - lock / door — required item bone key, amount 1
  - seaweed — required item bone key fragment ×3, `Reward Item` bone key (turns the pieces into the key)
  - `Placed Visuals` are switched on one per placed piece; `On Filled` is where you hook the door opening (`GameObject.SetActive(false)`, an animation, etc.)
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
| Door open / close | `Door → Open Sound / Close Sound` (per door, prefab has the SFX clips) |

`Checkpoint chime.wav` and `Hit impact.wav` in `Assets/Sound/SFX Sound effects` are generated stand-ins — replace them.

### UI
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
`Player → Underwater Lighting`: fog colour and density, ambient colours, sun colour and shimmer. Live-updates in the editor.

---

## Folder map

```
Assets/
  _START HERE.asset       this guide, inside Unity
  Art/
    FBX 3D mallit/        models
    Materials/            Fish.mat, RespawnPlate.mat, FloorColors/
    Prefabs/Placeholders/ all placeholder prefabs (spawnable from the admin panel)
    Textures/UI/          UI_White.png
  Items/                  one ItemDefinition asset per collectable (Tools → Out of the Depths → Create GDD Items)
  Scenes/                 MainMenu, Main_Scene, TestArena
  Scripts/                see below
  Sound/                  SFX, music, ambience
  Settings/               URP render settings
```

## Scripts (`Assets/Scripts`)

Every script starts with a one-line comment saying what it does. By folder:

- **Player/** — `SwimController` (movement + camera feel), `HungerSystem`, `HealthSystem`, `DamageManager`, `DeathManager` (respawn), `PlayerInteractor` (E prompt), `PlayerInventory` (5-slot hotbar, 1-5 / wheel)
- **Combat/** — `SlashAttack` (needs a weapon item in the inventory), `Damageable` (enemy HP), `IDamageable`, `IHandAnimator` + `PlaceholderHandAnimator`
- **Creatures/** — `FishController` (alive → dead → edible → respawn), `FishWander` (solo wander or pack slot), `FishSchool` (a pack's shared route), `FishSteering` (wall avoidance), `FishAggression` (pufferfish chase/bite), `RendererTint`
- **Interaction/** — `IInteractable`, `EdibleFish`, `PickupItem`, `Door` (slide/swing, lock, closes behind), `IInteractTargetListener` (react to being looked at), `InteractableHighlight` (lights up on look), `InteractableIndicator` (sparkle), `HazardDamage`, `Checkpoint`
- **Items/** — `ItemDefinition` (one asset per collectable), `ItemSocket` (pedestal / lock / crafting spot that takes items from the inventory)
- **UI/** — `StatBarUI` (bar or any Filled sprite), `ScreenFlash`, `HitMarker`, `InventoryUI` (hotbar), `CollectibleCounterUI`, `MainMenuController`, `AdminPanel`
- **Environment/** — `UnderwaterLighting`
- **Editor/** — the *Tools → Out of the Depths* menu: `TestArenaBuilder`, `InteractablePrefabTools` (indicators on prefabs), `ItemTools` (GDD items, inventory HUD), `HudLayoutTools` (GDD HUD layout); editor-only

How the pieces connect: `SlashAttack` → `Damageable` → (fish) `FishController` enables `EdibleFish` → `PlayerInteractor` → `HungerSystem` → `HealthSystem` → `DeathManager` → `Checkpoint`. Items: `PlayerInteractor` → `PickupItem` → `PlayerInventory` → `ItemSocket` (pedestal / lock / crafting) → `onFilled` opens a door, enables a chest, etc.

## Admin panel (press 0)

Spawn any placeholder in front of you, fill/starve/kill the player, god mode, time scale, recolour all fish. It is a debug tool (IMGUI) — safe to leave in, it draws nothing until opened.

## Conventions

- `*_Placeholder` in a name = temporary art, replace it.
- Tunable numbers live in the Inspector, not in code (`Hunger System → Depletion Rate`, `Slash Attack → Damage`, ...).
- Test in `TestArena`, keep `Main_Scene` for the real level.
