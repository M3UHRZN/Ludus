---
title: Korku Sistemi
tags: [sistem, korku, player, efekt, test]
updated: 2026-05-16
---

# Korku Sistemi (Fear System)

**Sorumlu:** Mehmet Anil Ulku  
**Durum:** Test sahnesinde calisiyor. Su an Netcode'suz `MonoBehaviour` test implementasyonu kullaniliyor.  
**Test sahnesi:** `Assets/Scripts/FearSystem/Scene/FearSceneTest.unity`

## Dosya Yapisi

```
Assets/Scripts/FearSystem/
  FearSystem.cs      - Ana korku sistemi
  TestPlayer.cs      - WASD + mouse test player
  FakeEnemy.cs       - Test dusmani, player'a yurur
  FearSystemUI.cs    - Fear bar ve test butonlari
  Scene/
    FearSceneTest.unity
    FearSceneTest/FearVolume Profile.asset
```

## Mevcut Calisma Mantigi

Korku seviyesi `0-100` arasinda tutulur. Sistem dusmana olan mesafeyi olcer ve Inspector'daki `Distance Fear Table` degerlerine gore hedef korku seviyesini hesaplar. Korku bir anda ziplamaz; `MoveTowards` ile hedef degere dogru yavasca akar.

Distance detection sinirinda ani ziplama olmamasi icin `enemyDetectRadius + enemyFalloffBuffer` ile tablodaki en uzak mesafe arasinda fear `0`dan ilk tablo degerine dogru interpolate edilir. Ayrica hedef fear, `fearTargetFollowSpeed` ile yumusatilir.

Yakın mesafede stabilite icin enemy uzakligi artik enemy transform merkezinden degil, collider'in player'a en yakin noktasindan (`Collider.ClosestPoint`) olculur. Bu, dusmanin dibindeyken merkez mesafesi yuzunden efektin garip ziplama yapmasini azaltir.

Ek olarak mesafenin kendisi de yon bagimli yumusatilir:

- Dusmana yaklasirken mesafe daha yavas guncellenir.
- Dusmandan uzaklasirken mesafe daha hizli toparlanir.

Bu, ileri giderken sistemin sertlesip geri gelirken guzel hissettirmesi problemini azaltmak icin eklendi.

Ornek mesafe tablosu:

| Mesafe | Hedef korku |
|--------|-------------|
| 8 m | 10 |
| 5 m | 40 |
| 3 m | 70 |
| 1 m | 95 |

Bu tablo Inspector'dan elle degistirilebilir. Daha yakin mesafe daha yuksek korku demektir.

Test sahnesindeki su anki Inspector degerleri:

| Mesafe | Hedef korku |
|--------|-------------|
| 7 m | 65 |
| 5 m | 75 |
| 3 m | 85 |
| 1 m | 95 |

Bu degerler efekti daha erken gostermek icin yuksek tutuldu.

## Tetikleyiciler

| Tetikleyici | Kaynak | Not |
|-------------|--------|-----|
| Dusman yakinligi | `Physics.OverlapSphere` | `enemyLayer` icindeki collider'lari arar |
| Yakin oyuncu olumu | `PlayerDiedEvent` | `nearDeathRadius` icindeyse korku ekler |
| Hasar alma | `PlayerDamagedEvent` | anlik korku ekler |
| Karanlik ortam | `RenderSettings.ambientIntensity` | ortam karanliksa minimum korku hedefi verir |

## Efektler

| Efekt | Ne yapar | Nereden ayarlanir |
|-------|----------|-------------------|
| Vignette | Ekran kenarlarini karartir, tunel gorusu hissi verir | `FearVolume Profile` |
| Chromatic Aberration | Kenarlarda RGB renk kaymasi olusturur | `FearSystem.cs` + `FearVolume Profile` |
| Lens Distortion | Goruntuyu ice/disari buker | `FearSystem.cs` + `FearVolume Profile` |
| Depth Of Field | Panik/stun sirasinda blur verir | `FearSystem.cs` + `FearVolume Profile` |
| FearOverlay | Kirmizi ekran bindirmesi | Canvas altindaki `FearOverlay` Image |
| Camera Shake | Stun/panik sirasinda kamerayi sallar | `shakeCamera`, `shakeAmount`, `shakeFrequency` |
| Panic Re-arm | Panic loop'u engeller | `panicRearmFear` |

### Chromatic Aberration Notu

Enemy objesinin kenarlarinda gordugun renk gecisleri **Chromatic Aberration** efektidir. URP'de maksimum `Intensity = 1.0` oldugu icin daha guclu hissettirmek icin:

