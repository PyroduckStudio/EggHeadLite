using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.pyroduck.eggheadslite.Editor.Promotion
{
    public sealed class EggHeadsLiteSetupWindow : EditorWindow
    {
        private const string RemotePreference = "PyroDuck.EggHeadsLite.RemoteFeatured";
        private CampaignService _service;
        private IVisualElementScheduledItem _timer;
        private int _index;
        private bool _hovered;
        private double _nextSlide;
        private double _nextRefresh;
        private string _displayed;
        private Texture2D _displayedImage;
        private string _displayedStatus;

        internal static string ShownPreference => "PyroDuck.EggHeadsLite.SetupShown." + Hash128.Compute(Application.dataPath);

        [MenuItem("Tools/PyroDuck/EggHeadsLite/Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<EggHeadsLiteSetupWindow>();
            window.titleContent = new GUIContent("EggHeads Lite Setup");
            window.minSize = new Vector2(520, 620);
            window.Show();
        }

        public void CreateGUI()
        {
            Cleanup();
            rootVisualElement.Clear();
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))).Replace('\\', '/');
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/EggHeadsLiteSetupWindow.uxml");
            if (tree == null)
            {
                rootVisualElement.Add(new HelpBox("Reimport the EggHeads Lite editor assets to restore Setup.", HelpBoxMessageType.Error));
                return;
            }
            tree.CloneTree(rootVisualElement);
            foreach (var file in new[] { "PyroDuckAboutWindow.uss", "EggHeadsLiteSetupWindow.uss" })
            {
                var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(folder + "/" + file);
                if (style != null) rootVisualElement.styleSheets.Add(style);
            }
            rootVisualElement.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            foreach (var label in rootVisualElement.Query<Label>().ToList()) label.enableRichText = false;
            rootVisualElement.Q<Button>("generator").clicked += () => EditorApplication.ExecuteMenuItem("Tools/PyroDuck/EggHeadsLite/Generator");
            rootVisualElement.Q<Button>("demo").clicked += EggHeadsLiteCharacterPrefabGenerator.OpenDemo;
            rootVisualElement.Q<Button>("quick-start").clicked += () => EggHeadsLiteCharacterPrefabGenerator.OpenDocumentation("QuickStart.md");
            rootVisualElement.Q<Button>("migration").clicked += () => EggHeadsLiteCharacterPrefabGenerator.OpenDocumentation("UpgradeToFull.md");
            rootVisualElement.Q<Button>("done").clicked += Close;
            rootVisualElement.Q<Button>("previous").clicked += () => Advance(-1);
            rootVisualElement.Q<Button>("next").clicked += () => Advance(1);
            rootVisualElement.Q<Button>("view-asset").clicked += OpenSelected;
            var featured = rootVisualElement.Q("featured");
            featured.RegisterCallback<MouseEnterEvent>(_ => _hovered = true);
            featured.RegisterCallback<MouseLeaveEvent>(_ => { _hovered = false; ResetSlide(); });
            var settings = AssetDatabase.LoadAssetAtPath<PyroDuckCampaignSettings>(folder + "/PyroDuckCampaignSettings.asset");
            var fallback = settings != null ? settings.fallbackCatalog : AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/campaigns.json");
            _service = new CampaignService(settings != null ? settings.feedUrl : "", fallback != null ? fallback.text : "", settings);
            _service.Changed += RenderCampaign;
            var remote = rootVisualElement.Q<Toggle>("remote-toggle");
            remote.SetValueWithoutNotify(EditorPrefs.GetBool(RemotePreference, true));
            remote.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(RemotePreference, evt.newValue);
                _service.Refresh(evt.newValue);
            });
            rootVisualElement.Q<Button>("refresh").clicked += () => _service.Refresh(remote.value, true);
            _index = 0;
            _hovered = false;
            _service.Refresh(remote.value);
            ResetSlide();
            _nextRefresh = EditorApplication.timeSinceStartup + 21600;
            _timer = rootVisualElement.schedule.Execute(Tick).Every(250);
            // Persist only after the complete page was created. Reimports and script
            // reloads should not show another welcome page in the same project.
            EditorPrefs.SetBool(ShownPreference, true);
        }

        private Campaign Selected(out int count)
        {
            var active = _service.GetActive(DateTimeOffset.UtcNow);
            count = active?.Count ?? 0;
            if (count == 0) return null;
            _index = ((_index % count) + count) % count;
            return active[_index];
        }

        private void RenderCampaign()
        {
            var card = Selected(out var count);
            _displayed = card != null ? JsonUtility.ToJson(card) : null;
            _displayedImage = card != null ? _service.GetImage(card.imageUrl) : null;
            _displayedStatus = _service.Status;
            rootVisualElement.Q<Label>("status").text = _displayedStatus;
            rootVisualElement.Q("campaign-card").EnableInClassList("setup-hidden", card == null);
            rootVisualElement.Q("empty-state").EnableInClassList("setup-hidden", card != null);
            rootVisualElement.Q("navigation").EnableInClassList("setup-hidden", count < 2);
            rootVisualElement.Q<Image>("campaign-image").image = _displayedImage;
            if (card == null) return;
            rootVisualElement.Q<Label>("campaign-title").text = card.title;
            rootVisualElement.Q<Label>("campaign-description").text = card.description ?? "";
            var badge = rootVisualElement.Q<Label>("campaign-badge");
            badge.text = card.badge ?? "";
            badge.EnableInClassList("setup-hidden", string.IsNullOrEmpty(card.badge));
            var placeholder = rootVisualElement.Q<Label>("image-placeholder");
            placeholder.text = card.title;
            placeholder.EnableInClassList("setup-hidden", _displayedImage != null);
            var ribbon = rootVisualElement.Q<Label>("discount-ribbon");
            ribbon.text = card.HasDiscount ? card.DiscountLabel + " OFF" : "";
            ribbon.EnableInClassList("setup-sale", card.HasDiscount);
            rootVisualElement.Q<Label>("slide-count").text = (_index + 1) + " / " + count;
        }

        private void OpenSelected()
        {
            var card = Selected(out _);
            if (card != null && CampaignCatalog.IsDestination(card.targetUrl)) Application.OpenURL(card.targetUrl);
        }

        private void Advance(int direction)
        {
            var card = Selected(out var count);
            if (card != null) _index = (_index + direction + count) % count;
            ResetSlide();
            RenderCampaign();
        }

        private void ResetSlide() { _nextSlide = EditorApplication.timeSinceStartup + 5; }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextRefresh)
            {
                _nextRefresh = now + 21600;
                _service.Refresh(EditorPrefs.GetBool(RemotePreference, true));
            }
            var focused = rootVisualElement.focusController.focusedElement as VisualElement;
            bool keyboardInside = focused != null && rootVisualElement.Q("featured").Contains(focused);
            if (!_hovered && !keyboardInside && now >= _nextSlide) Advance(1);
            var card = Selected(out _);
            if ((card != null ? JsonUtility.ToJson(card) : null) != _displayed ||
                (card != null ? _service.GetImage(card.imageUrl) : null) != _displayedImage || _service.Status != _displayedStatus)
                RenderCampaign();
        }

        private void OnDisable() { Cleanup(); }
        private void Cleanup()
        {
            _timer?.Pause();
            _timer = null;
            _service?.Dispose();
            _service = null;
        }
    }

    [InitializeOnLoad]
    internal static class EggHeadsLiteSetupOnImport
    {
        static EggHeadsLiteSetupOnImport()
        {
            if (!Application.isBatchMode) EditorApplication.update += TryShow;
        }

        private static void TryShow()
        {
            if (EditorPrefs.GetBool(EggHeadsLiteSetupWindow.ShownPreference, false))
            {
                EditorApplication.update -= TryShow;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Import can span multiple updates. Wait until the UI is available.
            string path = AssetDatabase.GUIDToAssetPath("1cdd5f1f21774c9a8a250c6090da6e40");
            if (AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path) == null) return;
            EditorApplication.update -= TryShow;
            EggHeadsLiteSetupWindow.ShowWindow();
        }
    }
}
