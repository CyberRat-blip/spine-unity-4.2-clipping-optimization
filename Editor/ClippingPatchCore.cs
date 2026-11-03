using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SpineClippingOptimization {
    public static class ClippingPatchCore {
        const string OldCondition = "if (useClipping && clipper.IsClipping) {";
        const string NewCondition = "if (useClipping && clipper.IsClipping\n\t\t\t\t\t&& !clipper.CanSkipClipping(workingVerts, attachmentVertexCount * 2)) {";
        const string Method = @"
		public bool CanSkipClipping (float[] worldVertices, int worldVerticesLength) {
			if (!IsClipping || clippingPolygons.Count != 1 || worldVerticesLength == 0) return false;

			ExposedList<float> polygon = clippingPolygons.Items[0];
			float[] maskVertices = polygon.Items;
			for (int edge = 0; edge < polygon.Count - 2; edge += 2) {
				float startX = maskVertices[edge], startY = maskVertices[edge + 1];
				float edgeX = startX - maskVertices[edge + 2];
				float edgeY = startY - maskVertices[edge + 3];

				for (int vertex = 0; vertex < worldVerticesLength; vertex += 2) {
					float pointX = worldVertices[vertex], pointY = worldVertices[vertex + 1];
					float sideOfEdge = edgeY * (startX - pointX) - edgeX * (startY - pointY);
					if (!(sideOfEdge > 0)) return false;
				}
			}
			return true;
		}
";

        public static string PatchSkeleton(string source) {
            RejectExistingPatch(source);
            Require(source, "public class SkeletonClipping");
            Require(source, "internal ExposedList<ExposedList<float>> clippingPolygons;");
            string anchor = "\t\tpublic int ClipStart (Slot slot, ClippingAttachment clip) {";
            return ReplaceOnce(source, anchor, WithLineEnding(Method + "\n", source) + anchor);
        }

        public static string PatchMeshGenerator(string source) {
            RejectExistingPatch(source);
            Require(source, "attachmentVertexCount = 4;");
            Require(source, "clipper.ClipTriangles(workingVerts, attachmentTriangleIndices, attachmentIndexCount, uvs);");
            return ReplaceOnce(source, OldCondition, WithLineEnding(NewCondition, source));
        }

        public static void ValidateVersion(string compatibilitySource) {
            if (!Regex.IsMatch(compatibilitySource,
                @"compatibleBinaryVersions\s*=\s*\{\s*new\[\]\s*\{\s*4\s*,\s*2\s*,\s*0\s*\}"))
                throw new InvalidOperationException("Only the Spine 4.2 runtime layout is supported. No files were changed.");
        }

        public static void Apply(string skeletonPath, string meshPath, string compatibilityPath, string backupDirectory) {
            if (Directory.Exists(backupDirectory))
                throw new InvalidOperationException("Backup already exists. Restore the previous patch before applying again: " + backupDirectory);
            ValidateVersion(File.ReadAllText(compatibilityPath));
            string[] targets = { skeletonPath, meshPath };
            byte[][] originals = { File.ReadAllBytes(skeletonPath), File.ReadAllBytes(meshPath) };
            byte[][] patched = {
                Encode(PatchSkeleton(Decode(originals[0])), originals[0]),
                Encode(PatchMeshGenerator(Decode(originals[1])), originals[1])
            };
            foreach (string target in targets)
                if ((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0)
                    throw new IOException("File is read-only: " + target);

            Directory.CreateDirectory(backupDirectory);
            var state = new string[7]; state[0] = "spine-clipping-optimization-v1";
            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(backupDirectory));
            for (int i = 0; i < 2; i++) {
                File.WriteAllBytes(Path.Combine(backupDirectory, i + ".original"), originals[i]);
                string relativePath = RelativeTarget(projectRoot, targets[i]);
                state[1 + i] = Convert.ToBase64String(Encoding.UTF8.GetBytes(relativePath));
                state[3 + i] = Hash(originals[i]); state[5 + i] = Hash(patched[i]);
            }
            File.WriteAllLines(Path.Combine(backupDirectory, "state.txt"), state);
            try {
                for (int i = 0; i < 2; i++) File.WriteAllBytes(targets[i], patched[i]);
            } catch {
                for (int i = 0; i < 2; i++) File.WriteAllBytes(targets[i], originals[i]);
                DeleteBackup(backupDirectory);
                throw;
            }
        }

        public static void Restore(string backupDirectory) {
            string[] state = File.ReadAllLines(Path.Combine(backupDirectory, "state.txt"));
            if (state.Length != 7 || state[0] != "spine-clipping-optimization-v1")
                throw new IOException("Unrecognized backup record. Restore using version control or your backup files.");
            var targets = new string[2]; var originals = new byte[2][];
            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(backupDirectory));
            for (int i = 0; i < 2; i++) {
                string relativePath = Encoding.UTF8.GetString(Convert.FromBase64String(state[1 + i]));
                targets[i] = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
                RelativeTarget(projectRoot, targets[i]);
                originals[i] = File.ReadAllBytes(Path.Combine(backupDirectory, i + ".original"));
                if (Hash(originals[i]) != state[3 + i]) throw new IOException("Backup checksum mismatch.");
                string currentHash = Hash(File.ReadAllBytes(targets[i]));
                if (currentHash != state[3 + i] && currentHash != state[5 + i])
                    throw new IOException("Runtime source changed since installation. Automatic restore refused: " + targets[i]);
                if ((File.GetAttributes(targets[i]) & FileAttributes.ReadOnly) != 0)
                    throw new IOException("File is read-only: " + targets[i]);
            }
            for (int i = 0; i < 2; i++) File.WriteAllBytes(targets[i], originals[i]);
            DeleteBackup(backupDirectory);
        }

        static void DeleteBackup(string directory) {
            File.Delete(Path.Combine(directory, "0.original"));
            File.Delete(Path.Combine(directory, "1.original"));
            File.Delete(Path.Combine(directory, "state.txt"));
            Directory.Delete(directory, false);
        }
        static string RelativeTarget(string projectRoot, string target) {
            string prefix = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(target);
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison)) throw new IOException("Runtime source must be inside the backed-up project.");
            return fullPath.Substring(prefix.Length);
        }
        static void RejectExistingPatch(string source) {
            if (source.Contains("CanSkipClipping") || source.Contains("IsEntirelyInside"))
                throw new InvalidOperationException("Clipping optimization already present or manually modified. No files were changed.");
        }
        static void Require(string source, string text) {
            if (!source.Contains(text)) throw new InvalidOperationException("Unsupported runtime source layout: " + text);
        }
        static string ReplaceOnce(string source, string oldText, string newText) {
            int index = source.IndexOf(oldText, StringComparison.Ordinal);
            if (index < 0 || source.IndexOf(oldText, index + oldText.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Expected exactly one patch location. No files were changed.");
            return source.Substring(0, index) + newText + source.Substring(index + oldText.Length);
        }
        static string LineEnding(string source) { return source.Contains("\r\n") ? "\r\n" : "\n"; }
        static string WithLineEnding(string text, string source) { return text.Replace("\r\n", "\n").Replace("\n", LineEnding(source)); }
        static string Decode(byte[] bytes) {
            string text = new UTF8Encoding(false, true).GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }
        static byte[] Encode(string text, byte[] original) {
            bool bom = original.Length >= 3 && original[0] == 239 && original[1] == 187 && original[2] == 191;
            var encoding = new UTF8Encoding(bom, true);
            byte[] body = encoding.GetBytes(text), prefix = encoding.GetPreamble();
            byte[] bytes = new byte[prefix.Length + body.Length];
            Buffer.BlockCopy(prefix, 0, bytes, 0, prefix.Length); Buffer.BlockCopy(body, 0, bytes, prefix.Length, body.Length);
            return bytes;
        }
        static string Hash(byte[] bytes) {
            using (var algorithm = SHA256.Create()) return Convert.ToBase64String(algorithm.ComputeHash(bytes));
        }
    }
}
