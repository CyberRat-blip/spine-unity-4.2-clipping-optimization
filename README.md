# Spine Unity 4.2 clipping optimization

For **spine-unity 4.2, package dated December 10, 2025**.

## What it does

Reduces CPU work caused by clipping masks in Spine animations while preserving clipping and draw order.

## How it works

- `SkeletonClipping.cs` adds `CanSkipClipping`, which checks whether every mesh vertex is strictly inside a single convex clipping polygon.
- `MeshGenerator.cs` skips triangle clipping when that check passes and uses the original geometry. Otherwise, Spine's existing clipping algorithm runs as usual.

## Installation

### Unity package

Download the `.unitypackage` from Releases, import it into your project and confirm replacement of the two scripts.

### Manual

Open these files in the repository:

- [SkeletonClipping.cs](Runtime/spine-csharp/SkeletonClipping.cs)
- [MeshGenerator.cs](Runtime/spine-unity/Mesh%20Generation/MeshGenerator.cs)

Copy the entire contents of each file and replace the code in the corresponding script in your project's Spine runtime. Keep the existing `.meta` files.

Let Unity recompile and keep **Use Clipping** enabled.
