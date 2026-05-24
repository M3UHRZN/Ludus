# Market System Workspace

This folder is the isolated workspace for the `feature/market_System` branch.

Current scope:
- design and prototype a lobby/ship market system
- keep market work separate from the main lobby scene
- use a copied test scene for iteration
- support team-shared money
- support buying flashbang only for now
- support selling loot one-by-one and all at once
- purchased items should physically drop at a delivery point

Test scene:
- `Assets/Scripts/MarketSystem/Scene/MarketSystemTest.unity`
- copied from `Assets/Scenes/LobbyScene.unity`

Useful existing systems:
- `GameSessionManager`: stores collected credits during a run
- `BaseItem` / `IItem`: existing item value and item metadata
- `ItemRegistry`: maps item IDs to item prefabs
- `PlayerInventory`: owns player item slots
- `PlayerInteraction` / `IInteractable`: raycast interaction flow
- `MainMenuUI`, `LobbyRoomManager`: existing lobby UI/session flow

Important direction:
- Market should start as a local/prototype flow.
- Final multiplayer version should keep purchase validation server-side.
- Obsidian notes are personal planning docs and should not be included in PR text.

Debug/editor tools:
- `VoidHaul/Market/Setup Test Scene Objects`
  - creates `MarketSystem`
  - creates `MarketDeliveryPoint`
  - creates `MarketTerminal`
  - creates basic `MarketCanvas` / `MarketPanel`
  - creates/updates `EventSystem` with `InputSystemUIInputModule`
  - adds default Flashbang catalog entry
- `VoidHaul/Market/Add 100 Credits`
- `VoidHaul/Market/Reset Credits`
- `VoidHaul/Market/Clear Local Inventory`

Runtime scripts:
- `MarketCatalogItem.cs`
- `MarketCatalog.cs`
- `MarketWallet.cs`
- `MarketTransactionService.cs`
- `MarketTerminal.cs`
- `MarketUIController.cs`
- `MarketDebugTools.cs`
