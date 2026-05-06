# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Ludus** (working title: *VoidHaul*) is a Unity 6 co-op extraction/heist game inspired by R.E.P.O. Players grab and haul physics objects out of a level before a timer runs out.

- **Unity version:** 6000.3.13f1 (Unity 6)
- **Render pipeline:** URP 17.3.0
- **Networking:** Netcode for GameObjects (NGO) 2.11.0 + Unity Multiplayer Services 2.2.1 (Sessions API / Relay)

## Development Commands

Unity has no CLI build/test runner configured — all builds and Play Mode tests run inside the Unity Editor. Use the Unity Test Framework (`com.unity.test-framework` 1.6.0) via **Window → General → Test Runner**.

For Editor-only scripting tasks, open the project in Unity 6 (6000.3.13f1) and work in the Editor.

The custom Editor tool **VoidHaul → Build Lobby Scene** (`Assets/Scripts/Editor/LobbyBuilder.cs`) procedurally places 143 prefabs from `Assets/Lobby/Scene/ScenePrefab/Sci-Fi Styled Modular Pack/` into the active scene.

## Architecture

### Event System — `GameEventBus` (Observer pattern)
`Assets/Scripts/Core/GameEventBus.cs` is a static, type-keyed pub/sub bus. All cross-system communication goes through it. Event structs (`EnemyDiedEvent`, `ItemPickedUpEvent`, `PlayerDamagedEvent`, `PlayerDiedEvent`, `TimerEventTriggered`) are defined at the bottom of the same file.

- Subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- Call `GameEventBus.Clear()` on scene transitions.
- The HUD (`HUDController`) is driven entirely by bus events — it never polls game state directly.

### Networking — `ConnectionManager`
`Assets/Scripts/Network/ConnectionManager.cs` uses the Sessions API (`MultiplayerService.Instance.CreateOrJoinSessionAsync`) with **Distributed Authority** (`WithDistributedAuthorityNetwork()`). There is no dedicated server; any client can become session owner via `OnSessionOwnerPromoted`. `UnityServices.InitializeAsync()` is started in `Awake` and awaited before sign-in.

### Enemy AI — Strategy / State pattern
`EnemyController` owns the active behavior and delegates every frame to `IEnemyBehavior.Tick()`. Behaviors (`PatrolBehavior`, `ChaseBehavior`) are plain C# classes (no MonoBehaviour). Switching state calls `Exit` on the old behavior then `Enter` on the new one. `ChaseBehavior` uses a 2-second lost-sight grace period before reverting to patrol.

Stub: `EnemyController.SetBlinded()` is a TODO for Sprint 2 (`FleeBehavior`).

### Item System — Interface + Decorator pattern
`IItem` (in `Assets/Scripts/Items/IItem.cs`) defines the item contract. `BaseItem : MonoBehaviour, IItem` is the concrete Unity component. `ItemDecorator` (abstract, pure C#) wraps any `IItem` and overrides only what it needs — `FlashbangDecorator` is the first concrete decorator; it blinds nearby enemies on pickup via `EnemyController.SetBlinded`.

Item size maps directly to weight: Small = 1, Medium = 3, Large = 6.

### Physics Grab System
`GrabSystem` (on the player/camera) raycasts to find a `PhysicsObject` in range, then drives it to a hold point in `FixedUpdate` using a spring–damper force (`holdSpringStrength` / `holdDamping`). Throw force scales with charge time (hold LMB). If the held object drifts more than `grabDistance × 2.5` from the hold point it auto-drops.

### Player
`PlayerMovement` uses `CharacterController` with walk/run/crouch speeds and a manual gravity accumulator. `PlayerLook` handles mouse-look (separate component). `GrabSystem` attaches to the camera object.

### Object Pool
`ObjectPool<T>` (`Assets/Scripts/Core/ObjectPool.cs`) is a generic, non-MonoBehaviour pool. Instantiate it in `Awake`, call `Get()` / `Return()`.

### HUD
`HUDController` subscribes to `TimerEventTriggered`, `ItemPickedUpEvent`, and `PlayerDiedEvent`. `SetPlayerCount(n)` must be called at game start (currently invoked via `MockTest` keyboard shortcuts: `4` = 4 players, `6` = 6 players).

## Key Conventions

- Event structs live in `GameEventBus.cs`. Add new events there.
- Enemy behaviors are stateless where possible; state lives in `EnemyController` public properties.
- `MockTest.cs` and `MockTimer.cs` are in-Editor test harnesses (keyboard-driven), not production code.
- Comments and some Debug strings are in Turkish — this is intentional (team language).
- The `Assets/Scripts/Editor/` folder is Editor-only; scripts there must be inside `#if UNITY_EDITOR` or the `Editor` asmdef.
