# Flashbang Prototype

This folder is the isolated workspace for the `feature/flashbang` branch.

Current scope:
- local flashbang feedback prototype
- dedicated test scene copy: `Assets/Scripts/Flashbang/Scene/FlashbangTest.unity`
- trigger key: `G`
- enemy throws a flashbang every 10 seconds
- player throw uses direct launch velocity from the camera look direction
- projectile falls naturally with gravity after launch

Current scripts:
- `FlashbangEffect.cs`: handles white-screen flash and optional ringing audio
- `FlashbangTestController.cs`: player-side straight launch trigger with blast radius
- `FlashbangTestPlayer.cs`: standalone movement/look controller with flashbang penalties
- `FlashbangLocalEnemy.cs`: local enemy that throws flashbangs at intervals
- `FlashbangProjectile.cs`: lightweight runtime projectile with velocity + gravity
- `FlashbangTestBootstrap.cs`: builds the whole local test rig automatically on scene load

Existing project-side flashbang-related files found elsewhere:
- `Assets/Scripts/Player/PlayerInventory.cs`
- `Assets/Scripts/Items/Decorators/ItemDecorator.cs`
- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/Enemy/EnemyTestDummy.cs`

Important note:
- `EnemyController.SetBlinded()` currently requires server authority.
- The copied `FlashbangTest.unity` scene already contains `NetworkManager` and `TestAutoHost`.

Suggested current test flow:
- Open `Assets/Scripts/Flashbang/Scene/FlashbangTest.unity`
- Press Play
- Runtime bootstrap disables legacy test/network objects and creates a clean local flashbang rig
- Press `G` to throw a flashbang straight toward where the camera is looking
- Wait 10 seconds to receive a flashbang from the enemy
