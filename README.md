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
| `TestArena` | Flat arena with the player, every fish type, a hazard, checkpoints and the HUD. **Use this to try things.** Not included in builds. |

Open a scene and press Play. `MainMenu` → New Game loads `Main_Scene`.

## Controls

| Input | Does |
|---|---|
| Mouse | Look. Looking up/down while swimming forward changes depth. |
| W A S D | Swim |
| Shift | Sprint |
| E | Interact (eat a dead fish, later: doors, chests) |
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

### Hands / dagger (the hand-drawn animation)
Under `Player → CameraPivot → Hands`. Right now `Placeholder Hand Animator` just swings a stick.
When the ENA-style hand frames are ready: put them on a camera-facing quad under `Hands`, write/attach a hand animator that implements `IHandAnimator` (one method, `PlaySlash()`), and drag it into `Player → Slash Attack → Hand Animator`. Nothing else changes.
`Slash Attack → Hit Delay` sets when the damage lands after the click — match it to your impact frame.

### Checkpoint plate
`Checkpoint_*` objects use a squashed cylinder and `RespawnPlate.mat` (emissive cyan). Swap the mesh freely; the `Checkpoint` component only needs the trigger collider. Active/inactive glow colours are on the component.

### Sounds
Drop a clip into the field, done:

| Sound | Where |
|---|---|
| Swing / hit | `Player → Slash Attack → Swing Sound / Hit Sound` |
| Eat | `EdibleFish → Eat Sound` (per fish prefab) |
| Enemy hit / death | `Damageable → Hit Sound / Death Sound` |
| Pufferfish bite | `Fish Aggression → Attack Sound` |
| Low hunger warning | `Player → Hunger System → Warning Sound` |
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
  Scenes/                 MainMenu, Main_Scene, TestArena
  Scripts/                see below
  Sound/                  SFX, music, ambience
  Settings/               URP render settings
```

## Scripts (`Assets/Scripts`)

Every script starts with a one-line comment saying what it does. By folder:

- **Player/** — `SwimController` (movement + camera feel), `HungerSystem`, `HealthSystem`, `DamageManager`, `DeathManager` (respawn), `PlayerInteractor` (E prompt)
- **Combat/** — `SlashAttack`, `Damageable` (enemy HP), `IDamageable`, `IHandAnimator` + `PlaceholderHandAnimator`
- **Creatures/** — `FishController` (alive → dead → edible → respawn), `FishWander`, `FishAggression` (pufferfish chase/bite), `RendererTint`
- **Interaction/** — `IInteractable`, `EdibleFish`, `HazardDamage`, `Checkpoint`
- **UI/** — `StatBarUI`, `ScreenFlash`, `HitMarker`, `MainMenuController`, `AdminPanel`
- **Environment/** — `UnderwaterLighting`

How the pieces connect: `SlashAttack` → `Damageable` → (fish) `FishController` enables `EdibleFish` → `PlayerInteractor` → `HungerSystem` → `HealthSystem` → `DeathManager` → `Checkpoint`.

## Admin panel (press 0)

Spawn any placeholder in front of you, fill/starve/kill the player, god mode, time scale, recolour all fish. It is a debug tool (IMGUI) — safe to leave in, it draws nothing until opened.

## Conventions

- `*_Placeholder` in a name = temporary art, replace it.
- Tunable numbers live in the Inspector, not in code (`Hunger System → Depletion Rate`, `Slash Attack → Damage`, ...).
- Test in `TestArena`, keep `Main_Scene` for the real level.
