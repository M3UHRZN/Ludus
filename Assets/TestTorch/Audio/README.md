# TestTorch Audio

Put the flashlight click sound in this folder.

Use the same clip for both turning the torch on and off:

1. Select `Assets/TestTorch/Prefabs/TestTorchWorld.prefab`.
2. Find the `TestTorchItem` component.
3. Drag the click AudioClip into `Toggle Clip`.
4. Keep `Toggle Audio Source` assigned to `TestTorchLight`.

The script plays `Toggle Clip` every time the torch changes state, so one click sound is enough.
