# Camera Name Test

This folder contains a local, non-network test setup for player nickname display and camera mode switching.

## Test Scene

Open:

`Assets/Scripts/CameraNameTest/Scene/CameraNameTest.unity`

## Controls

- `WASD`: Move
- `Mouse`: Look
- `Space`: Jump
- `Left Shift`: Sprint
- `C`: Toggle first person / third person
- `Esc`: Toggle cursor lock

## Behavior

- The name above the player reads `PlayerPrefs["DisplayName"]`.
- `MainMenuUI` now saves the display name before host, join, browse, or browser join actions.
- Third-person camera uses a sphere cast to avoid clipping into walls.
- If the third-person camera is blocked too closely, it smoothly moves into first-person position.
- When the obstruction is gone, the camera returns to third person if third-person mode is still requested.

This test is intentionally not networked. The camera and nameplate logic can later be moved onto the local owner player in the Netcode player prefab.
