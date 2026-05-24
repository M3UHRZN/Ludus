# VoidHaul

**Takim:** LUDUS
**Ders:** CENG 454 — Game Programming, Bahar 2025-2026
**Repo:** https://github.com/M3UHRZN/Ludus

VoidHaul, 4-6 oyunculu co-op extraction horror prototipi. Karanlik bir tesise iniyorsun,
esyalari topluyorsun, agirlik seni yavaslatiyor, dusmanlardan kaciyorsun ve sayac
sifirlanmadan ekstraksiyon noktasina ulasmaya calisiyorsun. Lethal Company / R.E.P.O.
ilhamli, Unity 6 + NGO uzerinde calisiyor.

## Takim

| Ad | Rol |
| --- | --- |
| Metin Enes Ufuk | Tech Lead, Network (NGO), GameSession |
| Esmanur Tetik | Scrum Master, EventBus, HUD |
| Mehmet Anil Ulku | Harita / Lobby |
| Alp Doruk Sengun | Enemy AI (Strategy) |
| Yasin Kapaklikaya | Item sistemi, ObjectPool, Inventory |
| Beyza Nur Elitok | Decorator, korku mekanikleri |
| Deniz Ozan Tatar | Audio, VFX, Lore |

## Kurulum

1. **Unity 6000.3.13f1** kur (Hub > Installs > Add). Farkli patch kullaniyorsan
   `ProjectSettings/ProjectVersion.txt` ile karsilastir.
2. Repoyu klonla:
   ```
   git clone https://github.com/M3UHRZN/Ludus.git
   ```
3. Unity Hub'da `Add project from disk` ile klasoru ac.
4. Ilk acilista paketler restore olur (NGO, URP, Cinemachine, Input System, ProBuilder).
5. `Assets/Scenes/SampleScene.unity` veya `Lobby` sahnesini ac.

## Calistirma (Editor)

- **Host olmak icin:** Play -> ana menu / lobby UI'da `Host` butonuna bas.
- **Join olmak icin:** ikinci bir editor / build'da `Join` -> IP gir (lokal test icin
  `127.0.0.1`).
- Build alindiginda `File > Build Settings > Windows x64` hedefi kullaniliyor.

## Kontroller (Klavye + Mouse)

| Aksiyon | Tus |
| --- | --- |
| Hareket | W A S D |
| Bakis | Mouse |
| Zipla | Space |
| Cömelme | Left Ctrl |
| Kosma | Left Shift |
| Etkilesim / Kavra | E |
| Birak | G |
| Firlat (sik tut) | Sol Mouse |
| Kullan | Sag Mouse |
| El feneri | F |
| Tutma mesafesi | Mouse scroll |
| Walkie-Talkie | V |
| Emoji 1 / 2 / 3 | 1 / 2 / 3 |

Spectator modunda fare ile bakis, sol/sag tik ile sonraki/onceki oyuncuya gec, Space
serbest kamera.

## Kullanilan Unity paketleri

- Unity Netcode for GameObjects (NGO)
- Universal Render Pipeline (URP)
- Cinemachine
- Input System
- ProBuilder
- TextMeshPro

Tam liste icin `Packages/manifest.json`.

## Mimari ozet

- `Assets/Scripts/Core/` — `GameEventBus` (Observer), `ObjectPool`, `Singleton`,
  `GameSessionManager`.
- `Assets/Scripts/Enemy/` — `IEnemyBehavior` + `Patrol/Chase/Flee/Attack` (Strategy),
  `EnemyController`, network state.
- `Assets/Scripts/Items/` — `IItem`, `BaseItem`, `ItemPickup`, `Decorators/`
  (Decorator pattern).
- `Assets/Scripts/Player/` — Hareket, etkilesim, envanter, state machine.
- `Assets/Scripts/UI/` — HUD ekranlari.

## Smoke-test akisi

1. SampleScene'i ac, `Host` butonu.
2. Ikinci editor / build'dan `Join 127.0.0.1`.
3. WASD ile hareket, agirlik gostergesi gorunur olmali.
4. E ile bir item kavra, agirlik artmali, hiz dusmeli.
5. Patrol eden bir dusman gorus alanina girince Chase'e gecer; yakinlasinca
   Attack'a gecer ve hasar verir.
6. Flashbang item'ini al -> dusman Flee'ye gecer ve uzaklasir.
7. Sayac biter -> session sona erer, extraction ozeti acilir.

## Bilinen sorunlar / TODO

- AudioManager ve ses efektleri sprint 2'de tamamlanacak.
- MapGenerator (prosedurel oda baglantisi) sprint 2'de devrede olacak.
- Patrol "ses duyma" tepkisi henuz pasif (HeardNoise flag yazili, dinleyici eklenecek).
- Permadeath save/load akisi sprint 3'e biraktik.

## Asset & 3rd-party krediler

> Bu liste sprint 3 sonunda final formuna alinacak. Kullandigimiz indirilen asset
> varsa (model, animasyon, ses, shader) buraya kaynak linki ile beraber ekliyoruz.

- Karakter modelleri / animasyonlar: (kaynak eklenecek)
- Lobby uzay arka plani: (kaynak eklenecek)
- Ses efektleri: (kaynak eklenecek)

## Lisans

Akademik proje, ders kapsaminda. Ucuncu parti varliklarin kendi lisans kosullari
gecerlidir.
