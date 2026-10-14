using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace com.pyroduck.eggheadslite.Editor.Promotion
{
    /// <summary>Window-owned requests only; no runtime/player dependencies.</summary>
    public sealed class CampaignService : IDisposable
    {
        private readonly string _url;
        private readonly string _fallback;
        private readonly PyroDuckCampaignSettings _settings;
        private readonly string _cacheFolder;
        private readonly Dictionary<string, Texture2D> _images = new Dictionary<string, Texture2D>();
        private IEnumerator _work;
        private bool _disposed;
        public CampaignCatalog Catalog { get; private set; }
        public string Status { get; private set; } = "Featured picks";
        public event Action Changed;

        public CampaignService(string url, string fallback, PyroDuckCampaignSettings settings = null)
        {
            _settings = settings;
            _url = url ?? "";
            _fallback = fallback;
            _cacheFolder = Path.Combine("Library", "PyroDuck", "Campaigns", Hash(_url));
            EditorApplication.update += Tick;
        }

        public List<Campaign> GetActive(DateTimeOffset now) =>
            Catalog?.GetActive(now, _settings) ?? new List<Campaign>();

        public void Refresh(bool remoteEnabled, bool force = false)
        {
            if (_disposed) return;
            (_work as IDisposable)?.Dispose();
            _work = null;
            foreach (var texture in _images.Values) UnityEngine.Object.DestroyImmediate(texture);
            _images.Clear();
            Catalog = null;
            if (remoteEnabled && CampaignCatalog.IsHttps(_url))
            {
                string cached = ReadCache("catalog.json", 262144, TimeSpan.FromDays(7));
                if (CampaignCatalog.TryParse(cached, out var catalog)) Catalog = catalog;
            }
            if (Catalog == null && CampaignCatalog.TryParse(_fallback, out var bundled)) Catalog = bundled;
            Status = remoteEnabled && CampaignCatalog.IsHttps(_url) ? "Checking featured picks…" : "Featured picks";
            Changed?.Invoke();
            if (remoteEnabled && CampaignCatalog.IsHttps(_url)) _work = Fetch(force);
        }

        public Texture2D GetImage(string url)
        {
            // Bundled textures are owned by AssetDatabase, not the download cache.
            // GUIDs survive moving the package between Assets and Packages.
            if (url != null && url.StartsWith("assetguid:", StringComparison.Ordinal))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(url.Substring(10)));
            return url != null && _images.TryGetValue(url, out var texture) ? texture : null;
        }

        private void Tick()
        {
            if (_work == null) return;
            try
            {
                if (_work.MoveNext()) return;
            }
            catch (Exception)
            {
                // A network/cache failure must not break the editor or spam its Console.
                Status = "Offline · showing available picks";
                Changed?.Invoke();
            }
            (_work as IDisposable)?.Dispose();
            _work = null;
        }

        private IEnumerator Fetch(bool force)
        {
            if (force || !CampaignCatalog.TryParse(ReadCache("catalog.json", 262144, TimeSpan.FromHours(6)), out _))
            {
                using (var request = CreateRequest(_url, 262144))
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone) yield return null;
                    var bytes = ((BoundedDownload)request.downloadHandler).Bytes;
                    string json = Encoding.UTF8.GetString(bytes);
                    if (request.result == UnityWebRequest.Result.Success && CampaignCatalog.TryParse(json, out var catalog))
                    {
                        Catalog = catalog; // Empty/disabled valid feeds intentionally suppress fallback ads.
                        WriteCache("catalog.json", bytes);
                        Status = "Featured picks";
                    }
                    else Status = "Offline · showing available picks";
                }
            }
            else Status = "Featured picks";
            Changed?.Invoke();
            if (Catalog == null || !Catalog.enabled) yield break;
            // Fetch scheduled cards too, so dates can become active while the window stays open.
            foreach (var campaign in Catalog.campaigns)
            {
                if (campaign == null || !campaign.enabled || !campaign.active ||
                    (_settings != null && _settings.IsCurrentProduct(campaign)) ||
                    !CampaignCatalog.IsHttps(campaign.imageUrl) || _images.ContainsKey(campaign.imageUrl)) continue;
                string key = Hash(campaign.imageUrl) + ".image";
                byte[] bytes = ReadBytes(key, 4194304, TimeSpan.FromDays(7));
                if (bytes == null)
                {
                    using (var request = CreateRequest(campaign.imageUrl, 4194304))
                    {
                        var operation = request.SendWebRequest();
                        while (!operation.isDone) yield return null;
                        if (request.result != UnityWebRequest.Result.Success) continue;
                        bytes = ((BoundedDownload)request.downloadHandler).Bytes;
                    }
                }
                if (!HasSupportedImageSize(bytes)) continue;
                var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (!texture.LoadImage(bytes, true) || texture.width > 4096 || texture.height > 4096)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    continue;
                }
                _images[campaign.imageUrl] = texture;
                WriteCache(key, bytes);
                Changed?.Invoke();
            }
        }

        private static UnityWebRequest CreateRequest(string url, int limit)
        {
            return new UnityWebRequest(url, "GET", new BoundedDownload(limit), null)
            {
                timeout = 10,
                redirectLimit = 0 // Feed/image URLs must point directly to the HTTPS resource.
            };
        }

        // Check dimensions before decoding untrusted remote images into a Texture2D.
        private static bool HasSupportedImageSize(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 24) return false;
            if (bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10)
            {
                uint width = ((uint)bytes[16] << 24) | ((uint)bytes[17] << 16) | ((uint)bytes[18] << 8) | bytes[19];
                uint height = ((uint)bytes[20] << 24) | ((uint)bytes[21] << 16) | ((uint)bytes[22] << 8) | bytes[23];
                return width > 0 && width <= 4096 && height > 0 && height <= 4096;
            }
            if (bytes[0] != 255 || bytes[1] != 216) return false;
            int offset = 2;
            while (offset + 4 < bytes.Length)
            {
                if (bytes[offset++] != 255) return false;
                while (offset < bytes.Length && bytes[offset] == 255) offset++;
                if (offset + 2 >= bytes.Length) return false;
                int marker = bytes[offset++];
                if (marker == 217 || marker == 218) return false;
                if (marker == 1 || (marker >= 208 && marker <= 215)) continue;
                int length = (bytes[offset] << 8) | bytes[offset + 1];
                if (length < 2 || offset + length > bytes.Length) return false;
                if (marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204)
                {
                    if (length < 8) return false;
                    int height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                    int width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                    return width > 0 && width <= 4096 && height > 0 && height <= 4096;
                }
                offset += length;
            }
            return false;
        }

        private string ReadCache(string name, int limit, TimeSpan age)
        {
            byte[] bytes = ReadBytes(name, limit, age);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        }

        private byte[] ReadBytes(string name, int limit, TimeSpan age)
        {
            try
            {
                var file = new FileInfo(Path.Combine(_cacheFolder, name));
                if (!file.Exists || file.Length > limit || DateTime.UtcNow - file.LastWriteTimeUtc > age) return null;
                return File.ReadAllBytes(file.FullName);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private void WriteCache(string name, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(_cacheFolder);
                File.WriteAllBytes(Path.Combine(_cacheFolder, name), bytes);
                var files = new DirectoryInfo(_cacheFolder).GetFiles("*.image");
                Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = 32; i < files.Length; i++) files[i].Delete();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EditorApplication.update -= Tick;
            (_work as IDisposable)?.Dispose(); // Disposes any in-flight UnityWebRequest.
            _work = null;
            foreach (var texture in _images.Values) UnityEngine.Object.DestroyImmediate(texture);
            _images.Clear();
            Changed = null;
        }

        private sealed class BoundedDownload : DownloadHandlerScript
        {
            private readonly int _limit;
            private readonly MemoryStream _stream = new MemoryStream();
            public byte[] Bytes => _stream.ToArray();
            public BoundedDownload(int limit) : base(new byte[16384]) { _limit = limit; }
            protected override bool ReceiveData(byte[] data, int length)
            {
                if (data == null || _stream.Length + length > _limit) return false;
                _stream.Write(data, 0, length);
                return true;
            }
            public override void Dispose() { _stream.Dispose(); base.Dispose(); }
        }
    }
}