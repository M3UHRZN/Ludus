# Market System Plan

## Goal

Create a lobby/ship market where players sell recovered loot and spend shared credits on useful equipment before a run.

The first version should be easy to test locally. The final version should be safe for multiplayer by keeping purchase validation on the server.

## Current Project Facts

Existing systems we should reuse:

- `GameSessionManager`
  - Tracks run credits in `NetTotalCredit`.
  - Current credits are collected during gameplay through `ItemPickedUpEvent`.
- `IItem` / `BaseItem`
  - Items already have name, size, weight, and credit value.
- `ItemRegistry`
  - Maps item IDs to prefabs.
  - Useful for market item delivery.
- `PlayerInventory`
  - Stores item IDs in `NetworkList<ushort> Slots`.
  - Has max slot count and active slot selection.
- `PlayerInteraction` + `IInteractable`
  - Already supports raycast interaction.
  - Market terminal should implement `IInteractable`.
- `LobbyScene`
  - Best production target for a market terminal.
- `MarketSystemTest.unity`
  - Test scene copied from `LobbyScene`.

## Main Design Choice

Use a team market wallet first, not per-player money.

Reason:

- VoidHaul is co-op extraction.
- Credits are already tracked at team/session level in `GameSessionManager.NetTotalCredit`.
- Shared team wallet is simpler and matches extraction-game logic better.
- Per-player wallet can be added later if design needs it.

Confirmed decisions:

- Market is only accessible in lobby/ship, not during runs.
- Current money is team-shared.
- The main long-term loop is collecting products/loot from maps and selling them in the market.
- The first and only buyable item for now is Flashbang.
- More buyable items can be added later.
- The market should be placed near the tablet/terminal area in front of the lobby spawn point.

## System Pieces

### 1. Market Catalog

Purpose:

- Stores what can be bought and what can be sold.
- Keeps prices out of UI code.

Recommended file:

- `MarketCatalogItem.cs`
- `MarketCatalog.cs` or `MarketCatalogSO.cs`

Suggested data fields:

- `ushort itemId`
- `string displayName`
- `string description`
- `Sprite icon`
- `int price`
- `int sellValue`
- `bool canBuy`
- `bool canSell`
- `int maxStock`
- `bool unlockedByDefault`
- `GameObject previewPrefab`

Prototype version:

- Can be a serialized list on a `MarketCatalog` MonoBehaviour.

Better final version:

- Use ScriptableObject catalog assets.

### 2. Market Wallet

Purpose:

- Owns current spendable credits.
- Separates "credits collected this run" from "credits available in lobby".

Why not only use `GameSessionManager.NetTotalCredit` directly:

- `NetTotalCredit` is run result money.
- Market needs persistent lobby money or at least carried-over money.
- Spending should not accidentally change current run extraction accounting.

Recommended prototype:

- `MarketWallet.cs`
- local `int CurrentCredits`
- Inspector debug starting credits

Recommended multiplayer version:

- `NetworkVariable<int> NetCredits`
- server-only `TrySpend(int amount)`
- server-only `AddCredits(int amount)`

Later integration:

- After extraction, transfer net earned credits into wallet.

### 3. Market Terminal

Purpose:

- A world object in lobby/ship.
- Player looks at it and presses interact.

Recommended file:

- `MarketTerminal.cs`

Should implement:

- `IInteractable`

Behavior:

- `InteractPrompt`: `"E - Open Market"`
- `CanInteract(PlayerStateMachine player)`: true if player is alive and not carrying/holding restricted things.
- `Interact(PlayerStateMachine player)`: opens market UI for local player.

Important:

- Keep opening UI local.
- Purchase requests can later go to server.

### 4. Market UI

Purpose:

- Shows buy/sell tabs, selected item details, price, stock, current credits, buy button, and sell button.

Recommended file:

- `MarketUIController.cs`

UI elements:

- root panel
- close button
- credits text
- item list parent
- item row prefab
- selected item name
- selected item description
- selected item price
- selected item icon
- buy button
- sell button
- buy/sell tab control
- error/status text

