# pyroduck.com kampanya dosyalari

Bu klasor mevcut EggHeads Lite kampanya okuyucusu icin hazirlanmistir.

## Sunucuya yukleme

Site kok dizinine asagidaki dosyalari yukleyin:

- `campaigns.json` -> `https://pyroduck.com/campaigns.json`
- `campaigns/egg-heads-full-v1.png` -> `https://pyroduck.com/campaigns/egg-heads-full-v1.png`

Her iki adres de HTTPS uzerinden dogrudan HTTP 200 donmelidir. Yonlendirme,
giris ekrani veya bot dogrulama sayfasi olmamalidir. JSON icin
`Content-Type: application/json; charset=utf-8`, gorsel icin `image/png` kullanin.
JSON icin onerilen HTTP basligi: `Cache-Control: public, max-age=300`.
Unity okuyucusu JSON'u ayrica alti saat boyunca yerel onbellekte tutar.

## Unity baglantisi

Dosyalar yayinlandiktan sonra
`Assets/PyroDuck/EggHeadsLite/Editor/PyroDuck/AboutUs/PyroDuckCampaignSettings.asset`
dosyasini Unity Inspector'da secin ve **Feed Url** alanina
`https://pyroduck.com/campaigns.json` yazin. **Fallback Catalog** atamasini koruyun.

Generator ve About Us pencerelerini kapatip yeniden acin. About Us icinde
**Load online featured content** acik olmali; **Refresh** ile yeni istegi zorlayin.
Bu ayari iceren Lite paketini dagitin. Feed Url alani bos olan eski kullanici
paketleri bu adresi otomatik kesfedemez.

## Kampanyayi degistirme

- `description`, `title` ve `badge`: Tanitim metinleri.
- `targetUrl`: Full urunun Asset Store adresi.
- `discountPercent`: Su anda `0`; indirim etiketi kapali. Gercek bir kampanyada
  1-100 arasinda tam sayi kullanin.
- `startUtc` ve `endUtc`: Bos oldugunda suresiz. Zamanli kampanya icin
  `2026-10-10T00:00:00Z` bicimini kullanin. Baslangic dahil, bitis harictir.
- Kampanyalari tamamen kapatmak icin kokteki `enabled` degerini `false` yapin.
- Gorseli degistirirken yeni bir dosya adi ve imageUrl kullanin, ornegin
  `egg-heads-full-v2.png`; boylece eski gorsel onbellegi kullanilmaz.

Bu dosyalar yayinlamaya hazirdir; siteye yukleme veya Unity Feed Url ayarini
degistirme bu hazirlik kapsaminda yapilmamistir.
