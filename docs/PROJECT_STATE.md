# VoidHaul / Ludus — Proje Durumu

**Son guncelleme:** 29 Mayis 2026 (sunum: 2 Haziran 2026, T-4 gun)
**Sahip:** Alp Doruk (CENG 454)

Bu dosya context kaybedildiginde / yeni session'da takim arkadasligi yapabilmek
icin tutulur. Asagidakini okuyan herhangi bir oturum projenin tam halini bilir.

---

## 🌐 Takim & Sorumluluk

| Kisi | Commit | Sorumluluk |
|---|---|---|
| **Alp (sen)** | 123 | TUM enemy AI (Strategy 9 concrete), multiplayer adaptation, slow projectile, loot guardian, respawn loop, body drop, bug fix / defensive coding |
| **Anil** | 9 | DungeonGenerator, FearSystem temeli, Heartbeat audio, MarketSystem temeli, Lobby design, PR #110 cam+nickname |
| **Beyza** | 10 | GameEventBus (Observer), ItemDecorator + PoisonDecorator + StunDecorator (Decorator), Grab/drop/throw |
| **Yasin** | 30 | ItemSystem, BaseItem, ObjectPool, Extraction (ExitZone+ExtractZone+ExtractionManager), Spectator camera, CorpseItem + CorpseEvents, GDD §6.4 penalty |
| **Esmanur** | 31 | Stamina, Audio (3D, AudioManager), MainMenu, Settings, Inventory/Stamina/Extraction UI, Crosshair |
| **Metin** | 142 | Project lead, merge'ler, NetworkManager.prefab, Player prefab fix'leri |

---

## 📦 Merge Edilmis PR'lar (main'de)

| # | Sahip | Icerik |
|---|---|---|
| #107 | Alp | Throwable items damage + stun |
| #108 | Yasin | Extraction networking |
| #109 | Alp | RNGmap lib cleanup |
| #110 | Anil | Camera pose + nickname |
| #112 | Alp | **13 commit AI overhaul** — F1 laser, F3 lure (multi-carrier + warp), F5 priest 1000 HP, F6 respawn, F7 priest ambience + ekstra fix'ler |
| #114 | Alp+Esmanur | Audio merge (electricity bell) |
| #115 | Alp | Item spawn 4 katmanli guard (MapEnemyBridge floor-only) |
| #116 | Esmanur | MainMenu |
| #117 | Alp | Esmanur UI PR #113'un conflict-free versiyonu |

## 🔄 Bekleyen PR'lar

| # | Branch | Durum |
|---|---|---|
| **#118** | `feature/alp-body-drop-and-corpse-carry` | ✅ Hazir — body drop + corpse carry + extraction abandonment + nameplate + hide visual. Test bekliyor. |

## ✅ Bitmis Ozellikler

### AI (Alp, PR #112)
- Robot lazer telegraph (1.2sn)
- Priest lure (carrier-tracking, warp behind cover, multi-carrier hysteresis)
- Priest 1000 HP boss
- Loot room guardian
- Respawn loop (LOS safety)
- Slow projectile (ISlowable interface)
- 3-tier target priority (damage 4s, carrier hint, closest +3m hysteresis)
- Priest proximity ambient (vignette flicker + bonus fear + 3D bell)
- LOS multi-sample (head/body/feet)
- Chase obstacle push

### Mekanikler (Yasin + Alp PR #118)
- Throw damage + stun
- Extraction (ExitZone + ExtractZone)
- Inventory persistence across scene transitions
- Body drop on death + corpse carry + abandonment penalty
- Corpse nameplate billboard + player visual hide on death

### Sistem (Beyza)
- GameEventBus (Observer, defensive dispatch Alp eklemesi)
- ItemDecorator + PoisonDecorator + StunDecorator (Decorator)
- Grab/drop/throw

### Map (Anil)
- Procedural DungeonGenerator
- MapEnemyBridge (Adapter pattern)

