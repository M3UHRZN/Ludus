# TestTorch

Network-ready flashlight test assets for VoidHaul.

This folder contains the copied low-poly torch model, material, textures, light cookies,
the `TestTorchItem` runtime script, an item definition, and a network prefab. The original
third-party import folder was removed after the needed files were copied here to avoid
duplicate demo scripts and legacy Input usage.

Controls:

- Pick up / stash uses the existing project interaction flow.
- While holding the torch, press `Q` to toggle the light.
- The toggle request is server-validated against `PhysicsObject.NetGrabberClientId`.

Networking:

- `TestTorchItem.NetLightOn` is server-written and replicated to every client.
- Toggle audio is a one-shot RPC.
- The visual light state is applied on every peer from the replicated state.

Market:

- Item id: `3`
- Item definition: `Assets/TestTorch/ItemDefinitions/TestTorch.asset`
- World prefab: `Assets/TestTorch/Prefabs/TestTorchWorld.prefab`
- The lobby market can spawn the torch as a network pickup.
- Players can pick it up with `E`, carry it through inventory, and use it in Lobby or RNG Map.

Audio:

- Put the click sound in `Assets/TestTorch/Audio/`.
- Assign that clip to `TestTorchWorld.prefab > TestTorchItem > Toggle Clip`.
- The same clip is used for turning the torch on and off.