Controls:

- `Open(MarketTerminal terminal)`
- `Close()`
- `SelectItem(MarketCatalogItem item)`
- `TryBuySelected()`
- `TrySellSelected()`
- `Refresh()`

Prototype shortcut:

- Build UI in scene with Unity Canvas.
- Rows can be simple buttons.

### 5. Market Transaction Service

Purpose:

- Central place for buy and sell rules.
- UI should not directly subtract money or add items.

Recommended file:

- `MarketTransactionService.cs`

Buy rules:

- item exists in catalog
- item is unlocked
- item has stock
- wallet has enough credits
- buyer has inventory space or delivery spawn point exists

Sell rules:

- sold item exists in buyer inventory or sell container
- item is sellable
- sell value is valid
- server removes item
- server adds credits to shared wallet

Prototype:

- local methods `TryBuy(PlayerInventory inventory, MarketCatalogItem item)` and `TrySell(PlayerInventory inventory, ushort itemId)`

Final multiplayer:

- buyer sends `RequestPurchaseServerRpc(itemId)`
- buyer sends `RequestSellServerRpc(itemId or slotIndex)`
- server checks wallet/catalog/inventory
- server subtracts credits
- server adds item ID to inventory or spawns item object
- server removes sold item and adds credits
- server sends result back to client

### 6. Delivery Mode

There are two possible item delivery styles.

Option A: Add directly to inventory

- Good for small consumables.
- Simple and fast.
- Uses `PlayerInventory.TryAddItem(itemId)`.

Risk:

- PlayerInventory currently calls server RPC from owner-side.
- Market service may need a server-side inventory method later.

Option B: Spawn item on market table

- Better for physical co-op feel.
- Uses existing pickup/grab world interaction.
- More visible and fun.

Risk:

- Requires network object prefab setup for final multiplayer.

Recommended first prototype:

- Direct inventory add for item IDs.

Recommended final direction:

- Small items go to inventory.
- Heavy/equipment items spawn on a delivery table.

### 7. Stock System

Prototype:

- unlimited stock or simple local count.

Final:

- `NetworkList<MarketStockEntry>`
- each entry: `itemId`, `remainingStock`
- server decrements stock on purchase.

Suggested behavior:

- flashbang: stock 3
- future medkit: stock 2
- future flashlight battery: stock 4
- future scanner/tool: stock 1

### 8. Price Rules

Prototype:

- static price per item.

Future:

- price can scale by day/run number
- sale/discount can appear randomly
- rare items can unlock after quota milestones

Do not implement dynamic pricing in first pass. It will make testing noisy.

### 9. Save/Persistence

Do not implement real save in first pass.

Prototype:

- Inspector starting credits.
- Reset every play session.

Later:

- save wallet in session manager or persistent profile.
- sync wallet when returning to lobby.

### 10. Events

Suggested new events:

- `MarketOpenedEvent`
- `MarketClosedEvent`
- `MarketPurchaseSucceededEvent`
- `MarketPurchaseFailedEvent`
- `MarketSellSucceededEvent`
- `MarketSellFailedEvent`
- `MarketCreditsChangedEvent`

Use events for UI/audio feedback, not for purchase authority.

Purchase authority should stay in service/server logic.

## First Prototype Flow

1. Open `MarketSystemTest.unity`.
2. Player looks at market terminal near the lobby spawn tablet area.
3. Press interact.
4. Market UI opens.
5. Buy tab lists Flashbang.
6. Sell tab lists recovered/sellable loot.
7. Player selects an item.
8. Buy button checks wallet and inventory space.
9. Sell button checks ownership and sell rules.
10. If buy is valid:
   - credits decrease
   - item ID is added to inventory or item spawns on table
   - UI refreshes
11. If sell is valid:
   - item is removed
   - credits increase
   - UI refreshes
12. If invalid:
   - show status message
   - do not change credits/items
13. Close UI and return control to player.

