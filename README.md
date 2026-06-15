# HAUL

**Takim:** LUDUS (Group 6)
**Ders:** CENG 454 - Game Programming, Bahar 2025-2026
**Repo:** https://github.com/M3UHRZN/Ludus

HAUL, 4-6 oyunculu co-op extraction horror prototipi. Karanlik bir tesise iniyorsun,
prosedurel uretilen dungeon icinde oda oda gezerek degerli esyalari topluyorsun,
agirlik seni yavaslatiyor, dusmanlardan kaciyor veya catismaya giriyorsun ve
extraction noktasindan tahliye ediyorsun. Unity 6 + Netcode for GameObjects (NGO)
uzerinde, server-authoritative host-client mimarisiyle calisir.

## Kazanma / Kaybetme

- **Kazanma:** Takim, 3 ardisik run (gun) icinde belirlenen kredi kotasini doldurursa kazanir.
- **Kaybetme:** 3 gun sonunda kota dolmamissa veya tum takim oldurulurse run kaybedilir.
- **Restart:** Lobi sahnesine geri donus ile yeni 3 gunluk dongu baslar.

## Takim

| Ad | Rol |
| --- | --- |
| Metin Enes Ufuk | Tech Lead, Network (NGO), GameSession |
| Esmanur Tetik | Scrum Master, EventBus, HUD |
| Mehmet Anil Ulku | Harita / Lobby |
| Alp Doruk Sengun | Enemy AI (Strategy), body-drop / corpse, multiplayer adaptasyon |
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
5. Lobby sahnesini ac.

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
| Comelme (sessiz) | Left Ctrl |
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

- Unity Netcode for GameObjects (NGO) 2.x
- Universal Render Pipeline (URP)
- AI Navigation (NavMesh)
- Cinemachine
- Input System
- ProBuilder
- TextMeshPro

Tam liste icin `Packages/manifest.json`.

## Mimari ozet

- `Assets/Scripts/Core/` - `GameEventBus` (Observer), `ObjectPool`, `Singleton`,
  `GameSessionManager`.
- `Assets/Scripts/Enemy/` - `IEnemyBehavior` + 9 concrete davranis
  (Patrol, Wandering, Chase, Attack, RangedAttack, RangedAim, Lure, Flee) (Strategy),
  `EnemyController`, `EnemyNetState` (network state).
- `Assets/Scripts/Items/` - `IItem`, `BaseItem`, `ItemPickup`, `Decorators/`
  (Decorator pattern).
- `Assets/Scripts/Player/` - hareket, etkilesim, envanter, `PlayerStateMachine`
  (State pattern), body-drop / corpse.
- `Assets/Scripts/UI/` - HUD ekranlari.

## Smoke-test akisi

1. Lobby sahnesini ac, `Host` butonu.
2. Ikinci editor / build'dan `Join 127.0.0.1`.
3. WASD ile hareket, agirlik gostergesi gorunur olmali.
4. E ile bir item kavra, agirlik artmali, hiz dusmeli.
5. Patrol eden bir dusman gorus alanina girince Chase'e gecer; yakinlasinca
   Attack veya RangedAim/RangedAttack'a gecer ve hasar verir.
6. Flashbang item'ini al -> dusman Flee'ye gecer ve uzaklasir.
7. Bir oyuncu olunce cesedi yere duser; takim arkadasi cesedi ExitZone'a tasiyabilir.
8. Sayac biter / kota degerlendirilir -> session sona erer, extraction ozeti acilir.
9. Restart: Lobi'ye don, yeni run baslat.

## Bilinen sorunlar / sinirlamalar

- `EnemyController` halen server-only MonoBehaviour olarak calisir; tam NetworkBehaviour
  migrasyonu ileri bir asamaya birakilmistir.
- Prosedurel oda yerlesiminde nadiren bir item spawn noktasi oda tavanina yakin
  olusabilir (kozmetik; tavan mesh'i kapatir).
- Ceset tasima ve birlikte tahliye akisinin gozlemlenmesi en az iki aktif client gerektirir.

## Asset & 3rd-party krediler

| Varlik | Tur | Kullanim |
| --- | --- | --- |
| Paperman.fbx (Sci-Fi Robots Bundle, "Same Gev Dudios") | 3D model + animasyon + materyal | Player robot karakteri |
| Cursed Priest 3D model | 3D model + animasyon | Type B priest dusman (Git LFS) |
| Engie.fbx, Robert.fbx | 3D model | Yedek robot karakter modelleri |
| TirgamesAssets dungeon kiti | Dungeon mesh kitleri (oda, kapi, koridor) | DungeonGenerator parcalari |
| Sci-Fi Styled Modular Pack | Sahne prefab/mesh | Lobby ortami |
| electricity.mp3, heartbeat.mp3, ambient ses paketi | Audio (free Asset Store) | Ses / atmosfer |
| Mixamo animasyonlari | Karakter animasyonu | Type B dusman retargeting |

Her varligin repoya hangi uye tarafindan eklendigi git history uzerinden gorulebilir.
Ucuncu parti varliklarin kendi lisans kosullari gecerlidir.

## Lisans

Akademik proje, ders kapsaminda. Ucuncu parti varliklarin kendi lisans kosullari
gecerlidir.
