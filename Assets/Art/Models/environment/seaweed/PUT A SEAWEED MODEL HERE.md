# Seaweed model slot

The seaweed in the arena is drawn by the `Seaweed` component (flat cartoon leaves, nothing to import). If you would
rather use a real model, drop it here (.obj or .fbx, with its textures if it has any), tick
**Tools -> Out of the Depths -> Seaweed Uses Dropped-In Model**, and run **Tools -> Out of the Depths -> Rebuild Test
Arena**. The builder picks the first file here (by name), ticks Read/Write on it so it can sway, scales it to the
Seaweed component's Height, stands it on the ground and paints it with the same base-to-tip gradient as the generated
leaves (untick Paint Model on the Seaweed component to keep the model's own materials). Untick the menu item and
rebuild to go back to the generated leaves; the file can stay here.

You can also drag any prefab or model onto the Custom Model field of a Seaweed component by hand.

## The model that is here now

`model.obj` is "Seaweed" by Laney XR Labs from Poly Pizza (https://poly.pizza/m/461xlaa6SZW), CC-BY 3.0: a very low
poly Google Blocks clump (512 faces, no UVs, one teal colour). It looks blocky next to the generated leaves, which is
why the switch is off. Its two siblings by the same author are Seaweed 2 (https://poly.pizza/m/b_eanaL8C6j) and
Seaweed 3 (https://poly.pizza/m/f_gXhnf06Oc). CC-BY means the credit line in `Assets/Sound/CREDITS.md` has to ship
with the game if one of them is used.
