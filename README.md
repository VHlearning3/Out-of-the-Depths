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
| `TestArena` | Labelled test zones around a spawn pad: movement course (slalom, low tunnel, vertical shaft, ramp), fish + food (one dead fish respawns), combat pen, hazard lane ending in a far checkpoint, pickup shelf, puzzle zone (pedestal / seaweed / bone-key door). **Use this to try things.** Not included in builds. Regenerate it any time with **Tools → Out of the Depths → Rebuild Test Arena** — it keeps Player/HUD/admin panel and rebuilds the rest from the placeholder prefabs. |

Open a scene and press Play. `MainMenu` → New Game loads `Main_Scene`.

## Controls

| Input | Does |
|---|---|
| Mouse | Look. Looking up/down while swimming forward changes depth. |
| W A S D | Swim |
| Shift | Sprint |
| 1–5 / mouse wheel | Select an inventory slot |
| E | Interact (eat a dead fish, pick up an item, activate a checkpoint, later: doors, chests) |
| Left mouse | Slash |
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

### Checkpoint plate
The `RespawnPlate` prefab is a complete checkpoint: squashed cylinder + `RespawnPlate.mat` (emissive cyan) + a trigger collider + the `Checkpoint` script — drop one anywhere and it works. The player activates it by looking at it and pressing **E** (swimming over it does nothing). Swap the mesh freely; the `Checkpoint` component only needs a collider. Active/inactive glow colours and the prompt text are on the component.

### Sparkle + highlight on fish and pickups
Every prefab whose root is interactable (`Fish_Wanderer`, `Pufferfish`, `WallFish`, `DeadFish`, `Pickup_*`) carries `Interactable Highlight` and `Interactable Indicator`; a live fish only starts sparkling once it is dead and edible. If you add a new interactable prefab, run **Tools → Out of the Depths → Add Indicators To Interactable Prefabs** and it gets both, wired to `Particle_Placeholder`.

### Inventory, items and puzzle sockets
Straight from the GDD: collected items go into a **5-slot hotbar at the bottom of the screen**, selected with **1–5 or the mouse wheel**.

- **Items** are assets in `Assets/Items` (`Item_StoneFragment`, `Item_BoneKeyFragment`, `Item_BoneKey`, `Item_SymbolKey`, `Item_FirstRoomKey`, `Item_Dagger`, `Item_Trident`, `Item_Pearl`, `Item_Shell`). New one: **Assets → Create → Out of the Depths → Item**. Give it an **Icon** sprite and the hotbar shows it instead of the name.
- **Pickup**: any object with `Pickup Item` (+ collider) and an Item set.
- **Socket** (`Item Socket` on any object with a collider) is the one script for every "use an item here" spot:
  - pedestal — `Required Item` stone fragment, `Required Amount` 3, `Consume Items` on; fragments go in automatically when you press E with them on you
  - lock / door — required item bone key, amount 1
  - seaweed — required item bone key fragment ×3, `Reward Item` bone key (turns the pieces into the key)
  - `Placed Visuals` are switched on one per placed piece; `On Filled` is where you hook the door opening (`GameObject.SetActive(false)`, an animation, etc.)
- **HUD**: `Inventory` under `HUD` (`Inventory UI`). Missing in a scene? **Tools → Out of the Depths → Add Inventory HUD To Open Scene** adds it and the `Player Inventory` component.

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

`Checkpoint chime.wav` and `Hit impact.wav` in `Assets/Sound/SFX Sound effects` are generated stand-ins — replace them.

### UI
`HUD` in each gameplay scene: `HungerBar`, `HealthBar` (Background + Fill + Label), `Reticle`, `InteractPrompt`, `DamageFlash`, `FoodFlash`, `DeathScreen`. Restyle anything; the scripts only need the references that are already wired. Bars use `UI_White` (a plain white square) so they work without art.

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
- **Combat/** — `SlashAttack`, `Damageable` (enemy HP), `IDamageable`, `IHandAnimator` + `PlaceholderHandAnimator`
- **Creatures/** — `FishController` (alive → dead → edible → respawn), `FishWander`, `FishAggression` (pufferfish chase/bite), `RendererTint`
- **Interaction/** — `IInteractable`, `EdibleFish`, `PickupItem`, `IInteractTargetListener` (react to being looked at), `InteractableHighlight` (lights up on look), `InteractableIndicator` (sparkle), `HazardDamage`, `Checkpoint`
- **Items/** — `ItemDefinition` (one asset per collectable), `ItemSocket` (pedestal / lock / crafting spot that takes items from the inventory)
- **UI/** — `StatBarUI`, `ScreenFlash`, `HitMarker`, `InventoryUI` (hotbar), `MainMenuController`, `AdminPanel`
- **Environment/** — `UnderwaterLighting`
- **Editor/** — the *Tools → Out of the Depths* menu: `TestArenaBuilder`, `InteractablePrefabTools` (indicators on prefabs), `ItemTools` (GDD items, inventory HUD); editor-only

How the pieces connect: `SlashAttack` → `Damageable` → (fish) `FishController` enables `EdibleFish` → `PlayerInteractor` → `HungerSystem` → `HealthSystem` → `DeathManager` → `Checkpoint`. Items: `PlayerInteractor` → `PickupItem` → `PlayerInventory` → `ItemSocket` (pedestal / lock / crafting) → `onFilled` opens a door, enables a chest, etc.

## Admin panel (press 0)

Spawn any placeholder in front of you, fill/starve/kill the player, god mode, time scale, recolour all fish. It is a debug tool (IMGUI) — safe to leave in, it draws nothing until opened.

## Conventions

- `*_Placeholder` in a name = temporary art, replace it.
- Tunable numbers live in the Inspector, not in code (`Hunger System → Depletion Rate`, `Slash Attack → Damage`, ...).
- Test in `TestArena`, keep `Main_Scene` for the real level.
