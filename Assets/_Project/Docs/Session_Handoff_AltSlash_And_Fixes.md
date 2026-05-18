# Session Handoff — Alt Slash System & Bug Fixes
**Date:** 2026-05-18  
**Branch:** main

---

## 1. Slash / AltSlash Alternating Attack System

### What It Does
Side attacks now alternate between two visually distinct slash effects each time the player attacks. If the player waits longer than `altAttackResetTime` (default 1 second) without attacking, the sequence resets back to the first slash. Up and Down attacks are never affected by the toggle.

### Code Changes

#### `Assets/_Project/Scripts/Hero/Combat/HeroAttackModule.cs`
- Added `public bool isAlt;` under `[Header("Identity")]`
- This flag distinguishes the normal side module from the alt side module in the inspector. `FindModule` uses it to pick the right one.

#### `Assets/_Project/Scripts/Hero/Core/HeroStateBlackboard.cs`
- Added `public bool altAttack;` — the live toggle state
- Added `public float altAttackTime;` — timestamp of the last side attack, used for the reset check

#### `Assets/_Project/Scripts/Hero/Core/HeroConfig.cs`
- Added `public float altAttackResetTime = 1f;` under `[Header("Attack")]`
- Controls how long the player must pause before the alt sequence resets to the first slash

#### `Assets/_Project/Scripts/Hero/Actions/HeroAttackAction.cs`

**`StartAttack()` — toggle logic inserted before `FindModule`:**
```csharp
bool useAlt = false;
if (currentDirection == HeroAttackDirection.Side)
{
    if (Time.unscaledTime - blackboard.altAttackTime > config.altAttackResetTime)
        blackboard.altAttack = false;

    useAlt = blackboard.altAttack;
    blackboard.altAttack = !blackboard.altAttack;
    blackboard.altAttackTime = Time.unscaledTime;
}
currentModule = FindModule(currentDirection, useAlt);
```

**`FindModule()` — updated signature and alt-aware lookup with fallback:**
```csharp
private HeroAttackModule FindModule(HeroAttackDirection direction, bool useAlt = false)
{
    // Exact match on direction + isAlt
    for (int i = 0; i < attackModules.Length; i++)
    {
        HeroAttackModule module = attackModules[i];
        if (module != null && module.direction == direction && module.isAlt == useAlt)
            return module;
    }
    // Fallback: if no alt module is wired up, use the normal one
    if (useAlt)
    {
        for (int i = 0; i < attackModules.Length; i++)
        {
            HeroAttackModule module = attackModules[i];
            if (module != null && module.direction == direction)
                return module;
        }
    }
    return null;
}
```

### What Was NOT Changed
- `HeroAttackDirection` enum — no new values
- `HeroAnimationLibrary` / `HeroAnimationController` — hero body animation is identical for both slashes (effect-only variation)
- Damage, force direction, hit receivers, clash detection — all unaffected
- Up / Down attacks — no toggle, no change

---

## 2. Prefab & Inspector Setup (Hero.prefab)

The user duplicated the `SlashSide` child GameObject and renamed it `SlashSideAlt`. Two issues were found and fixed via script:

### Issue A — `isAlt` flag was `false`
The duplicated module had `isAlt = false`, so `FindModule(Side, useAlt=true)` never matched it. Fixed by setting `isAlt = true` on the `SlashSideAlt` HeroAttackModule.

### Issue B — `SlashSideAlt` missing from `HeroActionController.attackModules`
The serialized array on `HeroActionController` only had 3 entries: `[SlashSide, SlashUp, SlashDown]`. `SlashSideAlt` was inserted at index 1, giving: `[SlashSide, SlashSideAlt, SlashUp, SlashDown]`.

**Final state of `HeroActionController.attackModules`:**
| Index | Name | Direction | isAlt |
|---|---|---|---|
| 0 | SlashSide | Side | false |
| 1 | SlashSideAlt | Side | true |
| 2 | SlashUp | Up | false |
| 3 | SlashDown | Down | false |

---

## 3. SlashSideAltVFX Animation Clip Fixes

**Clip path:** `Assets/_Project/Animations/Slash Animations/SlashSideAltVFX.anim`

Three separate problems were found and fixed:

### Fix A — Wrong Binding Paths
All curve bindings had an empty path `""` (targeting the animator root) instead of `"SlashArcVisual"` (targeting the child SpriteRenderer). This happened because the clip was recorded against the wrong object.

**Fixed:** All 5 bindings (m_Color.r/g/b/a, m_Sprite) retargeted from `""` to `"SlashArcVisual"`.

### Fix B — Invisible Alpha Curve
The alpha (`m_Color.a`) curve had been reduced to a single keyframe `t=0, v=0` when the user edited the color, making the effect permanently invisible.

**Fixed:** Alpha curve replaced with a shape matching `SlashSideVFX`, proportionally scaled to `SlashSideAltVFX`'s shorter duration (0.33s vs 0.45s). The RGB color values set by the user were preserved — only the alpha envelope was replaced.

Final alpha envelope (scaled):
- `t=0.000 → v=0` (start invisible)
- `t=0.011 → v=1` (quick fade in)
- `t=0.110 → v=1` (hold)
- `t=0.170 → v=0.65` (begin fade)
- `t=0.333 → v=0` (gone)

### Fix C — Missing Animation Events (Root Cause of No Damage)
`SlashSideAltVFX` had zero animation events. Every other VFX clip (`SlashSideVFX`, `SlashUpVFX`, `SlashDownVFX`) has two events that drive the hit window:

| Event | Time | Purpose |
|---|---|---|
| `BeginAttackWindow` | t = 0.05s | Enables the damage collider |
| `EndAttackWindow` | t = 0.18s | Disables the damage collider |

Without these events, `HeroAttackAction.attackWindowActive` was never set to `true`, so `EvaluateDamageCollider()` never ran and no damage was dealt.

**Fixed:** Both events copied from `SlashSideVFX` at the same timestamps (t=0.05 and t=0.18).

> **Architecture note:** The attack window is driven entirely by animation events on the VFX clip, not the hero body animation. Every new slash VFX clip must include `BeginAttackWindow` and `EndAttackWindow` events or it will deal no damage.

---

## 4. Editor Warning Fixes

### Fix A — MMF_ParticlesInstantiation & MMF_CameraShake Missing [Serializable]

**Files modified:**
- `Assets/Plugins/Feel/MMFeedbacks/MMFeedbacks/Feedbacks/MMF_ParticlesInstantiation.cs`
- `Assets/Plugins/Feel/MMFeedbacks/MMFeedbacks/Feedbacks/MMF_CameraShake.cs`

**Change:** Added `[System.Serializable]` immediately before each class declaration. These are third-party Feel package files — the attribute is the minimum change required and does not alter behaviour.

Warning suppressed:
> *The type MoreMountains.Feedbacks.MMF_X is being serialized by [SerializeReference], but is missing the [Serializable] attribute.*

### Fix B — Particle Orbital Velocity Curves Mixed Mode

**Affected prefabs (all 4 fixed):**
- `Assets/_Project/Prefabs/VFX/Combat/EnemyDeathBurst.prefab`
- `Assets/_Project/Prefabs/VFX/Combat/HitSpark.prefab`
- `Assets/_Project/Prefabs/VFX/Combat/PogoSpark.prefab`
- `Assets/_Project/Prefabs/VFX/Combat/TerrainImpact.prefab`

**Problem:** `VelocityOverLifetime.orbitalX` and `orbitalY` were in `Constant` mode (0) while `orbitalZ` was in `TwoConstants` mode (3). Unity requires all three orbital axes to use the same curve mode.

**Fix:** `orbitalX` and `orbitalY` changed from `Constant` to `TwoConstants`. Since both channels were 0, the random range is `(0, 0)` — behaviour is identical, warning is gone.

Warning suppressed:
> *Particle Orbital Velocity curves must all be in the same mode*

---

## Key Architecture Notes for Future Work

- **New slash VFX clips** must always include `BeginAttackWindow` (at the frame damage should start) and `EndAttackWindow` (at the frame it should end) animation events, or no damage will be dealt.
- **New alt-direction modules** (e.g., an alt Up slash) would follow the same pattern: add a second `HeroAttackModule` with `direction=Up` and `isAlt=true`. The current `FindModule` already supports this.
- **Alt reset time** is tuned via `HeroConfig.altAttackResetTime` (ScriptableObject in inspector) — no code change needed to adjust the combo reset window.
