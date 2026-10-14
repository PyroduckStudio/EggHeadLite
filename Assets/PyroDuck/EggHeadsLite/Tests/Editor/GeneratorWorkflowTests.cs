using System;
using System.Collections.Generic;
using System.Reflection;
using com.pyroduck.eggheadslite.Editor;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class GeneratorWorkflowTests
    {
        private EggHeadsLiteCharacterPrefabGenerator window;
        private string folder;
        private Scene scene;
        private Scene previousScene;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private object Call(string name, params object[] args) => window.GetType().GetMethod(name, Flags).Invoke(window, args);
        private void Set(string name, object value) => window.GetType().GetField(name, Flags).SetValue(window, value);
        private T Get<T>(string name) => (T)window.GetType().GetField(name, Flags).GetValue(window);

        [SetUp]
        public void SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            if (!Application.isBatchMode && string.IsNullOrEmpty(previousScene.path))
                Assert.Ignore("Save the current scene before running scene-isolation tests interactively.");
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            folder = "Assets/__GeneratorWorkflow_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            window = ScriptableObject.CreateInstance<EggHeadsLiteCharacterPrefabGenerator>();
            Set("outputFolder", folder);
            Set("autoRegisterGeneratedPrefab", false);
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null) UnityEngine.Object.DestroyImmediate(window);
            if (scene.IsValid())
            {
                if (Application.isBatchMode) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else EditorSceneManager.CloseScene(scene, true);
            }
            if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void GalleryIncludesAllWeaponSubgroupsWithoutDuplicates()
        {
            var choices = (List<VisualDataSO>)Call("GetVisualChoices", VisualType.Weapon);
            Assert.GreaterOrEqual(choices.Count, 3);
            var paths = string.Join("\n", choices.ConvertAll(v => AssetDatabase.GetAssetPath(v.BodyPartPrefab)));
            StringAssert.Contains("/Melee/", paths);
            StringAssert.Contains("/Ranged/", paths);
            StringAssert.Contains("/Throwables/", paths);
            Assert.AreEqual(choices.Count, new HashSet<GameObject>(choices.ConvertAll(v => v.BodyPartPrefab)).Count);
        }

        [Test]
        public void ResetRestoresDefaultsAndUndoRestoresAppearance()
        {
            Set("eyesColor", Color.red);
            Set("eyebrows", null);
            Call("ResetCharacterDefaults");
            Undo.FlushUndoRecordObjects();
            Assert.AreEqual(Color.white, Get<Color>("eyesColor"));
            Assert.AreEqual("EyeBrow_0 Variant", Get<GameObject>("eyebrows").name);
            Assert.AreEqual("Mustache_0 Variant", Get<GameObject>("beardMustache").name);
            Assert.AreEqual(folder, Get<string>("outputFolder"));
            Undo.PerformUndo();
            Assert.AreEqual(Color.red, Get<Color>("eyesColor"));
            Assert.IsNull(Get<GameObject>("eyebrows"));
        }

        [Test]
        public void GenerateKeepsSceneCleanAndNoneRemovesPart()
        {
            Set("beardMustache", null);
            bool dirty = scene.isDirty;
            int roots = scene.rootCount;
            var first = (GameObject)Call("GeneratePrefab");
            var second = (GameObject)Call("GeneratePrefab");
            Assert.IsNotNull(first);
            Assert.AreNotEqual(AssetDatabase.GetAssetPath(first), AssetDatabase.GetAssetPath(second));
            Assert.AreEqual(roots, scene.rootCount);
            Assert.AreEqual(dirty, scene.isDirty);
            var controller = first.GetComponent<CharacterVisualController>();
            var mapping = controller.VisualMappings.Find(m => m.Type == VisualType.Moustache);
            Assert.AreEqual(0, mapping.Parent.childCount);
        }

        [Test]
        public void ScenePlacementIsUndoableAndDoesNotDeleteSavedPrefab()
        {
            window.CreateGUI();
            int count = scene.rootCount;
            Call("GenerateAndAddToScene");
            var instance = Selection.activeGameObject;
            Assert.AreEqual(count + 1, scene.rootCount);
            Assert.AreEqual(scene, instance.scene);
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.AreEqual(count, scene.rootCount);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        [TestCase("PistolHatMan")]
        [TestCase("SwordPunkMan")]
        [TestCase("OldBomberMan")]
        public void PresetLoadsDistinctWeaponAndExportableAppearance(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PyroDuck/EggHeadsLite/Runtime/Prefabs/Characters/" + name + ".prefab");
            Call("ApplyPreset", prefab);
            Assert.IsNotNull(Get<GameObject>("eyes"));
            Assert.IsNotNull(Get<GameObject>("weapon"));
            var preview = Get<GameObject>("_previewInstance");
            var data = Get<EggHeadDatabaseSO>("database");
            var json = CharacterSerializer.SaveToJson(preview.GetComponent<CharacterVisualController>(), preview.GetComponent<CharacterColorizer>(), data.visualGroups);
            Assert.Greater(JsonUtility.FromJson<CharacterSaveData>(json).Parts.Count, 0);
        }

        [Test]
        public void RebuildingUIKeepsOneSetOfActionsAndLoadsStyle()
        {
            window.CreateGUI();
            window.CreateGUI();
            var buttons = window.rootVisualElement.Query<Button>().ToList();
            Assert.AreEqual(1, buttons.FindAll(b => b.text == "Generate & Add to Scene").Count);
            Assert.AreEqual(1, buttons.FindAll(b => b.text == "Open Playable Demo").Count);
            Assert.Greater(window.rootVisualElement.styleSheets.count, 0);
        }

        [Test]
        public void NoneSelectionSurvivesWindowDisableEnable()
        {
            Set("eyebrows", null);
            Call("OnDisable");
            Call("OnEnable");
            Assert.IsNull(Get<GameObject>("eyebrows"));
        }

        [Test]
        public void GenerationRegistersInSelectedDatabaseWithoutChangingSourcePrefab()
        {
            var database = ScriptableObject.CreateInstance<EggHeadDatabaseSO>();
            AssetDatabase.CreateAsset(database, folder + "/Database.asset");
            Set("database", database);
            Set("autoRegisterGeneratedPrefab", true);
            var original = Get<GameObject>("characterBase");
            var sourceText = System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(original));
            var generated = (GameObject)Call("GeneratePrefab");
            CollectionAssert.Contains(database.characterPrefabs, generated);
            Assert.AreEqual(sourceText, System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(original)));
        }
    }
}
