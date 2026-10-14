using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace PyroDuck.EggHeadsUpgrade
{
    [Serializable] public sealed class GuidChange { public string oldGuid; public string newGuid; }
    [Serializable] public sealed class FolderMeta { public string relativePath; public string text; }
    [Serializable] public sealed class LegacyMap
    {
        public int schemaVersion;
        public GuidChange[] guidChanges;
        public FolderMeta[] fullFolderMetas;
        public string coreEditorAssembly;
        public string coreEditorAssemblyMeta;
    }

    public sealed class LegacyUpgradePlan
    {
        public readonly Dictionary<string, byte[]> Originals = new Dictionary<string, byte[]>();
        public readonly Dictionary<string, string> Replacements = new Dictionary<string, string>();
        public string ProjectRoot;
        public LegacyMap Map;

        public static string Transform(string text, LegacyMap map)
        {
            var lookup = map.guidChanges.ToDictionary(x => x.oldGuid, x => x.newGuid, StringComparer.Ordinal);
            string updated = Regex.Replace(text, @"\b[a-f0-9]{32}\b", m => lookup.TryGetValue(m.Value, out var value) ? value : m.Value);
            return updated.Replace("com.pyroduck.eggheadslite.Runtime", "com.pyroduck.eggheads.Runtime")
                .Replace("com.pyroduck.eggheadslite.Editor.EggHeadDatabaseInspector", "com.pyroduck.eggheads.Editor.EggHeadDatabaseInspector");
        }

        public static LegacyUpgradePlan Analyze(string projectRoot, LegacyMap map)
        {
            if (map == null || map.schemaVersion != 1 || map.guidChanges == null || map.fullFolderMetas == null)
                throw new InvalidOperationException("Upgrade map is missing or invalid.");
            foreach (var entry in map.guidChanges)
                if (!Regex.IsMatch(entry.oldGuid ?? "", "^[a-f0-9]{32}$") || !Regex.IsMatch(entry.newGuid ?? "", "^[a-f0-9]{32}$"))
                    throw new InvalidOperationException("Invalid GUID in upgrade map.");
            var allowedMetas = new HashSet<string> { "Assets/PyroDuck/EggHeads.meta", "Assets/PyroDuck/EggHeads/Runtime.meta", "Assets/PyroDuck/EggHeads/Editor.meta" };
            if (map.fullFolderMetas.Any(x => !allowedMetas.Contains(x.relativePath)))
                throw new InvalidOperationException("Invalid core folder metadata.");
            var plan = new LegacyUpgradePlan { ProjectRoot = Path.GetFullPath(projectRoot), Map = map };
            string legacyAssembly = plan.PathFor("Assets/PyroDuck/EggHeadsLite/Runtime/com.pyroduck.eggheadslite.Runtime.asmdef");
            if (!File.Exists(legacyAssembly)) throw new InvalidOperationException("This project already uses the shared core, or no legacy Lite runtime was found.");
            if (Directory.Exists(plan.PathFor("Assets/PyroDuck/EggHeads")))
                throw new InvalidOperationException("Prepare the legacy project BEFORE importing Full. Restore your pre-import backup if Full is already installed.");
            var extensions = new HashSet<string> { ".cs", ".asmdef", ".asmref", ".meta", ".prefab", ".unity", ".asset", ".anim", ".mat", ".controller", ".overrideController", ".playable", ".uxml", ".uss", ".json" };
            foreach (string file in Directory.GetFiles(plan.PathFor("Assets"), "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(plan.ProjectRoot.Length + 1).Replace('\\', '/');
                if (relative.StartsWith("Assets/PyroDuck/EggHeadsUpgradeTools/", StringComparison.Ordinal) || !extensions.Contains(Path.GetExtension(file))) continue;
                byte[] bytes = File.ReadAllBytes(file);
                // Unity binary serialization cannot be rewritten as text.
                if (Array.IndexOf(bytes, (byte)0) >= 0)
                {
                    bool inCurrentProject = string.Equals(plan.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(Path.GetDirectoryName(Application.dataPath)).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
                    bool dependsOnLite = inCurrentProject && AssetDatabase.GetDependencies(relative).Any(x => x.StartsWith("Assets/PyroDuck/EggHeadsLite/", StringComparison.Ordinal));
                    if (relative.StartsWith("Assets/PyroDuck/EggHeadsLite/", StringComparison.Ordinal) || Path.GetExtension(file) == ".prefab" || Path.GetExtension(file) == ".unity" || dependsOnLite)
                        throw new InvalidOperationException("Binary serialized Lite asset: " + relative + ". Re-save it as text before upgrading.");
                    continue;
                }
                string text = Encoding.UTF8.GetString(bytes);
                string updated = Transform(text, map);
                if (relative.EndsWith(".cs", StringComparison.Ordinal) && !relative.EndsWith("/EggHeadDatabaseInspector.cs", StringComparison.Ordinal))
                    updated = Regex.Replace(updated, @"(?<![\w.])EggHeadDatabaseInspector\b", "com.pyroduck.eggheads.Editor.EggHeadDatabaseInspector");
                if (relative.EndsWith("/com.pyroduck.eggheadslite.Editor.asmdef", StringComparison.Ordinal) && !updated.Contains("\"com.pyroduck.eggheads.Editor\""))
                    updated = updated.Replace("\"com.pyroduck.eggheads.Runtime\"", "\"com.pyroduck.eggheads.Runtime\", \"com.pyroduck.eggheads.Editor\"");
                if (updated == text) continue;
                plan.Originals.Add(relative, bytes);
                plan.Replacements.Add(relative, updated);
            }
            string settings = "ProjectSettings/EditorBuildSettings.asset";
            if (File.Exists(plan.PathFor(settings)))
            {
                var bytes = File.ReadAllBytes(plan.PathFor(settings));
                string text = Encoding.UTF8.GetString(bytes), updated = Transform(text, map);
                if (updated != text) { plan.Originals.Add(settings, bytes); plan.Replacements.Add(settings, updated); }
            }
            return plan;
        }

        public string PathFor(string relative)
        {
            string root = Path.GetFullPath(ProjectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Path outside project.");
            return path;
        }

        public string Apply()
        {
            if (Directory.Exists(PathFor("Assets/PyroDuck/EggHeads")))
                throw new InvalidOperationException("The shared core was installed after preview. Analyze again.");
            // A preview must never overwrite edits made since it was created.
            foreach (var original in Originals)
                if (!File.ReadAllBytes(PathFor(original.Key)).SequenceEqual(original.Value))
                    throw new InvalidOperationException("File changed since preview: " + original.Key + ". Analyze again.");
            string backup = PathFor("EggHeadsUpgradeBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(backup);
            // Back up all package files, not just rewritten files: the subsequent
            // package import can overwrite shared source code.
            CopyDirectory(PathFor("Assets/PyroDuck/EggHeadsLite"), Path.Combine(backup, "Assets/PyroDuck/EggHeadsLite"));
            var rootMeta = PathFor("Assets/PyroDuck/EggHeadsLite.meta");
            if (File.Exists(rootMeta)) File.Copy(rootMeta, Path.Combine(backup, "Assets/PyroDuck/EggHeadsLite.meta"));
            foreach (var original in Originals)
            {
                string target = Path.Combine(backup, original.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, original.Value);
            }
            var moves = new List<KeyValuePair<string, string>>();
            var created = new List<string>();
            var helperOriginals = new Dictionary<string, byte[]>();
            foreach (var name in new[] { "EggHeadDatabaseInspector.cs", "EggHeadsTmpResourcesInstaller.cs" })
            {
                string relative = "Assets/PyroDuck/EggHeadsLite/Editor/" + name;
                helperOriginals.Add(relative, File.ReadAllBytes(PathFor(relative)));
            }
            try
            {
                foreach (var edit in Replacements) File.WriteAllText(PathFor(edit.Key), edit.Value, new UTF8Encoding(false));
                foreach (var folder in Map.fullFolderMetas)
                {
                    string target = PathFor(folder.relativePath);
                    Directory.CreateDirectory(target.Substring(0, target.Length - 5));
                    File.WriteAllText(target, folder.text);
                    created.Add(target);
                }
                if (!string.IsNullOrEmpty(Map.coreEditorAssembly))
                {
                    foreach (var pair in new[] {
                        new KeyValuePair<string, string>("Assets/PyroDuck/EggHeads/Editor/EggHeadEditor.asmdef", Map.coreEditorAssembly),
                        new KeyValuePair<string, string>("Assets/PyroDuck/EggHeads/Editor/EggHeadEditor.asmdef.meta", Map.coreEditorAssemblyMeta) })
                    {
                        string target = PathFor(pair.Key);
                        File.WriteAllText(target, pair.Value);
                        created.Add(target);
                    }
                }
                Move("Assets/PyroDuck/EggHeadsLite/Runtime/Scripts", "Assets/PyroDuck/EggHeads/Runtime/Scripts", moves);
                Move("Assets/PyroDuck/EggHeadsLite/Runtime/Scripts.meta", "Assets/PyroDuck/EggHeads/Runtime/Scripts.meta", moves);
                foreach (var suffix in new[] { "", ".meta" })
                    Move("Assets/PyroDuck/EggHeadsLite/Runtime/com.pyroduck.eggheadslite.Runtime.asmdef" + suffix,
                        "Assets/PyroDuck/EggHeads/Runtime/com.pyroduck.eggheads.Runtime.asmdef" + suffix, moves);
                foreach (var name in new[] { "EggHeadDatabaseInspector.cs", "EggHeadsTmpResourcesInstaller.cs" })
                    foreach (var suffix in new[] { "", ".meta" })
                        Move("Assets/PyroDuck/EggHeadsLite/Editor/" + name + suffix, "Assets/PyroDuck/EggHeads/Editor/" + name + suffix, moves);
                // The shared editor helpers can live in Assembly-CSharp-Editor
                // until the updated Lite or Full package supplies its asmdef.
                foreach (var name in new[] { "EggHeadDatabaseInspector.cs", "EggHeadsTmpResourcesInstaller.cs" })
                {
                    string path = PathFor("Assets/PyroDuck/EggHeads/Editor/" + name);
                    File.WriteAllText(path, File.ReadAllText(path).Replace("com.pyroduck.eggheadslite.Editor", "com.pyroduck.eggheads.Editor"));
                }
                File.WriteAllText(Path.Combine(backup, "RESTORE.txt"),
                    "Close Unity. Remove the prepared Assets/PyroDuck/EggHeads shared-core folder, then copy this backup's Assets and ProjectSettings contents back into your project. Copy files without deleting unrelated project files. Restore before importing another package.\n");
                return backup;
            }
            catch
            {
                for (int i = moves.Count - 1; i >= 0; i--)
                {
                    var move = moves[i];
                    if (Directory.Exists(move.Value)) Directory.Move(move.Value, move.Key);
                    else if (File.Exists(move.Value)) File.Move(move.Value, move.Key);
                }
                foreach (var original in Originals) File.WriteAllBytes(PathFor(original.Key), original.Value);
                foreach (var original in helperOriginals) File.WriteAllBytes(PathFor(original.Key), original.Value);
                foreach (var file in created) if (File.Exists(file)) File.Delete(file);
                foreach (var relative in new[] { "Assets/PyroDuck/EggHeads/Editor", "Assets/PyroDuck/EggHeads/Runtime", "Assets/PyroDuck/EggHeads" })
                {
                    string directory = PathFor(relative);
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                }
                throw;
            }
        }

        private void Move(string source, string target, List<KeyValuePair<string, string>> moves)
        {
            string from = PathFor(source), to = PathFor(target);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to);
            moves.Add(new KeyValuePair<string, string>(from, to));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    public sealed class LegacyLiteUpgradeWindow : EditorWindow
    {
        [Serializable] private sealed class SavedScene { public string path; public bool isLoaded; public bool isActive; }
        [Serializable] private sealed class PendingScenes { public SavedScene[] scenes; }
        private const string PendingKey = "PyroDuck.EggHeadsUpgrade.PendingScenes";

        [InitializeOnLoadMethod]
        private static void ScheduleSceneRestore() { EditorApplication.delayCall += RestoreScenes; }

        private static void RestoreScenes()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += RestoreScenes; return; }
            string pending = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(pending)) return;
            SessionState.EraseString(PendingKey);
            var saved = JsonUtility.FromJson<PendingScenes>(pending);
            if (saved.scenes != null && saved.scenes.All(x => !string.IsNullOrEmpty(x.path)))
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(x => new SceneSetup { path = x.path, isLoaded = x.isLoaded, isActive = x.isActive }).ToArray());
        }
        private LegacyUpgradePlan _plan;
        private Label _report;
        private Button _apply;

        [MenuItem("Tools/PyroDuck/EggHeadsLite/Prepare Legacy Project for Full")]
        public static void ShowWindow() { GetWindow<LegacyLiteUpgradeWindow>("Lite to Full").minSize = new Vector2(500, 300); }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.Add(new HelpBox("For older Lite projects: run this BEFORE importing updated Lite or Full. New Lite projects already use the shared core and can import Full directly.", HelpBoxMessageType.Info));
            _report = new Label("Analyze to preview the source and serialized-reference changes.");
            _report.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(_report);
            rootVisualElement.Add(new Button(Analyze) { text = "Analyze Project" });
            _apply = new Button(Apply) { text = "Back Up and Prepare Project" };
            _apply.SetEnabled(false);
            rootVisualElement.Add(_apply);
        }

        private void Analyze()
        {
            try
            {
                string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
                var map = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/LegacyLiteMap.json");
                _plan = LegacyUpgradePlan.Analyze(Path.GetDirectoryName(Application.dataPath), JsonUtility.FromJson<LegacyMap>(map.text));
                _report.text = _plan.Replacements.Count + " files will be updated. Scene/prefab local IDs and custom asset GUIDs stay intact. A complete Lite folder backup is created outside Assets.";
                _apply.SetEnabled(true);
            }
            catch (Exception exception) { _plan = null; _apply.SetEnabled(false); _report.text = exception.Message; }
        }

        private void Apply()
        {
            if (_plan == null || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Analyze(); // Saving a scene may have changed the preview's snapshot.
            if (_plan == null) return;
            if (!EditorUtility.DisplayDialog("Prepare Lite project", _report.text + "\n\nContinue with the previewed changes?", "Back Up and Prepare", "Cancel")) return;
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            SessionState.SetString(PendingKey, JsonUtility.ToJson(new PendingScenes { scenes = scenes.Select(x => new SavedScene { path = x.path, isLoaded = x.isLoaded, isActive = x.isActive }).ToArray() }));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string backup = null;
            AssetDatabase.StartAssetEditing();
            try { backup = _plan.Apply(); }
            catch (Exception exception) { Debug.LogException(exception); _report.text = exception.Message; }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            ScheduleSceneRestore();
            _apply.SetEnabled(false);
            if (backup != null) EditorUtility.DisplayDialog("Project prepared", "Import the updated Lite or Full package next.\n\nBackup: " + backup, "OK");
        }
    }
}