### Player
- PlayerStateMachine (State pattern, 5 concrete state)
- PlayerStamina (Esmanur)
- PlayerLook + PlayerMovement + Inventory
- DeadState (gravity-fall fix Alp PR #112)

### UI (Esmanur + Alp)
- HUDController, InventoryUIController (null-safe Alp fix)
- ExtractionUIController, StaminaUIController
- MainMenu, Settings
- **FlashbangCounterDisplay** (Alp, sag alt counter)
- ItemDatabase (Esmanur)

### Audio (Esmanur + Anil)
- AudioManager singleton
- EnemyAudioController (footstep animation event)
- PlayerAudioController (AudioHolder lookup)
- Heartbeat audio (Anil), priest electricity ambient (Alp)

### Lore
- docs/lore/HAUL_Ilk_Vaaz_Gecesi_Lore.pdf (3 MB)
- docs/lore/README.md (kod ile mapping)

---

## ⏳ Yapilacaklar (oncelik sirasiyla)

### 🟢 Once (sunum oncesi sigar)
1. **PR #118 merge** — body drop + corpse + extraction abandonment
2. **Heavy item slow** (20 dk) — PhysicsObject.weight'e gore taşıyaniyaslat (ISlowable kanal)
3. **Item audio cue** (30 dk) — degerli esya yakin "hum" sesi (3D AudioSource runtime)
4. **Power outage event** ⭐ (1.5 sa) — periyodik karanlik + fear +30, en buyuk demo hook

### 🟡 Vakit kalirsa
5. **Decoy items** (1 sa) — sahte loot patlamasi
6. **Extraction rotation** (1.5 sa) — aktif extract point degisiyor

### 🔴 Sunum sonrasi
7. **Map MVP** (3.5 sa) — REPO tarzi minimap

### Zorunlu
- Bireysel rapor (1 Haziran taslak, 21 Haziran deadline)
- Smoke build (1 Haziran)
- Test cycle (1 Haziran)
- 🎯 SUNUM (2 Haziran)

---

## 🎨 Pattern Showcase (rapor icin)

| Pattern | Sahibi | Konkret |
|---|---|---|
| **Strategy** ⭐ | Alp | IEnemyBehavior + 9 concrete (Patrol/Wander/Chase/Attack/RangedAttack/RangedAim/Lure/Flee + base) |
| **Observer** ⭐ | Beyza (temel) + Alp (defensive) | GameEventBus + 15+ event tipi |
| **Decorator** | Beyza | ItemDecorator + Poison + Stun |
| **State** | Karma | IPlayerState + 5 concrete |
| **ObjectPool** | Yasin | ObjectPool<T> generic |
| **Singleton** | Karma | GameSessionManager, AudioManager, ItemDatabase, PlayerSpawnCoordinator, FlashbangCounterDisplay |
| **Registry** | Alp | PhysicsObject.HeldItems, PlayerStateMachine.ServerPlayers, ExitZone._corpsesInZone |
| **Factory Method** | Alp | EnemyController.CreateAttackBehavior/DefaultBehavior |
| **Adapter** | Anil | MapEnemyBridge |
| **Service Locator** | Alp | PlayerStateMachine.GetServerPlayer |

**Toplam: 10 pattern** — rapor icin BUYUK avantaj

---

## 🐛 Bilinen Acik Konular (sunum sonrasi)

- **Test/dev scriptleri** (18 dosya): CameraNameTest klasoru, EnemyTestDummy, TestAutoHost, TestMapReadyTrigger, FakeEnemy, TestPlayer, FearDebugHUD, FlashbangTest*, ItemSpawnerTester, ObjectPoolTester, Market test'leri. Silinmesin, raporda "test driven" diye gosterilebilir.
- **Esmanur HUD slot UI** — Esmanur'un kendi wiring'i bekliyor (sahnede yok su an)
- **ItemDatabase entry'leri** — flashbang (ID 100) icon + prefab atanmamis (designer isi)
- **NetworkTransform interpolate** — Agent.Warp "slide" gibi gozukebilir (cosmetic)
- **Heavy slow + power outage** — henuz yapilmamis (sira icinde)

---

## 🔑 Kritik Mimari Notlar

- **Server-authoritative AI**: EnemyController.Start non-server'da disable
- **PhysicsObject.HeldItems**: server-side static registry, LureBehavior buradan multi-carrier
- **Inventory persistence**: PlayerInventory.s_ServerInventorySnapshot ile sahne gecisinde Slots korunur
- **MarketWallet.TrySpend**: CurrentCredits PROPERTY kullan (field stale kalir, eski bug)
- **GameEventBus.Publish**: try/catch per handler — bir subscriber crash ederse digerleri etkilenmez
- **FlashbangCounterDisplay**: DontDestroyOnLoad singleton, runtime kendi UI'ini yaratir
- **CorpseItem**: nameplate billboard, NetOwnerName NetworkVariable ile sync
- **ExitZoneInteractable**: _playersInZone + _corpsesInZone iki ayri set; ABANDONED = (canli zone disinda) VEYA (olu ve cesedi zone disinda)
- **PlayerStateMachine.ApplyDamage death**: drop held + drop inventory + spawn corpse + hide visuals RPC + publish event
- **MapEnemyBridge.EstimateRoomBounds**: SADECE Floor renderer'lari (PR #115 fix, "Floor" name filtre)

---

## 📁 Dosya Yapisi

```
Assets/
├── Audio/                      # 19 mp3 (Esmanur)
├── Prefabs/
│   ├── BaseItem.prefab         # Yasin
│   ├── CorpseItem.prefab       # Alp (PR #118)
│   ├── Enemy A/B.prefab        # AI
│   ├── Network/
│   │   ├── PlayerV0.3.prefab   # Aktif player
│   │   ├── PlayerV0.5.prefab   # Lobby player
│   │   ├── NetworkManager.prefab
│   │   └── ...
│   └── UI/Esmanur_HUD_Root 1.prefab
├── Scripts/
│   ├── Audio/                  # Esmanur (3 controller)
│   ├── Core/                   # GameEventBus, Singleton, ObjectPool, SceneNames
│   ├── Enemy/                  # 21 AI (Alp domain)
│   ├── FearSystem/             # Anil temeli + Alp katmani
│   ├── Flashbang/              # 6 dosya
│   ├── Interfaces/             # IDamageable, IKnockbackable, ISlowable...
│   ├── Items/                  # Yasin + Beyza Decorator
│   ├── Map/                    # Anil DungeonGenerator
│   ├── MarketSystem/           # Anil + Esmanur
│   ├── Network/                # ExtractionManager, PlayerSpawnCoordinator
│   ├── Player/ + States/       # Karma
│   └── UI/                     # Esmanur + Alp (FlashbangCounter)
└── Scenes/
    ├── LobbyScene.unity
    ├── RNGmap.unity            # Ana gameplay sahnesi
    ├── MainMenu.unity
    └── DevTest/                # Test sahneleri

docs/
├── README.md
└── lore/
    ├── HAUL_Ilk_Vaaz_Gecesi_Lore.pdf
    └── README.md
```

---

## 🚦 Komut Sirasi (yeni session icin)

```bash
# Mevcut durum
git checkout main && git pull --ff-only
git log --oneline -10

# PR #118 hala acik mi?
gh pr view 118 --json state,mergeable,mergeStateStatus

# Aktif feature branch
git branch --show-current

# Test
# Unity'i ac, RNGmap'te Play, market'ten flashbang al, ol, ceset kaldir, extraction
```

---

## 📝 Bireysel Rapor — Alp icin anahtar cumleler

> "Projede tum dusman yapay zeka sistemi benim tarafimdan tasarlanip implement edildi. Strategy pattern altinda 9 concrete davranis (Patrol, Wander, Chase, Attack, RangedAttack, **RangedAim**, **Lure**, Flee + base) yazdim. Yaninda 4 oyuncuya kadar destekleyen multiplayer-aware 3 katmanli hedef oncelik sistemi (damage source priority, carrier hint, closest with hysteresis), slow projectile mekanigi icin yeni `ISlowable` interface'i, loot room guardian sistemi ve respawn döngusu gelistirdim."
>
> "Anil'in temelini attigi FearSystem uzerine priest proximity bonus + vignette flicker + 3D ambient audio katmanini ekleyerek atmosferik horror'u guclendirdim. Beyza'nin GameEventBus Observer pattern'i icin defensive dispatch variant'i yazip, bir subscriber'in crash ettiginde digerlerini etkilememesini sagladim."
>
> "Body drop on death sistemini kurarak Yasin'in CorpseItem altyapisi ile entegre ettim — olunce envanter yere dokulur, ceset spawn olur ve takim arkadaslari extraction noktasina taşıyabilir. ExitZone'da abandonment cezasi tam wire edildi: GDD §6.4 forumlu (max 0.25, playerCount/100) ile her unutulan beden / ceset icin ceza dususlu, herkes tahliye edildiyse cezasiz."
>
> "Toplam 123 commit ile multiplayer audit, encoding duzeltmeleri, 4 kademeli item spawn bug hunting ve takim arkadaslarimin (Esmanur UI, Esmanur Audio) PR'larini main'e guvenli sekilde merge etme suclerini de yonettim."

---

**SONUC:** Bu dosyayi yeni bir session okuyup hemen kaldigi yerden devam edebilir. Hicbir detay kaybedilmez.
