using System;
using System.Collections.Generic;
using System.IO;
using com.pyroduck.eggheads.Editor;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace com.pyroduck.eggheadslite.Editor
{
    public sealed partial class EggHeadsLiteCharacterPrefabGenerator
    {
        private Button _addToSceneButton;
        private Label _generationStatus;

        private void OnGeneratorUndo()
        {
            RefreshPreviewIfReady();
            if (_settingsPanel != null) BuildSettingsUI();
        }

        private void RecordChange(string label)
        {
            Undo.RecordObject(this, label);
        }

        private void UpdateGenerateButtons()
        {
            bool ready = characterBase != null && !string.IsNullOrWhiteSpace(prefabName) && !EditorApplication.isPlayingOrWillChangePlaymode;
            _generateButton?.SetEnabled(ready);
            _addToSceneButton?.SetEnabled(ready && PrefabStageUtility.GetCurrentPrefabStage() == null);
        }

        private void ResetCharacterDefaults()
        {
            RecordChange("Reset character defaults");
            characterBase = body = eyes = eyebrows = nose = mouth = beardMustache = hairOrHat = weapon = null;
            eyesColor = eyebrowsColor = noseColor = mouthColor = beardMustacheColor = hairOrHatColor = weaponColor = Color.white;
            LoadDefaults();
        }

        private static Button ActionButton(VisualElement parent, string title, Action action, string style = "secondary-button")
        {
            var button = new Button(action) { text = title };
            button.AddToClassList(style);
            parent.Add(button);
            return button;
        }

        private void BuildQuickStart()
        {
            var quick = SettingsCard("Quick Start", "1. Choose a look   2. Customize parts   3. Generate");
            var row = new VisualElement();
            row.AddToClassList("button-row");
            quick.Add(row);
            ActionButton(row, "Open Playable Demo", OpenDemo);
            ActionButton(row, "Getting Started", () => OpenDocumentation("QuickStart.md"));
            ActionButton(quick, "Check Setup", () => ShowSetupReport(quick));
            var presets = new Foldout { text = "Start from a preset", value = false };
            quick.Add(presets);
            foreach (var name in new[] { "PistolHatMan", "SwordPunkMan", "OldBomberMan" })
            {
                var prefab = LoadAsset("Runtime/Prefabs/Characters/" + name + ".prefab");
                if (prefab == null) continue;
                ActionButton(presets, ObjectNames.NicifyVariableName(name), () => { ApplyPreset(prefab); BuildSettingsUI(); });
            }
            var history = new VisualElement();
            history.AddToClassList("button-row");
            quick.Add(history);
            ActionButton(history, "Undo", Undo.PerformUndo);
            ActionButton(history, "Redo", Undo.PerformRedo);
        }

        private static string AssetPath(string relative)
        {
            var assets = "Assets/PyroDuck/EggHeadsLite/" + relative;
            return File.Exists(assets) ? assets : "Packages/" + PackageName + "/" + relative;
        }

        internal static void OpenDocumentation(string file)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath("Documentation/" + file));
            if (asset != null) AssetDatabase.OpenAsset(asset);
        }

        internal static void OpenDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = AssetPath("Samples/Platformer/Scenes/Platformer.unity");
            if (!File.Exists(path)) { EditorUtility.DisplayDialog("Demo unavailable", "Import the Platformer sample to open the demo.", "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        private void ShowSetupReport(VisualElement card)
        {
            card.Q<HelpBox>("setup-report")?.RemoveFromHierarchy();
            var messages = GetSetupIssues();
            var report = new HelpBox(messages.Count == 0 ? "Generator is ready. Open the demo and press Play to try movement and weapons." : string.Join("\n", messages),
                messages.Count == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning) { name = "setup-report" };
            card.Add(report);
        }

        internal List<string> GetSetupIssues()
        {
            var issues = new List<string>();
            if (characterBase == null) issues.Add("Assign a Character Base or use Load Defaults.");
            else if (characterBase.GetComponent<CharacterVisualController>() == null) issues.Add("Character Base needs a CharacterVisualController.");
            if (body == null) issues.Add("Choose a Body for your character.");
            if (database == null) issues.Add("Assign the included EggHeadDatabase to browse parts and register prefabs.");
            else
            {
                var report = EggHeadDatabaseInspector.Validate(database);
                if (report.ErrorCount > 0 || report.WarningCount > 0) issues.Add(report.ToString());
            }
            var layers = new[] { "Ground", "Throwable", "Character", "SecondGround", "Mele", "Bullet", "Spike", "Enemy" };
            var indices = new[] { 3, 6, 7, 8, 9, 10, 11, 12 };
            for (int i = 0; i < layers.Length; i++)
                if (LayerMask.NameToLayer(layers[i]) != indices[i])
                    issues.Add("Demo: '" + layers[i] + "' expects layer " + indices[i] + ". See Getting Started before remapping existing layers.");
            var installed = new HashSet<string>();
            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()) installed.Add(package.name);
            foreach (var package in new[] { "com.unity.inputsystem", "com.unity.2d.animation", "com.unity.2d.sprite", "com.unity.ugui" })
                if (!installed.Contains(package)) issues.Add("Sample dependency: install " + package + " using Package Manager.");
            if (!EggHeadsRenderPipeline.IsKnownPipeline)
                issues.Add("Custom render pipeline detected. Configure compatible sprite materials manually.");
            else if (EggHeadsRenderPipeline.PreviewShader() == null)
                issues.Add("Pipeline sprite shaders are unavailable. Use Tools > PyroDuck > Render Pipeline to configure this project.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
                issues.Add("Demo UI: check TMP Essential Resources (Window > TextMeshPro). Generator itself does not need TMP.");
            var settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length > 0)
            {
                var settings = new SerializedObject(settingsAssets[0]);
                var input = settings.FindProperty("activeInputHandler");
                if (input != null && input.intValue == 0) issues.Add("Demo controls: set Active Input Handling to Input System Package (New) or Both, then restart Unity.");
            }
            return issues;
        }

        internal List<VisualDataSO> GetVisualChoices(VisualType type)
        {
            var choices = new List<VisualDataSO>();
            var seen = new HashSet<GameObject>();
            if (database?.visualGroups == null) return choices;
            // Include every subgroup, especially melee/ranged/throwable and hair/helmets.
            foreach (var group in database.visualGroups)
            {
                if (group == null || group.VisualType != type || group.VisualList == null) continue;
                foreach (var visual in group.VisualList)
                    if (visual != null && visual.BodyPartPrefab != null && seen.Add(visual.BodyPartPrefab)) choices.Add(visual);
            }
            return choices;
        }

        private void AddPartGallery(VisualElement card, string label, Func<GameObject> selection, Action<GameObject> choose)
        {
            VisualType type;
            switch (label)
            {
                case "Eyes": type = VisualType.Eyes; break;
                case "Eyebrows": type = VisualType.Eyebrow; break;
                case "Nose": type = VisualType.Nose; break;
                case "Mouth": type = VisualType.Mouth; break;
                case "Beard / Mustache": type = VisualType.Moustache; break;
                case "Hair / Hat": type = VisualType.HairOrHat; break;
                case "Weapon": type = VisualType.Weapon; break;
                default: return;
            }
            var gallery = new Foldout { text = "Browse " + label, value = false, viewDataKey = "parts-" + type };
            card.Add(gallery);
            var search = new TextField { label = "Search" };
            gallery.Add(search);
            var grid = new VisualElement();
            grid.AddToClassList("part-grid");
            gallery.Add(grid);
            var choices = GetVisualChoices(type);
            Action redraw = () =>
            {
                grid.Clear();
                var selected = selection();
                var none = ActionButton(grid, "None", () => choose(null), "part-tile");
                none.EnableInClassList("selected", selected == null);
                foreach (var visual in choices)
                {
                    string title = string.IsNullOrEmpty(visual.Name) ? visual.BodyPartPrefab.name : visual.Name;
                    if (!string.IsNullOrEmpty(search.value) && title.IndexOf(search.value, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var tile = new Button(() => choose(visual.BodyPartPrefab)) { tooltip = title };
                    tile.AddToClassList("part-tile");
                    tile.EnableInClassList("selected", selected == visual.BodyPartPrefab);
                    var image = new Image { scaleMode = ScaleMode.ScaleToFit };
                    if (visual.IconSprite != null) image.sprite = visual.IconSprite;
                    else image.image = AssetPreview.GetAssetPreview(visual.BodyPartPrefab) ?? AssetPreview.GetMiniThumbnail(visual.BodyPartPrefab);
                    tile.Add(image);
                    AddLabel(tile, title, "part-tile-title");
                    grid.Add(tile);
                }
            };
            search.RegisterValueChangedCallback(_ => redraw());
            gallery.RegisterValueChangedCallback(evt => { if (evt.target == gallery && evt.newValue) redraw(); });
            // Repaint selections without rebuilding the field that owns this gallery.
            grid.RegisterCallback<ClickEvent>(_ => grid.schedule.Execute(redraw));
            redraw();
        }

        private static GameObject SourcePrefab(GameObject instance)
        {
            if (instance == null) return null;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private void ApplyPreset(GameObject preset)
        {
            var controller = preset.GetComponent<CharacterVisualController>();
            if (controller == null) return;
            RecordChange("Apply character preset");
            characterBase = LoadAsset("Runtime/Prefabs/Characters/CharacterBase.prefab");
            body = LoadAsset("Runtime/Prefabs/Bodies/Body.prefab");
            eyes = eyebrows = nose = mouth = beardMustache = hairOrHat = weapon = null;
            eyesColor = eyebrowsColor = noseColor = mouthColor = beardMustacheColor = hairOrHatColor = weaponColor = Color.white;
            foreach (var mapping in controller.VisualMappings)
            {
                var current = mapping.CurrentVisual;
                if (current == null && mapping.Parent != null && mapping.Parent.childCount > 0) current = mapping.Parent.GetChild(0).gameObject;
                var part = SourcePrefab(current);
                var renderer = current != null ? current.GetComponentInChildren<SpriteRenderer>(true) : null;
                var color = renderer != null ? renderer.color : Color.white;
                switch (mapping.Type)
                {
                    case VisualType.Eyes: eyes = part; eyesColor = color; break;
                    case VisualType.Eyebrow: eyebrows = part; eyebrowsColor = color; break;
                    case VisualType.Nose: nose = part; noseColor = color; break;
                    case VisualType.Mouth: mouth = part; mouthColor = color; break;
                    case VisualType.Moustache: beardMustache = part; beardMustacheColor = color; break;
                    case VisualType.HairOrHat: hairOrHat = part; hairOrHatColor = color; break;
                    case VisualType.Weapon: weapon = part; weaponColor = color; break;
                }
            }
            RefreshPreviewIfReady();
        }

        private void GenerateAndAddToScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            var prefab = GeneratePrefab();
            if (prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(instance, "Add generated EggHead to scene");
            var view = SceneView.lastActiveSceneView;
            if (view != null) instance.transform.position = new Vector3(view.pivot.x, view.pivot.y, 0);
            Selection.activeGameObject = instance;
            if (view != null) view.FrameSelected();
            _generationStatus.text = "Prefab saved and character added to the active scene. Undo removes the scene instance.";
        }

        private void BuildFullComparison()
        {
            var foldout = new Foldout { text = "Explore Egg Heads Full", value = false };
            foldout.AddToClassList("settings-card");
            _settingsPanel.Add(foldout);
            AddLabel(foldout, "Let your players create their own characters.", "section-title");
            AddLabel(foldout, "Lite: editor character generation, playable demo, and a starter collection.\nFull: a larger collection, ready-made runtime creator with color wheel, and an integrated character save/load workflow.", "body-text");
            ActionButton(foldout, "View Full & Product Demos", () => Application.OpenURL("https://assetstore.unity.com/packages/2d/characters/egg-heads-2d-364008"), "primary-button");
            ActionButton(foldout, "Lite to Full Guide", () => OpenDocumentation("UpgradeToFull.md"));
            ActionButton(foldout, "Export Character for Full", ExportForFull);
        }

        private void ExportForFull()
        {
            if (_previewInstance == null || database == null) return;
            var path = EditorUtility.SaveFilePanel("Export character appearance", "", prefabName, "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, CharacterSerializer.SaveToJson(_previewInstance.GetComponent<CharacterVisualController>(),
                _previewInstance.GetComponent<CharacterColorizer>(), database.visualGroups));
            _generationStatus.text = "Appearance exported. See the Lite to Full Guide for matching parts and importing.";
        }
    }
}