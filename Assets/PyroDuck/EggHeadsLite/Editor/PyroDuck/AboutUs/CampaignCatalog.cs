using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace com.pyroduck.eggheadslite.Editor.Promotion
{
    [Serializable]
    public sealed class Campaign
    {
        public string id;
        public string productId;
        public bool enabled;
        public bool active;
        public int priority;
        public string startUtc;
        public string endUtc;
        public string title;
        public string description;
        public string badge;
        public int discountPercent;

        // Missing or invalid values leave the campaign visible without a sale ribbon.
        public bool HasDiscount => discountPercent > 0 && discountPercent <= 100;
        public string DiscountLabel => HasDiscount ? $"%{discountPercent}" : "";

        public string imageUrl;
        public string targetUrl;
    }

    [Serializable]
    public sealed class CampaignCatalog
    {
        public int schemaVersion;
        public bool enabled;
        public Campaign[] campaigns;

        public static bool TryParse(string json, out CampaignCatalog catalog)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > 262144) return false;
            try
            {
                var parsed = JsonUtility.FromJson<CampaignCatalog>(json);
                if (parsed == null || parsed.schemaVersion != 1 || parsed.campaigns == null ||
                    parsed.campaigns.Length > 20) return false;
                catalog = parsed;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public List<Campaign> GetActive(DateTimeOffset now, PyroDuckCampaignSettings settings = null)
        {
            var result = new List<Campaign>();
            if (!enabled || campaigns == null) return result;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in campaigns)
            {
                if (item == null || !item.enabled || !item.active || string.IsNullOrWhiteSpace(item.id) ||
                    string.IsNullOrWhiteSpace(item.title) || item.title.Length > 100 ||
                    (item.description?.Length ?? 0) > 500 || (item.badge?.Length ?? 0) > 80 ||
                    !IsDestination(item.targetUrl) || !InDateRange(item, now) ||
                    (settings != null && settings.IsCurrentProduct(item)) || !ids.Add(item.id)) continue;
                result.Add(item);
            }
            result.Sort((a, b) =>
            {
                int priority = b.priority.CompareTo(a.priority);
                return priority != 0 ? priority : string.CompareOrdinal(a.id, b.id);
            });
            return result;
        }

        private static bool InDateRange(Campaign item, DateTimeOffset now)
        {
            var start = DateTimeOffset.MinValue;
            var end = DateTimeOffset.MaxValue;
            if (!string.IsNullOrEmpty(item.startUtc) && !TryDate(item.startUtc, out start)) return false;
            if (!string.IsNullOrEmpty(item.endUtc) && !TryDate(item.endUtc, out end)) return false;
            return start < end && now >= start && now < end;
        }

        private static bool TryDate(string value, out DateTimeOffset date)
        {
            return DateTimeOffset.TryParseExact(value,
                new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" },
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);
        }

        public static bool IsHttps(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                   uri.Port == 443 && string.IsNullOrEmpty(uri.UserInfo) &&
                   uri.HostNameType == UriHostNameType.Dns && uri.Host.Contains(".") &&
                   !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDestination(string value)
        {
            if (!IsHttps(value)) return false;
            var uri = new Uri(value);
            return uri.Host.Equals("assetstore.unity.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.Equals("pyroduck.com", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.EndsWith(".pyroduck.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}