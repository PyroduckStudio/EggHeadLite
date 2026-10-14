using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.pyroduck.eggheadslite.Editor.Promotion
{
    public sealed class PyroDuckAboutWindow : EditorWindow
    {
        private const string RemotePreference = "PyroDuck.EggHeadsLite.RemoteFeatured";
        private CampaignService _service;
        private List<Campaign> _active = new List<Campaign>();
        private int _index;
        private bool _hovered;
        private double _nextSlide;
        private double _nextRefresh;
        private string _folder;
        private Image _image;
        private Label _placeholder;
        private Label _title;
        private Label _description;
        private Label _badge;
        private Label _discountRibbon;
        private Label _status;
        private VisualElement _card;
        private VisualElement _dots;
        private Label _empty;
        private Button _view;
        private IVisualElementScheduledItem _timer;

        [MenuItem("Tools/PyroDuck/About Us")]
        public static void ShowWindow()
        {
            var window = GetWindow<PyroDuckAboutWindow>();
            window.titleContent = new GUIContent("PyroDuck");
            window.minSize = new Vector2(420, 600);
        }

        public void CreateGUI()
        {
            Cleanup();
            rootVisualElement.Clear();
            titleContent = new GUIContent("PyroDuck");
            minSize = new Vector2(420, 600);
            _folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))).Replace('\\', '/');
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(_folder + "/PyroDuckAboutWindow.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(_folder + "/PyroDuckAboutWindow.uss");
            if (tree == null)
            {
                rootVisualElement.Add(new HelpBox("Reimport the PyroDuck About Us editor assets to restore this window.", HelpBoxMessageType.Error));
                return;
            }
            tree.CloneTree(rootVisualElement);
            if (styles != null && !rootVisualElement.styleSheets.Contains(styles)) rootVisualElement.styleSheets.Add(styles);
            rootVisualElement.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            foreach (var label in rootVisualElement.Query<Label>().ToList()) label.enableRichText = false;
            _image = rootVisualElement.Q<Image>("campaign-image");
            _placeholder = rootVisualElement.Q<Label>("image-placeholder");
            _title = rootVisualElement.Q<Label>("campaign-title");
            _description = rootVisualElement.Q<Label>("campaign-description");
            _badge = rootVisualElement.Q<Label>("campaign-badge");
            _discountRibbon = rootVisualElement.Q<Label>("discount-ribbon");
            _status = rootVisualElement.Q<Label>("status");
            _card = rootVisualElement.Q("campaign-card");
            _dots = rootVisualElement.Q("dots");
            _empty = rootVisualElement.Q<Label>("empty-state");
            _view = rootVisualElement.Q<Button>("view-asset-button");
            rootVisualElement.Q<Button>("asset-store-button").clicked += () => OpenLink("https://assetstore.unity.com/publishers/137662");
            rootVisualElement.Q<Button>("website-button").clicked += () => OpenLink("https://pyroduck.com");
            _view.clicked += () =>
            {
                Refilter();
                if (_active.Count > 0) OpenLink(_active[_index].targetUrl);
            };
            rootVisualElement.Q<Button>("previous").clicked += () => Advance(-1);
            rootVisualElement.Q<Button>("next").clicked += () => Advance(1);
            var featured = rootVisualElement.Q("featured");
            featured.RegisterCallback<MouseEnterEvent>(_ => _hovered = true);
            featured.RegisterCallback<MouseLeaveEvent>(_ => { _hovered = false; ResetSlideClock(); });
            var toggle = rootVisualElement.Q<Toggle>("remote-toggle");
            toggle.SetValueWithoutNotify(EditorPrefs.GetBool(RemotePreference, true));
            toggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(RemotePreference, evt.newValue);
                _service.Refresh(evt.newValue);
            });
            rootVisualElement.Q<Button>("refresh").clicked += () => _service.Refresh(toggle.value, true);
            var settings = AssetDatabase.LoadAssetAtPath<PyroDuckCampaignSettings>(_folder + "/PyroDuckCampaignSettings.asset");
            var fallback = settings != null ? settings.fallbackCatalog : AssetDatabase.LoadAssetAtPath<TextAsset>(_folder + "/campaigns.json");
            _service = new CampaignService(settings != null ? settings.feedUrl : "", fallback != null ? fallback.text : "", settings);
            _service.Changed += Render;
            _hovered = false;
            _index = 0;
            _service.Refresh(toggle.value);
            ResetSlideClock();
            _nextRefresh = EditorApplication.timeSinceStartup + 21600;
            _timer = rootVisualElement.schedule.Execute(Tick).Every(250);
        }

        private void Refilter()
        {
            string currentId = _active.Count > 0 ? _active[_index].id : null;
            _active = _service.GetActive(DateTimeOffset.UtcNow);
            int existing = _active.FindIndex(c => c.id == currentId);
            _index = existing >= 0 ? existing : 0;
        }

        private void Render()
        {
            Refilter();
            _status.text = _service.Status;
            bool hasCards = _active.Count > 0;
            _card.style.display = hasCards ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.style.display = hasCards ? DisplayStyle.None : DisplayStyle.Flex;
            _dots.Clear();
            rootVisualElement.Q("navigation").style.display = _active.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasCards) { _image.image = null; return; }
            var campaign = _active[_index];
            _discountRibbon.text = $"{campaign.DiscountLabel} OFF";
            _discountRibbon.style.display = campaign.HasDiscount ? DisplayStyle.Flex : DisplayStyle.None;
            _title.text = campaign.title;
            _description.text = campaign.description ?? "";
            _badge.text = campaign.badge ?? "";
            _badge.style.display = string.IsNullOrEmpty(campaign.badge) ? DisplayStyle.None : DisplayStyle.Flex;
            _image.image = _service.GetImage(campaign.imageUrl);
            _placeholder.text = campaign.title;
            _placeholder.style.display = _image.image != null ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < _active.Count; i++)
            {
                int selected = i;
                var dot = new Button(() => { _index = selected; ResetSlideClock(); DrawSelected(); })
                {
                    text = i == _index ? "●" : "○", tooltip = _active[i].title
                };
                dot.AddToClassList("dot");
                _dots.Add(dot);
            }
        }

        private void DrawSelected()
        {
            // Render preserves selection by id, including after date/priority changes.
            Render();
        }

        private void Advance(int direction)
        {
            Refilter();
            if (_active.Count > 0) _index = (_index + direction + _active.Count) % _active.Count;
            ResetSlideClock();
            DrawSelected();
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextRefresh)
            {
                _nextRefresh = now + 21600;
                _service.Refresh(EditorPrefs.GetBool(RemotePreference, true));
            }
            // Date boundaries are evaluated even while hover pauses slide rotation.
            var valid = _service.GetActive(DateTimeOffset.UtcNow);
            bool changed = (valid?.Count ?? 0) != _active.Count;
            if (!changed && valid != null)
                for (int i = 0; i < valid.Count; i++) if (valid[i].id != _active[i].id) { changed = true; break; }
            if (changed) Render();
            var focused = rootVisualElement.focusController.focusedElement as VisualElement;
            bool keyboardInside = focused != null && rootVisualElement.Q("featured").Contains(focused);
            if (!_hovered && !keyboardInside && now >= _nextSlide) Advance(1);
        }

        private void ResetSlideClock() { _nextSlide = EditorApplication.timeSinceStartup + 5; }
        private static void OpenLink(string url) { if (CampaignCatalog.IsDestination(url)) Application.OpenURL(url); }
        private void OnDisable() { Cleanup(); }
        private void Cleanup()
        {
            _timer?.Pause();
            _timer = null;
            _service?.Dispose();
            _service = null;
        }
    }
}