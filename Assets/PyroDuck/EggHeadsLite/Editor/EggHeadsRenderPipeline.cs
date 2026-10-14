using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using com.pyroduck.eggheadslite.Rendering;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace com.pyroduck.eggheadslite.Editor
{
    // No URP/HDRP assembly references: this menu also compiles in Built-in projects.
    // Holds transient state in a ScriptableSingleton so the static class has no static fields.
    internal sealed class PipelineState : ScriptableSingleton<PipelineState>
    {
        [SerializeField] public AddRequest Install;
        [SerializeField] public bool Applying;
    }

    [InitializeOnLoad]
    public static class EggHeadsRenderPipeline
    {
        public enum Target { BuiltIn, URP, HDRP }
        public const string GeneratedRoot = "Assets/PyroDuck/EggHeadsPipelineSettings";
        private const string PendingKey = "PyroDuck.EggHeads.Pipeline.Pending";
        private const string BusyKey = "PyroDuck.EggHeads.Pipeline.Busy";
        private const string LastBackupKey = "PyroDuck.EggHeads.Pipeline.LastBackup";
        private const string PreviousColorKey = "PyroDuck.EggHeads.Pipeline.PreviousColorSpace";
        private const string Menu = "Tools/PyroDuck/Render Pipeline/";
        private static string[] ContentRoots() { return new[] { "Assets/PyroDuck/EggHeadsLite", "Assets/PyroDuck/EggHeads" }; }
        private const BindingFlags StaticMethods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        static EggHeadsRenderPipeline()
        {
            // Unsubscribe first so repeated static construction never double-registers.
            EditorApplication.update -= Resume;
            EditorApplication.update += Resume;
        }

        [MenuItem(Menu + "Make URP", false, 100)] private static void MakeURP() { Begin(Target.URP); }
        [MenuItem(Menu + "Make HDRP", false, 101)] private static void MakeHDRP() { Begin(Target.HDRP); }
        [MenuItem(Menu + "Make Built-in", false, 102)] private static void MakeBuiltIn() { Begin(Target.BuiltIn); }
        [MenuItem(Menu + "Restore Last Switch", false, 120)] private static void RestoreLast()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            { Notify("Close Prefab Mode before restoring render pipelines.", true); return; }
            var backup = EditorPrefs.GetString(LastBackupKey + Application.dataPath, "");
            if (!Directory.Exists(backup)) { Notify("No pipeline switch backup is available in this project.", true); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RestoreBackup(backup);
            Notify("Restored project settings and EggHeads assets from:\n" + backup, false);
        }

        [MenuItem(Menu + "Make URP", true)]
        [MenuItem(Menu + "Make HDRP", true)]
        [MenuItem(Menu + "Make Built-in", true)]
        [MenuItem(Menu + "Restore Last Switch", true)]
        private static bool CanSwitch() { return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !SessionState.GetBool(BusyKey, false); }

        private static void Begin(Target target)
        {
            if (!CanSwitch() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // A saved scene is necessary to reopen it after changing serialized sample assets.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                { Notify("Save or close untitled scenes before switching render pipelines.", true); return; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            { Notify("Close Prefab Mode before switching render pipelines.", true); return; }
            if (target == Target.HDRP && !SystemInfo.supportsComputeShaders)
            { Notify("HDRP requires a graphics device with compute shader support.", true); return; }
            SessionState.SetInt(PendingKey, (int)target);
            SessionState.SetBool(BusyKey, true);
            Resume();
        }

        private static void Resume()
        {
            if (PipelineState.instance.Applying || !SessionState.GetBool(BusyKey, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var target = (Target)SessionState.GetInt(PendingKey, 0);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PipelineState.instance.Install != null)
            {
                if (!PipelineState.instance.Install.IsCompleted) return;
                if (PipelineState.instance.Install.Status == StatusCode.Failure)
                { var error = PipelineState.instance.Install.Error.message; PipelineState.instance.Install = null; Finish(); Notify("Package installation failed. Pipeline and EggHeads assets were not changed.\n" + error, true); return; }
                PipelineState.instance.Install = null;
                AssetDatabase.Refresh();
                return; // Let package import and domain reload complete.
            }
            // Stock Lite prefabs refer to URP package materials. Resolve those originals
            // before converting, even when the destination is Built-in or HDRP.
            var registered = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            string package = !registered.Any(p => p.name == "com.unity.render-pipelines.universal")
                ? "com.unity.render-pipelines.universal" : PackageName(target);
            if (package != null && !UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == package))
            {
                // Match the installed SRP Core version, instead of fetching an incompatible latest package.
                var core = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().FirstOrDefault(p => p.name == "com.unity.render-pipelines.core");
                string id = package + (core != null ? "@" + core.version : "");
                Debug.Log("[PyroDuck] Installing " + id + ". The pipeline switch resumes after compilation.");
                try { PipelineState.instance.Install = Client.Add(id); }
                catch (Exception error) { Finish(); Notify("Could not start package installation.\n" + error.Message, true); }
                return;
            }
            PipelineState.instance.Applying = true;
            string backup = null;
            try
            {
                backup = CreateBackup();
                Apply(target);
                EditorPrefs.SetString(LastBackupKey + Application.dataPath, backup);
                Notify("EggHeads is configured for " + target + ".\nBackup: " + backup +
                       "\nOnly stock EggHeads materials and EggHeads sample cameras were converted. Check custom scene lighting and materials separately.", false);
            }
            catch (Exception ex)
            {
                if (backup != null)
                {
                    try { RestoreBackup(backup); }
                    catch (Exception restoreError) { Debug.LogError("[PyroDuck] Restore failed. Backup: " + backup + "\n" + restoreError); }
                }
                Debug.LogException(ex);
                Notify("Pipeline switch failed; rollback was attempted.\n" + ex.Message + "\nBackup: " + backup, true);
            }
            finally { PipelineState.instance.Applying = false; Finish(); }
        }

        private static void Finish() { SessionState.SetBool(BusyKey, false); }
        private static string PackageName(Target target)
        { return target == Target.URP ? "com.unity.render-pipelines.universal" : target == Target.HDRP ? "com.unity.render-pipelines.high-definition" : null; }
        private static void Notify(string message, bool error)
        {
            if (error) Debug.LogError("[PyroDuck] " + message); else Debug.Log("[PyroDuck] " + message);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("PyroDuck Render Pipeline", message, "OK");
        }

        public static Target CurrentTarget
        {
            get
            {
                var pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (pipeline == null) return Target.BuiltIn;
                return pipeline.GetType().Name.Contains("HDRenderPipeline") ? Target.HDRP : Target.URP;
            }
        }

        public static bool IsKnownPipeline
        {
            get
            {
                var pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                return pipeline == null || pipeline.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset" ||
                       pipeline.GetType().FullName == "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset";
            }
        }

        public static Shader PreviewShader()
        {
            if (!IsKnownPipeline) return null;
            return Shader.Find(CurrentTarget == Target.BuiltIn ? "Sprites/Default" :
                CurrentTarget == Target.URP ? "Universal Render Pipeline/2D/Sprite-Unlit-Default" : "PyroDuck/EggHeads/HDRP Sprite Unlit");
        }

        // Public for isolated-project validation. Menu entry points handle package installation.
        public static void Apply(Target target)
        {
            bool wasHDRP = IsKnownPipeline && CurrentTarget == Target.HDRP;
            var previousColorSpace = PlayerSettings.colorSpace;
            EnsureFolder(GeneratedRoot);
            var pipeline = GetOrCreatePipeline(target);
            var spriteShader = GetSpriteShader(target);
            var particleShader = target == Target.BuiltIn ? Shader.Find("Particles/Standard Unlit") :
                target == Target.URP ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : spriteShader;
            if (spriteShader == null || particleShader == null) throw new InvalidOperationException("Required sprite/particle shaders are unavailable for " + target);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                spriteShader = GetSpriteShader(target);
                if (target == Target.HDRP) particleShader = spriteShader;
                foreach (var path in OrderedPrefabs())
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (ConvertRenderers(root, target, spriteShader, particleShader) > 0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                            if (!saved) throw new IOException("Could not save prefab: " + path);
                        }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", ExistingRoots()))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    spriteShader = GetSpriteShader(target);
                    particleShader = target == Target.HDRP ? spriteShader : Shader.Find(target == Target.URP ? "Universal Render Pipeline/Particles/Unlit" : "Particles/Standard Unlit");
                    bool changed = false;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        changed |= ConvertRenderers(root, target, spriteShader, particleShader) > 0;
                        foreach (var camera in root.GetComponentsInChildren<Camera>(true)) changed |= ConfigureCamera(camera, target);
                    }
                    if (changed && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save scene: " + path);
                }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Closing scenes can unload assets held only by local C# variables.
                // Resolve the persistent pipeline again immediately before assigning it.
                pipeline = GetOrCreatePipeline(target);
                SetPipelineForAllQualityLevels(pipeline);
                if (target == Target.HDRP)
                {
                    if (!wasHDRP) EditorPrefs.SetInt(PreviousColorKey + Application.dataPath, (int)previousColorSpace);
                    PlayerSettings.colorSpace = ColorSpace.Linear;
                }
                else if (wasHDRP && EditorPrefs.HasKey(PreviousColorKey + Application.dataPath))
                    PlayerSettings.colorSpace = (ColorSpace)EditorPrefs.GetInt(PreviousColorKey + Application.dataPath);
                AssetDatabase.SaveAssets();
                if (GraphicsSettings.defaultRenderPipeline != pipeline || QualitySettings.renderPipeline != pipeline || CurrentTarget != target)
                    throw new InvalidOperationException("Unity did not retain the selected pipeline settings.");
                Debug.Log("[PyroDuck] Pipeline settings: Graphics=" + GraphicsSettings.defaultRenderPipeline + ", Quality=" + QualitySettings.renderPipeline);
            }
            finally { RestoreScenes(setup); }
        }

        public static void SetPipelineForAllQualityLevels(RenderPipelineAsset pipeline)
        {
            // Write the serialized setting, without temporarily changing the user's quality level.
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            quality.ApplyModifiedPropertiesWithoutUndo();
            // Update Unity's active quality cache as well as the serialized levels.
            QualitySettings.renderPipeline = pipeline;
            GraphicsSettings.defaultRenderPipeline = pipeline;
        }

        private static RenderPipelineAsset GetOrCreatePipeline(Target target)
        {
            if (target == Target.BuiltIn) return null;
            var type = FindType(target == Target.URP ? "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset" : "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset");
            if (type == null) throw new InvalidOperationException("Install " + PackageName(target) + " before switching.");
            string path = GeneratedRoot + "/EggHeads" + target + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (existing != null) return existing;
            RenderPipelineAsset asset;
            if (target == Target.URP)
            {
                var rendererType = FindType("UnityEngine.Rendering.Universal.Renderer2DData");
                if (rendererType == null) throw new InvalidOperationException("URP 2D Renderer is unavailable.");
                string rendererPath = GeneratedRoot + "/EggHeadsRenderer2D.asset";
                var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance(rendererType);
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                    ReloadResources(renderer, "Packages/com.unity.render-pipelines.universal");
                }
                var create = type.GetMethods(StaticMethods).FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 1);
                if (create == null) throw new MissingMethodException(type.FullName, "Create(rendererData)");
                asset = (RenderPipelineAsset)create.Invoke(null, new object[] { renderer });
            }
            else asset = (RenderPipelineAsset)ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ReloadResources(Object asset, string packagePath)
        {
            var reloader = FindType("UnityEngine.Rendering.ResourceReloader");
            var method = reloader?.GetMethods(StaticMethods).FirstOrDefault(m => m.Name == "ReloadAllNullIn" && m.GetParameters().Length == 2);
            method?.Invoke(null, new object[] { asset, packagePath });
            EditorUtility.SetDirty(asset);
        }

        private static Shader GetSpriteShader(Target target)
        {
            if (target == Target.BuiltIn) return Shader.Find("Sprites/Default");
            if (target == Target.URP) return Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            string path = GeneratedRoot + "/EggHeadsHDRPSprite.shader";
            string scriptPath = AssetDatabase.FindAssets("EggHeadsHDRPSprite").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith(".shader.txt", StringComparison.Ordinal));
            if (scriptPath == null) throw new FileNotFoundException("EggHeads HDRP shader template is missing.");
            if (!File.Exists(path) || File.ReadAllText(path) != File.ReadAllText(scriptPath))
            {
                File.Copy(scriptPath, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("EggHeads HDRP sprite shader could not compile.");
            return shader;
        }

        public static int ConvertRenderers(GameObject root, Target target, Shader spriteShader, Shader particleShader)
        {
            int changed = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SpriteRenderer) && !(renderer is ParticleSystemRenderer)) continue;
                var materials = renderer.sharedMaterials;
                bool dirty = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var replacement = ConvertMaterial(materials[i], target, renderer is SpriteRenderer ? spriteShader : particleShader);
                    if (replacement == materials[i]) continue;
                    materials[i] = replacement; dirty = true;
                }
                if (dirty)
                {
                    renderer.sharedMaterials = materials;
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    changed++;
                }
                if (renderer is SpriteRenderer sprite)
                {
                    var adapter = sprite.GetComponent<EggHeadsSpritePipelineAdapter>();
                    bool needsAdapter = target == Target.HDRP && sprite.sharedMaterial != null && sprite.sharedMaterial.shader.name == EggHeadsSpritePipelineAdapter.ShaderName;
                    if (needsAdapter && adapter == null) { adapter = sprite.gameObject.AddComponent<EggHeadsSpritePipelineAdapter>(); changed++; }
                    else if (!needsAdapter && adapter != null) { Object.DestroyImmediate(adapter); changed++; }
                    if (needsAdapter) adapter.ApplyProperties();
                }
            }
            return changed;
        }

        public static bool IsStockMaterial(Material material)
        {
            if (material == null || material.shader == null) return false;
            string path = AssetDatabase.GetAssetPath(material);
            if (path.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal)) return true;
            // Preserve custom materials even when they use a Unity stock shader.
            bool unityAsset = path.StartsWith("Packages/", StringComparison.Ordinal) || path.StartsWith("Resources/unity_builtin", StringComparison.Ordinal) || string.IsNullOrEmpty(path);
            bool supplied = ContentRoots().Any(r => path.StartsWith(r + "/", StringComparison.Ordinal));
            if (!unityAsset && !supplied) return false;
            string shader = material.shader.name;
            return shader == "Sprites/Default" || shader == "Universal Render Pipeline/2D/Sprite-Lit-Default" ||
                   shader == "Universal Render Pipeline/2D/Sprite-Unlit-Default" || shader.StartsWith("Particles/", StringComparison.Ordinal) ||
                   shader == "Legacy Shaders/Particles/Alpha Blended" || shader == "Universal Render Pipeline/Particles/Unlit";
        }

        [Serializable] private class MaterialOrigin { public string guid; public long localId; public string path; }
        public static Material ConvertMaterial(Material source, Target target, Shader shader)
        {
            if (!IsStockMaterial(source)) return source;
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            var origin = new MaterialOrigin();
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal))
            {
                string record = sourcePath + ".origin.json";
                if (!File.Exists(record)) return source; // Never guess an original material.
                origin = JsonUtility.FromJson<MaterialOrigin>(File.ReadAllText(record));
                source = AssetDatabase.LoadAllAssetsAtPath(origin.path).OfType<Material>().FirstOrDefault(m =>
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string guid, out long id) && guid == origin.guid && id == origin.localId);
                if (source == null) throw new InvalidOperationException("Original material is missing: " + origin.path);
            }
            else
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out origin.guid, out origin.localId);
                origin.path = sourcePath;
            }
            if (source.shader == shader) return source;
            string path = GeneratedRoot + "/" + origin.guid + "_" + origin.localId + "_" + target + ".mat";
            var converted = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = converted == null;
            EnsureFolder(GeneratedRoot);
            if (isNew) converted = new Material(shader) { name = source.name + " (" + target + ")" };
            else converted.shader = shader;
            string sourceTexture = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            string targetTexture = converted.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (source.HasProperty(sourceTexture) && converted.HasProperty(targetTexture))
            {
                converted.SetTexture(targetTexture, source.GetTexture(sourceTexture));
                converted.SetTextureScale(targetTexture, source.GetTextureScale(sourceTexture));
                converted.SetTextureOffset(targetTexture, source.GetTextureOffset(sourceTexture));
            }
            string targetColor = converted.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            string sourceColor = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_TintColor") ? "_TintColor" : "_Color";
            if (source.HasProperty(sourceColor) && converted.HasProperty(targetColor)) converted.SetColor(targetColor, source.GetColor(sourceColor));
            if (converted.HasProperty("_Surface"))
            {
                converted.SetFloat("_Surface", 1);
                converted.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                converted.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                converted.SetFloat("_ZWrite", 0);
                converted.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                converted.SetOverrideTag("RenderType", "Transparent");
                converted.renderQueue = (int)RenderQueue.Transparent;
            }
            if (converted.HasProperty("_Mode")) // Built-in Standard particle shader.
            {
                converted.SetFloat("_Mode", 2);
                converted.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                converted.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                converted.SetFloat("_ZWrite", 0);
                converted.EnableKeyword("_ALPHABLEND_ON");
                converted.SetOverrideTag("RenderType", "Transparent");
                converted.renderQueue = (int)RenderQueue.Transparent;
            }
            if (isNew) AssetDatabase.CreateAsset(converted, path);
            else EditorUtility.SetDirty(converted);
            string recordPath = path + ".origin.json";
            string recordJson = JsonUtility.ToJson(origin);
            if (!File.Exists(recordPath) || File.ReadAllText(recordPath) != recordJson)
            {
                File.WriteAllText(recordPath, recordJson);
                AssetDatabase.ImportAsset(recordPath);
            }
            return converted;
        }

        public static bool ConfigureCamera(Camera camera, Target target)
        {
            bool changed = false;
            foreach (string typeName in new[] { "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData", "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData" })
            {
                var type = FindType(typeName);
                if (type == null) continue;
                var component = camera.GetComponent(type) as Behaviour;
                bool active = target == Target.URP ? typeName.Contains("Universal") : target == Target.HDRP && typeName.Contains("HighDefinition");
                if (component == null && active) { component = camera.gameObject.AddComponent(type) as Behaviour; changed = true; }
                if (component == null) continue;
                if (component.enabled != active) { component.enabled = active; changed = true; }
                if (active && target == Target.URP)
                {
                    var serialized = new SerializedObject(component);
                    var index = serialized.FindProperty("m_RendererIndex");
                    if (index != null && index.intValue != -1) { index.intValue = -1; serialized.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
                }
                if (active && target == Target.HDRP)
                {
                    var serialized = new SerializedObject(component);
                    var mode = serialized.FindProperty("clearColorMode");
                    var color = serialized.FindProperty("backgroundColorHDR");
                    if (mode != null && mode.intValue != 1) { mode.intValue = 1; changed = true; }
                    if (color != null && color.colorValue != camera.backgroundColor) { color.colorValue = camera.backgroundColor; changed = true; }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            return changed;
        }

        private static Type FindType(string name)
        { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null); }
        private static string[] ExistingRoots() { return ContentRoots().Where(AssetDatabase.IsValidFolder).ToArray(); }
        private static IEnumerable<string> OrderedPrefabs()
        {
            // Convert bases/nested prefabs before their dependents, avoiding redundant variant overrides.
            var paths = new HashSet<string>(AssetDatabase.FindAssets("t:Prefab", ExistingRoots()).Select(AssetDatabase.GUIDToAssetPath));
            var visited = new HashSet<string>();
            var result = new List<string>();
            foreach (var path in paths.OrderBy(p => p, StringComparer.Ordinal)) VisitPrefab(path, paths, visited, result);
            return result;
        }
        private static void VisitPrefab(string path, HashSet<string> owned, HashSet<string> visited, List<string> result)
        {
            if (!visited.Add(path)) return;
            foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                if (owned.Contains(dependency)) VisitPrefab(dependency, owned, visited, result);
            result.Add(path);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static string CreateBackup()
        {
            AssetDatabase.SaveAssets();
            string path = Path.GetFullPath("PyroDuckBackups/RenderPipeline/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(path);
            var qualitySettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qualitySettings.FindProperty("m_QualitySettings");
            var snapshot = new SettingsSnapshot { graphicsGuid = PipelineGuid(GraphicsSettings.defaultRenderPipeline), activeQuality = QualitySettings.GetQualityLevel(), colorSpace = PlayerSettings.colorSpace, qualityGuids = new string[levels.arraySize] };
            for (int i = 0; i < levels.arraySize; i++)
                snapshot.qualityGuids[i] = PipelineGuid(levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue as RenderPipelineAsset);
            File.WriteAllText(Path.Combine(path, "pipeline-settings.json"), JsonUtility.ToJson(snapshot));
            var files = Directory.GetFiles("ProjectSettings", "*", SearchOption.AllDirectories)
                .Concat(ExistingRoots().SelectMany(r => Directory.GetFiles(r, "*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".prefab", StringComparison.Ordinal) || f.EndsWith(".unity", StringComparison.Ordinal))));
            foreach (var file in files)
            {
                string destination = Path.Combine(path, file);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }
            return path;
        }

        public static void RestoreBackup(string path)
        {
            string root = Path.GetFullPath("PyroDuckBackups/RenderPipeline") + Path.DirectorySeparatorChar;
            path = Path.GetFullPath(path);
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path))
                throw new InvalidOperationException("Not a pipeline backup belonging to this project.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(path.Length + 1);
                    if (relative == "pipeline-settings.json") continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(relative));
                    File.Copy(file, relative, true);
                }
                // Import settings as well as assets so Unity's in-memory settings match the backup.
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (string file in Directory.GetFiles("ProjectSettings", "*", SearchOption.TopDirectoryOnly))
                    AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                string settingsFile = Path.Combine(path, "pipeline-settings.json");
                if (File.Exists(settingsFile))
                {
                    var snapshot = JsonUtility.FromJson<SettingsSnapshot>(File.ReadAllText(settingsFile));
                    var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                    var levels = quality.FindProperty("m_QualitySettings");
                    for (int i = 0; i < levels.arraySize && i < snapshot.qualityGuids.Length; i++)
                        levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = ResolvePipeline(snapshot.qualityGuids[i]);
                    quality.ApplyModifiedPropertiesWithoutUndo();
                    QualitySettings.SetQualityLevel(snapshot.activeQuality, false);
                    QualitySettings.renderPipeline = ResolvePipeline(snapshot.qualityGuids[snapshot.activeQuality]);
                    GraphicsSettings.defaultRenderPipeline = ResolvePipeline(snapshot.graphicsGuid);
                    PlayerSettings.colorSpace = snapshot.colorSpace;
                }
            }
            finally { RestoreScenes(setup); }
        }

        private static void RestoreScenes(SceneSetup[] setup)
        {
            if (setup.Length > 0 && setup.Any(s => s.isActive && s.isLoaded && !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Serializable] private class SettingsSnapshot
        {
            public string graphicsGuid;
            public string[] qualityGuids;
            public int activeQuality;
            public ColorSpace colorSpace;
        }
        private static string PipelineGuid(RenderPipelineAsset pipeline)
        { return pipeline != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pipeline)) : ""; }
        private static RenderPipelineAsset ResolvePipeline(string guid)
        { return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid)); }
    }
}
