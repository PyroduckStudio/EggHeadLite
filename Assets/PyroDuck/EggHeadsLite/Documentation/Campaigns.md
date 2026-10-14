# PyroDuck editor campaigns

Open **Tools > PyroDuck > About Us**, or **About PyroDuck** in the Lite generator.
The window uses UI Toolkit (C#, UXML and USS), with a Featured carousel below
the studio description. About Us does not open itself or add code to player builds.
The first-import **EggHeads Lite Setup** page opens once per project after import
and compilation finish, and uses the same campaign service, feed and online-content
preference. Requests run while these windows are open. Setup can be reopened from
**Tools > PyroDuck > EggHeadsLite > Setup**. Its first-show flag is stored in local
EditorPrefs per project path; reimports and domain reloads do not reopen it.
Batch-mode imports do not open Setup or set its first-show flag.

## Set up remote publishing

1. Select `Editor/PyroDuck/AboutUs/PyroDuckCampaignSettings.asset` in the package.
2. **Feed Url** is configured as
   `https://pyroduck.com/API/campaigns.php?product=eggheads-lite`.
   Clearing it uses the bundled `campaigns.json` without network requests.
3. Copy `Editor/PyroDuck/AboutUs/campaigns.json` to your HTTPS host. For example,
   you could publish it at `https://pyroduck.com/campaigns.json`; that example URL
   is not configured or published by this change.
4. Enter the actual public HTTPS URL in **Feed Url** before distributing your next
   `.unitypackage`. Keep **Fallback Catalog** assigned to the bundled JSON.
5. Close and reopen About Us after changing the settings asset. **Refresh** forces
   a new request. Later campaigns only require updating the hosted JSON/images.

The initial feed URL must be distributed once in the settings asset. Already
distributed copies with an empty URL cannot discover a future server by themselves.

No hosting account, server or web deployment is included in this implementation.
The URLs must return the JSON/image directly (HTTP 200); redirects and login pages
are not followed. Use UTF-8 JSON and PNG/JPEG images. No CORS setup is needed for
the native Unity editor requests.

## Reusing the promotion system in another package

Set **Current Product Id** on `PyroDuckCampaignSettings.asset` to the identity of
the package containing the windows. Lite ships with `eggheads-lite`; use
`eggheads-full` when distributing the promotion system inside Full, or your own
stable identity for another project. Update the feed's `?product=` value as well.
Do not use the shared runtime assembly name as the identity: Lite and Full share it.

Every campaign should provide `productId` for the advertised product. Campaign
`id` identifies an individual promotion; multiple campaigns can advertise the same
product. All matching campaigns are hidden in both Setup and About Us, including
cached and offline fallback campaigns. Hidden campaign images are not downloaded.
Other products remain eligible; Lite can still advertise Full.

**Current Product Aliases** accepts previous identities of the same product.
**Current Product Urls** accepts its store/product links and recognizes older feeds
without `productId`. URL tracking queries, fragments and trailing slashes are ignored;
Asset Store links also match by numeric product ID even after a slug rename.
Fill in the host's own links when reusing this system with legacy feeds. Unknown
products remain visible; the system never guesses identity from a title.

The PHP API also omits campaigns whose `productId` matches the host's `product`
query. Its `products` targeting list describes the audience, not the advertised
product. Existing feeds without `productId` remain supported. Reopen windows after
changing the settings. This does not install the promotion windows into Full yet.

## JSON format

```json
{
  "schemaVersion": 1,
  "enabled": true,
  "campaigns": [
    {
      "id": "egg-heads-2d",
      "productId": "eggheads-full",
      "enabled": true,
      "active": true,
      "priority": 100,
      "startUtc": "",
      "endUtc": "",
      "title": "Egg Heads 2D",
      "description": "Create customizable 2D characters for your Unity games.",
      "discountPercent": 0,
      "badge": "Explore the full collection",
      "imageUrl": "",
      "targetUrl": "https://assetstore.unity.com/packages/2d/characters/egg-heads-2d-364008"
    }
  ]
}
```

- `schemaVersion` must be `1`; `campaigns` must be an array with at most 20 items.
- Global `enabled: false`, or a valid empty array, hides all cards, including the
  fallback card. This state is also respected when read from the cache.
- Each card requires a unique `id`, `enabled: true`, `active: true`, a `title`
  and a valid `targetUrl`. Higher priorities appear first; ties sort by ID.
- Empty dates mean no date limit. Otherwise use UTC, e.g.
  `2026-10-01T00:00:00Z`. The start is inclusive; the end is exclusive.
  Invalid dates/ranges hide that card. Dates are reevaluated while the window is open.
- Titles support up to 100 characters, descriptions 500, badges 80. Text is plain
  text; rich-text tags are not interpreted. Only publish actual campaign offers.
- `imageUrl` is optional. Empty, missing or failed images show a branded text card.
  Images may use a public HTTPS CDN. Maximum size is 4 MiB and 4096×4096 pixels;
  a landscape image around 1200×600 is sufficient.
- Destination links must use HTTPS on `assetstore.unity.com`, `pyroduck.com` or
  its subdomains. Nothing opens until the user clicks a link button.

## Carousel and offline behavior

Slides rotate every five seconds. Hovering over Featured or focusing one of its
controls pauses rotation. Arrows and dot buttons support manual navigation.
One card hides the navigation controls; zero cards displays an empty state.

The feed is checked when the window opens and every six hours while it stays open.
Fresh cache avoids redundant requests. **Refresh** bypasses the six-hour feed cache.
Requests time out after ten seconds; the JSON response is limited to 256 KiB.

Validated JSON and decoded images are cached per feed under
`Library/PyroDuck/Campaigns/`. A failed request uses cached JSON for up to seven
days, then the bundled catalog. Campaign expiry dates still apply offline.
Images are cached for seven days, with at most 32 retained image files per feed.
Use versioned image URLs (e.g. `banner-v2.png`) when changing artwork.

**Load online featured content** is a local EditorPrefs preference. Turning it off
stops requests and shows the bundled catalog. Closing the window aborts pending
requests and releases downloaded textures. No analytics, identifiers or project
content are sent; the configured host receives ordinary JSON/image GET requests.

The current repository tests cover the UI tree, fallback lifecycle, schema,
priority/date filtering and destination validation. Live host delivery must be
checked once your feed is published, including offline and image behavior.


## Per-product discount ribbon

Set discountPercent on each campaign to an integer from 1 to 100.
For example, "discountPercent": 30 displays %30 on a red diagonal ribbon
in the top-left corner of the image in both About Us and the Generator.
The ribbon also appears on image placeholders. Missing, zero, negative, or
above-100 values hide the ribbon without hiding the product.
The bundled example uses 0; set the actual discount when applicable.


## Bundled Egg Heads image

The bundled catalog uses imageUrl with an assetguid: prefix to load EggHeadsCampaign.png through AssetDatabase, including offline. The PNG was converted from https://assetstorev1-prd-cdn.unity3d.com/key-image/32c75e4b-fc5b-4055-ba73-a61ae12e1f0b.webp . Bundled textures remain owned by Unity and are not destroyed by the download service. For remotely hosted artwork, publish PNG/JPEG and set imageUrl to its direct HTTPS URL; remote WebP decoding is not supported.
