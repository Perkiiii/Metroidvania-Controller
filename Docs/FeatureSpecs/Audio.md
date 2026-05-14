# Feature Spec — Audio

## Responsibilities

Route all sound output through a single persistent manager so that volume settings, mix groups, and interrupt logic can be changed without touching call sites.

---

## Current State

No audio system exists. `HeroAttackModule` has an `AudioSource` field for slash SFX — this is technical debt to be replaced in Milestone 2. This spec describes the intended architecture.

---

## Design Goals

- Single call-site pattern: action classes pass an `AudioClip` to `AudioManager`; they never hold or manage an `AudioSource`
- Vertical slice API is minimal (two methods); the internal implementation can grow without breaking call sites
- Music is controlled by `GameManager`, not by scene objects or gameplay MonoBehaviours
- Volume, mix groups, and audio settings are added inside `AudioManager` without touching callers

---

## AudioManager

`AudioManager` (MonoBehaviour, DontDestroyOnLoad, initialised in `Bootstrap`).

**Vertical slice API:**

```csharp
public void PlaySFX(AudioClip clip)
public void PlaySFX(AudioClip clip, Vector3 worldPosition)   // optional overload for spatial SFX
public void PlayMusic(AudioClip clip, bool loop = true)
public void StopMusic()
```

**Internal implementation (vertical slice):**
- SFX: `AudioSource.PlayClipAtPoint` or a pool of `AudioSource` components on child GameObjects. The pool can be added later without changing the public API.
- Music: a dedicated `AudioSource` on `AudioManager` itself. `PlayMusic` stops any current track and starts the new one.

**Future expansion (no call-site changes required):**
- Add `AudioMixerGroup` parameters to route SFX / Music / UI through separate mixer groups.
- Add `SetVolume(AudioCategory, float)` for settings.
- Replace the SFX implementation with a proper pool for high-frequency sounds.
- Add `PlaySFXWithDelay`, `FadeOutMusic`, etc. as new methods — existing callers are unaffected.

---

## Call Sites

| Event | Caller | Method |
|---|---|---|
| Slash attack | `HeroAttackModule.Activate()` | `AudioManager.Instance.PlaySFX(slashSfx)` |
| Jump | `HeroJumpAction` on jump start | `AudioManager.Instance.PlaySFX(jumpSfx)` |
| Land | `HeroJumpAction` or `HeroMotor` on ground contact | `AudioManager.Instance.PlaySFX(landSfx)` |
| Dash | `HeroDashAction` on dash start | `AudioManager.Instance.PlaySFX(dashSfx)` |
| Hurt | `HeroController` on OnDamaged | `AudioManager.Instance.PlaySFX(hurtSfx)` |
| Enemy hit | `EnemyHealthComponent.ReceiveHeroAttack` | `AudioManager.Instance.PlaySFX(hitSfx)` |
| Scene music | `GameManager.BeginSceneTransition` | `AudioManager.Instance.PlayMusic(clip)` |

SFX clips are stored as `AudioClip` fields on the relevant ScriptableObject (`HeroConfig`, `EnemyConfig`) or MonoBehaviour (`HeroAttackModule`). The clip reference lives close to the tuning data it belongs to; `AudioManager` is only the router.

---

## Music Routing

Music is scene-scoped. The clip to play for each scene is stored on a lightweight scene-descriptor ScriptableObject or directly on `GameManager` as a serialised `SceneAudioEntry[]` array (scene name → AudioClip). `GameManager.BeginSceneTransition` looks up the clip for the incoming scene and calls `AudioManager.PlayMusic` after the fade-in.

Do not call `AudioManager.PlayMusic` from `Awake`, `Start`, or `SceneInit` handlers in gameplay scenes — music is the transition's responsibility.

---

## AudioListener

One `AudioListener` on the main gameplay camera. `UICamera` must not have an `AudioListener`. `AudioManager` must not have an `AudioListener`.

---

## Dependencies

- `GameManager` — triggers music changes on scene transition
- `HeroConfig`, `EnemyConfig`, `HeroAttackModule` — store `AudioClip` references for their domain

---

## Rules

- Do not call `AudioSource.Play()` or `AudioSource.PlayOneShot()` directly on hero or enemy prefabs. All SFX goes through `AudioManager.PlaySFX`.
- Do not play or stop music from any gameplay MonoBehaviour. Music is `GameManager`'s responsibility.
- Do not add an `AudioListener` to any camera other than the main gameplay camera.
- `AudioManager` must not reference `HeroController`, `EnemyController`, or any gameplay MonoBehaviour.