## Recommended First Items

Start with one buyable item:

- Flashbang
  - price: 40
  - item ID: to be assigned from registry
  - reason: recent branch already has flashbang test work.

Future buyable examples:

- Battery
- Medkit
- Scanner or detector

Sellable loot:

- Use `BaseItem.CreditValue` as default sell value.
- Later, rare loot can override or multiply sell value.
- Selling should happen in lobby market, not during a run.

## UI Layout

Recommended layout:

- left panel: buy/sell item list
- right panel: selected item details
- top right: current credits
- bottom right: buy/sell action button
- bottom: status/error line

Do not overdesign the first version.

The important part is:

- readable prices
- obvious affordance for Buy/Sell
- clear feedback when purchase fails

## Network Plan

Prototype:

- local MonoBehaviour wallet and purchase flow.

Migration:

- convert wallet/purchase service to NetworkBehaviour.
- make wallet server writable.
- client UI calls `RequestPurchaseServerRpc`.
- server validates item, stock, credits, inventory.
- server sends result through ClientRpc or NetworkVariable updates.

Security rules:

- client never subtracts credits directly.
- client never grants item directly in final build.
- client only requests purchase.

## Risk List

### Inventory API risk

`PlayerInventory.TryAddItem` is owner-side and calls a server RPC. A server-side market purchase may need a new method like:

- `ServerAddItem(ushort itemId)`

This should be added carefully later.

### Credits ownership risk

`GameSessionManager.NetTotalCredit` is run score, not necessarily spendable wallet.

Market should not mutate it directly unless the team decides credits are not persistent.

### UI ownership risk

Only the local player should open/close UI.

Do not network UI state.

### Scene risk

Do not edit `LobbyScene` directly during prototyping.

Use `MarketSystemTest.unity`.

## Unused / Test-only Script Candidates

Not safe to delete automatically, but worth reviewing:

- `ItemSpawnerTester.cs`
- `ObjectPoolTester.cs`
- `MockTest.cs`
- `ItemPickup.cs`
- `EnemyTestDummy.cs`
- `TestMapReadyTrigger.cs`
- `FakeEnemy.cs`
- `FlashbangTest*` scripts
- `LobbyBuilder.cs`
- `DungeonGeneratorEditor.cs`
- `FlashbangSceneAuthoring.cs`

The first real cleanup should be only test scripts that are not referenced by active scenes or prefabs.

## Proposed Implementation Order

1. Create `MarketCatalogItem` data model.
2. Create local `MarketWallet`.
3. Create `MarketTerminal : IInteractable`.
4. Create `MarketUIController`.
5. Create simple UI prefab/scene objects in `MarketSystemTest`.
6. Add local purchase service.
7. Add test catalog entries.
8. Add buy success/fail UI feedback.
9. Add inventory delivery.
10. Add spawn-on-table delivery if needed.
11. Convert purchase path to server-authoritative version.
12. Integrate into real `LobbyScene`.

## Questions Before Implementation

Settled:

- Team-shared money for now.
- Market is lobby-only.
- Main game loop includes manually selling collected products/loot in market.
- First purchasable item is Flashbang only.

Still open:

Settled after follow-up:

- Bought flashbang should physically drop at a delivery point.
- Selling should support both `Sell One` and `Sell All`.
- Flashbang must never be sellable.
- Debug/editor tooling should support adding credits, filling inventory, and clearing inventory.

Current implementation files:

- `MarketCatalogItem.cs`
- `MarketCatalog.cs`
- `MarketWallet.cs`
- `MarketTransactionService.cs`
- `MarketTerminal.cs`
- `MarketUIController.cs`
- `MarketDebugTools.cs`
- `Assets/Scripts/Editor/MarketSystemEditorTools.cs`

Editor menu:

- `VoidHaul/Market/Setup Test Scene Objects`
- `VoidHaul/Market/Add 100 Credits`
- `VoidHaul/Market/Reset Credits`
- `VoidHaul/Market/Clear Local Inventory`
