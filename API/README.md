# PyroDuck kampanya API'si

Veritabanı ve Composer gerektirmeyen PHP API ve oturum korumalı yönetim paneli.
PHP 8.1 veya üzeri gerekir; sunucuda halen güvenlik güncellemesi alan bir PHP
sürümü kullanın. HTTPS zorunludur. Unity için JWT veya gizli anahtar gerekmez.

## Kurulum

1. Bu klasörü sitenin köküne `API` adıyla yükleyin. `tests` klasörünü yüklemeniz
   gerekmez. PHP dosyalarının çalıştırıldığı bir hosting kullanın.
2. Yerel bilgisayarda veya sunucunun terminalinde bu klasöre girip çalıştırın:

   ```sh
   php setup.php
   ```

   Kullanıcı adı ve 12-72 bayt uzunluğunda güçlü bir parola girin. Parola terminalde
   görünebilir. Üretilen `config.local.php` dosyasında yalnızca parola hash'i tutulur.
   Yerelde oluşturduysanız bu dosyayı sunucudaki `API` klasörüne ayrıca yükleyin.
   Sunucuda SSH yoksa yerelde PHP ile çalıştırabilirsiniz. Web üzerinden açık bir
   hesap oluşturma sayfası yoktur. Aynı komutla kullanıcı/parola değiştirilebilir.
3. PHP kullanıcısına `API/private` klasörüne yazma yetkisi verin. `777` kullanmayın.
   Daha iyi izolasyon için `config.local.php` dönüş dizisine `storage_dir` ekleyip
   site kökünün dışında, PHP'nin yazabildiği bir mutlak dizin belirtin.
4. Yönetim panelini açın: `https://pyroduck.com/API/admin.php`.
5. API yanıtını kontrol edin: `https://pyroduck.com/API/campaigns.php`.
   Yanıt HTTP 200 ve JSON, görsel adresi HTTP 200 ve PNG olmalıdır. Hostun bot
   doğrulaması veya yönlendirmesi Unity isteklerini engellememelidir.

Klasör adı büyük/küçük harfe duyarlıdır. `/api` adıyla yüklerseniz `config.local.php`
içine `'public_base_url' => 'https://pyroduck.com/api'` ekleyin ve bağlantıları buna
göre değiştirin. Önceden kaydedilmiş kampanyaların görsel adreslerini panelde düzeltin.

Apache `.htaccess` desteği varsa dahil edilen dosyalar dizin listelemesini ve özel
dosyalara erişimi engeller. Hosting `Options -Indexes` satırına izin vermiyor ve
HTTP 500 dönüyorsa sadece bu satırı kaldırın, listelemeyi hosting panelinden kapatın.
Nginx `.htaccess` okumaz; mevcut PHP/FastCGI ayarlarına şu engelleri ekleyin:

```nginx
location ^~ /API/private/ { deny all; }
location ^~ /API/tests/ { deny all; }
location ~ ^/API/(config(?:\.local)?\.php|bootstrap\.php|setup\.php|README\.md|campaigns\.example\.json)$ {
    deny all;
}
```

PHP yapılandırma ve veri dosyaları doğrudan çağrıldıklarında ayrıca 404 döndürür.
HTTPS ters proxy üzerinde sonlanıyorsa hostingin PHP'ye güvenilir HTTPS bilgisi
ilettiğini yapılandırın. Uygulama istemciden gelen X-Forwarded-Proto başlığına güvenmez.

## Unity bağlantısı

Unity Inspector'da şu dosyayı seçin:

`Assets/PyroDuck/EggHeadsLite/Editor/PyroDuck/AboutUs/PyroDuckCampaignSettings.asset`

**Feed Url**:

```text
https://pyroduck.com/API/campaigns.php?product=eggheads-lite
```

**Fallback Catalog** atamasını koruyun. Generator ve About Us pencerelerini kapatıp
açın. About Us içinde **Load online featured content** açıkken **Refresh** kullanın.
Bu ayarı içeren Lite paketini yeniden dağıtın; eski boş URL'li paketler API'yi
kendiliğinden keşfedemez. Bu hazırlık Unity ayarını değiştirmez.

## Kampanya yönetimi

Panelde JSON'u düzenleyip **Kontrol et ve kaydet** düğmesine basın. Geçersiz veri
kaydedilmez. Başlangıç kampanyası Full ürün bağlantısını ve yerel PNG'yi kullanır;
indirim oranı 0'dır. Görselleri FTP/hosting paneliyle `images` klasörüne yükleyin,
panelde tam HTTPS adresini yazın. PNG/JPEG, en fazla 4 MiB ve 4096×4096 kullanın.
Yeni görsel için farklı dosya adı kullanın; Unity görselleri yedi gün önbellekler.

