using System;
using UnityEngine;

namespace com.pyroduck.eggheadslite.Editor.Promotion
{
    public sealed class PyroDuckCampaignSettings : ScriptableObject
    {
        [Tooltip("Identity of the package hosting these promotions. Must match campaign productId, not campaign id.")]
        public string currentProductId = "";
        [Tooltip("Previous identities of this same product. Keep Lite and Full identities separate.")]
        public string[] currentProductAliases = Array.Empty<string>();
        [Tooltip("Links to this product, also used to recognize older feeds without productId. Query strings and fragments are ignored.")]
        public string[] currentProductUrls = Array.Empty<string>();
        [Tooltip("Public HTTPS URL of campaigns.json. Empty keeps the bundled card offline.")]
        public string feedUrl = "";
        [Tooltip("Bundled content used until a valid remote catalog is available.")]
        public TextAsset fallbackCatalog;

        public bool IsCurrentProduct(Campaign campaign)
        {
            if (campaign == null) return false;
            if (MatchesId(campaign.productId, currentProductId)) return true;
            if (currentProductAliases != null)
                foreach (var alias in currentProductAliases)
                    if (MatchesId(campaign.productId, alias)) return true;
            string target = ProductUrlKey(campaign.targetUrl);
            if (target != null && currentProductUrls != null)
                foreach (var url in currentProductUrls)
                    if (target == ProductUrlKey(url)) return true;
            return false;
        }

        private static bool MatchesId(string left, string right) =>
            !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string ProductUrlKey(string value)
        {
            if (!CampaignCatalog.IsDestination(value)) return null;
            var uri = new Uri(value);
            string path = uri.AbsolutePath.TrimEnd('/');
            // Asset Store slugs may change while the numeric product identity stays the same.
            if (uri.Host.Equals("assetstore.unity.com", StringComparison.OrdinalIgnoreCase) &&
                path.StartsWith("/packages/", StringComparison.OrdinalIgnoreCase))
            {
                int separator = path.LastIndexOf('-');
                string id = separator >= 0 ? path.Substring(separator + 1) : "";
                if (id.Length > 0 && Array.TrueForAll(id.ToCharArray(), c => c >= '0' && c <= '9'))
                    return "assetstore:" + id;
            }
            return uri.Host.ToLowerInvariant() + path;
        }
    }
}