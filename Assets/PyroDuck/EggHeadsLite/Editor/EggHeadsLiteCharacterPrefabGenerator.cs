using System.Collections.Generic;
using System.IO;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace com.pyroduck.eggheadslite.Editor
{
    public sealed partial class EggHeadsLiteCharacterPrefabGenerator : EditorWindow
    {
        private const string PackageName = "com.pyroduck.eggheadslite";
        private const string DefaultOutputFolder = "Assets/EggHeadsLite/GeneratedPrefabs";

        [SerializeField] private GameObject characterBase;
        [SerializeField] private GameObject body;
        [SerializeField] private GameObject eyes;
        [SerializeField] private GameObject eyebrows;
        [SerializeField] private GameObject nose;
        [SerializeField] private GameObject mouth;
        [SerializeField] private GameObject beardMustache;
        [SerializeField] private GameObject hairOrHat;
        [SerializeField] private GameObject weapon;
        [SerializeField] private Color eyesColor = Color.white;
        [SerializeField] private Color eyebrowsColor = Color.white;
        [SerializeField] private Color noseColor = Color.white;
        [SerializeField] private Color mouthColor = Color.white;
        [SerializeField] private Color beardMustacheColor = Color.white;
        [SerializeField] private Color hairOrHatColor = Color.white;
        [SerializeField] private Color weaponColor = Color.white;
        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private string prefabName = "GeneratedEggHeadCharacter";
        [SerializeField] private EggHeadDatabaseSO database;
        [SerializeField] private bool autoRegisterGeneratedPrefab = true;
        [SerializeField] private bool defaultsInitialized;

        private readonly Dictionary<string, VisualDataSO> VisualDataByPrefabPath = new();
        private bool visualDataLookupInitialized;

        private PreviewRenderUtility _previewUtility;
        private GameObject _previewInstance;
        private readonly List<Material> _previewMaterials = new();
        private float _previewZoom = 1f;
        private bool _hasPreview;
        private const string RemoteCampaignPreference = "PyroDuck.EggHeadsLite.RemoteFeatured";
        private Promotion.CampaignService _campaignService;
        private int _campaignIndex;
        private double _nextCampaignSlide;
        private double _nextCampaignRefresh;
        private bool _campaignHovered;
        private bool _remoteCampaigns;


        [MenuItem("Tools/PyroDuck/EggHeadsLite/Generator")]
        private static void Open()
        {
            GetWindow<EggHeadsLiteCharacterPrefabGenerator>("EggHeads Lite Generator").minSize =
                new Vector2(760f, 520f);
        }

        private void OnEnable()
        {
            RebuildVisualDataLookup();
            if (!defaultsInitialized) LoadDefaults();
            else RefreshPreviewIfReady();
            InitializeCampaigns();
            Undo.undoRedoPerformed += OnGeneratorUndo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnGeneratorUndo;
            CleanupPreview();
            _campaignService?.Dispose();
            _campaignService = null;
            _featuredPanel = null;
            _settingsPanel = null;
            _previewPanel = null;
        }

        private VisualElement _settingsPanel;
        private VisualElement _featuredPanel;
        private Button _generateButton;
        private IMGUIContainer _previewPanel;

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            minSize = new Vector2(820, 560);
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)))
                .Replace('\\', '/');
            var sharedStyle =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(folder + "/PyroDuck/AboutUs/PyroDuckAboutWindow.uss");
            var generatorStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(folder + "/EggHeadsLiteGenerator.uss");
            if (sharedStyle != null) rootVisualElement.styleSheets.Add(sharedStyle);
            if (generatorStyle != null) rootVisualElement.styleSheets.Add(generatorStyle);
            rootVisualElement.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            var page = new VisualElement();
            page.AddToClassList("page");
            rootVisualElement.Add(page);
            var header = new VisualElement();
            header.AddToClassList("generator-header");
            page.Add(header);
            AddLabel(header, "PYRODUCK", "logo");
            AddLabel(header, "EggHeads Lite Generator", "section-title");
            AddLabel(header, "Build your character. Make it your own.", "subtitle");
            var about = new Button(Promotion.PyroDuckAboutWindow.ShowWindow) { text = "About PyroDuck" };
            about.AddToClassList("secondary-button");
            about.AddToClassList("about-link");
            header.Add(about);
            var columns = new VisualElement();
            columns.AddToClassList("generator-columns");
            page.Add(columns);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("generator-settings");
            columns.Add(scroll);
            _settingsPanel = scroll.contentContainer;
            BuildSettingsUI();
            var previewCard = new VisualElement();
            previewCard.AddToClassList("generator-preview");
            previewCard.AddToClassList("campaign-card");
            columns.Add(previewCard);
            AddLabel(previewCard, "CHARACTER PREVIEW", "eyebrow");
            AddLabel(previewCard, "Updates as you choose parts. Scroll to zoom.", "subtitle");
            var refresh = new Button(RefreshPreviewIfReady) { text = "Refresh Preview" };
            refresh.AddToClassList("secondary-button");
            refresh.AddToClassList("preview-refresh-button");
            previewCard.Add(refresh);
            _previewPanel = new IMGUIContainer(DrawPreviewPanel);
            _previewPanel.style.flexGrow = 1;
            _previewPanel.style.minHeight = 280;
            previewCard.Add(_previewPanel);
        }

        private static Label AddLabel(VisualElement parent, string text, string className)
        {
            var label = new Label(text) { enableRichText = false };
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        private VisualElement SettingsCard(string title, string subtitle)
        {
            var card = new VisualElement();
            card.AddToClassList("campaign-card");
            card.AddToClassList("settings-card");
            AddLabel(card, title, "section-title");
            AddLabel(card, subtitle, "subtitle");
            _settingsPanel.Add(card);
            return card;
        }

        private void PartField(VisualElement card, string label, GameObject value, System.Action<GameObject> assign,
            Color color = default, System.Action<Color> assignColor = null)
        {
            var field = new ObjectField(label)
                { objectType = typeof(GameObject), allowSceneObjects = false, value = value };
            card.Add(field);
            ColorField tint = null;
            if (assignColor != null)
            {
                tint = new ColorField("Tint") { value = color };
                tint.AddToClassList("part-tint");
                tint.style.display = IsColorable(value) ? DisplayStyle.Flex : DisplayStyle.None;
                tint.RegisterValueChangedCallback(evt =>
                {
                    RecordChange("Change part color");
                    assignColor(evt.newValue);
                    RefreshPreviewIfReady();
                });
                card.Add(tint);
            }

            field.RegisterValueChangedCallback(evt =>
            {
                var prefab = evt.newValue as GameObject;
                RecordChange("Change " + label);
                assign(prefab);
                if (tint != null) tint.style.display = IsColorable(prefab) ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateGenerateButtons();
                RefreshPreviewIfReady();
            });
            AddPartGallery(card, label, () => field.value as GameObject, selected => field.value = selected);
        }

        private void BuildSettingsUI()
        {
            _settingsPanel.Clear();
            BuildQuickStart();
            var baseCard = SettingsCard("Character Base", "Choose the foundation for your character.");
            PartField(baseCard, "Base", characterBase, v => characterBase = v);
            var parts = SettingsCard("Character Parts", "Drag in prefabs and customize available colors.");
            PartField(parts, "Body", body, v => body = v);
            PartField(parts, "Eyes", eyes, v => eyes = v, eyesColor, v => eyesColor = v);
            PartField(parts, "Eyebrows", eyebrows, v => eyebrows = v, eyebrowsColor, v => eyebrowsColor = v);
            PartField(parts, "Nose", nose, v => nose = v, noseColor, v => noseColor = v);
            PartField(parts, "Mouth", mouth, v => mouth = v, mouthColor, v => mouthColor = v);
            PartField(parts, "Beard / Mustache", beardMustache, v => beardMustache = v, beardMustacheColor,
                v => beardMustacheColor = v);
            PartField(parts, "Hair / Hat", hairOrHat, v => hairOrHat = v, hairOrHatColor, v => hairOrHatColor = v);
            PartField(parts, "Weapon", weapon, v => weapon = v, weaponColor, v => weaponColor = v);
            var output = SettingsCard("Save Character", "Create a prefab for your project.");
            var folder = new TextField("Output Folder") { value = outputFolder };
            folder.RegisterValueChangedCallback(evt => { RecordChange("Change output folder"); outputFolder = evt.newValue; });
            output.Add(folder);
            var name = new TextField("Prefab Name") { value = prefabName };
            name.RegisterValueChangedCallback(evt =>
            {
                RecordChange("Change prefab name");
                prefabName = evt.newValue;
                UpdateGenerateButtons();
            });
            output.Add(name);
            var data = new ObjectField("Database SO")
                { objectType = typeof(EggHeadDatabaseSO), allowSceneObjects = false, value = database };
            data.RegisterValueChangedCallback(evt => { RecordChange("Change database"); database = evt.newValue as EggHeadDatabaseSO; _settingsPanel.schedule.Execute(BuildSettingsUI); });
            output.Add(data);
            var register = new Toggle("Auto Register In SO") { value = autoRegisterGeneratedPrefab };
            register.RegisterValueChangedCallback(evt => { RecordChange("Change registration"); autoRegisterGeneratedPrefab = evt.newValue; });
            output.Add(register);
            var actions = new VisualElement();
            actions.AddToClassList("button-row");
            output.Add(actions);
            var defaults = new Button(() =>
            {
                ResetCharacterDefaults();
                BuildSettingsUI();
            }) { text = "Load Defaults" };
            defaults.AddToClassList("secondary-button");
            actions.Add(defaults);
            var cache = new Button(() =>
            {
                RebuildVisualDataLookup();
                RefreshPreviewIfReady();
                BuildSettingsUI();
            }) { text = "Refresh Colorable Cache" };
            cache.AddToClassList("secondary-button");
            actions.Add(cache);
            _generateButton = new Button(() =>
            {
                GeneratePrefab();
                folder.SetValueWithoutNotify(outputFolder);
                data.SetValueWithoutNotify(database);
            }) { text = "Generate" };
            _generateButton.AddToClassList("primary-button");
            _generateButton.SetEnabled(characterBase != null && !string.IsNullOrWhiteSpace(prefabName));
            output.Add(_generateButton);
            _addToSceneButton = ActionButton(output, "Generate & Add to Scene", GenerateAndAddToScene, "primary-button");
            _generationStatus = AddLabel(output, "", "status");
            UpdateGenerateButtons();
            _featuredPanel = new VisualElement();
            _featuredPanel.AddToClassList("generator-featured");
            _featuredPanel.RegisterCallback<MouseEnterEvent>(_ => _campaignHovered = true);
            _featuredPanel.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                _campaignHovered = false;
                _nextCampaignSlide = EditorApplication.timeSinceStartup + 5;
            });
            _settingsPanel.Add(_featuredPanel);
            RenderCampaignUI();
            BuildFullComparison();
        }

        private string _displayedCampaign;
        private Texture2D _displayedCampaignImage;
        private string _displayedCampaignStatus;

        private void RenderCampaignUI()
        {
            if (_featuredPanel == null || _campaignService == null) return;
            _featuredPanel.Clear();
            AddLabel(_featuredPanel, "FEATURED BY PYRODUCK", "eyebrow");
            var campaigns = _campaignService.Catalog?.GetActive(System.DateTimeOffset.UtcNow);
            if (campaigns == null || campaigns.Count == 0)
            {
                _displayedCampaign = null;
                _displayedCampaignImage = null;
                _displayedCampaignStatus = _campaignService.Status;
                AddLabel(_featuredPanel, "No featured picks right now.", "body-text");
                return;
            }

            _campaignIndex = ((_campaignIndex % campaigns.Count) + campaigns.Count) % campaigns.Count;
            var campaign = campaigns[_campaignIndex];
            _displayedCampaign = JsonUtility.ToJson(campaign);
            _displayedCampaignImage = _campaignService.GetImage(campaign.imageUrl);
            _displayedCampaignStatus = _campaignService.Status;
            var card = new VisualElement();
            card.AddToClassList("campaign-card");
            _featuredPanel.Add(card);
            var frame = new VisualElement();
            frame.AddToClassList("image-frame");
            card.Add(frame);
            if (_displayedCampaignImage != null)
            {
                var image = new Image { image = _displayedCampaignImage, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("campaign-image");
                frame.Add(image);
            }
            else AddLabel(frame, campaign.title, "image-placeholder");

            var ribbon = AddLabel(frame, $"{campaign.DiscountLabel} OFF", "discount-ribbon");
            ribbon.pickingMode = PickingMode.Ignore;
            ribbon.style.display = campaign.HasDiscount ? DisplayStyle.Flex : DisplayStyle.None;
            var content = new VisualElement();
            content.AddToClassList("card-content");
            card.Add(content);
            if (!string.IsNullOrEmpty(campaign.badge)) AddLabel(content, campaign.badge, "badge");
            AddLabel(content, campaign.title, "campaign-title");
            AddLabel(content, campaign.description ?? "", "body-text");
            var view = new Button(() =>
            {
                var active = _campaignService.Catalog?.GetActive(System.DateTimeOffset.UtcNow);
                if (active != null && active.Exists(c => c.id == campaign.id && c.targetUrl == campaign.targetUrl)
                                   && Promotion.CampaignCatalog.IsDestination(campaign.targetUrl))
                    Application.OpenURL(campaign.targetUrl);
            }) { text = "View Asset" };
            view.AddToClassList("primary-button");
            content.Add(view);
            if (campaigns.Count > 1)
            {
                var navigation = new VisualElement();
                navigation.AddToClassList("navigation");
                _featuredPanel.Add(navigation);
                var previous = new Button(() => ChangeCampaign(-1, campaigns.Count)) { text = "<" };
                previous.AddToClassList("arrow");
                navigation.Add(previous);
                for (int i = 0; i < campaigns.Count; i++)
                {
                    int selected = i;
                    var dot = new Button(() =>
                        {
                            _campaignIndex = selected;
                            _nextCampaignSlide = EditorApplication.timeSinceStartup + 5;
                            RenderCampaignUI();
                        })
                        { text = i == _campaignIndex ? "●" : "○", tooltip = campaigns[i].title };
                    dot.AddToClassList("dot");
                    navigation.Add(dot);
                }

                var next = new Button(() => ChangeCampaign(1, campaigns.Count)) { text = ">" };
                next.AddToClassList("arrow");
                navigation.Add(next);
            }

            AddLabel(_featuredPanel, _campaignService.Status, "status");
        }

        private void InitializeCampaigns()
        {
            _campaignService?.Dispose();
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)))
                .Replace('\\', '/') + "/PyroDuck/AboutUs";
            var settings =
                AssetDatabase.LoadAssetAtPath<Promotion.PyroDuckCampaignSettings>(folder +
                    "/PyroDuckCampaignSettings.asset");
            var fallback = settings != null
                ? settings.fallbackCatalog
                : AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/campaigns.json");
            _campaignService = new Promotion.CampaignService(settings != null ? settings.feedUrl : "",
                fallback != null ? fallback.text : "");
            _campaignService.Changed += RenderCampaignUI;
            _remoteCampaigns = EditorPrefs.GetBool(RemoteCampaignPreference, true);
            _campaignService.Refresh(_remoteCampaigns);
            _nextCampaignSlide = EditorApplication.timeSinceStartup + 5;
            _nextCampaignRefresh = EditorApplication.timeSinceStartup + 21600;
        }

        private void OnInspectorUpdate()
        {
            if (_campaignService == null) return;
            double now = EditorApplication.timeSinceStartup;
            bool remote = EditorPrefs.GetBool(RemoteCampaignPreference, true);
            if (remote != _remoteCampaigns || now >= _nextCampaignRefresh)
            {
                _remoteCampaigns = remote;
                _nextCampaignRefresh = now + 21600;
                _campaignService.Refresh(remote);
            }

            var focused = rootVisualElement.focusController?.focusedElement as VisualElement;
            if (_campaignHovered || (focused != null && _featuredPanel != null && _featuredPanel.Contains(focused)))
                _nextCampaignSlide = now + 5;
            if (now >= _nextCampaignSlide)
            {
                _campaignIndex++;
                RenderCampaignUI();
                _nextCampaignSlide = now + 5;
            }

            var campaigns = _campaignService.Catalog?.GetActive(System.DateTimeOffset.UtcNow);
            var current = campaigns != null && campaigns.Count > 0
                ? campaigns[((_campaignIndex % campaigns.Count) + campaigns.Count) % campaigns.Count]
                : null;
            if ((current != null ? JsonUtility.ToJson(current) : null) != _displayedCampaign ||
                (current != null ? _campaignService.GetImage(current.imageUrl) : null) != _displayedCampaignImage ||
                _campaignService.Status != _displayedCampaignStatus)
                RenderCampaignUI();
            UpdateGenerateButtons();
            _previewPanel?.MarkDirtyRepaint();
        }

        private void ChangeCampaign(int direction, int count)
        {
            _campaignIndex = (_campaignIndex + direction + count) % count;
            _nextCampaignSlide = EditorApplication.timeSinceStartup + 5;
            RenderCampaignUI();
        }

        private void DrawPreviewPanel()
        {
            var previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            previewRect = EditorGUI.IndentedRect(previewRect);
            EditorGUI.DrawRect(previewRect, new Color(0.18f, 0.18f, 0.18f, 1f));

            if (characterBase == null)
            {
                EditorGUI.LabelField(previewRect, "Assign a Character Base to preview.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (!_hasPreview || _previewUtility == null || _previewInstance == null)
            {
                EditorGUI.LabelField(previewRect, "Assign parts to preview the character.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandlePreviewInput(previewRect);
            DrawPreviewRender(previewRect);
        }

        private void HandlePreviewInput(Rect previewRect)
        {
            var currentEvent = Event.current;
            if (!previewRect.Contains(currentEvent.mousePosition))
                return;

            switch (currentEvent.type)
            {
                case EventType.ScrollWheel:
                    _previewZoom *= 1f - currentEvent.delta.y * 0.05f;
                    _previewZoom = Mathf.Clamp(_previewZoom, 0.4f, 3f);
                    currentEvent.Use();
                    Repaint();
                    break;
            }
        }

        private void DrawPreviewRender(Rect previewRect)
        {
            if (Event.current.type != EventType.Repaint || previewRect.width <= 0f || previewRect.height <= 0f ||
                _previewUtility == null || _previewInstance == null)
                return;

            _previewUtility.BeginPreview(previewRect, GUIStyle.none);

            var bounds = GetPreviewBounds(_previewInstance);
            var center = bounds.center;
            var maxExtent = Mathf.Max(bounds.extents.x / (previewRect.width / previewRect.height), bounds.extents.y,
                0.5f);
            var camera = _previewUtility.camera;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = maxExtent * 1.2f * _previewZoom;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(center.x, center.y, center.z - 10f);
            camera.transform.rotation = Quaternion.identity;

            if (EggHeadsRenderPipeline.IsKnownPipeline)
                EggHeadsRenderPipeline.ConfigureCamera(camera, EggHeadsRenderPipeline.CurrentTarget);

            EditorUtility.SetCameraAnimateMaterials(camera, true);
            foreach (var adapter in _previewInstance.GetComponentsInChildren<com.pyroduck.eggheadslite.Rendering.EggHeadsSpritePipelineAdapter>(true))
                adapter.ApplyProperties();
            _previewUtility.Render(true);
            EditorUtility.SetCameraAnimateMaterials(camera, false);

            var texture = _previewUtility.EndPreview();
            if (texture != null)
                GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
        }

        private void RefreshPreviewIfReady()
        {
            if (characterBase == null)
            {
                CleanupPreview();
                Repaint();
                return;
            }

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            RebuildPreview();
            Repaint();
        }

        private void RebuildPreview()
        {
            CleanupPreview();

            if (characterBase == null)
                return;

            try
            {
                _previewUtility = new PreviewRenderUtility(true);
                _previewUtility.cameraFieldOfView = 28f;

                if (_previewUtility.lights != null && _previewUtility.lights.Length > 0)
                {
                    _previewUtility.lights[0].intensity = 1.15f;
                    _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                }

                if (_previewUtility.lights != null && _previewUtility.lights.Length > 1)
                {
                    _previewUtility.lights[1].intensity = 0.85f;
                    _previewUtility.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
                }

                _previewInstance =
                    (GameObject)PrefabUtility.InstantiatePrefab(characterBase, _previewUtility.camera.scene);
                if (_previewInstance == null)
                    _previewInstance = (GameObject)Object.Instantiate(characterBase);

                _previewInstance.hideFlags = HideFlags.HideAndDontSave;
                _previewUtility.AddSingleGO(_previewInstance);
                ApplyCharacterConfiguration(_previewInstance);
                PreparePreviewMaterials(_previewInstance);

                var renderers = _previewInstance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Debug.LogWarning(
                        "[EggHeads Lite Generator] Preview has no renderers. Check assigned part prefabs.");
                    CleanupPreview();
                    return;
                }

                _previewZoom = 1f;
                _hasPreview = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EggHeads Lite Generator] Preview failed: {ex.Message}\n{ex.StackTrace}");
                CleanupPreview();
            }
        }

        private void PreparePreviewMaterials(GameObject root)
        {
            var previewShader = EggHeadsRenderPipeline.PreviewShader();

            if (previewShader == null)
            {
                Debug.LogWarning("[EggHeads Lite Generator] Could not find a compatible preview shader.");
                return;
            }

            foreach (var spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var previewMaterial = new Material(previewShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (spriteRenderer.sprite != null)
                    previewMaterial.mainTexture = spriteRenderer.sprite.texture;

                previewMaterial.color = Color.white;

                var source = spriteRenderer.sharedMaterial;
                if (source != null)
                {
                    if (source.HasProperty("_Color"))
                        previewMaterial.color = source.GetColor("_Color");
                    else if (source.HasProperty("_BaseColor"))
                        previewMaterial.color = source.GetColor("_BaseColor");
                }

                _previewMaterials.Add(previewMaterial);
                spriteRenderer.sharedMaterial = previewMaterial;
            }
        }

        private void CleanupPreview()
        {
            _hasPreview = false;
            foreach (var material in _previewMaterials)
                if (material != null)
                    DestroyImmediate(material);
            _previewMaterials.Clear();
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }

            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        private static Bounds GetPreviewBounds(GameObject root)
        {
            var spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (spriteRenderers.Length > 0)
            {
                var bounds = spriteRenderers[0].bounds;
                for (var i = 1; i < spriteRenderers.Length; i++)
                    bounds.Encapsulate(spriteRenderers[i].bounds);
                return bounds;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 2f);

            var rendererBounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                rendererBounds.Encapsulate(renderers[i].bounds);

            return rendererBounds;
        }

        private static GameObject ObjectField(string label, GameObject value)
        {
            return (GameObject)EditorGUILayout.ObjectField(label, value, typeof(GameObject), false);
        }

        private void DrawColorFieldIfColorable(string label, GameObject prefab, ref Color color)
        {
            if (!IsColorable(prefab))
                return;

            EditorGUI.indentLevel++;
            color = EditorGUILayout.ColorField(label, color);
            EditorGUI.indentLevel--;
        }

        private void LoadDefaults()
        {
            characterBase = characterBase != null
                ? characterBase
                : LoadAsset("Runtime/Prefabs/Characters/CharacterBase.prefab");
            body = body != null ? body : LoadAsset("Runtime/Prefabs/Bodies/Body.prefab");
            database = database != null ? database : LoadDatabase("Runtime/Data/EggHeadDatabase.asset");
            eyes = eyes != null
                ? eyes
                : LoadDefaultVisual(VisualType.Eyes, "Runtime/Prefabs/Eyes/Eyes_0 Variant.prefab");
            eyebrows = eyebrows != null ? eyebrows : LoadAsset("Runtime/Prefabs/Eyebrows/EyeBrow_0 Variant.prefab");
            nose = nose != null
                ? nose
                : LoadDefaultVisual(VisualType.Nose, "Runtime/Prefabs/Noses/Nose_0 Variant.prefab");
            mouth = mouth != null
                ? mouth
                : LoadDefaultVisual(VisualType.Mouth, "Runtime/Prefabs/Mouths/Mouth_0 Variant.prefab");
            beardMustache = beardMustache != null
                ? beardMustache
                : LoadAsset("Runtime/Prefabs/BeardMustache/Mustache_0 Variant.prefab");
            hairOrHat = hairOrHat != null
                ? hairOrHat
                : LoadDefaultVisual(VisualType.HairOrHat, "Runtime/Prefabs/HairAndHelmets/Hats/Hat_0 Variant.prefab");
            weapon = weapon != null
                ? weapon
                : LoadDefaultVisual(VisualType.Weapon, "Runtime/Prefabs/Weapons/Melee/WeaponMele_0 Variant.prefab");
            defaultsInitialized = true;
            RefreshPreviewIfReady();
        }

        private static GameObject LoadAsset(string packageRelativePath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>($"Packages/{PackageName}/{packageRelativePath}")
                   ?? AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PyroDuck/EggHeadsLite/{packageRelativePath}");
        }

        private static EggHeadDatabaseSO LoadDatabase(string packageRelativePath)
        {
            return AssetDatabase.LoadAssetAtPath<EggHeadDatabaseSO>($"Packages/{PackageName}/{packageRelativePath}")
                   ?? AssetDatabase.LoadAssetAtPath<EggHeadDatabaseSO>(
                       $"Assets/PyroDuck/EggHeadsLite/{packageRelativePath}");
        }

        private GameObject LoadDefaultVisual(VisualType visualType, string fallbackPath)
        {
            var visualGroup = database != null ? database.GetGroup(visualType) : null;
            if (visualGroup?.VisualList != null)
            {
                foreach (var visual in visualGroup.VisualList)
                {
                    if (visual != null && visual.BodyPartPrefab != null)
                        return visual.BodyPartPrefab;
                }
            }

            return LoadAsset(fallbackPath);
        }

        private GameObject GeneratePrefab()
        {
            if (characterBase == null || string.IsNullOrWhiteSpace(prefabName) || EditorApplication.isPlayingOrWillChangePlaymode) return null;
            outputFolder = NormalizeOutputFolder(outputFolder);
            EnsureFolder(outputFolder);

            var staging = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(characterBase, staging);
                if (root == null) throw new System.InvalidOperationException("Character Base must be a prefab asset.");
                root.name = SanitizeFileName(prefabName);
                ApplyCharacterConfiguration(root);

                var path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{root.name}.prefab");
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path)
                                  ?? AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (autoRegisterGeneratedPrefab)
                    RegisterGeneratedPrefab(savedPrefab);

                AssetDatabase.SaveAssets();
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(Selection.activeObject);
                Debug.Log($"EggHeads Lite generated character prefab: {path}");
                if (_generationStatus != null) _generationStatus.text = "Saved: " + path;
                return savedPrefab;
            }
            finally
            {
                if (root != null) DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(staging);
            }
        }

        private void ApplyCharacterConfiguration(GameObject root)
        {
            RemoveMissingScripts(root);

            var visualController = root.GetComponent<CharacterVisualController>();
            var bodyInstance = AddPart(root.transform, "BodyParent", body);
            AddVisualPart(visualController, VisualType.Eyes, eyes);
            ApplyVisualColor(visualController, VisualType.Eyes, eyes, eyesColor);
            AddVisualPart(visualController, VisualType.Eyebrow, eyebrows);
            ApplyVisualColor(visualController, VisualType.Eyebrow, eyebrows, eyebrowsColor);
            AddVisualPart(visualController, VisualType.Nose, nose);
            ApplyVisualColor(visualController, VisualType.Nose, nose, noseColor);
            AddVisualPart(visualController, VisualType.Mouth, mouth);
            ApplyVisualColor(visualController, VisualType.Mouth, mouth, mouthColor);
            AddVisualPart(visualController, VisualType.Moustache, beardMustache);
            ApplyVisualColor(visualController, VisualType.Moustache, beardMustache, beardMustacheColor);
            AddVisualPart(visualController, VisualType.HairOrHat, hairOrHat);
            ApplyVisualColor(visualController, VisualType.HairOrHat, hairOrHat, hairOrHatColor);
            AddVisualPart(visualController, VisualType.Weapon, weapon);
            ApplyVisualColor(visualController, VisualType.Weapon, weapon, weaponColor);
            ConfigureBodyReferences(root, bodyInstance);
        }

        private void RegisterGeneratedPrefab(GameObject generatedPrefab)
        {
            if (generatedPrefab == null)
                return;

            database = database != null ? database : LoadDatabase("Runtime/Data/EggHeadDatabase.asset");
            if (database == null)
            {
                Debug.LogWarning(
                    "EggHeads Lite generator could not find an EggHeadDatabaseSO to register the generated prefab.");
                return;
            }

            Undo.RecordObject(database, "Register generated EggHead");
            database.characterPrefabs ??= new List<GameObject>();
            if (database.characterPrefabs.Contains(generatedPrefab))
                return;

            database.characterPrefabs.Add(generatedPrefab);
            EditorUtility.SetDirty(database);
            Debug.Log($"EggHeads Lite registered generated prefab in {AssetDatabase.GetAssetPath(database)}.");
        }

        private static GameObject AddPart(Transform root, string slotName, GameObject prefab)
        {
            if (prefab == null)
                return null;

            var slot = FindDirectChild(root, slotName) ?? FindChild(root, slotName);
            if (slot == null)
            {
                Debug.LogWarning($"EggHeads Lite generator could not find slot '{slotName}' on {root.name}.");
                return null;
            }

            return AddPart(slot, prefab);
        }

        private static GameObject AddPart(Transform slot, GameObject prefab)
        {
            if (slot == null || prefab == null)
                return null;

            ClearChildren(slot);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
            if (instance == null)
                instance = Object.Instantiate(prefab, slot);

            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            RemoveMissingScripts(instance);
            return instance;
        }

        private static GameObject AddVisualPart(CharacterVisualController visualController, VisualType visualType,
            GameObject prefab)
        {
            if (visualController == null || visualController.VisualMappings == null)
            {
                Debug.LogWarning($"EggHeads Lite generator could not find visual mappings for '{visualType}'.");
                return null;
            }

            foreach (var visualMapping in visualController.VisualMappings)
            {
                if (visualMapping.Type != visualType)
                    continue;

                if (visualMapping.Parent == null)
                {
                    Debug.LogWarning($"EggHeads Lite generator could not find parent for visual type '{visualType}'.");
                    return null;
                }

                if (prefab == null)
                {
                    ClearChildren(visualMapping.Parent);
                    visualMapping.CurrentVisual = null;
                    return null;
                }
                var instance = AddPart(visualMapping.Parent, prefab);
                visualMapping.CurrentVisual = instance;
                return instance;
            }

            Debug.LogWarning($"EggHeads Lite generator could not find mapping for visual type '{visualType}'.");
            return null;
        }

        private void ApplyVisualColor(
            CharacterVisualController visualController,
            VisualType visualType,
            GameObject prefab,
            Color color)
        {
            if (visualController == null || !IsColorable(prefab))
                return;

            var colorizer = visualController.GetComponent<CharacterColorizer>();
            if (colorizer == null)
                return;

            colorizer.bodyParts ??= new List<com.pyroduck.eggheads.Runtime.Scripts.Character.BodyPart>();
            colorizer.RefreshForVisualType(visualType);
            colorizer.SetColorForVisualType(visualType, color);
            EditorUtility.SetDirty(colorizer);
        }

        private bool IsColorable(GameObject prefab)
        {
            return FindVisualData(prefab)?.isColorable == true;
        }

        private VisualDataSO FindVisualData(GameObject prefab)
        {
            if (prefab == null)
                return null;

            if (!visualDataLookupInitialized)
                RebuildVisualDataLookup();

            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            return !string.IsNullOrEmpty(prefabPath) &&
                   VisualDataByPrefabPath.TryGetValue(prefabPath, out var visualData)
                ? visualData
                : null;
        }

        private void RebuildVisualDataLookup()
        {
            VisualDataByPrefabPath.Clear();

            var folders = GetExistingVisualDataFolders();
            var guids = FindAssetGuids("t:VisualDataSO", folders);
            if (guids.Length == 0)
                guids = FindAssetGuids("t:ScriptableObject", folders);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var visualData = AssetDatabase.LoadAssetAtPath<VisualDataSO>(path);
                if (visualData == null || visualData.BodyPartPrefab == null)
                    continue;

                AddVisualDataPath(AssetDatabase.GetAssetPath(visualData.BodyPartPrefab), visualData);
            }

            visualDataLookupInitialized = true;
        }

        private void AddVisualDataPath(string prefabPath, VisualDataSO visualData)
        {
            if (string.IsNullOrEmpty(prefabPath) || visualData == null)
                return;

            AddVisualDataPathAlias(prefabPath, visualData);

            var assetRoot = "Assets/PyroDuck/EggHeadsLite/";
            var packageRoot = $"Packages/{PackageName}/";

            if (prefabPath.StartsWith(assetRoot))
            {
                AddVisualDataPathAlias(packageRoot + prefabPath.Substring(assetRoot.Length), visualData);
            }
            else if (prefabPath.StartsWith(packageRoot))
            {
                AddVisualDataPathAlias(assetRoot + prefabPath.Substring(packageRoot.Length), visualData);
            }
        }

        private void AddVisualDataPathAlias(string prefabPath, VisualDataSO visualData)
        {
            if (!VisualDataByPrefabPath.ContainsKey(prefabPath))
                VisualDataByPrefabPath.Add(prefabPath, visualData);
        }

        private static string[] GetExistingVisualDataFolders()
        {
            var folders = new List<string>();
            AddFolderIfExists(folders, "Assets/PyroDuck/EggHeadsLite/Runtime/Data");
            AddFolderIfExists(folders, $"Packages/{PackageName}/Runtime/Data");
            return folders.ToArray();
        }

        private static void AddFolderIfExists(List<string> folders, string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                folders.Add(folder);
        }

        private static string[] FindAssetGuids(string filter, string[] folders)
        {
            return folders != null && folders.Length > 0
                ? AssetDatabase.FindAssets(filter, folders)
                : AssetDatabase.FindAssets(filter);
        }

        private static void ConfigureBodyReferences(GameObject root, GameObject bodyInstance)
        {
            if (root == null || bodyInstance == null) return;

            var visualController = root.GetComponent<CharacterVisualController>();
            if (visualController == null) return;

            var bodyTransform = FindDirectChild(bodyInstance.transform, "Body")
                                ?? FindChild(bodyInstance.transform, "Body");
            var bodyRotator = FindDirectChild(bodyInstance.transform, "BodyRotator")
                              ?? FindChild(bodyInstance.transform, "BodyRotator");
            var weaponPlace = FindDirectChild(bodyInstance.transform, "WeaponPlace")
                              ?? FindChild(bodyInstance.transform, "WeaponPlace");

            if (bodyTransform != null)
                visualController.BodyVisual = bodyTransform;

            if (bodyRotator != null)
            {
                visualController.RotatingBodyParent = bodyRotator.gameObject;
                if (bodyRotator.GetComponent<RotationConstraint>() == null)
                    bodyRotator.gameObject.AddComponent<RotationConstraint>();
            }

            var weaponController = root.GetComponent<WeaponController>()
                                   ?? root.GetComponentInChildren<WeaponController>(true);
            weaponController?.SetWeaponPlaceTransform(weaponPlace);

            ConfigureVisualConstraintSources(root.transform, bodyInstance.transform);
        }

        private static void ConfigureVisualConstraintSources(Transform root, Transform bodyRoot)
        {
            SetPositionAndRotationConstraintSources(root, "EyeBrowParent", bodyRoot, "EyeBrowPlace");
            SetPositionAndRotationConstraintSources(root, "DeathEyesParent", bodyRoot, "EyesPlace", "EyePlace");
            SetPositionAndRotationConstraintSources(root, "EyesParent", bodyRoot, "EyesPlace", "EyePlace");
            SetPositionAndRotationConstraintSources(root, "MouthParent", bodyRoot, "MouthPlace");
            SetPositionAndRotationConstraintSources(root, "HatParent", bodyRoot, "HatPlace");
            SetPositionAndRotationConstraintSources(root, "BreadMustacheParent", bodyRoot, "BreadMustachePlace");
            SetPositionAndRotationConstraintSources(root, "NoseParent", bodyRoot, "NosePlace");
            SetPositionAndRotationConstraintSources(root, "WeaponParent", bodyRoot, "WeaponPlace");
        }

        private static void SetPositionAndRotationConstraintSources(
            Transform root,
            string constrainedName,
            Transform sourceRoot,
            params string[] sourceNames)
        {
            var constrained = FindDirectChild(root, constrainedName) ?? FindChild(root, constrainedName);
            var source = FindFirstChild(sourceRoot, sourceNames);
            if (constrained == null || source == null)
                return;

            SetConstraintSource(constrained.GetComponent<PositionConstraint>(), source);
            SetConstraintSource(constrained.GetComponent<RotationConstraint>(), source);
        }

        private static Transform FindFirstChild(Transform root, params string[] names)
        {
            foreach (var name in names)
            {
                var child = FindDirectChild(root, name) ?? FindChild(root, name);
                if (child != null)
                    return child;
            }

            return null;
        }

        private static void SetConstraintSource(PositionConstraint constraint, Transform source)
        {
            if (constraint == null || source == null)
                return;

            var wasLocked = constraint.locked;
            constraint.locked = false;
            var constraintSource = new ConstraintSource { sourceTransform = source, weight = 1f };
            if (constraint.sourceCount == 0)
                constraint.AddSource(constraintSource);
            else
                constraint.SetSource(0, constraintSource);
            constraint.constraintActive = true;
            constraint.weight = 1f;
            constraint.locked = wasLocked;
            EditorUtility.SetDirty(constraint);
        }

        private static void SetConstraintSource(RotationConstraint constraint, Transform source)
        {
            if (constraint == null || source == null)
                return;

            var wasLocked = constraint.locked;
            constraint.locked = false;
            var constraintSource = new ConstraintSource { sourceTransform = source, weight = 1f };
            if (constraint.sourceCount == 0)
                constraint.AddSource(constraintSource);
            else
                constraint.SetSource(0, constraintSource);
            constraint.constraintActive = true;
            constraint.weight = 1f;
            constraint.locked = wasLocked;
            EditorUtility.SetDirty(constraint);
        }

        private static void ClearChildren(Transform slot)
        {
            for (var i = slot.childCount - 1; i >= 0; i--)
                DestroyImmediate(slot.GetChild(i).gameObject);
        }

        private static void RemoveMissingScripts(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var normalized = folder.Replace('\\', '/').Trim('/');
            var parts = normalized.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string NormalizeOutputFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return DefaultOutputFolder;

            var normalized = folder.Replace('\\', '/').TrimEnd('/');
            return normalized == "Assets" || normalized.StartsWith("Assets/")
                ? normalized
                : DefaultOutputFolder;
        }

        private static string SanitizeFileName(string rawName)
        {
            var name = string.IsNullOrWhiteSpace(rawName) ? "GeneratedEggHeadCharacter" : rawName.Trim();
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                name = name.Replace(invalidChar, '_');
            return name;
        }
    }
}