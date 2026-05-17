# Enemy AI Sistemi — Hızlı Kurulum

Bu klasördeki sistemler: Strategy pattern üzerine 4 davranış (Patrol / Chase / Flee / Attack), prosedürel haritaya bağlanan EnemySpawner, sound reactivity ve NavMesh entegrasyonu.

Sahnede çalışır hâle getirmek için aşağıdaki adımlar yeterli.

---

## 1) Yeni bir test/oyun sahnesi açıyorsanız

Sahnede şu objeler bulunmalı:

### A) DungeonManager (haritayı üretir)
Boş GameObject, üzerinde sırayla şu component'ler:
1. `DungeonGeneratorRunner` — `Config` slotuna `Assets/Resources/DungeonGeneratorConfig.asset` ata.
2. `DungeonVisualizer` — 13 prefab slotunu doldur:
   - Floor → `Assets/Prefabs/Floor.prefab`
   - Wall N/S/E/W (hepsi) → `Assets/Prefabs/Wall.prefab`
   - Door N/S/E/W (hepsi) → `Assets/Prefabs/Door.prefab`
   - Corridor → `Assets/Prefabs/Corridor.prefab`
   - Merged Floor 2x2 → `Floor2x2.prefab`
   - Merged Floor 1x3 → `Floor1x3.prefab`
   - Merged Floor 3x1 → `Floor3x1.prefab`
3. `MapEnemyBridge` — defaults yeterli; gerekirse `Bake Delay Frames` arttırılabilir (NavMesh bake için süre tanır).

Bridge ne yapıyor: Visualizer'ın "DungeonLayout" altında ürettiği oda parent'larını tarar, her odanın merkezine `EnemySpawnPoint` + `PatrolWaypointGroup` ekler, sonra runtime NavMesh bake eder ve `GameEventBus.Publish(new MapReadyEvent(...))` yayınlar.

### B) NetworkManager
Boş GameObject:
- `Network Manager` (Unity Netcode) — Player Prefab: `Assets/Prefabs/Network/PlayerV0.3.prefab`, Auto Spawn KAPALI.
- `TestAutoHost` (test sahnesinde otomatik StartHost için)

### C) EnemySpawner
Boş GameObject:
- `EnemySpawner` (NetworkBehaviour, NetworkObject auto-eklenir)
- Inspector:
  - `Enemy Prefabs` → Size 1, Element 0: `Assets/Prefabs/Enemy.prefab` (Type B için daha fazla element eklenebilir, weighted random)
  - `Max Alive Enemies` → 5 (default)
  - `Rooms Per Enemy` → 10 (target = roomCount / 10, max 5'i geçmez)
  - `First Spawn Delay` → 5 sn
  - `Spawn Interval` → 15 sn
  - `Min Distance From Player` → 20 (küçük sahnede 5 yap)
  - `Spawn On Start If No Map Event` → kapalı (Bridge zaten event yayınlıyor)

### D) Player
`Assets/Prefabs/Network/PlayerV0.3.prefab`'ı sahneye sürükle, position `(0, 1, 0)` — Tag mutlaka `Player` olmalı (mesafe kontrolü buna bakıyor).

---

## 2) Akış (sağlıklı log zinciri)

```
[DungeonGeneratorRunner] Seed: 123
[MapEnemyBridge] N oda marker'li hazirlandi.
[MapEnemyBridge] Runtime NavMesh bake tamamlandi.
[MapEnemyBridge] MapReadyEvent yayinlandi (seed=-1, rooms=N).
[TestAutoHost] StartHost cagrildi.
[EnemySpawner] OnNetworkSpawn: MapReadyEvent + EnemyDiedEvent dinlemeye basladi.
[EnemySpawner] MapReadyEvent alindi (rooms=N). Target enemy count: K.
... (First Spawn Delay sonra)
[EnemySpawner] Yeni enemy spawn edildi (Enemy). Alive: 1/K.
[PatrolBehavior] devriye basladi
[ChaseBehavior] Kovalama basladi.
[AttackBehavior] Saldiri menziline girildi.
```

---

## 3) Anıl için — MapGenerator entegrasyonu (opsiyonel temizlik)

Şu an `MapEnemyBridge` Visualizer'ın oluşturduğu "DungeonLayout" objesini tarayarak çalışıyor — yani sizin koda dokunmuyoruz. Daha temiz olması için ileride iki ek yapabilirsen güzel olur:

1. `DungeonGeneratorRunner.GenerateAndVisualize` sonuna 1 satır:
   ```csharp
   GameEventBus.Publish(new MapReadyEvent(gen.LastSeed, data.RoomCount));
   ```
2. `Map.asmdef` references'ına Core (GameEventBus) eklenmesi (eğer Core ayrı asmdef'e taşınırsa).

Bu olmadan da sistem çalışıyor; sadece Bridge gerektirmeyen, doğrudan kontratlı bir akış olur. Pattern Evidence için de güzel kanıt.

---

## 4) Metin için — Network entegrasyonu notları

- `EnemyController` şu an `MonoBehaviour`. Server-only çalışıyor; sahnede `EnemyNetState` NetworkBehaviour'u blinded state'i sync ediyor.
- Spawn server-authoritative: `EnemySpawner.OnNetworkSpawn` içinde `if (!IsServer) return;` var, sadece host enemy üretir.
- `Enemy.prefab` üzerinde NetworkObject + Animator + NavMeshAgent + EnemyController + EnemyNetState bulunmalı.
- Eğer transform sync gerekirse `NetworkTransform` eklenebilir (şu an yok, server tarafından spawn edildiği için pozisyon başlangıçta doğru — patrol/chase sırasında client görmüyor olabilir).
- Animator sync için `NetworkAnimator` eklenebilir (Sprint 2 bonus / Sprint 3).

---

## 5) Tuning ipuçları

| Problem | Nereye bak |
|---|---|
| Enemy dar koridorlarda takılıyor | Enemy prefab → NavMeshAgent → Radius'u 0.5'ten 0.3'e indir |
| Hiç spawn olmuyor | EnemySpawner → Enemy Prefabs array dolu mu, Player tag "Player" mı |
| Spawn olur olmaz görüyorsun | Min Distance From Player'ı 15-25 arası tut |
| NavMesh bake yetmiyor | MapEnemyBridge → Bake Delay Frames: 5-10 |
| Çok fazla enemy var (performance) | Max Alive Enemies'i 3-4'e indir |
| Spawn çok seyrek | Spawn Interval'ı 10-12 sn'ye çek |

---

## 6) Pattern özet (rapor için)

- **Strategy**: `IEnemyBehavior` + 4 concrete (Patrol/Chase/Flee/Attack), runtime `SwitchBehavior`.
- **Observer**: `EnemyDiedEvent`, `MapReadyEvent`, `NoiseEmittedEvent` GameEventBus üzerinde. Spawner hem üretici (spawn log), hem tüketici (alive count + map ready).
- Sound reactivity zinciri: NoiseEmittedEvent → EnemyController.OnNoiseHeard → PatrolBehavior → ChaseBehavior(noisePosition) → huntingNoise modu.
