# Adding art — quick version

Full guide: `README.md` in the project root, or click `Assets/_START HERE` in Unity.

**Rule:** replace the visual, keep the object that has the scripts.

- **Fish** → open a prefab in `Prefabs/Placeholders`, swap the `Visual` child for your model, nose along +Z. Colour with the `Renderer Tint` component. The normal fish (`Models/fish`) is already on `Fish_Wanderer`, `WallFish` and `DeadFish` (**Tools > Out of the Depths > Use Normal Fish Model On Fish Prefabs**); its animations play from `NormalFish.controller` through the `Fish Animator` on the fish.
- **Hands** → `Player → CameraPivot → Hands`. Any hand animator that implements `IHandAnimator` plugs into `Slash Attack`.
- **Sounds** → every sound is an Inspector field on the component that plays it.
- **Materials** → `Materials/`. Placeholder fish share `Fish.mat`, the normal fish use `Models/fish/NormalFish.mat`; checkpoints use `RespawnPlate.mat`.
- **Seaweed** → the `Seaweed` component grows its low-poly leaves itself (they part around the player). For a real model, drag it onto `Custom Model`; note in `Models/environment/seaweed`.
- **Try it** → open `Scenes/TestArena`, press Play, press `0` to spawn things.
