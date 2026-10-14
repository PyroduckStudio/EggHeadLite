using System.Reflection;
using com.pyroduck.eggheadslite.Editor.Promotion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class SetupWindowTests
    {
        [Test]
        public void Setup_CreatesActionsAndOfflineCampaign_AndRemembersProject()
        {
            const string remoteKey = "PyroDuck.EggHeadsLite.RemoteFeatured";
            bool hadRemote = EditorPrefs.HasKey(remoteKey);
            bool remote = EditorPrefs.GetBool(remoteKey, true);
            string shownKey = (string)typeof(EggHeadsLiteSetupWindow)
                .GetProperty("ShownPreference", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            bool hadShown = EditorPrefs.HasKey(shownKey);
            bool shown = EditorPrefs.GetBool(shownKey, false);
            EditorPrefs.SetBool(remoteKey, false);
            EditorPrefs.DeleteKey(shownKey);
            var window = ScriptableObject.CreateInstance<EggHeadsLiteSetupWindow>();
            try
            {
                window.CreateGUI();
                var root = window.rootVisualElement;
                foreach (var name in new[] { "generator", "demo", "quick-start", "migration", "done", "view-asset", "refresh" })
                    Assert.IsNotNull(root.Q<Button>(name), name);
                Assert.AreEqual("Egg Heads 2D", root.Q<Label>("campaign-title").text);
                // Headless editors may not load textures. A missing image must
                // still leave a usable, branded campaign card.
                var placeholder = root.Q<Label>("image-placeholder");
                if (root.Q<Image>("campaign-image").image == null)
                {
                    Assert.AreEqual("Egg Heads 2D", placeholder.text);
                    Assert.IsFalse(placeholder.ClassListContains("setup-hidden"));
                }
                else Assert.IsTrue(placeholder.ClassListContains("setup-hidden"));
                Assert.IsFalse(root.Q("campaign-card").ClassListContains("setup-hidden"));
                Assert.IsTrue(root.Q("navigation").ClassListContains("setup-hidden"));
                Assert.IsFalse(root.Q<Toggle>("remote-toggle").value);
                Assert.IsTrue(EditorPrefs.GetBool(shownKey, false));
                window.CreateGUI();
                Assert.AreEqual(1, root.Query<Button>("generator").ToList().Count);
                Assert.Greater(root.styleSheets.count, 0);
                Assert.IsTrue(EditorPrefs.GetBool(shownKey, false));
            }
            finally
            {
                Object.DestroyImmediate(window);
                if (hadRemote) EditorPrefs.SetBool(remoteKey, remote); else EditorPrefs.DeleteKey(remoteKey);
                if (hadShown) EditorPrefs.SetBool(shownKey, shown); else EditorPrefs.DeleteKey(shownKey);
            }
        }
    }
}
