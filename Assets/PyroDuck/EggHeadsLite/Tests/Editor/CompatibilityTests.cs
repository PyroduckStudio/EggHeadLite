using System.Collections;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class CompatibilityTests
    {
        [Test]
        public void ObjectIds_AreStableAndDistinct()
        {
            var first = new GameObject("first");
            var second = new GameObject("second");
            try
            {
                Assert.AreEqual(first.GetObjectId(), first.GetObjectId());
                Assert.AreNotEqual(first.GetObjectId(), second.GetObjectId());
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [UnityTest]
        public IEnumerator Platformer_StartsWithPlayableCharacter()
        {
            EditorSceneManager.OpenScene("Assets/PyroDuck/EggHeadsLite/Samples/Platformer/Scenes/Platformer.unity");
            yield return new EnterPlayMode();
            for (int i = 0; i < 120; i++)
                yield return null;
#if UNITY_6000_5_OR_NEWER
            var controller = Object.FindAnyObjectByType<EggHeadController>();
#else
            var controller = Object.FindFirstObjectByType<EggHeadController>();
#endif
            Assert.IsNotNull(controller);
            Assert.IsNotNull(controller.CurrentVisualController);
            Assert.IsTrue(controller.IsAlive);
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