- `FearSystem.cs` icinde efekt daha erken baslatildi.
- Su an `t = 0.1` civarinda baslar, `t = 0.8` civarinda maksimuma yaklasir.
- Daha da belirgin istenirse `Distance Fear Table` degerleri yukseltilir veya `Chromatic Aberration` daha erken baslatilir.
- URP limitinden dolayi `Chromatic Aberration Intensity` pratikte `1.0` ustune cikmaz.

### Depth Of Field Notu

Depth Of Field sadece panik/stun sirasinda fark edilir. Profile icinde:

- `Depth Of Field` override ekli olmali.
- Mode: **Gaussian**
- `Max Radius` override checkbox acik olmali.
- Panic test etmek icin `FearSystemUI.SimulatePanic()` butonu kullanilabilir.
- Test profile'inda `Depth Of Field / Max Radius` override acik ve degeri `1`.

Mesafe degisince tum fear efekti hala fazla zipliyorsa:

- `Fear Target Follow Speed` dusur.
- Ornek sakin degerler: `3` veya `4`.
- Tepki cok gec kalirsa `6` veya `8` denenebilir.
- Hizlica detection sinirindan girip cikinca bozuluyorsa `Enemy Falloff Buffer` artir.
- Ornek: `Enemy Detect Radius = 8`, `Enemy Falloff Buffer = 2` ise fear 10m-7m arasinda yumusak girer/cikar.
- Ileri giderken hala goz yoruyorsa `Distance Approach Follow Speed` dusur.
- Ornek sakin degerler: `1.5` veya `2`.
- Geri cekilirken fazla gec toparlarsa `Distance Retreat Follow Speed` artir.
- Ornek hizli toparlama: `8` veya `10`.

### Panic Re-arm Mekanigi

Panic bir kere tetiklendikten sonra cooldown bitse bile hemen tekrar tetiklenmez. Tekrar panik olabilmek icin fear seviyesinin once `panicRearmFear` degerine veya altina dusmesi gerekir.

Sebep:

- Oyuncu enemy dibinde kalinca panic/stun loop'a girmesin.
- Panic daha nadir ve anlamli hissedilsin.
- Fear yuksek kalabilir ama ayni tehdit icinde surekli stun spam olmaz.

Varsayilan:

- `panicThreshold`: 90
- `panicRearmFear`: 75
- `panicTriggerHoldTime`: 1.25 sn

Yani fear 90 ustunde kisa sure kalinca panic tetiklenir, yeniden tetiklenmesi icin fear once 75'e dusmelidir.

## Test Sahnesi Kurulumu

Hierarchy beklenen yapi:

```
FearSceneTest
  Directional Light
  Plane
  TestPlayer
    Main Camera
  FakeEnemy
  FearVolume
  Canvas
    FearOverlay
    HUD
      FearBar
      FearText
      PanicLabel
      Test Buttons
```

## Inspector Baglantilari

`TestPlayer` objesinde:

- `CharacterController`
- `TestPlayer`
- `FearSystem`

`FearSystem` alanlari:

| Alan | Verilecek obje/deger |
|------|----------------------|
| `Player Movement` | `TestPlayer` component |
| `Post Process Volume` | `FearVolume` |
| `Fear Overlay` | Canvas altindaki `FearOverlay` Image |
| `Shake Camera` | `Main Camera` transform |
| `Enemy Layer` | Projedeki mevcut lowercase `enemy` layer |
| `Distance Fear Table` | Mesafe/korku degerleri |
| `Fear Target Follow Speed` | Mesafe degisimlerinin fear hedefine ne kadar hizli yansiyacagi |
| `Enemy Falloff Buffer` | Detection sinirindan hizli girip cikinca fear'in kopmadan yumusak sonmesini saglar |
| `Distance Approach Follow Speed` | Dusmana yaklasirken mesafe guncellemesinin hizi |
| `Distance Retreat Follow Speed` | Dusmandan uzaklasirken mesafe guncellemesinin hizi |
| `Panic Trigger Hold Time` | Fear threshold ustunde bu sure kalmadan panic baslamaz |
| `High Fear Blur Radius` | Panic oncesi yuksek fear blur miktari |

`FakeEnemy` objesinde:

- Layer: `enemy`
- Test sahnesinde `FakeEnemy` layer index'i 8, yani `enemy`.
- `FakeEnemy.target`: `TestPlayer`
- Collider olmali. FearSystem dusmani collider uzerinden algilar.

