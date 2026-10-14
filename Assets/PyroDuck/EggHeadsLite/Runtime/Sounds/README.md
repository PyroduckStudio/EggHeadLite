# Audio Assets

This folder contains the runtime audio clips used by `AudioManager`,
`AudioLibrarySO`, weapon prefabs, projectile prefabs, and sample scenes.

## Included clips


| File                         | Used by                            | `SoundId`                                                |
| ---------------------------- | ---------------------------------- | -------------------------------------------------------- |
| `Sounds/BubbleShot.wav`      | `ProjectileBuble` prefab (direct)  | —                                                        |
| `Sounds/Bumerang.wav`        | `AudioLibrary`                     | `ThrowableImpact` (201)                                  |
| `Sounds/Explosion.wav`       | `AudioLibrary`                     | `ProjectileImpact` (101)                                 |
| `Sounds/ShurikenFly.wav`     | `AudioLibrary`                     | `ThrowableThrow` (200)                                   |
| `UsingSounds/ClickSound.wav` | Weapon base prefabs (direct)       | —                                                        |
| `UsingSounds/Explosion1.wav` | `AudioLibrary`                     | `ProjectileExplosion` (102) + `ThrowableExplosion` (202) |
| `UsingSounds/KnifeStab2.wav` | `AudioLibrary`                     | `MeleeImpact` (301)                                      |
| `UsingSounds/Pistol.wav`     | `AudioLibrary`                     | `ProjectileFire` (100)                                   |
| `UsingSounds/Reload.wav`     | `WeaponRangedBase` prefab (direct) | —                                                        |
| `UsingSounds/Swing.wav`      | `AudioLibrary`                     | `MeleeSwing` (300)                                       |


A pre-configured `AudioLibrary.asset` is at `Runtime/Data/AudioLibrary.asset` and is already
wired into the Platformer sample scene. To add or replace clips:

1. Open `Runtime/Data/AudioLibrary.asset` in the Inspector.
2. Expand the entry for the `SoundId` you want to change.
3. Drag your new `.wav` / `.ogg` clip into the `Clips` array.

To add entirely new sounds, add a new value to `SoundId.cs` first, then add a
matching `SoundEntry` to the `AudioLibrary.asset`.

## Setup in a custom scene

1. Add an **AudioManager** component to a GameObject in your scene.
2. Assign `Runtime/Data/AudioLibrary.asset` to its **Library** field.

Playback from anywhere:

```csharp
using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Events;

EventManager.Publish(new PlaySoundEvent   { Id = SoundId.ProjectileFire });
EventManager.Publish(new PlaySoundAtEvent { Id = SoundId.MeleeImpact, Position = hitPos });
```

