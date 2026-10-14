using System;
using System.IO;
using NUnit.Framework;
using PyroDuck.EggHeadsUpgrade;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public sealed class LegacyUpgradeTests
    {
        private string _root;
        private const string Old = "11111111111111111111111111111111";
        private const string New = "22222222222222222222222222222222";
        private const string Custom = "33333333333333333333333333333333";
        private LegacyMap Map => new LegacyMap {
            schemaVersion = 1,
            guidChanges = new[] { new GuidChange { oldGuid = Old, newGuid = New } },
            fullFolderMetas = new[] {
                new FolderMeta { relativePath = "Assets/PyroDuck/EggHeads.meta", text = "guid: " + New },
                new FolderMeta { relativePath = "Assets/PyroDuck/EggHeads/Runtime.meta", text = "guid: " + Custom },
                new FolderMeta { relativePath = "Assets/PyroDuck/EggHeads/Editor.meta", text = "guid: " + Custom }
            }
        };

        [SetUp] public void CreateLegacyProject()
        {
            _root = Path.Combine(Path.GetTempPath(), "EggHeadsUpgradeTests-" + Guid.NewGuid().ToString("N"));
            Write("Assets/PyroDuck/EggHeadsLite/Runtime/Scripts/Health.cs", "namespace com.pyroduck.eggheadslite.Runtime.Scripts {}");
            Write("Assets/PyroDuck/EggHeadsLite/Runtime/Scripts.meta", "guid: " + Custom);
            Write("Assets/PyroDuck/EggHeadsLite/Runtime/com.pyroduck.eggheadslite.Runtime.asmdef", "{\"name\":\"com.pyroduck.eggheadslite.Runtime\"}");
            Write("Assets/PyroDuck/EggHeadsLite/Runtime/com.pyroduck.eggheadslite.Runtime.asmdef.meta", "guid: " + Custom);
            foreach (string name in new[] { "EggHeadDatabaseInspector.cs", "EggHeadsTmpResourcesInstaller.cs" })
            {
                Write("Assets/PyroDuck/EggHeadsLite/Editor/" + name, "namespace com.pyroduck.eggheadslite.Editor {}");
                Write("Assets/PyroDuck/EggHeadsLite/Editor/" + name + ".meta", "guid: " + Old);
            }
            Write("Assets/MyGame/Hero.prefab", "--- !u!114 &7654321\nm_Script: {fileID: 11500000, guid: " + Old + ", type: 3}\nhealth: 137\n");
            Write("Assets/MyGame/Hero.prefab.meta", "guid: " + Custom);
            Write("Assets/MyGame/Player.cs", "using com.pyroduck.eggheadslite.Runtime.Scripts;\n");
        }

        [TearDown] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        private void Write(string relative, string text)
        { string path = Path.Combine(_root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text); }
        private string Read(string relative) => File.ReadAllText(Path.Combine(_root, relative));

        [Test] public void Prepare_PreservesCustomIdentityTuningAndLocalIds_WithRestorableBackup()
        {
            string original = Read("Assets/MyGame/Hero.prefab");
            string backup = LegacyUpgradePlan.Analyze(_root, Map).Apply();
            Assert.AreEqual(original.Replace(Old, New), Read("Assets/MyGame/Hero.prefab"));
            Assert.AreEqual("guid: " + Custom, Read("Assets/MyGame/Hero.prefab.meta"));
            Assert.AreEqual(original, File.ReadAllText(Path.Combine(backup, "Assets/MyGame/Hero.prefab")));
            Assert.IsTrue(File.Exists(Path.Combine(backup, "Assets/PyroDuck/EggHeadsLite/Runtime/Scripts/Health.cs")));
            StringAssert.Contains("com.pyroduck.eggheads.Runtime", Read("Assets/MyGame/Player.cs"));
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "Assets/PyroDuck/EggHeadsLite/Runtime/Scripts")));
            StringAssert.Contains("com.pyroduck.eggheads.Editor", Read("Assets/PyroDuck/EggHeads/Editor/EggHeadDatabaseInspector.cs"));
        }

        [Test] public void Prepare_RejectsEditsSincePreview()
        {
            var plan = LegacyUpgradePlan.Analyze(_root, Map);
            Write("Assets/MyGame/Hero.prefab", "changed");
            Assert.Throws<InvalidOperationException>(() => plan.Apply());
            Assert.AreEqual("changed", Read("Assets/MyGame/Hero.prefab"));
        }

        [Test] public void Prepare_RollsBackWhenRequiredFileMissing()
        {
            var plan = LegacyUpgradePlan.Analyze(_root, Map);
            File.Delete(Path.Combine(_root, "Assets/PyroDuck/EggHeadsLite/Runtime/Scripts.meta"));
            Assert.Catch(() => plan.Apply());
            StringAssert.Contains(Old, Read("Assets/MyGame/Hero.prefab"));
            StringAssert.Contains("eggheadslite.Editor", Read("Assets/PyroDuck/EggHeadsLite/Editor/EggHeadDatabaseInspector.cs"));
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "Assets/PyroDuck/EggHeads")));
        }

        [Test] public void Analyze_RejectsBinaryScenes()
        {
            Write("Assets/MyGame/Main.unity", "binary\0scene");
            Assert.Throws<InvalidOperationException>(() => LegacyUpgradePlan.Analyze(_root, Map));
        }
    }
}
