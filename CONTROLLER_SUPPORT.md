# Controller support

Input is delegated to unmodified SDL 3.4.16. Capabilities are checked on each opened device; a device name alone never enables RGB, rumble or battery data.

| Family | Input | Battery | Rumble | RGB | Player lights | Disconnect |
|---|---|---|---|---|---|---|
| DualSense / Edge | SDL HIDAPI | If reported; shown approximately | If advertised | If advertised | SDL player index; PS patterns | Sony Bluetooth address + Windows radio; unverified physically |
| DualShock 4 | SDL HIDAPI | If reported; shown approximately | If advertised | If advertised | Model dependent | Sony Bluetooth address + Windows radio; unverified physically |
| Xbox One / Series / XInput | SDL | API/transport dependent; unknown is shown unavailable | If advertised | Model dependent | API dependent | Not exposed by this adapter |
| Switch Pro / Joy-Con | SDL | API/transport dependent | If advertised | Model dependent | If advertised | Not exposed by this adapter |
| Other SDL-mapped gamepads | SDL mapping database | Capability dependent | Capability dependent | Capability dependent | Capability dependent | Not exposed |

Pame stores preferred player slots by vendor/product and serial, or device path when no serial exists. Four simultaneous slots are assigned without collisions; a fifth device is not assigned an occupied slot. Reassigning a player swaps with the current holder. RGB choices persist locally.

Sony player patterns are delegated to SDL: player one is the center indicator, player two uses two separated indicators, then three and four. SDL owns transport framing, Bluetooth CRC and report details; Pame does not duplicate those protocol implementations.

Input uses an analog deadzone, edge-triggered confirm/back and delayed directional repeat. Guide needs a 650 ms hold. Gyro and touchpad remapping are not exposed. Pame does not emulate an Xbox controller for games; Steam Input or the game's own support still determines gameplay compatibility.

Version 0.2 displays imported button PNGs for Xbox, PlayStation and Switch. Auto mode follows the active controller, with a manual family override. Desktop mode adds stick-driven pointer/scroll and face-button mouse/keyboard actions using normal-user Windows input. This does not replace native controller support inside games and still needs a physical-device trial.

Idle disconnect is offered for supported Sony wireless devices and defaults to seven minutes while browsing Pame. It is suspended during gameplay. Windows disconnect does not guarantee that every device powers itself off, so the UI calls it **disconnect**, not power off.

## Hardware validation

The machine's MediaTek adapter initially reported a paired, disconnected DualSense. During final 0.2 UI validation it connected: SDL reported wireless player 1, about 95% battery and rumble/RGB/player-light capabilities. A light report was accepted. Felt rumble, visible LED appearance, sustained physical input and disconnect remain unverified. A native SDL virtual gamepad test verifies enumeration, slot allocation, confirm, analog navigation and Guide hold through the same adapter. Its data is never presented as a physical controller in the normal library.

WinRT Bluetooth discovery located the exact disconnected Sony record. Pair/repair commands were validated in dry-run mode. No paired device was removed during testing.
