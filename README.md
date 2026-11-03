# Spine Unity 4.2 clipping optimization

A small patch that speeds up Spine animations by skipping triangle clipping for meshes fully inside a convex mask. Masks stay enabled, and no animation re-export or prefab changes are needed.

## Installation

1. Install Spine 4.2 in your Unity project.
2. Download the `.unitypackage` from this repository's Releases and import it.
3. Wait for Unity to compile. The patch applies automatically and prints `Patch applied automatically` in the Console.

Keep `Use Clipping` enabled. If you import the package during Play Mode, installation waits until you stop it.

You can also copy the `Editor` folder into `Assets/SpineClippingOptimization/Editor`.

## Supported versions

Tested with Unity 2022.3.60f1 and the Spine Unity 4.2 package dated December 10, 2025.

The installer supports one Spine installation under `Assets`, with the usual `Runtime/spine-csharp` and `Runtime/spine-unity` folder layout. It checks the runtime version and patch locations before changing any files. UPM installations and Spine 4.3 are not supported.

If the patch is already present, the installer leaves it alone. If installation fails, check the Console, fix the reported issue and reimport the package.

## How it works

The patch changes two files:

- `SkeletonClipping.cs`: adds `CanSkipClipping`, which checks whether every mesh vertex is strictly inside a single convex clipping polygon.
- `MeshGenerator.cs`: keeps the original vertices, UVs and triangle indices when that check passes.

Meshes touching or crossing the mask boundary still use Spine's clipping algorithm. So do masks that decompose into multiple polygons. Draw order and mask lifetime are unchanged.

This helps most when a mask covers many meshes that rarely cross its edges. It won't fix unrelated CPU or GPU bottlenecks.

## Removing the patch

Choose **Tools → Spine Clipping Optimization → Restore Original Files** before updating Spine or deleting the installer.

Backups are stored in `.spine-clipping-optimization/` at the project root. Keep that folder until you restore the original files. If you edit the patched source, automatic restore will stop rather than overwrite your changes; use version control or the `.original` backups instead.

After a restore, the patch stays off until you reimport the package. You can commit the patched Spine files for the rest of your team. The installer only runs in the Unity Editor and is excluded from player builds.

## Testing

Triangle positions and UVs were compared with the original algorithm on sampled frames from 39 skeleton exports, along with synthetic edge cases. The results matched. Installation, repeat imports and restore were also tested.

For one animation, geometry generation dropped from about 6.7 ms to 0.7 ms in a separate CPU test. That measures this stage only, not the whole frame. Check your own animations and profile on your target hardware.

## License

The installer is MIT licensed; see [LICENSE](LICENSE). Spine has its own license, reproduced in [NOTICE.md](NOTICE.md).

This is an unofficial project and is not affiliated with Esoteric Software.