`FearVolume` objesinde:

- `Volume`
- `Is Global`: acik
- `Priority`: 10 veya daha yuksek
- Profile override'lari:
  - Vignette
  - Chromatic Aberration
  - Lens Distortion
  - Depth Of Field

`Main Camera` objesinde:

- URP Camera ayarlarinda `Post Processing`: acik olmali.

## Bilinen Sorunlar ve Cozumler

### GUID YAML Parser uyarisi

Hata:

```
The GUID inside 'Assets/Scripts/Items/IItem.cs.meta' cannot be extracted by the YAML Parser.
```

Sebep: `IItem.cs.meta` icinde Git conflict marker'lari kalmisti.

Cozum: Conflict temizlendi ve tek GUID birakildi.

### Netcode transport hatasi

Hata:

```
[Netcode] No transport has been selected!
```

Sebep: Test sahnesinde NetworkManager/TestAutoHost ile host baslatmaya calismak.

Cozum: FearSystem test sahnesi simdilik Netcode'suz calisir. Test sahnesinde NetworkManager veya TestAutoHost zorunlu degil.

### Efektler calismiyor gibi gorunuyorsa

Kontrol listesi:

- `FearSystem.postProcessVolume` dolu mu?
- `FearSystem.fearOverlay` dolu mu?
- `FearSystem.enemyLayer` lowercase `enemy` mi?
- Enemy objesinde collider var mi?
- Enemy layer'i `enemy` mi?
- Main Camera'da Post Processing acik mi?
- FearVolume Profile icinde override checkbox'lari acik mi?

## Gelecek Network Entegrasyonu

Su anki test surumu networksuzdur. Ileride asil player prefabina alinirken:

- `FearSystem` owner client tarafinda calistirilabilir.
- Fear seviyesi server'a sadece gerekli durumlarda bildirilebilir.
- Panik/stun etkisi `PlayerStateMachine` veya server onayli RPC ile baglanabilir.
- Bu test implementasyonu network ekibinin isini zorlastirmamak icin Netcode'a bagimli yazilmadi.

## Proje Taramasi - 2026-05-16

Bulunanlar:

- `Assets/Scripts/FearSystem` klasoru mevcut ve dort ana script iceriyor.
- `FearSceneTest.unity` build settings'e ekli.
- `FearVolume Profile.asset` icinde Vignette, Chromatic Aberration, Lens Distortion ve Depth Of Field override'lari mevcut.
- `FearSceneTest.unity` icinde `FearSystem.postProcessVolume`, `fearOverlay`, `shakeCamera`, `playerMovement` ve `enemyLayer` baglantilari dolu.
- `ProjectSettings/TagManager.asset` icinde lowercase `enemy` layer mevcut ve test sahnesinde kullaniliyor.
- `IItem.cs.meta` icindeki Git conflict temizlendi.
- Projede `Assets/Scripts/Enemy/TestAutoHost.cs` halen var; FearSystem test sahnesinde kullanilmamali, yoksa transport ayari olmayan NetworkManager hatasi geri gelir.
- Projede bazi Sprint/TODO notlari var: `EnemyController` icin ileride NetworkBehaviour'a tasima, `PlayerStateMachine` icin anti-cheat mesafe dogrulama.
- Obsidian'da sadece bu korku sistemi notu var; FearSystem icin gerekli kurulum bilgileri bu dosyada toplandi.

## Degisiklik Notu - 2026-05-16

Degisiklikler:

- Panic oncesi hafif Depth Of Field blur.
- Reset ve panic sirasinda kameranin rest pozisyonuna geri donmesi.
- Panic Re-arm mekanigi: panic tekrar tetiklenmeden once fear'in `panicRearmFear` altina dusmesi gerekir.
- Distance edge smoothing: enemy detect sinirinda fear bir anda yuksek degere ziplamaz; 0'dan tablo degerine dogru akar.
- Fear target smoothing: mesafe degisimleri once hedef fear'a yumusak yansir.
- Enemy falloff buffer: hizli gir/cik yapinca enemy algisi sert sekilde acilip kapanmaz.
- Close range stabilization: enemy mesafesi transform merkezinden degil collider'in en yakin noktasindan olculur.
- Panic hold time: yakindayken fear bir frame 90 ustune cikti diye panic aninda patlamaz.
- Directional distance smoothing: dusmana yaklasma ve uzaklasma farkli hizlarda yumusatilir.
- Fear Pulse mekanigi tamamen kaldirildi.

Commit/push yapilmadi.
