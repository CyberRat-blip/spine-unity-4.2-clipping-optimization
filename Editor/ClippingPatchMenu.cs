using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpineClippingOptimization {
    [InitializeOnLoad]
    public static class ClippingPatchMenu {
        const string Version = "0.2.0";
        const string PendingImport = "SpineClippingOptimization.PendingImport";
        static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
        static string BackupDirectory { get { return Path.Combine(ProjectRoot, ".spine-clipping-optimization"); } }
        static string ReceiptPath { get { return Path.Combine(ProjectRoot, ".spine-clipping-optimization-install.txt"); } }

        static ClippingPatchMenu() { QueueInstallCheck(); }

        internal static void RequestAutoInstall() {
            SessionState.SetBool(PendingImport, true);
            QueueInstallCheck();
        }

        internal static void QueueInstallCheck() {
            EditorApplication.update -= InstallWhenReady;
            EditorApplication.update += InstallWhenReady;
        }

        static void InstallWhenReady() {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= InstallWhenReady;
            bool importedAgain = SessionState.GetBool(PendingImport, false);
            SessionState.EraseBool(PendingImport);
            if (!importedAgain && File.Exists(ReceiptPath) && File.ReadAllText(ReceiptPath) == Version) return;
            try {
                File.WriteAllText(ReceiptPath, Version);
                if (Directory.Exists(BackupDirectory)) {
                    Debug.Log("[Spine Clipping Optimization] Existing backup found; no new changes. Use Restore Original Files before repatching or updating Spine.");
                    return;
                }
                string[] candidates = Directory.GetFiles(Application.dataPath, "SkeletonClipping.cs", SearchOption.AllDirectories);
                if (candidates.Length != 1)
                    throw new InvalidOperationException("Expected one editable Spine 4.2 installation under Assets; found " + candidates.Length +
                        ". UPM/PackageCache installations are not supported. Install Spine first, then reimport this package.");
                string skeletonPath = candidates[0];
                string runtimeDirectory = Directory.GetParent(Path.GetDirectoryName(skeletonPath)).FullName;
                string meshPath = Path.Combine(runtimeDirectory, "spine-unity", "Mesh Generation", "MeshGenerator.cs");
                string compatibilityPath = Path.Combine(runtimeDirectory, "spine-unity", "Asset Types", "SkeletonDataCompatibility.cs");
                if (!File.Exists(meshPath) || !File.Exists(compatibilityPath))
                    throw new InvalidOperationException("Unsupported Spine folder layout. Expected sibling spine-csharp and spine-unity folders under Runtime.");
                if (File.ReadAllText(skeletonPath).Contains("CanSkipClipping") && File.ReadAllText(meshPath).Contains("CanSkipClipping")) {
                    Debug.Log("[Spine Clipping Optimization] An existing clipping patch was detected. No files were changed; this installer has no backup to restore it.");
                    return;
                }
                ClippingPatchCore.Apply(skeletonPath, meshPath, compatibilityPath, BackupDirectory);
                Debug.Log("[Spine Clipping Optimization] Patch applied automatically. Wait for recompilation and compare performance with Use Clipping enabled.");
                AssetDatabase.Refresh();
            } catch (Exception exception) {
                Debug.LogWarning("[Spine Clipping Optimization] Automatic installation stopped: " + exception.Message +
                    " Fix the issue, then reimport the .unitypackage to try again.");
            }
        }

        [MenuItem("Tools/Spine Clipping Optimization/Restore Original Files")]
        public static void Restore() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) {
                EditorUtility.DisplayDialog("Spine Clipping Optimization", "Exit Play Mode and wait for compilation/import to finish before restoring.", "OK");
                return;
            }
            try {
                File.WriteAllText(ReceiptPath, Version);
                SessionState.EraseBool(PendingImport);
                ClippingPatchCore.Restore(BackupDirectory);
                Debug.Log("[Spine Clipping Optimization] Original files restored. Automatic installation is paused until you reimport the package.");
                AssetDatabase.Refresh();
            } catch (Exception exception) {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Spine Clipping Optimization", exception.Message, "OK");
            }
        }
    }

    public sealed class ClippingPatchImportListener : AssetPostprocessor {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            foreach (string asset in importedAssets) {
                if (!asset.EndsWith("/SpineClippingOptimization/Editor/ClippingPatchMenu.cs", StringComparison.Ordinal)) continue;
                ClippingPatchMenu.RequestAutoInstall();
                break;
            }
        }
    }
}