- `enabled`: En üst seviyede false olursa tüm kartlar gizlenir.
- `active`, kampanya içindeki `enabled`: Tek kartın gösterimini kontrol eder.
- `discountPercent`: 0 etiketi gizler; 1-100 gerçek indirim oranı gösterir.
- `startUtc`, `endUtc`: Boş değer süresizdir. Örnek: `2026-10-10T00:00:00Z`.
  Başlangıç dahil, bitiş hariçtir. Tarih kontrolünü Unity yapar; ileride başlayacak
  kampanyalar da API yanıtında kalır.
- `priority`: Büyük değer önce gösterilir; sıralamayı Unity yapar.

İsteğe bağlı kampanya hedeflemesi:

```json
"products": ["eggheads-lite"],
"languages": ["en", "tr"],
"minVersion": "1.0",
"maxVersion": "2.0"
```

Bu alanlar yoksa kampanya herkes içindir. Hedefleme varsa ilgili sorgu parametresi
zorunludur; uyuşmayan veya eksik parametre kampanyayı sonuçtan çıkarır. Sürüm
sınırları dahildir. Örnek sorgu:

```text
https://pyroduck.com/API/campaigns.php?product=eggheads-lite&version=1.0&lang=tr
```

Mevcut Unity okuyucusu sürüm/dil parametrelerini otomatik eklemez. Şimdilik Feed Url
içinde sabit belirtilebilir; otomatik hedefleme için Unity tarafında ayrıca kod
değişikliği gerekir. Parametreler kimlik veya lisans doğrulaması değildir.

## Önbellek ve koruma

API Cache-Control ile beş dakikalık HTTP önbelleğine izin verir; Unity ayrıca altı
saatlik yerel önbellek kullanır. Anında kontrol için About Us > Refresh kullanın.
API 304 döndürmez; mevcut Unity okuyucusu doğrudan HTTP 200 bekler. Hata yanıtları
önbelleklenmez. Ağ hatalarında Unity mevcut yerel yedeğini kullanır.

Yönetim paneli hash ile parola doğrulaması, HttpOnly/SameSite oturum çerezi, CSRF
kontrolü ve 30 dakikalık hareketsizlik süresi kullanır. Panel dışı kampanya yazma
endpoint'i yoktur. JSON kaydı atomik dosya değişimiyle yapılır; eşzamanlı panel
kayıtlarında son tamamlanan kayıt geçerlidir.

API için dakikada 120, giriş için 15 dakikada 10 deneme sınırı vardır. İstemci IP'si
hash'lenerek 4096 olası kovadan birine atanır; aynı kovaya düşen IP'ler sınırı
paylaşabilir. Ham IP kaydedilmez. Proxy arkasında tüm istekler aynı IP'den görünebilir;
gerekiyorsa hosting katmanında güvenilir gerçek IP ve istek sınırı yapılandırın.
Dağıtık sunucular için dosya yerine ortak bir limit deposu gerekir.

Parola değişimi mevcut oturumları anında iptal etmez; acil iptal için hostingin PHP
oturumlarını temizleyin. API analiz/telemetri toplamıyor; standart sunucu erişim
logları hosting ayarlarına bağlıdır. JWT kullanılmıyor.

## Yerel test

Kampanyadaki `productId` reklamı yapılan üründür; `id` kampanyanın kimliğidir.
Örneğin Full reklamında `productId: "eggheads-full"` kullanılır. `?product=` ise
reklamı gösteren pakettir. Aynı kimlikler eşleştiğinde API reklamı döndürmez.
`products` listesi gösterilecek hedef paketleri belirler; bu iki alan farklıdır.
Unity ayarlarındaki `Current Product Id` de aynı paket kimliği olmalıdır; istemci
önbellek ve çevrimdışı yedekteki kendi reklamını ayrıca gizler. Eski kataloglarda
`productId` isteğe bağlıdır; Unity'de `Current Product Urls` ile eski bağlantılar
eşleştirilebilir. Güncel alanı mevcut kayıtlı katalogdaki her kampanyaya ekleyin;
örnek JSON dosyasını değiştirmek mevcut yönetim paneli kaydını değiştirmez.

```sh
php tests/integration.php
```

Testler geçici bir kopyada yerel PHP sunucusu açar; gerçek ayar ve kampanya verilerini
değiştirmez. Üretim sunucusunda test klasörünü yayınlamayın.

PHP referansları: [parola hash'i](https://www.php.net/manual/en/function.password-hash.php),
[oturum çerezi seçenekleri](https://www.php.net/manual/en/function.session-set-cookie-params.php).
