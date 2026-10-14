using System;
using com.pyroduck.eggheadslite.Editor.Promotion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public class CampaignTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        private static Campaign Card(string id, int priority = 0)
        {
            return new Campaign { id = id, title = id, active = true, enabled = true,
                priority = priority, targetUrl = "https://assetstore.unity.com/publishers/137662" };
        }

        [Test]
        public void Catalog_FiltersDatesFlagsAndSortsByPriority()
        {
            var low = Card("low");
            var high = Card("high", 10);
            var expired = Card("expired"); expired.endUtc = "2026-09-06T12:00:00Z";
            var future = Card("future"); future.startUtc = "2026-09-07T00:00:00Z";
            var disabled = Card("disabled"); disabled.enabled = false;
            var inactive = Card("inactive"); inactive.active = false;
            var malformed = Card("malformed"); malformed.endUtc = "tomorrow";
            high.startUtc = "2026-09-06T12:00:00Z";
            var catalog = new CampaignCatalog { enabled = true, campaigns = new[] { low, expired, future, disabled, inactive, malformed, high } };
            var active = catalog.GetActive(Now);
            Assert.AreEqual(2, active.Count);
            Assert.AreEqual("high", active[0].id);
            Assert.AreEqual("low", active[1].id);
        }

        [Test]
        public void Catalog_KillSwitchAndEmptyFeedAreValid()
        {
            Assert.IsTrue(CampaignCatalog.TryParse("{\"schemaVersion\":1,\"enabled\":false,\"campaigns\":[]}", out var catalog));
            Assert.IsEmpty(catalog.GetActive(Now));
            Assert.IsFalse(CampaignCatalog.TryParse("{\"schemaVersion\":2,\"campaigns\":[]}", out _));
            Assert.IsFalse(CampaignCatalog.TryParse("<html>error</html>", out _));
        }

        [TestCase("javascript:alert(1)")]
        [TestCase("file:///C:/secret")]
        [TestCase("http://pyroduck.com")]
        [TestCase("https://assetstore.unity.com.evil.example/asset")]
        [TestCase("https://user:password@pyroduck.com")]
        public void Destination_RejectsUnsafeOrUnrelatedLinks(string url)
        {
            Assert.IsFalse(CampaignCatalog.IsDestination(url));
        }

        [Test]
        public void Catalog_DeduplicatesAndRejectsInvalidDateRanges()
        {
            var broken = Card("broken"); broken.startUtc = "2026-09-07T00:00:00Z"; broken.endUtc = "2026-09-06T00:00:00Z";
            var catalog = new CampaignCatalog { enabled = true, campaigns = new[] { Card("same"), Card("same"), broken } };
            Assert.AreEqual(1, catalog.GetActive(Now).Count);
        }

        [Test]
        public void ProductIdentity_HidesEverySelfCampaignButKeepsOtherProducts()
        {
            var settings = ScriptableObject.CreateInstance<PyroDuckCampaignSettings>();
            try
            {
                settings.currentProductId = "eggheads-full";
                settings.currentProductAliases = new[] { "old-full-name" };
                var self = Card("sale"); self.productId = " EGGHEADS-FULL ";
                var alias = Card("launch"); alias.productId = "old-full-name";
                var lite = Card("lite"); lite.productId = "eggheads-lite";
                var other = Card("another-project"); other.productId = "duck-game";
                var catalog = new CampaignCatalog { enabled = true, campaigns = new[] { self, alias, lite, other } };
                Assert.AreEqual(2, catalog.GetActive(Now, settings).Count);
                settings.currentProductId = "eggheads-lite";
                settings.currentProductAliases = Array.Empty<string>();
                Assert.AreEqual(3, catalog.GetActive(Now, settings).Count);
                Assert.Contains(self, catalog.GetActive(Now, settings));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void LegacyFeed_MatchesProductLinkDespiteTrackingAndChangedStoreSlug()
        {
            var settings = ScriptableObject.CreateInstance<PyroDuckCampaignSettings>();
            try
            {
                settings.currentProductUrls = new[] { "https://assetstore.unity.com/packages/2d/characters/egg-heads-2d-364008" };
                var legacy = Card("old-campaign");
                legacy.targetUrl = "https://assetstore.unity.com/packages/2d/characters/new-name-364008/?utm_source=lite#details";
                Assert.IsTrue(settings.IsCurrentProduct(legacy));
                legacy.targetUrl = "https://assetstore.unity.com/packages/2d/characters/new-name-364009";
                Assert.IsFalse(settings.IsCurrentProduct(legacy));
                settings.currentProductUrls = new[] { "https://pyroduck.com/products/duck" };
                legacy.targetUrl = "https://pyroduck.com/products/duck/?ref=setup";
                Assert.IsTrue(settings.IsCurrentProduct(legacy));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void BothWindows_HideSelfPromotionFromOfflineFallback()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PyroDuckCampaignSettings>(
                "Assets/PyroDuck/EggHeadsLite/Editor/PyroDuck/AboutUs/PyroDuckCampaignSettings.asset");
            string originalId = settings.currentProductId, originalFeed = settings.feedUrl;
            var about = ScriptableObject.CreateInstance<PyroDuckAboutWindow>();
            var setup = ScriptableObject.CreateInstance<EggHeadsLiteSetupWindow>();
            try
            {
                settings.currentProductId = "eggheads-full";
                settings.feedUrl = "";
                about.CreateGUI();
                setup.CreateGUI();
                Assert.AreEqual(DisplayStyle.None, about.rootVisualElement.Q("campaign-card").style.display.value);
                Assert.IsTrue(setup.rootVisualElement.Q("campaign-card").ClassListContains("setup-hidden"));
                Assert.IsNotNull(setup.rootVisualElement.Q<Button>("generator"));
            }
            finally
            {
                settings.currentProductId = originalId;
                settings.feedUrl = originalFeed;
                UnityEngine.Object.DestroyImmediate(about);
                UnityEngine.Object.DestroyImmediate(setup);
            }
        }

        [Test]
        public void Service_UnconfiguredFeedUsesBundledCatalogAndDisposesTwice()
        {
            var fallback = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PyroDuck/EggHeadsLite/Editor/PyroDuck/AboutUs/campaigns.json");
            var service = new CampaignService("", fallback.text);
            try
            {
                service.Refresh(true);
                Assert.AreEqual("egg-heads-2d", service.Catalog.GetActive(Now)[0].id);
                service.Refresh(false);
                Assert.AreEqual(1, service.Catalog.GetActive(Now).Count);
            }
            finally { service.Dispose(); service.Dispose(); }
        }

        [Test]
        public void AboutWindow_LoadsUxmlStylesAndFallbackCard()
        {
            var window = ScriptableObject.CreateInstance<PyroDuckAboutWindow>();
            try
            {
                window.CreateGUI();
                var root = window.rootVisualElement;
                Assert.IsNotNull(root.Q<Button>("asset-store-button"));
                Assert.AreEqual("Egg Heads 2D", root.Q<Label>("campaign-title").text);
                Assert.AreEqual(DisplayStyle.Flex, root.Q("campaign-card").style.display.value);
                Assert.Greater(root.styleSheets.count, 0);
                // Recreating the UI must dispose the previous service and avoid duplicate trees.
                window.CreateGUI();
                Assert.AreEqual(1, root.Query<Button>("view-asset-button").ToList().Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }
    }
}