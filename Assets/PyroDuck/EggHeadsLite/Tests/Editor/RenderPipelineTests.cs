using System;
using System.IO;
using System.Linq;
using com.pyroduck.eggheadslite.Editor;
using com.pyroduck.eggheadslite.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class RenderPipelineTests
    {
        private string _folder;
        [SetUp] public void SetUp()
        {
            _folder = "Assets/__PipelineTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
        }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(_folder); }

        [Test] public void UserMaterial_IsPreservedEvenWithStockShader()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, _folder + "/Custom.mat");
            Assert.IsFalse(EggHeadsRenderPipeline.IsStockMaterial(material));
            Assert.AreSame(material, EggHeadsRenderPipeline.ConvertMaterial(material, EggHeadsRenderPipeline.Target.URP,
                Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")));
        }

        [Test] public void SpriteMaterial_RoundTripReturnsOriginalAndConversionIsIdempotent()
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (original == null) Assert.Ignore("URP package is required for this test.");
            var shader = Shader.Find("Sprites/Default");
            var builtIn = EggHeadsRenderPipeline.ConvertMaterial(original, EggHeadsRenderPipeline.Target.BuiltIn, shader);
            Assert.AreNotSame(original, builtIn);
            Assert.AreSame(shader, builtIn.shader);
            Assert.AreSame(builtIn, EggHeadsRenderPipeline.ConvertMaterial(builtIn, EggHeadsRenderPipeline.Target.BuiltIn, shader));
            Assert.AreSame(original, EggHeadsRenderPipeline.ConvertMaterial(builtIn, EggHeadsRenderPipeline.Target.URP, original.shader));
        }

        [Test] public void Conversion_PreservesSpriteTintAndCustomMaterialSlots()
        {
            var root = new GameObject("test");
            var custom = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(custom, _folder + "/Custom.mat");
            try
            {
                var stock = root.AddComponent<SpriteRenderer>();
                stock.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
                if (stock.sharedMaterial == null) Assert.Ignore("URP package is required for this test.");
                stock.color = new Color(.2f, .4f, .8f, .7f); stock.flipX = true;
                var child = new GameObject("custom"); child.transform.SetParent(root.transform);
                var untouched = child.AddComponent<SpriteRenderer>(); untouched.sharedMaterial = custom;
                int changes = EggHeadsRenderPipeline.ConvertRenderers(root, EggHeadsRenderPipeline.Target.BuiltIn,
                    Shader.Find("Sprites/Default"), Shader.Find("Particles/Standard Unlit"));
                Assert.AreEqual(1, changes);
                Assert.AreEqual(new Color(.2f, .4f, .8f, .7f), stock.color);
                Assert.IsTrue(stock.flipX);
                Assert.AreEqual(custom, untouched.sharedMaterial);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void StockParticle_ConversionUsesTransparentBlending()
        {
            var stock = AssetDatabase.LoadAssetAtPath<Material>("Assets/PyroDuck/EggHeadsLite/Samples/Platformer/Particle.mat");
            Assert.IsNotNull(stock);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) Assert.Ignore("URP package is required for this test.");
            var converted = EggHeadsRenderPipeline.ConvertMaterial(stock, EggHeadsRenderPipeline.Target.URP, shader);
            Assert.AreSame(shader, converted.shader);
            Assert.AreEqual((float)BlendMode.SrcAlpha, converted.GetFloat("_SrcBlend"));
            Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, converted.GetFloat("_DstBlend"));
            Assert.AreEqual(0, converted.GetFloat("_ZWrite"));
            Assert.AreEqual((int)RenderQueue.Transparent, converted.renderQueue);
            Assert.AreEqual(stock.GetTexture("_MainTex"), converted.GetTexture("_BaseMap"));
        }

        [Test] public void PickupFactory_InheritsConfiguredMaterial()
        {
            var data = ScriptableObject.CreateInstance<com.pyroduck.eggheads.Runtime.Scripts.Data.VisualDataSO>();
            var visual = new GameObject("visual");
            var material = new Material(Shader.Find("Sprites/Default"));
            GameObject pickup = null;
            try
            {
                visual.AddComponent<SpriteRenderer>().sharedMaterial = material;
                data.Name = "test"; data.BodyPartPrefab = visual;
                pickup = com.pyroduck.eggheads.Runtime.Scripts.Combat.WeaponPickupFactory.Create(data, Vector3.zero, Vector2.zero);
                Assert.AreEqual(material, pickup.GetComponent<SpriteRenderer>().sharedMaterial);
            }
            finally { Object.DestroyImmediate(pickup); Object.DestroyImmediate(visual); Object.DestroyImmediate(material); Object.DestroyImmediate(data); }
        }

        [Test] public void SpriteAdapter_PreservesExistingPropertyBlockValues()
        {
            var shader = Shader.Find(EggHeadsSpritePipelineAdapter.ShaderName);
            if (shader == null) Assert.Ignore("Run with the generated HDRP sprite shader available.");
            var root = new GameObject("adapter-test");
            var material = new Material(shader);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
                renderer.color = new Color(.2f, .4f, .8f, .6f); renderer.flipY = true;
                var block = new MaterialPropertyBlock(); block.SetFloat("_CustomProperty", 42); renderer.SetPropertyBlock(block);
                var adapter = root.AddComponent<EggHeadsSpritePipelineAdapter>(); adapter.ApplyProperties();
                renderer.GetPropertyBlock(block);
                Assert.AreEqual(42, block.GetFloat("_CustomProperty"));
                Assert.Less(((Vector4)(renderer.color - block.GetColor("_EggHeadsRendererColor"))).sqrMagnitude, .000001f);
                Assert.AreEqual(1, block.GetFloat("_EggHeadsIsSprite"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(material); }
        }

        [Test] public void PipelineSwitch_RoundTripPreservesPrefabGuidAndBackupRestoresSettings()
        {
            // This test modifies package samples/settings; run only in an isolated batch project.
            if (!Application.isBatchMode) Assert.Ignore("Run the pipeline integration test in an isolated batch project.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string body = "Assets/PyroDuck/EggHeadsLite/Runtime/Prefabs/Bodies/Body.prefab";
            string guid = AssetDatabase.AssetPathToGUID(body);
            string beforeBody = File.ReadAllText(body);
            string beforeQuality = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            string backup = EggHeadsRenderPipeline.CreateBackup();
            try
            {
                EggHeadsRenderPipeline.Apply(EggHeadsRenderPipeline.Target.BuiltIn);
                Assert.IsNull(GraphicsSettings.defaultRenderPipeline);
                Assert.IsNull(QualitySettings.renderPipeline);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(body));
                Assert.AreEqual("Sprites/Default", AssetDatabase.LoadAssetAtPath<GameObject>(body).GetComponentInChildren<SpriteRenderer>().sharedMaterial.shader.name);
                EggHeadsRenderPipeline.Apply(EggHeadsRenderPipeline.Target.URP);
                Assert.IsNotNull(GraphicsSettings.defaultRenderPipeline);
                Assert.AreSame(GraphicsSettings.defaultRenderPipeline, QualitySettings.renderPipeline);
                Assert.AreEqual("Universal Render Pipeline/2D/Sprite-Lit-Default", AssetDatabase.LoadAssetAtPath<GameObject>(body).GetComponentInChildren<SpriteRenderer>().sharedMaterial.shader.name);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(body));
            }
            finally { EggHeadsRenderPipeline.RestoreBackup(backup); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
            Assert.AreEqual(beforeBody, File.ReadAllText(body));
            Assert.AreEqual(beforeQuality, File.ReadAllText("ProjectSettings/QualitySettings.asset"));
        }

        [Test] public void HDRP_SwitchCompilesSpriteShaderAndConfiguresCamera()
        {
            if (!Application.isBatchMode) Assert.Ignore("Run HDRP integration in an isolated batch project.");
            if (!UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == "com.unity.render-pipelines.high-definition"))
                Assert.Ignore("HDRP package is required for this integration test.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var backup = EggHeadsRenderPipeline.CreateBackup();
            try
            {
                EggHeadsRenderPipeline.Apply(EggHeadsRenderPipeline.Target.HDRP);
                Assert.AreEqual(EggHeadsRenderPipeline.Target.HDRP, EggHeadsRenderPipeline.CurrentTarget);
                Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
                var shader = EggHeadsRenderPipeline.PreviewShader();
                Assert.IsNotNull(shader);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
                VerifyHDRPSpriteRendering(shader);
                var scene = EditorSceneManager.OpenScene("Assets/PyroDuck/EggHeadsLite/Samples/Platformer/Scenes/Platformer.unity");
                var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).First();
                Assert.IsNotNull(camera.GetComponent("HDAdditionalCameraData"));
                EggHeadsRenderPipeline.Apply(EggHeadsRenderPipeline.Target.URP);
                Assert.AreEqual(EggHeadsRenderPipeline.Target.URP, EggHeadsRenderPipeline.CurrentTarget);
            }
            finally { EggHeadsRenderPipeline.RestoreBackup(backup); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        private static void VerifyHDRPSpriteRendering(Shader shader)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("render-test-camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            EggHeadsRenderPipeline.ConfigureCamera(camera, EggHeadsRenderPipeline.Target.HDRP);
            DisableHDRPPostProcessing(camera);
            var texture = new Texture2D(2, 2) { filterMode = FilterMode.Point };
            texture.SetPixels(new[] { Color.white, Color.clear, Color.white, Color.clear }); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 1, 0, SpriteMeshType.FullRect);
            var material = new Material(shader);
            var renderer = new GameObject("render-test-sprite").AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.color = Color.green;
            var adapter = renderer.gameObject.AddComponent<EggHeadsSpritePipelineAdapter>();
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); target.Create();
            var pixels = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                var left = pixels.GetPixel(16, 32); var right = pixels.GetPixel(48, 32);
                File.WriteAllBytes("hdrp-sprite-before-flip.png", pixels.EncodeToPNG());
                Assert.Greater(left.g, .2f, "Opaque sprite pixels must render with SpriteRenderer tint.");
                Assert.Less(left.r, .1f, "SpriteRenderer.color must tint the sprite green.");
                Assert.Less(right.g, .1f, "Transparent sprite pixels must reveal the background.");
                renderer.flipX = true;
                adapter.ApplyProperties();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                File.WriteAllBytes("hdrp-sprite-after-flip.png", pixels.EncodeToPNG());
                Assert.Less(pixels.GetPixel(16, 32).g, .1f, "flipX must mirror transparent pixels.");
                Assert.Greater(pixels.GetPixel(48, 32).g, .2f, "flipX must mirror opaque pixels.");
                renderer.flipX = false; renderer.color = new Color(0, 1, 0, .5f); adapter.ApplyProperties();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixel(16, 32).g, Is.EqualTo(.5f).Within(.03f), "Sprite tint alpha must be applied once.");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); Object.DestroyImmediate(material);
                Object.DestroyImmediate(renderer.gameObject); Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            }
        }

        private static void DisableHDRPPostProcessing(Camera camera)
        {
            var data = camera.GetComponent("HDAdditionalCameraData");
            var type = data.GetType();
            type.GetField("customRenderingSettings").SetValue(data, true);
            var frameField = type.GetField("m_RenderingPathCustomFrameSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var frame = frameField.GetValue(data);
            var maskField = type.GetField("renderingPathCustomFrameSettingsOverrideMask");
            var mask = maskField.GetValue(data);
            var bitsField = mask.GetType().GetField("mask");
            var bits = bitsField.GetValue(mask);
            var enumType = type.Assembly.GetType("UnityEngine.Rendering.HighDefinition.FrameSettingsField");
            foreach (var name in new[] { "Postprocess", "ExposureControl" })
            {
                var flag = Enum.Parse(enumType, name);
                frame.GetType().GetMethod("SetEnabled").Invoke(frame, new[] { flag, (object)false });
                bits.GetType().GetProperty("Item").SetValue(bits, true, new object[] { Convert.ToUInt32(flag) });
            }
            bitsField.SetValue(mask, bits);
            frameField.SetValue(data, frame);
            maskField.SetValue(data, mask);
        }
    }
}
