# Seaweed

The seaweed in the arena and the ship is grown by the `Seaweed` component (`Scripts/Environment/Seaweed.cs`): a
low-poly clump of faceted leaves in one of four kinds (Kind on the component: kelp, sea grass, broad leaf, ribbon), painted base to tip, that sways in the current, parts around
the player when they swim through it (their body pushes it aside and their wake drags it, then it springs back) and
swoops when the bone key is tied. Everything about it is on the component and changes live in the Inspector: count,
height, shape, colours, sway, and the player reaction (Touch Radius, Wake Radius, Spring Stiffness...).

For a real model instead, drop it here (.obj or .fbx) and drag it onto the component's Custom Model field: it is
scaled to Height, stood on the ground and bent the same way (tick Read/Write on the import settings so it can sway).
Free low-poly clumps that fit: Seaweed (https://poly.pizza/m/461xlaa6SZW), Seaweed 2
(https://poly.pizza/m/b_eanaL8C6j) and Seaweed 3 (https://poly.pizza/m/f_gXhnf06Oc) by Laney XR Labs on Poly Pizza,
CC-BY 3.0, so the credit line in `Assets/Sound/CREDITS.md` has to ship with the game if one is used.
