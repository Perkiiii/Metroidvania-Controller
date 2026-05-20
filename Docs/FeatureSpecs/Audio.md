# Feature Spec — Audio

**Last audited:** 2026-05-20

## Responsibilities

Provide one global audio router for music, enemy/world/UI one-shots, and future settings, while keeping the hero's fixed action sound palette visible on the hero prefab.

---

## Current State

`AudioManager` is a persistent singleton created through the boot flow. It owns generic SFX playback and music playback.

The hero has a local `HeroAudioController` on the root hero object. Its serialized fields reference one `AudioSource` per sound under `Hero/Sounds/*`:

- `Jump`
- `Land`
- `Dash`
- `WallJump`
- `WallSlide`
- `TakeDamage`
- `Death`
- `Footstep`
- `TerrainImpact`

This is intentionally Hollow-Knight-style authoring for the hero only. It lets a small fixed set of hero sounds expose clip, volume, pitch, spatial blend, and future mixer routing directly on the prefab.

---

## AudioManager

`AudioManager` (MonoBehaviour, DontDestroyOnLoad, initialised in `Bootstrap`).

**API:**

```csharp
public void PlaySFX(AudioClip clip)
public void PlaySFX(AudioClip clip, float pitchMin, float pitchMax, float volume = 1f)
public void PlayMusic(AudioClip clip, bool loop = true)
```

**Internal implementation:**
- SFX without pitch uses the configured `SFX` child source through `sfxSource.PlayOneShot`.
- Pitch-varied SFX use a small reusable pool of `PooledSFX` child sources under the configured `SFX` source.
- Music uses a dedicated `AudioSource` on `AudioManager`.
- On startup, `AudioManager` resolves missing `SFX` / `Music` references from child objects by name, and creates the child source if it is missing.

---

## HeroAudioController

`HeroAudioController` is the only hero subsystem allowed to call `AudioSource.Play()` or `AudioSource.Stop()` directly.

Action classes receive it through `HeroActionController.Initialize(...)` and call named methods such as `PlayJump`, `PlayDash`, `PlayWallJump`, and `PlayTerrainImpact`. The action classes do not hold `AudioSource` references.

Footsteps are animation-event-driven through `AnimEventFootstep()`, with one immediate footstep when grounded movement first becomes allowed. The footstep source ignores events while already playing and is stopped immediately from `Tick()` when the hero is no longer grounded, moving, controllable, or otherwise allowed to produce footsteps.

Wall slide uses a dedicated local source and is stopped by `HeroAudioController` as soon as `blackboard.wallSliding` becomes false.

---

## Call Sites

| Event | Caller | Method |
|---|---|---|
| Slash attack | `HeroAttackModule.Activate()` | `AudioManager.Instance.PlaySFX(slashClip, pitchMin, pitchMax)` |
| Jump | `HeroJumpAction` on jump start | `HeroAudioController.PlayJump()` |
| Land | `HeroController` on ground contact | `HeroAudioController.PlayLand()` |
| Dash | `HeroDashAction` on dash start | `HeroAudioController.PlayDash()` |
| Wall jump | `HeroWallJumpAction` on wall jump start | `HeroAudioController.PlayWallJump()` |
| Wall slide | `HeroWallSlideAction` on slide entry | `HeroAudioController.PlayWallSlide()` |
| Hurt | `HeroController` on `OnDamaged` or `OnHazardDamaged` | `HeroAudioController.PlayTakeDamage()` |
| Death | `HeroController` on `OnDeath` | `HeroAudioController.PlayDeath()` |
| Footstep | Walk/run animation event | `HeroAudioController.AnimEventFootstep()` |
| Terrain hit | `HeroAttackAction.EvaluateTerrainImpact()` | `HeroAudioController.PlayTerrainImpact()` |
| Enemy hit/death | Enemy health components | `AudioManager.Instance.PlaySFX(...)` |
| Scene music | `GameManager.BeginSceneTransition` | `AudioManager.Instance.PlayMusic(clip)` |

---

## Music Routing

Music is scene-scoped. The clip to play for each scene is stored on a lightweight scene-descriptor ScriptableObject or directly on `GameManager` as a serialised `SceneAudioEntry[]` array (scene name -> AudioClip). `GameManager.BeginSceneTransition` looks up the clip for the incoming scene and calls `AudioManager.PlayMusic` after the fade-in.

Do not call `AudioManager.PlayMusic` from `Awake`, `Start`, or `SceneInit` handlers in gameplay scenes. Music is the transition's responsibility.

---

## AudioListener

One `AudioListener` on the main gameplay camera. `UICamera` must not have an `AudioListener`. `AudioManager` must not have an `AudioListener`.

---

## Dependencies

- `GameManager` - triggers music changes on scene transition
- `AudioManager` - routes global SFX and music
- `HeroAudioController` - owns hero-local action sources
- `EnemyConfig`, `HeroAttackModule` - store non-hero-local or module-local `AudioClip` references

---

## Rules

- Do not call `AudioSource.Play()` or `AudioSource.PlayOneShot()` directly from actions, enemies, world objects, or UI.
- Hero movement, hurt, death, footstep, and terrain-impact sounds must go through `HeroAudioController`.
- `HeroAudioController` is the only hero component allowed to directly play or stop hero-owned `AudioSource`s.
- Non-hero gameplay, UI, world, enemy, and music audio must go through `AudioManager`.
- Do not play or stop music from any gameplay MonoBehaviour. Music is `GameManager`'s responsibility.
- Do not add an `AudioListener` to any camera other than the main gameplay camera.
- `AudioManager` must not reference `HeroController`, `EnemyController`, or any gameplay MonoBehaviour.
