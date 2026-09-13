# Happy Harvest — Weather / Lighting / Atmosphere Forensic Investigation

**Status: READ-ONLY investigation. No project files were modified, created, deleted, copied, or committed.**
Root: `E:\GameDev\Projects\Happy Harvest`
Purpose: let another agent ("Astra") inspect the exact original Happy Harvest assets without repeating discovery, and judge reuse for a Unity 6 URP 2D side-scrolling / 2.5D project that already owns its own weather/climate/time simulation.

---

## 1. EXECUTIVE SUMMARY

Happy Harvest's environmental presentation is built from **two fully decoupled systems** that never call into each other in code:

1. **`WeatherSystem` / `WeatherSystemElement`** (`Assets/HappyHarvest/Scripts/WeatherSystem.cs`, `WeatherSystemElement.cs`) — a trivial `[Flags]` enum (`Sun=1, Rain=2, Thunder=4`) broadcaster. `WeatherSystem.ChangeWeather()` just calls `GameObject.SetActive(element.WeatherType.HasFlag(current))` on every `WeatherSystemElement` found in the scene. There is **no intensity ramping, no blending, no timers** — it is a hard on/off gate. All actual audio-visual behavior lives on whatever GameObjects carry the tag.
2. **`DayCycleHandler` / `DayEventHandler` / `LightInterpolator` / `ShadowInstance`** — a continuous time-of-day driver. `GameManager.Update()` advances a `CurrentDayRatio` (0..1) every frame and calls `DayCycleHandler.Tick()`, which evaluates 5 authored `Gradient`s onto 5 `Light2D` components (1 Global + 4 Point) and 2 `AnimationCurve`s onto registered fake-shadow transforms. Separately, `DayEventHandler` fires discrete `UnityEvent`s when the ratio enters/exits authored time windows (used to flip street-lamp lights and cross-fade day/night ambience audio).

**Rain** is pure **VFX Graph** (zero legacy `ParticleSystem` anywhere in the rain pipeline) split across 3 `.vfx` assets: a background/world layer (`VFX_2DRain.vfx`), a foreground overlay layer (`VFX_RainForeground.vfx`, Sorting Layer "Foreground", order 90), and a localized drip/splash effect (`VFX_WaterDrop.vfx`) used both loose in the scene and under a street lamp.

**Storm/lightning is not a separate effect** — it is the exact same `VFX_RainForeground.vfx` asset, instantiated a second time with one exposed Boolean overridden: `Lightnings = true`. That instance additionally carries Unity's stock **`VFXOutputEventPlayAudio`** sample component (from the installed VFX Graph "OutputEvent Helpers" sample), which plays a `Thunder.wav` `AudioSource` whenever the VFX graph internally raises its `"Thunder"` output event. Thunder timing/randomness is therefore authored **inside the VFX Graph itself**, not in C#.

**Clear weather** is confirmed (not assumed) to be nothing more than every Rain/Thunder-tagged `WeatherSystemElement` being deactivated, plus one extra detail: two `VFX_WaterDrop.vfx` instances under `Prefab_Streetlamp.prefab` are tagged `WeatherType = Sun` and only turn on in clear weather — an authored "residual drip after the rain stops" touch. No lighting, Volume, or audio changes are coded against the Sun state.

**Day/night and weather never interact in code.** Rain doesn't dim differently at night; lamps and ambience crossfade purely off the day/night ratio, independent of whatever `WeatherSystem` is doing.

Most promising authored assets for later inspection: `VFX_RainForeground.vfx` (storm mechanism), `2DwaterSplashFlipbook.shadergraph` + `RainSplashFlipbook.png` (self-contained, perspective-agnostic splash technique), `DayCycleHandler.cs` (gradient-driven Light2D day/night driver — engine-generic and highly portable), `VFX_DustParticles.vfx` (camera-bound tile-wrap ambient particle technique), and `ShaderGraph_MoveVertices.shadergraph` (vertex-displacement wind sway for vegetation sprites).

---

## 2. PROJECT / RENDERING CONFIGURATION

- **Unity Editor**: `6000.3.10f1` (`ProjectSettings/ProjectVersion.txt`)
- **URP**: `com.unity.render-pipelines.universal` **17.3.0**
- **Shader Graph**: **17.3.0** (URP-bundled)
- **VFX Graph**: `com.unity.visualeffectgraph` **17.3.0**
- **Timeline**: `com.unity.timeline` **1.8.10**
- **2D packages**: `com.unity.2d.animation` 13.0.4, `com.unity.2d.aseprite` 3.0.1, `com.unity.2d.common` 12.0.2, `com.unity.2d.psdimporter` 12.0.1, `com.unity.2d.spriteshape` 13.0.0, `com.unity.2d.tilemap` 1.0.0/`.extras` 6.0.1, `com.unity.2d.pixel-perfect` 5.1.1, `com.unity.feature.2d` 2.0.2
- **Audio**: only stock `com.unity.modules.audio` / `.unitywebrequestaudio` — **no FMOD/Wwise**
- Other: `com.unity.cinemachine` 3.1.2, `com.unity.inputsystem` 1.18.0

**Pipeline assets** (note: live under top-level `Assets/Settings/`, **not** `Assets/HappyHarvest/Settings` — that folder doesn't exist):
- `Assets/Settings/UniversalRP.asset` — GUID `681886c5eb7344803b6206f758bf0b1c` (Pipeline Asset, `m_RendererType: 1` = 2D Renderer)
- `Assets/Settings/Renderer2D.asset` — GUID `424799608f7334c24bf367e4bbfa7f9a` (Renderer2DData). `m_RendererFeatures: []` — **no custom Renderer Features configured**. Default material for un-overridden sprites = built-in **Sprite-Lit-Default** (`guid a97c105638bdf8b4a8650670310a4cd3`).
- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` — GUID `c5b1007f7acde4fe5acc0bedb9181368`
- Confirmed active in `ProjectSettings/GraphicsSettings.asset` (`m_CustomRenderPipeline` matches UniversalRP.asset guid).

**Sorting Layers** (`ProjectSettings/TagManager.asset`, in order): `Bottom` → `Default` → `Objects` → `ObjectsFront` → `Foreground` (uniqueID `1304480043`).

**2D Light Blend Styles** (on Renderer2D.asset, stock/unmodified 4-style set): `Multiply`, `Additive`, `Multiply with Mask`, `Additive with Mask`.

**Volume Profiles** (`Assets/HappyHarvest/Scenes/Volume Profiles/`):
- `Volume_Profile.asset` (GUID `81b2d2e143e313845a66e51cf0baed1f`) — Farm_Outdoor. **Only one override: Bloom** (threshold 1.14, intensity 1.72, scatter 0.729, warm-yellow tint `{0.95,0.83,0.22,1}`).
- `Volume_Profile_Interior.asset` (GUID `17a0b00baba43ae4ab6eb3b1adcf1ccd`) — House_Interior. **Only Bloom** (threshold 0.9, intensity 6.36, white tint).
- No ColorAdjustments/Vignette/WhiteBalance/Shadows-Midtones-Highlights override exists anywhere. Both Volumes are scene-level `m_IsGlobal: 1`, one per scene, `priority 0 / weight 1`.
- **Neither Volume Profile is referenced by any weather/day-night script** — confirmed no code-level link.

**Scenes**: `Farm_Outdoor.unity` (the only scene with weather — confirmed zero `WeatherSystem`/`WeatherSystemElement` GUID occurrences in `House_Interior.unity`), `House_Interior.unity`, `Loader.unity`, `MainMenu.unity`.

**ShadowCaster2D**: confirmed **unused anywhere in the project** (zero occurrences of its script GUID `7db70e0ea77f5ac47a8f4565a9406397`). All "shadows" in the game are faked (see §8/§20 `ShadowInstance`).

---

## 3. ENVIRONMENT ARCHITECTURE

```text
GameManager.Update()  [Assets/HappyHarvest/Scripts/GameManager.cs, DefaultExecutionOrder(-9999)]
    │  advances m_CurrentTimeOfTheDay, computes CurrentDayRatio (0..1)
    │
    ├──> foreach DayEventHandler in m_EventHandlers:
    │        if ratio enters/exits Events[i].[StartTime,EndTime] → fire OnEvents/OffEvent (UnityEvent)
    │        ┌─ SoundManager.prefab's DayEventHandler → AmbienceBlender.BlendToDay()/BlendToNight()
    │        └─ StreetLamp/HouseLamp prefabs' DayEventHandler → toggle Light.enabled / ParticleSystemRenderer.enabled
    │
    └──> DayCycleHandler.Tick() → UpdateLight(ratio)
             ├─ DayLightGradient.Evaluate(ratio)      → DayLight (Light2D, Point)
             ├─ NightLightGradient.Evaluate(ratio)    → NightLight (Light2D, Point)
             ├─ AmbientLightGradient.Evaluate(ratio)  → AmbientLight (Light2D, GLOBAL)
             ├─ SunRimLightGradient.Evaluate(ratio)   → SunRimLight (Light2D, Point)
             ├─ MoonRimLightGradient.Evaluate(ratio)  → MoonRimLight (Light2D, Point)
             ├─ LightsRoot.rotation = Euler(0,0,360*ratio)   [rotates the "sun/moon rig"]
             └─ UpdateShadow(ratio):
                    ├─ ShadowAngle.Evaluate / ShadowLength.Evaluate (AnimationCurve)
                    │      → applied to every registered ShadowInstance (rotate+scale a shadow sprite transform)
                    └─ foreach LightInterpolator: SetRatio(ratio)
                           → blends TargetLight.color/intensity/shapePath between authored LightFrame keyframes
                           (used for the warehouse building's animated window-glow shape)

WeatherSystem.ChangeWeather(newType)  [Assets/HappyHarvest/Scripts/WeatherSystem.cs]
    │  (called from UI dev-buttons in UIHandler.cs, or StartingWeather at Start())
    └──> foreach WeatherSystemElement found via FindObjectsByType:
             element.gameObject.SetActive( element.WeatherType.HasFlag(currentWeather) )
             ┌─ "Visual Effect Rain"                    (type Rain|Thunder) → VFX_2DRain.vfx        [background layer]
             ├─ "Visual Effect Rain Foreground"          (type Rain)         → VFX_RainForeground.vfx [foreground, Lightnings=false]
             ├─ "Visual Effect Rain Foreground Thunder"  (type Thunder)      → VFX_RainForeground.vfx [foreground, Lightnings=true]
             │        └─ VFX internal "Thunder" OutputEvent → VFXOutputEventPlayAudio → AudioSource "ThunderSound" (Thunder.wav)
             ├─ "VFX_WaterDrop" x2                        (type Rain|Thunder) → VFX_WaterDrop.vfx (loose world drops)
             ├─ "RainSound"                               (type Rain|Thunder) → AudioSource (Rain.wav, looping)
             └─ StreetLamp's VFX_WaterDropLeft/Right      (type Sun)          → VFX_WaterDrop.vfx (post-rain residual drip)
```

Weather and day/night are architecturally independent: neither system's script references the other.

---

## 4. MASTER ENVIRONMENT ASSET INDEX

| System | Asset | Exact Path | GUID | Type | Purpose | Depends On | Referenced By | Reuse |
|---|---|---|---|---|---|---|---|---|
| Weather core | WeatherSystem.cs | `Assets/HappyHarvest/Scripts/WeatherSystem.cs` | `99728f0e23208b44d90d36e0f9cff02f` | C# script | Flag-based weather state broadcaster | GameManager, UIHandler | WeatherSystemElement (via static call), Farm_Outdoor.unity | C |
| Weather core | WeatherSystemElement.cs | `Assets/HappyHarvest/Scripts/WeatherSystemElement.cs` | `70d84b2a31b315e44bf3caa61b8dd45f` | C# script | Per-object weather tag/gate | WeatherSystem.WeatherType enum | 6 GameObjects in Farm_Outdoor.unity, 2 in Prefab_Streetlamp | C |
| Rain VFX | VFX_2DRain.vfx | `Assets/HappyHarvest/VFX/Rain/VFX_2DRain.vfx` | `e5970f8d750064f2bbab714ac07feb6c` | VFX Graph | Background/world rain streaks + splash (GPU event) | SpriteLitAlphaShadergraph, 2DwaterSplashFlipbook.shadergraph, RainSplashFlipbook.png | "Visual Effect Rain" GO, Farm_Outdoor.unity | B |
| Rain VFX | VFX_RainForeground.vfx | `Assets/HappyHarvest/VFX/Rain/VFX_RainForeground.vfx` | `f8cc1e97555be4bb7853de24f314366b` | VFX Graph | Foreground rain overlay + lightning-flash + Thunder output event | SpriteLitAlphaShadergraph, BlankSquare.png | "Visual Effect Rain Foreground"(+"...Thunder") GOs | B (near-A) |
| Rain VFX | VFX_WaterDrop.vfx | `Assets/HappyHarvest/VFX/Rain/VFX_WaterDrop.vfx` | `46383f17472111942a4f8d0a994bbdd9` | VFX Graph | Localized drop + impact splash (GPU event, built-in flipbook UV) | RainSplashFlipbook.png | 2 loose GOs in Farm_Outdoor.unity, 2 in Prefab_Streetlamp | B |
| Rain shading | 2DwaterSplashFlipbook.shadergraph | `Assets/HappyHarvest/VFX/Rain/2DwaterSplashFlipbook.shadergraph` | `3cd2c8fd0c91c4f73a53d5fcb0dd7dc8` | Shader Graph (Sprite Lit) | 3×2 flipbook splash shader | RainSplashFlipbook.png | VFX_2DRain.vfx | A |
| Rain texture | RainSplashFlipbook.png | `Assets/HappyHarvest/VFX/Rain/RainSplashFlipbook.png` | `53adf5ad71d994e56ba749a1070a386d` | Texture2D | 3×2 splash flipbook sheet | — | 2DwaterSplashFlipbook.shadergraph, VFX_WaterDrop.vfx (built-in flipbook) | A |
| Rain texture | BlankSquare.png | `Assets/HappyHarvest/VFX/Rain/BlankSquare.png` | `d3c6cc9c36390462099e14cb5c6b9c23` | Sprite/Texture2D | Foreground streak quad texture | — | VFX_RainForeground.vfx | A |
| Rain — shared shader | SpriteLitAlphaShadergraph.shadergraph | `Assets/HappyHarvest/VFX/Common/SpriteLitAlphaShadergraph.shadergraph` | `823c0ef43fca54ea4be96177f6eedb6b` | Shader Graph Sub Target (Sprite Lit) | Generic lit-alpha VFX output shader | — | VFX_2DRain.vfx, VFX_RainForeground.vfx | A |
| Storm/Thunder | VFXOutputEventPlayAudio.cs | `Assets/HappyHarvest/Samples/Visual Effect Graph/17.0.3/OutputEvent Helpers/Runtime/VFXOutputEventPlayAudio.cs` | `4a4ed0a47000743b29d4c880beee34a0` | C# script (Unity VFX sample) | Plays an AudioSource when VFX raises a named output event | VFX Graph "OutputEvent" mechanism | "Visual Effect Rain Foreground Thunder" GO | A |
| Storm/Thunder audio | Thunder.wav | `Assets/HappyHarvest/Audio/Ambience/Thunder.wav` | `eb1d9da85240c4c64a623a7fb437516a` | AudioClip | One-shot thunder clap | — | "ThunderSound" AudioSource | A |
| Rain audio | Rain.wav | `Assets/HappyHarvest/Audio/Ambience/Rain.wav` | `70d55056a43cc4e899b0fc211fecab23` | AudioClip | Looping rain ambience | — | "RainSound" AudioSource | A |
| Day/Night core | DayCycleHandler.cs | `Assets/HappyHarvest/Scripts/DayCycleHandler.cs` | `bc8fb613d11589342b357dd6a3787bac` | C# script | Gradient/curve-driven Light2D + shadow day-night driver | Light2D (URP), Gradient/AnimationCurve authored data | DayCycleHandler.prefab, DayCycleHandler_House.prefab | A |
| Day/Night data | DayCycleHandler.prefab | `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler.prefab` | `ae2488684f645a54e8793357ece28d8f` | Prefab | Outdoor day/night driver instance + authored gradients/curves | DayCycleHandler.cs | Farm_Outdoor.unity | B |
| Day/Night data | DayCycleHandler_House.prefab | `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler_House.prefab` | `4967c0e1f750e4d068c110860c915ed4` | Prefab | Interior day/night driver (self-contained lights) | DayCycleHandler.cs | House_Interior.unity | B |
| Day/Night events | DayEventHandler.cs | `Assets/HappyHarvest/Scripts/DayEventHandler.cs` | `cd41505808a6ef44eab48fd2d704589b` | C# script | Discrete time-window UnityEvent trigger | GameManager (registration) | SoundManager.prefab, Prefab_Streetlamp, Prefab_Sprite_HouseLamp, Farm_Outdoor.unity, House_Interior.unity | A |
| Day/Night shape-blend | LightInterpolator.cs | `Assets/HappyHarvest/Scripts/Effects/LightInterpolator.cs` | `aa1d2dc24a28c8d4092dedf85da3bd0f` | C# script | Blends Light2D color/intensity/**shapePath** between keyframes | Light2D, DayCycleHandler (registration) | Light 2D_Warehouse.prefab | A |
| Day/Night fake shadow | ShadowInstance.cs | `Assets/HappyHarvest/Scripts/ShadowInstance.cs` | `2a36ebd5a10583041ae36dd8294c0935` | C# script | Registers a transform to be rotated/scaled by DayCycleHandler's shadow curves | DayCycleHandler | scene shadow-sprite objects (not exhaustively enumerated) | C |
| Audio crossfade | AmbienceBlender.cs | `Assets/HappyHarvest/Scripts/Audio/AmbienceBlender.cs` | `07b0771e8886c2340855eec75a066f04` | C# script | Day/Night ambience volume crossfade | 2 AudioSources | SoundManager.prefab | A |
| Audio mixer | MainMixer.mixer | `Assets/HappyHarvest/Common/Audio/MainMixer.mixer` | `3b8e3f485549b1e479a56438f6a2e5c7` | AudioMixer | Master→SFX/BGM routing, volume params | — | SoundManager.cs, SoundManager.prefab | A |
| Audio manager | SoundManager.prefab | `Assets/HappyHarvest/Prefabs/Managers/SoundManager.prefab` | `a7d6799acefeff1448bb67520e687542` | Prefab | Central audio manager (SFX pool, ambience, day/night events) | SoundManager.cs, AmbienceBlender.cs, DayEventHandler.cs, MainMixer.mixer | Farm_Outdoor.unity | A |
| Wind/Vegetation | ShaderGraph_MoveVertices.shadergraph | `Assets/HappyHarvest/ShaderGraphs/ShaderGraph_MoveVertices.shadergraph` | `de89a77b947988849a9d2d1ed2a707c3` | Shader Graph (Sprite Lit) | Vertex-displacement wind sway | SubGraph_WaveSubGraph, SubGraph_Transform | Material_Plants.mat, Material_Crops*.mat | A/B |
| Wind subgraph | SubGraph_WaveSubGraph.shadersubgraph | `Assets/HappyHarvest/ShaderGraphs/Subgraphs/SubGraph_WaveSubGraph.shadersubgraph` | `4b332ed3d43fcbd4698126328b8a3de8` | Shader Graph Sub Graph | Wave-motion math utility | — | ShaderGraph_MoveVertices | A |
| Wind subgraph | SubGraph_Transform.shadersubgraph | `Assets/HappyHarvest/ShaderGraphs/Subgraphs/SubGraph_Transform.shadersubgraph` | `192f8a092dba89846b5e7e2e8a17c85f` | Shader Graph Sub Graph | Translate/Rotate/Scale/Pivot utility | — | ShaderGraph_MoveVertices | A |
| Atmospherics | VFX_DustParticles.vfx | `Assets/HappyHarvest/VFX/Ambient Dust/VFX_DustParticles.vfx` | `231f13b595b0e4fc3959ce91d53d6c1f` | VFX Graph | Camera-bound tile-wrap ambient dust | SpriteLitAdditiveShadergraph | (scene usage not exhaustively traced) | A |
| Atmospherics | VFX_Leaves.vfx | `Assets/HappyHarvest/VFX/Leaves/VFX_Leaves.vfx` | `1bbdedb9f68441d428e8620dade2bb78` | VFX Graph | Wind-driven falling leaves | Leaf.png | (scene usage not exhaustively traced) | A |
| Atmospherics | VFX_Fire.vfx | `Assets/HappyHarvest/VFX/Fire/VFX_Fire.vfx` | `9090aae97109e7b4e8883a93558c8242` | VFX Graph | Fully-baked fire (no exposed params) | ShaderGraph_Fire.shadergraph | VFX_Fire.prefab | C |
| Atmospherics | Smoke.prefab | `Assets/HappyHarvest/VFX/Smoke/Smoke.prefab` | `befd51526a6ad234aa90ff7ce18117be` | Prefab | LineRenderer-based chimney smoke (not VFX/ParticleSystem) | Mat_Smoke.mat, ShaderGraph_Smoke.shadergraph | (chimney props, not exhaustively traced) | B |
| Water (non-rain) | VFX_WaterLines.vfx / VFX_WaterLinesStorm.vfx | `Assets/HappyHarvest/VFX/Water/` | `908b3a6e053fd154db4c41bed608b3f2` / `78591f2702ad70e4e9bacf5d9b3a79fb` | VFX Graph | Wind-driven river/cliff ripple lines; "Storm" variant exists | ShaderGraph_CliffWater.shadergraph | Farm_Outdoor.unity (3 "Storm" instances found, NOT WeatherSystemElement-gated) | B |
| Lighting | Light 2D_Warehouse.prefab | `Assets/HappyHarvest/Prefabs/Light 2D_Warehouse.prefab` | `ca7a44a91a0e4904fbe3db41a629db88` | Prefab | Animated-shape building glow (uses LightInterpolator) | LightInterpolator.cs, Light2D | Farm_Outdoor.unity | B |
| Post-processing | Volume_Profile.asset | `Assets/HappyHarvest/Scenes/Volume Profiles/Volume_Profile.asset` | `81b2d2e143e313845a66e51cf0baed1f` | Volume Profile | Outdoor Bloom-only post-fx | — | Farm_Outdoor.unity "Volume" GO | B |
| Post-processing | Volume_Profile_Interior.asset | `Assets/HappyHarvest/Scenes/Volume Profiles/Volume_Profile_Interior.asset` | `17a0b00baba43ae4ab6eb3b1adcf1ccd` | Volume Profile | Interior Bloom-only post-fx | — | House_Interior.unity "Volume" GO | B |

(Full per-system manifests with every file are in §5–§13.)

---

## 5. RAIN — COMPLETE DEPENDENCY MAP

**How it works**: rain is entirely **VFX Graph** — confirmed zero `ParticleSystem` components anywhere in the rain pipeline (5 project-wide `ParticleSystem` hits are all unrelated: StepDust, Character, Moth, Bush, House_Interior). Three independent `.vfx` assets cover three roles:

- **`VFX_2DRain.vfx`** (world/background layer, Sorting Layer `Default`, order 0) — Spawner→Initialize→Update→`VFXURPLitPlanarPrimitiveOutput` for streaks (shader = `SpriteLitAlphaShadergraph.shadergraph`, `_MainTex` left at the VFX package's built-in default particle texture, i.e. **no custom rain-streak texture is authored** — streaks are tinted default quads), plus a `VFXBasicGPUEvent`-triggered second particle system → `VFXComposedParticleOutput` using `2DwaterSplashFlipbook.shadergraph` for ground splashes. Exposes `RainSpeed`, `RainDirection` (Vector2), `RainRate`, `RainColor`.
- **`VFX_RainForeground.vfx`** (foreground overlay, Sorting Layer `Foreground`, order 90) — streak output uses a plain `VFXPlanarPrimitiveOutput` (no shadergraph) textured with `BlankSquare.png`; a second internal chain feeds a `VFXOutputEvent` node (`eventName: Thunder`) for the lightning/thunder trigger (see §6); a third chain outputs more streaks via `SpriteLitAlphaShadergraph`. Exposes `RainSpeed`, `RainDirection`, `RainTintColor`, `RainRate`, and the storm-toggle **`Lightnings`** bool. Does **not** reference the splash flipbook shader/texture at all — this graph has no ground splashes.
- **`VFX_WaterDrop.vfx`** (localized drip/splash, used loose and under lamps) — Initialize→Update→`VFXPlanarPrimitiveOutput` for the falling drop, GPU-event-bridged to a second `VFXPlanarPrimitiveOutput` in **flipbook UV mode** (`flipBookSize = {3,2}`) sampling `RainSplashFlipbook.png` directly (bypassing the custom shadergraph — uses VFX Graph's built-in flipbook feature instead). Exposes `RainTintColor`, `Lifetime`, `Impact`, `Impact Size Range`, `Wind Directio` (sic, authored typo), `Wind Speed`, `ImpactColor`.

**No standalone `.mat` material exists for rain** — VFX Graph auto-generates the material from the shadergraph reference embedded in each output context; confirmed via guid grep (0 matches in any `.mat` file).

**Gating**: none of the 3 VFX assets or the splash shader are referenced by name/GUID from any C# script. The only script-level connection is the generic `WeatherSystemElement.WeatherType` flag + `WeatherSystem.SwitchAllElementsToCurrentWeather()`'s `SetActive` call.

**Scene instances** (`Assets/HappyHarvest/Scenes/Farm_Outdoor.unity`, all root-level, no camera-follow/parenting of any kind):

| GameObject | VFX asset | WeatherType | Sorting Layer / Order |
|---|---|---|---|
| `Visual Effect Rain` | VFX_2DRain (`e5970f8d...`) | 6 (Rain\|Thunder) | Default / 0 |
| `Visual Effect Rain Foreground` | VFX_RainForeground (`f8cc1e97...`), `Lightnings` not overridden (false) | 2 (Rain) | Foreground / 90 |
| `Visual Effect Rain Foreground Thunder` | VFX_RainForeground (same asset), `Lightnings = true` override | 4 (Thunder) | Foreground / 90 |
| `VFX_WaterDrop`, `VFX_WaterDrop (1)` | VFX_WaterDrop (`46383f17...`) | 6 (Rain\|Thunder) | — (fixed world positions x=-4.33/5.32, y=4.613) |
| `RainSound` | (AudioSource, Rain.wav) | 6 (Rain\|Thunder) | — |

Additionally, **`Assets/HappyHarvest/Art/Environment/Lamps/StreetLamp/Prefab_Streetlamp.prefab`** contains `VFX_WaterDropRight`/`VFX_WaterDropLeft` (both using `VFX_WaterDrop.vfx`) tagged `WeatherType = 1 (Sun)` — a "residual drip after rain stops" detail, active only in clear weather, not during rain itself.

**Known stale data (flag, not a bug to fix)**: the scene's `m_PropertySheet` overrides on the rain VFX instances include `RainDirectionV3`, `CollisionPlaneNoSplashes_position/normal` (on all 3) and `RainTintColor` (on the plain `VFX_2DRain` instance) — **none of these names are currently exposed** in any of the 3 `.vfx` graphs. These are orphaned leftovers from an earlier graph iteration; Unity silently ignores unmatched PropertySheet entries.

### Rain — File Manifest

**Core**
- `Assets/HappyHarvest/VFX/Rain/VFX_2DRain.vfx` (`e5970f8d750064f2bbab714ac07feb6c`)
- `Assets/HappyHarvest/VFX/Rain/VFX_RainForeground.vfx` (`f8cc1e97555be4bb7853de24f314366b`)
- `Assets/HappyHarvest/VFX/Rain/VFX_WaterDrop.vfx` (`46383f17472111942a4f8d0a994bbdd9`)

**Shaders / Shader Graphs**
- `Assets/HappyHarvest/VFX/Rain/2DwaterSplashFlipbook.shadergraph` (`3cd2c8fd0c91c4f73a53d5fcb0dd7dc8`) — Sprite Lit target; exposes `flipbookTex` (default = RainSplashFlipbook.png) and `FrameIndex`; internal `FlipbookNode` Width=3 Height=2.
- `Assets/HappyHarvest/VFX/Common/SpriteLitAlphaShadergraph.shadergraph` (`823c0ef43fca54ea4be96177f6eedb6b`)

**Textures / Flipbooks**
- `Assets/HappyHarvest/VFX/Rain/RainSplashFlipbook.png` (`53adf5ad71d994e56ba749a1070a386d`)
- `Assets/HappyHarvest/VFX/Rain/BlankSquare.png` (`d3c6cc9c36390462099e14cb5c6b9c23`)

**Prefabs**
- `Assets/HappyHarvest/Art/Environment/Lamps/StreetLamp/Prefab_Streetlamp.prefab` (drip-under-lamp instance of VFX_WaterDrop, WeatherType=Sun)

**Scripts**
- `Assets/HappyHarvest/Scripts/WeatherSystem.cs` (`99728f0e23208b44d90d36e0f9cff02f`)
- `Assets/HappyHarvest/Scripts/WeatherSystemElement.cs` (`70d84b2a31b315e44bf3caa61b8dd45f`)

**Audio**
- `Assets/HappyHarvest/Audio/Ambience/Rain.wav` (`70d55056a43cc4e899b0fc211fecab23`)

**Supporting Assets**
- `Assets/HappyHarvest/Scenes/Farm_Outdoor.unity` (only scene with rain instances)

---

## 6. STORM — COMPLETE DEPENDENCY MAP

Storm is **not** a distinct effect or asset — it is a **configuration state** of the same `VFX_RainForeground.vfx` graph:

- The "calm rain" instance (`WeatherType=2`) and the "storm" instance (`WeatherType=4`, GameObject `Visual Effect Rain Foreground Thunder`) use the **identical VFX asset** (`f8cc1e97555be4bb7853de24f314366b`) and, per the actual scene data, **identical `RainSpeed`(6) / `RainRate`(1000)** overrides — no rain-density/speed increase is authored between the two states. The only differentiators are:
  1. The exposed **`Lightnings`** bool: unset (false-by-graph-default) on the Rain instance, explicitly overridden to `1` (true) on the Thunder instance.
  2. The Thunder instance alone carries an extra MonoBehaviour: **`VFXOutputEventPlayAudio`** (`4a4ed0a47000743b29d4c880beee34a0`, from `Assets/HappyHarvest/Samples/Visual Effect Graph/17.0.3/OutputEvent Helpers/Runtime/VFXOutputEventPlayAudio.cs`, part of Unity's installed VFX Graph sample content), configured with `outputEvent.m_Name = "Thunder"` and `audioSource` pointing at a separate root-level `ThunderSound` GameObject's `AudioSource` (clip = `Thunder.wav`, `PlayOnAwake=0`).
  3. Internally, `VFX_RainForeground.vfx` contains its own `VFXOutputEvent` context (`eventName: Thunder`) fed by a spawner chain — meaning **lightning-flash timing/randomness is authored inside the VFX Graph itself**, not scripted in C#. The exact node-level logic that gates this chain behind the `Lightnings` bool was not fully unpacked from the raw YAML (context/spawner structure was traced; individual block/operator logic inside those contexts needs the VFX Graph editor view to inspect further — see §18 Unknowns).

No separate lightning sprite/light-flash/camera-shake/post-processing flash was found anywhere in scripts or scenes — no `Volume`/`Light2D` intensity spike tied to "Thunder" was located in code. If a visual flash exists, it is produced entirely inside the VFX Graph's own particle color/alpha animation (unconfirmed at node level).

No storm-specific fog/wind/cloud/background/color-grading changes exist — the Volume Profile is unchanged between weather states (confirmed: no script references either Volume Profile).

Separately, `Assets/HappyHarvest/VFX/Water/VFX_WaterLinesStorm.vfx` (a "storm" variant of the river/cliff ripple-line VFX) exists and has 3 instances placed in `Farm_Outdoor.unity`, but **carries no `WeatherSystemElement` component and has no dependency edge to the rain VFX or `WeatherSystem`** — it appears to be either a manually-placed always-on decoration or an unwired leftover. Flagged as **uncertain**, not confirmed weather-reactive.

### Storm — File Manifest

**Core**
- `Assets/HappyHarvest/VFX/Rain/VFX_RainForeground.vfx` (`f8cc1e97555be4bb7853de24f314366b`) — same asset as calm rain, `Lightnings=true` override differentiates the storm instance.

**Scripts**
- `Assets/HappyHarvest/Samples/Visual Effect Graph/17.0.3/OutputEvent Helpers/Runtime/VFXOutputEventPlayAudio.cs` (`4a4ed0a47000743b29d4c880beee34a0`)
- `Assets/HappyHarvest/Scripts/WeatherSystem.cs` / `WeatherSystemElement.cs` (same as Rain)

**Audio**
- `Assets/HappyHarvest/Audio/Ambience/Thunder.wav` (`eb1d9da85240c4c64a623a7fb437516a`)

**Supporting Assets (uncertain link, listed for completeness)**
- `Assets/HappyHarvest/VFX/Water/VFX_WaterLinesStorm.vfx` (`78591f2702ad70e4e9bacf5d9b3a79fb`)

---

## 7. CLEAR WEATHER — COMPLETE DEPENDENCY MAP

Confirmed by trace (not assumed): "clear weather" = `WeatherType.Sun` (value `1`). Because `WeatherSystemElement.WeatherType.HasFlag(current)` requires the element's own flags to be a superset of the current state, setting weather to `Sun` (bit `1`) deactivates every Rain/Thunder-tagged element (none of which include bit `1`) and activates only elements explicitly tagged `Sun`. The only such elements found are the two `VFX_WaterDrop.vfx` instances (`VFX_WaterDropLeft`/`Right`) under `Prefab_Streetlamp.prefab` — a deliberate "water still dripping off the lamp after the rain stopped" touch.

No lighting, Volume Profile, ambient-audio, or shader/material change is coded against the Sun state — day/night lighting continues to run independently via `DayCycleHandler` regardless of weather.

### Clear Weather — File Manifest
- `Assets/HappyHarvest/VFX/Rain/VFX_WaterDrop.vfx` (`46383f17472111942a4f8d0a994bbdd9`) — the only asset gated specifically by Sun weather.
- `Assets/HappyHarvest/Art/Environment/Lamps/StreetLamp/Prefab_Streetlamp.prefab` — hosts the Sun-tagged instances.
- `Assets/HappyHarvest/Scripts/WeatherSystem.cs` / `WeatherSystemElement.cs`

---

## 8. DAY/NIGHT — COMPLETE DEPENDENCY MAP

**Time source**: `GameManager.cs` (`Assets/HappyHarvest/Scripts/GameManager.cs`, GUID `f7aaabf1ce21bf941ac2b6e07aeeaa6d`, `[DefaultExecutionOrder(-9999)]`, singleton `Instance`). Fields: `[Min(1.0f)] public float DayDurationInSeconds` (authored `120` on the outdoor prefab — a 2-minute demo day), `public float StartingTime` (authored `30`). Property `public float CurrentDayRatio => m_CurrentTimeOfTheDay / DayDurationInSeconds`. `Update()` advances/wraps `m_CurrentTimeOfTheDay`, iterates registered `DayEventHandler`s firing range-transition `UnityEvent`s, then calls `DayCycleHandler.Tick()` if present.

**Continuous presentation controller**: `DayCycleHandler.cs` (`Assets/HappyHarvest/Scripts/DayCycleHandler.cs`, GUID `bc8fb613d11589342b357dd6a3787bac`, `[DefaultExecutionOrder(10)]`). All authored curve/gradient data lives on the prefab instance, not a ScriptableObject:
- 5 `Gradient` fields (`DayLightGradient` 7 color/2 alpha keys, `NightLightGradient` 4/2, `AmbientLightGradient` 4/2, `SunRimLightGradient` 7/2, `MoonRimLightGradient` 4/2) — each evaluated by `ratio` and assigned to a `Light2D.color`.
- 2 `AnimationCurve` fields (`ShadowAngle` 5 keys, `ShadowLength` 7 keys) driving fake-shadow rotation/scale.
- `public void UpdateLight(float ratio)` sets the 5 Light2D colors, rotates `LightsRoot` (`Quaternion.Euler(0,0,360*ratio)`), then calls `UpdateShadow(ratio)`.
- `UpdateShadow` applies the curves to every registered `ShadowInstance` and calls `SetRatio(ratio)` on every registered `LightInterpolator`.
- `Save`/`Load` against a `DayCycleHandlerSaveData{ float TimeOfTheDay }` struct exist but are **stubbed no-ops** (commented out) — time persistence is not implemented.
- Ships a `[CustomEditor]` with a live preview time-slider (`DayCycleEditor`) for authoring convenience.

**Prefab instances**:
- `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler.prefab` (GUID `ae2488684f645a54e8793357ece28d8f`) — outdoor; **binds to scene Light2Ds by object reference** rather than owning its own lights:

  | Field | Bound Light2D GameObject | `m_LightType` |
  |---|---|---|
  | `DayLight` | `DayLight` | 3 (Point) |
  | `NightLight` | `NightLight` | 3 (Point) |
  | `SunRimLight` | `DayLightRim` | 3 (Point) |
  | `MoonRimLight` | `NightLightRim` | 3 (Point) |
  | `AmbientLight` | **`Ambient Light`** | **4 (Global)** ← the scene's one true Global Light |
  | `LightsRoot` | `LightsRotator` (Transform) | — |

  Also carries an orphaned serialized field `SunHeight: 0.8` with **no matching field in the current `DayCycleHandler.cs`** — harmless leftover from a removed feature.

- `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler_House.prefab` (GUID `4967c0e1f750e4d068c110860c915ed4`) — interior; **self-contained**, owns its own child `DayLight`/`NightLight` Light2D GameObjects rather than referencing scene lights. The interior scene's separate `Light Global` GameObject (static gray, intensity 0.2) is independent, fixed ambient — **not** driven by this handler.

**Discrete event layer**: `DayEventHandler.cs` (GUID `cd41505808a6ef44eab48fd2d704589b`) — `DayEvent[] Events`, each `{StartTime, EndTime, UnityEvent OnEvents, UnityEvent OffEvent}`; `IsInRange` checked every frame by `GameManager.Update()`. Confirmed live wirings:
- `SoundManager.prefab` — day window `0.294→0.751` → `AmbienceBlender.BlendToDay()` / `BlendToNight()`.
- `Prefab_Streetlamp.prefab` / `Prefab_Sprite_HouseLamp.prefab` — day window `0.2496→0.7927` → `Behaviour.set_enabled(false)` on the lamp light **and** `Renderer.set_enabled(false)` on a `ParticleSystemRenderer` (a glow/particle effect around the lamp) during the day, re-enabled outside that window (i.e., **lamps + their particle glow turn on at night automatically**).
- A standalone `DayEventHandler` in `Farm_Outdoor.unity` (window `0.041→0.715`) has **empty** persistent-call targets — authored but unwired, dead in that scene.

**Shape-blended lighting**: `LightInterpolator.cs` (GUID `aa1d2dc24a28c8d4092dedf85da3bd0f`, `[DefaultExecutionOrder(999)] [ExecuteInEditMode]`). `LightFrame[] { Light2D ReferenceLight; float NormalizedTime; }` — `SetRatio(t)` finds the bracketing pair and Lerps `TargetLight.color`, `.intensity`, **and its freeform `shapePath`** (`Vector3.Lerp` per vertex, then `SetShapePath`). Registers with `DayCycleHandler` in `OnEnable`. Concrete instance: **`Assets/HappyHarvest/Prefabs/Light 2D_Warehouse.prefab`** (GUID `ca7a44a91a0e4904fbe3db41a629db88`) — 5 `LightFrame`s (`NormalizedTime` 0.18/0.3/0.5/0.76/0.86) blending between 4 inactive "reference" freeform-light shape templates (child GameObjects `Frame1..4`, alpha ≈0.19, purple/violet/blue-violet hues) onto one visible `TargetLight` — this is how a building's window-glow silhouette/shape appears to shift across the day without swapping sprites.

**Fake shadows**: `ShadowInstance.cs` (GUID `2a36ebd5a10583041ae36dd8294c0935`, `[ExecuteInEditMode]`) — `[Range(0,10)] public float BaseLength`; registers with `DayCycleHandler` in `OnEnable`/`OnDisable`. `DayCycleHandler.UpdateShadow` rotates the instance's transform to `currentShadowAngle*360` degrees and scales Y to `BaseLength * currentShadowLength` — a purely transform-based "blob shadow" faked from curves, **not** using URP's real `ShadowCaster2D` system (which is unused project-wide).

**Weather interaction**: none. Confirmed no code path from `WeatherSystem`/`WeatherSystemElement` into `DayCycleHandler`/`LightInterpolator`/`ShadowInstance`, or vice-versa.

### Day/Night — File Manifest

**Core**
- `Assets/HappyHarvest/Scripts/GameManager.cs` (`f7aaabf1ce21bf941ac2b6e07aeeaa6d`)
- `Assets/HappyHarvest/Scripts/DayCycleHandler.cs` (`bc8fb613d11589342b357dd6a3787bac`)
- `Assets/HappyHarvest/Scripts/DayEventHandler.cs` (`cd41505808a6ef44eab48fd2d704589b`)
- `Assets/HappyHarvest/Scripts/ShadowInstance.cs` (`2a36ebd5a10583041ae36dd8294c0935`)
- `Assets/HappyHarvest/Scripts/Effects/LightInterpolator.cs` (`aa1d2dc24a28c8d4092dedf85da3bd0f`)

**Prefabs**
- `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler.prefab` (`ae2488684f645a54e8793357ece28d8f`)
- `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler_House.prefab` (`4967c0e1f750e4d068c110860c915ed4`)
- `Assets/HappyHarvest/Prefabs/Light 2D_Warehouse.prefab` (`ca7a44a91a0e4904fbe3db41a629db88`)
- `Assets/HappyHarvest/Art/Environment/Lamps/StreetLamp/Prefab_Streetlamp.prefab`
- `Assets/HappyHarvest/Art/Environment/Lamps/HouseLamp/Prefab_Sprite_HouseLamp.prefab`

**Audio (day/night ambience, not weather)**
- `Assets/HappyHarvest/Scripts/Audio/AmbienceBlender.cs` (`07b0771e8886c2340855eec75a066f04`)
- `Assets/HappyHarvest/Audio/Ambience/Background ambience outside - Day.wav` (`5d1a638179550f74fae0bc9c5d8a7144`)
- `Assets/HappyHarvest/Audio/Ambience/Background ambience outside - Night.wav` (`22618a90cc6fe91438786bb84140bd75`)

**Supporting Assets**
- `Assets/HappyHarvest/Scenes/Farm_Outdoor.unity`, `Assets/HappyHarvest/Scenes/House_Interior.unity`

---

## 9. ENVIRONMENTAL LIGHTING — FILE MANIFEST

- Light2D script (URP package): GUID `073797afb82c5a1438f328866b10b3f0`.
- **Farm_Outdoor.unity**: 14 Light2D components. One `Global` (`Ambient Light`, driven by `DayCycleHandler`), four `Point` (`DayLight`, `NightLight`, `DayLightRim`, `NightLightRim`, all driven by `DayCycleHandler`), remainder are `Freeform`/`Sprite` decorative fixtures.
- **House_Interior.unity**: 8 Light2D components. One `Global` (`Light Global`, static, **not** driven by any handler), remainder `Point` warm lamp fixtures; one uses `m_LightVolumeEnabled: 1` (volumetric light).
- **Prefabs with Light2D**: `Assets/HappyHarvest/Prefabs/Light 2D_Warehouse.prefab` (`ca7a44a91a0e4904fbe3db41a629db88`, 5 Freeform lights, animated via `LightInterpolator`); `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler_House.prefab` (`4967c0e1f750e4d068c110860c915ed4`, owns its own Day/Night Light2Ds).
- **ShadowCaster2D**: confirmed unused anywhere (script GUID `7db70e0ea77f5ac47a8f4565a9406397`, zero hits).
- **Lit shader graphs/materials relevant to environment**:
  - `Assets/HappyHarvest/ShaderGraphs/ShaderGraph_Water.shadergraph` (`efe6a526ef600ce41bce4b82fb291fa5`, Sprite Lit) → `Assets/HappyHarvest/Materials/Material_Water.mat`
  - `Assets/HappyHarvest/ShaderGraphs/ShaderGraph_MoveVertices.shadergraph` (`de89a77b947988849a9d2d1ed2a707c3`, Sprite Lit) → `Material_Plants.mat`, `Material_Crops*.mat`
  - Project-wide default fallback material for any unassigned SpriteRenderer = built-in **Sprite-Lit-Default** (`a97c105638bdf8b4a8650670310a4cd3`) — i.e. essentially all sprites are 2D-lit by default.
- **2D Light Blend Styles** (on `Assets/Settings/Renderer2D.asset`): `Multiply`, `Additive`, `Multiply with Mask`, `Additive with Mask` (stock/unmodified).

---

## 10. WIND / VEGETATION — FILE MANIFEST

- **`Assets/HappyHarvest/ShaderGraphs/ShaderGraph_MoveVertices.shadergraph`** (`de89a77b947988849a9d2d1ed2a707c3`) — the vegetation-sway vertex-displacement utility. Exposed reference properties: `_Wind_Scale`, `_Wind_Speed`, `_WInd_Direction` (authored typo, verified genuine — not a transcription error), `_Mask`, `_Texture2D`, `_NormalMap`, `_height`, plus a 3-layer `_Translate{A,B,C}`/`_Rotation{A,B,C}`/`_Scale{A,B,C}`/`_Pivot{A,B,C}` wave stack. Uses `SubGraph_WaveSubGraph.shadersubgraph` (`4b332ed3d43fcbd4698126328b8a3de8`) and `SubGraph_Transform.shadersubgraph` (`192f8a092dba89846b5e7e2e8a17c85f`).
- Materials using it: `Assets/HappyHarvest/Materials/Material_Plants.mat` (sample values: `_Wind_Scale 0.17`, `_Wind_Speed 3.97`, `_height 0.53`), `Assets/HappyHarvest/Art/Crops/_Common/Materials/Material_Crops.mat`, `Material_Crops 1.mat`, `Material_Crops 2.mat`.
- **Confirmed absent**: no global/runtime wind controller. `Shader.SetGlobalFloat`/`SetGlobalVector` returned **zero matches** project-wide. Wind sway is **static per-material authored values only** — there is no dynamic/weather-reactive wind system in this project at all (i.e., wind does not visibly intensify during storms anywhere in code).
- **VFX-level wind** (separate mechanism from the shader): `Assets/HappyHarvest/VFX/Leaves/VFX_Leaves.vfx` (`1bbdedb9f68441d428e8620dade2bb78`, exposes `Wind Direction`/`Wind Speed`); `Assets/HappyHarvest/VFX/Water/VFX_WaterLines.vfx` / `VFX_WaterLinesStorm.vfx` (both expose `WindDirection`/`WindSpeed` for river-surface ripple lines).

---

## 11. ATMOSPHERICS — FILE MANIFEST

- **Ambient Dust**: `Assets/HappyHarvest/VFX/Ambient Dust/VFX_DustParticles.vfx` (`231f13b595b0e4fc3959ce91d53d6c1f`) — exposes `EffectSizeMultiplier`, `SpawnRate`, `Alpha`, `ColorGradient`, **`ActivateTurbulence`** (bool). Uses a camera-bound "TileWrap" position node (particles wrap within camera bounds) — a generically reusable screen-space technique. Shader: `Assets/HappyHarvest/VFX/Common/SpriteLitAdditiveShadergraph.shadergraph` (`ced56ab612cc5469483c318bf3357d0c`).
- **Leaves**: `Assets/HappyHarvest/VFX/Leaves/VFX_Leaves.vfx` (`1bbdedb9f68441d428e8620dade2bb78`) — wind-driven falling leaves; texture `Leaf.png` (`515e45b153e7fa04c86d1c3b5859603b`); Unlit output context.
- **Moth**: no `.vfx` — legacy `ParticleSystem`. `Assets/HappyHarvest/VFX/Moth/P_VFX_Moths.prefab` (`8c1696ccb5c493343beb818bc677f355`), `Mat_Moth.mat` (`b846d9d3a8c2748d18ab0d59ff18bd55`) using `ShaderGraph_Particles_Lit.shadergraph` (`336041b1315a09e47a73001c639221dc`), texture `Sprite_Moth.png` (`2f0b16b6499ff4836bf3b67b7aa4fb25`).
- **Smoke**: no `.vfx`/`ParticleSystem` — a `LineRenderer` with an animated shader. `Assets/HappyHarvest/VFX/Smoke/Smoke.prefab` (`befd51526a6ad234aa90ff7ce18117be`) → `Mat_Smoke.mat` (`5e12fcf7e18284fa99e38a820896a768`) → `ShaderGraph_Smoke.shadergraph` (`81ceb203a70b641bc81203f91d29b714`), texture `Sprite_Smoke_Blurred.png`.
- **Fire**: `Assets/HappyHarvest/VFX/Fire/VFX_Fire.vfx` (`9090aae97109e7b4e8883a93558c8242`) — **no exposed properties**, fully baked graph. Prefab `VFX_Fire.prefab` (`b3d0865e1be41fc4087c2c3c5269f7f8`). Shader `ShaderGraph_Fire.shadergraph` (`c6e4d202956f3e04099031b936b01323`).
- **StepDust**: legacy `ParticleSystem`, `Assets/HappyHarvest/VFX/StepDust/P_VFX_Step_Dust.prefab` (`f9c2915636332344e81b1379bcc1913c`).
- **Cliffs/River water** (adjacent, not rain): `VFX_CliffWater_Front.vfx` (`cbf0fdad8a6322b4594114bb60eb060e`), `VFX_CliffWater_Side.vfx` (`d61ff9b0ac048f54fa7585830d91044e`) via `ShaderGraph_CliffWater.shadergraph` (`ba156cca35e9df543b677cb6f0f674bf`); `VFX_Cliffs.prefab` (`bd0f22bfd906df541ac32752737f9570`, 25 VisualEffect instances placed around cliff geometry).

No fog, mist, clouds, dust-devils, pollen, or fireflies assets were found anywhere in the project (confirmed by directory inventory + name/keyword search across `Assets/HappyHarvest/VFX/`).

---

## 12. WETNESS / WATER — FILE MANIFEST

**No dedicated puddle, surface-wetness-darkening, or rain-ripple-on-ground system exists.** Confirmed by case-insensitive search for "puddle"/"ripple"/"wet" across `Assets/HappyHarvest/VFX/` — matches occur only inside the Rain folder's own splash mechanism (§5) and the unrelated river/cliff ripple-line VFX (`VFX_WaterLines*.vfx`, §10/§11).

The only "water interaction" presentation is the rain-drop **splash flipbook** technique itself:
- `Assets/HappyHarvest/VFX/Rain/2DwaterSplashFlipbook.shadergraph` (`3cd2c8fd0c91c4f73a53d5fcb0dd7dc8`)
- `Assets/HappyHarvest/VFX/Rain/RainSplashFlipbook.png` (`53adf5ad71d994e56ba749a1070a386d`)
- `Assets/HappyHarvest/VFX/Rain/VFX_2DRain.vfx` / `VFX_WaterDrop.vfx` (both consume the splash flipbook, via different mechanisms — custom shadergraph vs. built-in VFX flipbook UV mode, respectively)

`VFX_2DRain.vfx`'s orphaned `CollisionPlaneNoSplashes_position`/`_normal` PropertySheet overrides (§5) suggest a ground-collision-based "no splash zone" concept may have existed in an earlier iteration but is not currently wired to any exposed property in the shipped graph.

`Assets/HappyHarvest/Audio/Ambience/Water flowing.wav` (`9a23b0ce3c33a954c81db2f99547db09`) — **confirmed unreferenced anywhere** in scenes/prefabs; appears to be an orphaned/unused asset.

---

## 13. WEATHER AUDIO — FILE MANIFEST

- `Assets/HappyHarvest/Audio/Ambience/Rain.wav` (`70d55056a43cc4e899b0fc211fecab23`) — looping, `PlayOnAwake=1`, on "RainSound" AudioSource, gated by `WeatherSystemElement(WeatherType=6)`.
- `Assets/HappyHarvest/Audio/Ambience/Thunder.wav` (`eb1d9da85240c4c64a623a7fb437516a`) — one-shot, `PlayOnAwake=0`, on "ThunderSound" AudioSource, triggered by `VFXOutputEventPlayAudio` off the VFX Graph's internal `"Thunder"` output event (not directly weather-scripted, but effectively gated because the triggering VFX GameObject is itself Thunder-tagged).
- `Assets/HappyHarvest/Audio/Ambience/Background ambience outside - Day.wav` (`5d1a638179550f74fae0bc9c5d8a7144`) / `- Night.wav` (`22618a90cc6fe91438786bb84140bd75`) — day/night only, **not weather-reactive**, crossfaded by `AmbienceBlender.cs`.
- `Assets/HappyHarvest/Common/Audio/MainMixer.mixer` (`3b8e3f485549b1e479a56438f6a2e5c7`) — `Master → {SFX, BGM}`, one snapshot, exposed params `MainVolume`/`SFXVolume`/`BGMVolume`.
- `Assets/HappyHarvest/Prefabs/Managers/SoundManager.prefab` (`a7d6799acefeff1448bb67520e687542`) — carries `Template2DCommon.SoundManager` (SFX pool/UI sound/mixer volume control, GUID `bbcf1b6e2d19ba34f8d58754fa871fa5`), `HappyHarvest.AmbienceBlender`, and a `DayEventHandler`; 4 child `AudioSource`s (Day/Night ambience, UI, SFX-pool template).
- `Assets/HappyHarvest/Audio/Ambience/Water flowing.wav` — orphaned/unused (see §12).

---

## 14. USEFUL ADJACENT EFFECTS

| Asset | GUID | Type | Purpose | Why useful | Reuse |
|---|---|---|---|---|---|
| `Assets/HappyHarvest/VFX/Ambient Dust/VFX_DustParticles.vfx` | `231f13b595b0e4fc3959ce91d53d6c1f` | VFX Graph | Camera-bound wrap-around ambient dust | The camera-relative "TileWrap" technique is perspective-agnostic and directly useful for any screen-space ambient particle layer (dust, motes, snow) in a side-scroller | A |
| `Assets/HappyHarvest/ShaderGraphs/ShaderGraph_MoveVertices.shadergraph` | `de89a77b947988849a9d2d1ed2a707c3` | Shader Graph | Vertex-displacement wind sway for sprites | Perspective-agnostic vegetation-sway technique; directly applicable to side-scroller foliage sprites | A/B |
| `Assets/HappyHarvest/Scripts/DayCycleHandler.cs` | `bc8fb613d11589342b357dd6a3787bac` | C# script | Gradient-driven Light2D day/night evaluator | Simple, engine-generic pattern (Gradient.Evaluate → Light2D.color) with no top-down-specific assumptions except the 360° "sun rig" rotation | A/B |
| `Assets/HappyHarvest/Scripts/Effects/LightInterpolator.cs` | `aa1d2dc24a28c8d4092dedf85da3bd0f` | C# script | Blends Light2D color/intensity/**shape** between keyframes | Generic keyframe-blend utility for any animated Light2D; the underlying script logic is fully portable | A |
| `Assets/HappyHarvest/VFX/Rain/2DwaterSplashFlipbook.shadergraph` + `RainSplashFlipbook.png` | `3cd2c8fd0c91c4f73a53d5fcb0dd7dc8` / `53adf5ad71d994e56ba749a1070a386d` | Shader Graph + Texture | Self-contained flipbook splash shader | Camera/perspective-agnostic; only needs correct placement, not redesign | A |
| `Assets/HappyHarvest/Samples/Visual Effect Graph/17.0.3/OutputEvent Helpers/Runtime/VFXOutputEventPlayAudio.cs` | `4a4ed0a47000743b29d4c880beee34a0` | C# script (Unity sample) | VFX→Audio event bridge | Official Unity utility, engine-generic, zero project coupling | A |
| `Assets/HappyHarvest/Scripts/Audio/AmbienceBlender.cs` + `MainMixer.mixer` | `07b0771e8886c2340855eec75a066f04` / `3b8e3f485549b1e479a56438f6a2e5c7` | C# script + AudioMixer | Day/night ambience crossfade pattern | Fully engine/perspective-agnostic pattern; simple reference for any similar crossfade need | A |

---

## 15. POTENTIAL REUSABLE DEPENDENCY BUNDLES

### Rain Visual Core
Purpose: background + foreground rain streak rendering.
Files: `VFX_2DRain.vfx` (`e5970f8d750064f2bbab714ac07feb6c`), `VFX_RainForeground.vfx` (`f8cc1e97555be4bb7853de24f314366b`), `SpriteLitAlphaShadergraph.shadergraph` (`823c0ef43fca54ea4be96177f6eedb6b`), `BlankSquare.png` (`d3c6cc9c36390462099e14cb5c6b9c23`).
Required deps: URP 2D Renderer + VFX Graph package. Optional: `WeatherSystem`/`WeatherSystemElement` scripts (only needed if reusing the exact toggle pattern; a destination project's own weather sim can drive `VisualEffect.enabled`/exposed properties directly instead).
Portability: high for the technique/shader; the graphs' internal spawn-box geometry and `RainDirection` values are tuned for the top-down farm camera and will need re-tuning for a side-scroller's framing (not confirmed exact spawn volume dimensions — see §18).

### Rain Splash Extension
Purpose: ground-impact splash flipbook.
Files: `2DwaterSplashFlipbook.shadergraph` (`3cd2c8fd0c91c4f73a53d5fcb0dd7dc8`), `RainSplashFlipbook.png` (`53adf5ad71d994e56ba749a1070a386d`), `VFX_WaterDrop.vfx` (`46383f17472111942a4f8d0a994bbdd9`, demonstrates the built-in-flipbook alternative wiring).
Required deps: none beyond the shadergraph+texture pair for direct shader reuse; `VFX_WaterDrop.vfx` for the drop+impact GPU-event pattern.
Portability: high — self-contained and camera/perspective-agnostic.

### Storm Extension
Purpose: lightning-flash + thunder-audio trigger layered onto rain.
Files: `VFX_RainForeground.vfx` (`Lightnings` bool + internal `VFXOutputEvent`), `VFXOutputEventPlayAudio.cs` (`4a4ed0a47000743b29d4c880beee34a0`), `Thunder.wav` (`eb1d9da85240c4c64a623a7fb437516a`).
Required deps: VFX Graph OutputEvent Helpers sample (already imported under `Assets/HappyHarvest/Samples/`).
Portability: the audio-bridge script is directly portable (A); the internal flash logic requires opening the graph in-editor to fully understand/extract (see §18) before judging A/B/C.

### Day/Night Lighting Core
Purpose: continuous gradient-driven time-of-day lighting.
Files: `DayCycleHandler.cs` (`bc8fb613d11589342b357dd6a3787bac`), `GameManager.cs` (`f7aaabf1ce21bf941ac2b6e07aeeaa6d`, for `CurrentDayRatio` — or substitute the destination project's own time source), `DayCycleHandler.prefab` (`ae2488684f645a54e8793357ece28d8f`, authored gradient/curve values), `LightInterpolator.cs` (`aa1d2dc24a28c8d4092dedf85da3bd0f`), `ShadowInstance.cs` (`2a36ebd5a10583041ae36dd8294c0935`).
Required deps: URP `Light2D` components.
Portability: the scripts are engine-generic (A); the authored gradient/curve *values* and the 360°-rotation "sun rig" assumption are tuned for a top-down farm and would need re-authoring (B). `ShadowInstance`'s flat-ground-plane shadow-faking assumption is the most top-down-coupled piece (C for direct reuse).

### Environmental Atmospherics
Purpose: ambient dust, falling leaves, fire, smoke.
Files: `VFX_DustParticles.vfx` (`231f13b595b0e4fc3959ce91d53d6c1f`), `VFX_Leaves.vfx` (`1bbdedb9f68441d428e8620dade2bb78`), `VFX_Fire.vfx` (`9090aae97109e7b4e8883a93558c8242`), `Smoke.prefab` (`befd51526a6ad234aa90ff7ce18117be`).
Required deps: VFX Graph package (Fire/Leaves/Dust); `LineRenderer` + `ShaderGraph_Smoke.shadergraph` for Smoke.
Portability: Dust and Leaves are highly portable (A); Fire is a closed/baked graph (C — nothing to configure, take-it-or-leave-it); Smoke's LineRenderer technique is a good generic reference (B).

### Wind / Vegetation
Purpose: sprite vertex-sway shader.
Files: `ShaderGraph_MoveVertices.shadergraph` (`de89a77b947988849a9d2d1ed2a707c3`), `SubGraph_WaveSubGraph.shadersubgraph` (`4b332ed3d43fcbd4698126328b8a3de8`), `SubGraph_Transform.shadersubgraph` (`192f8a092dba89846b5e7e2e8a17c85f`), `Material_Plants.mat`.
Required deps: none beyond Shader Graph + Sprite Lit target.
Portability: high (A/B) — no runtime wind driver exists to port (confirmed absent), so only the static shader technique transfers, not any dynamic system.

### Wet Surface System
**Does not exist as a bundle** — no puddle/wetness assets were found (see §12). Nothing to bundle.

### Weather Audio
Purpose: rain loop, thunder one-shot, day/night ambience crossfade, central mixer.
Files: `Rain.wav` (`70d55056a43cc4e899b0fc211fecab23`), `Thunder.wav` (`eb1d9da85240c4c64a623a7fb437516a`), `AmbienceBlender.cs` (`07b0771e8886c2340855eec75a066f04`), `MainMixer.mixer` (`3b8e3f485549b1e479a56438f6a2e5c7`), `SoundManager.prefab` (`a7d6799acefeff1448bb67520e687542`).
Required deps: stock Unity Audio (no middleware).
Portability: high (A) — plain `AudioSource`/`AudioMixer` usage, no perspective coupling at all.

---

## 16. ASTRA INSPECTION MANIFEST

### PRIORITY 1 — CORE

**Asset**: `Assets/HappyHarvest/Scripts/WeatherSystem.cs` / `Assets/HappyHarvest/Scripts/WeatherSystemElement.cs`
**GUID**: `99728f0e23208b44d90d36e0f9cff02f` / `70d84b2a31b315e44bf3caa61b8dd45f`
**Type**: C# script
**Why inspect it**: This is the entire weather state machine — a 110-line flag-based `SetActive` gate. Understanding it is a 5-minute read that explains every "why is this GameObject active" question elsewhere in the report.
**Inspect alongside**: `Assets/HappyHarvest/Scripts/UI/UIHandler.cs` (dev UI that calls `ChangeWeather`), `Assets/HappyHarvest/Scenes/Farm_Outdoor.unity` (the "WeatherSystem" GameObject and its 6+2 tagged elements)
**What to understand**: the `[Flags] WeatherType` bit logic and `HasFlag` semantics (a subtlety: an element flagged with multiple bits matches more weather states than one might expect — see §5/§7 worked examples)

**Asset**: `Assets/HappyHarvest/VFX/Rain/VFX_RainForeground.vfx`
**GUID**: `f8cc1e97555be4bb7853de24f314366b`
**Type**: VFX Graph
**Why inspect it**: This single asset IS the storm/lightning/thunder mechanism. Open it in the VFX Graph editor to see the node-level logic gated by the `Lightnings` bool and the `VFXOutputEvent("Thunder")` chain — this report only traced it to context/spawner granularity from raw YAML.
**Inspect alongside**: `Assets/HappyHarvest/Samples/Visual Effect Graph/17.0.3/OutputEvent Helpers/Runtime/VFXOutputEventPlayAudio.cs`, the two scene instances ("Visual Effect Rain Foreground" / "...Thunder") in `Farm_Outdoor.unity`
**What to understand**: exposed VFX properties, the Lightnings-gated flash logic, output event wiring, foreground Sorting Layer/Order usage

**Asset**: `Assets/HappyHarvest/VFX/Rain/VFX_2DRain.vfx`
**GUID**: `e5970f8d750064f2bbab714ac07feb6c`
**Type**: VFX Graph
**Why inspect it**: The background rain + GPU-event-driven splash chain — the most complete "full rain system in one graph" example.
**Inspect alongside**: `VFX_RainForeground.vfx`, `2DwaterSplashFlipbook.shadergraph`
**What to understand**: particle spawning/rate, GPU Event bridging to a second particle system, splash flipbook wiring, lack of a custom streak texture (uses VFX package default)

**Asset**: `Assets/HappyHarvest/VFX/Rain/VFX_WaterDrop.vfx`
**GUID**: `46383f17472111942a4f8d0a994bbdd9`
**Type**: VFX Graph
**Why inspect it**: Demonstrates the alternate built-in-flipbook approach (vs. custom shadergraph) for splashes, and is reused for both loose world drops and lamp-drip effects.
**Inspect alongside**: `RainSplashFlipbook.png`, `Prefab_Streetlamp.prefab`
**What to understand**: flipbook UV mode configuration (`flipBookSize`), GPU Event impact-splash pattern, exposed Wind/Impact params

**Asset**: `Assets/HappyHarvest/VFX/Rain/2DwaterSplashFlipbook.shadergraph` + `Assets/HappyHarvest/VFX/Rain/RainSplashFlipbook.png`
**GUID**: `3cd2c8fd0c91c4f73a53d5fcb0dd7dc8` / `53adf5ad71d994e56ba749a1070a386d`
**Type**: Shader Graph (Sprite Lit) + Texture2D
**Why inspect it**: Fully self-contained, perspective-agnostic — the single most directly reusable rain-related asset in the project.
**Inspect alongside**: none required
**What to understand**: `FlipbookNode` (3×2 grid) wiring, `_FrameIndex` exposed property (driven per-particle by the VFX graph)

**Asset**: `Assets/HappyHarvest/Scripts/DayCycleHandler.cs` + `Assets/HappyHarvest/Prefabs/Managers/DayCycleHandler.prefab`
**GUID**: `bc8fb613d11589342b357dd6a3787bac` / `ae2488684f645a54e8793357ece28d8f`
**Type**: C# script + Prefab
**Why inspect it**: The entire day/night lighting driver — 5 gradients + 2 curves, ~270 lines total, engine-generic technique.
**Inspect alongside**: `Assets/HappyHarvest/Scripts/GameManager.cs` (time source), `Assets/HappyHarvest/Scripts/Effects/LightInterpolator.cs`, `Assets/HappyHarvest/Scripts/ShadowInstance.cs`
**What to understand**: gradient evaluation onto Light2D.color, the `LightsRoot` 360°-rotation "sun rig" assumption (likely needs rethinking for a side-scroller), shadow-curve faking

**Asset**: `Assets/HappyHarvest/Scenes/Farm_Outdoor.unity`
**GUID**: n/a (scene)
**Type**: Scene
**Why inspect it**: Ground truth for every wiring claim in this report — open it directly to see the "WeatherSystem", "RainSound", "ThunderSound", "Visual Effect Rain*", and "DayCycleHandler" GameObjects and their Inspector values live.
**Inspect alongside**: everything above
**What to understand**: exact spawn-box/transform placement of rain VFX (not fully resolved from YAML alone — see §18), Light2D hierarchy under "LightsRotator"

### PRIORITY 2 — SUPPORTING

**Asset**: `Assets/HappyHarvest/Scripts/DayEventHandler.cs`
**GUID**: `cd41505808a6ef44eab48fd2d704589b`
**Type**: C# script
**Why inspect it**: The generic discrete time-window event trigger used for lamp on/off and ambience crossfade — reusable pattern independent of `DayCycleHandler`'s continuous gradients.
**Inspect alongside**: `Assets/HappyHarvest/Art/Environment/Lamps/StreetLamp/Prefab_Streetlamp.prefab`, `Assets/HappyHarvest/Prefabs/Managers/SoundManager.prefab`
**What to understand**: `UnityEvent` OnEvents/OffEvent range-transition firing, the custom `PropertyDrawer` for the min/max time slider

**Asset**: `Assets/HappyHarvest/Scripts/Audio/AmbienceBlender.cs` + `Assets/HappyHarvest/Common/Audio/MainMixer.mixer`
**GUID**: `07b0771e8886c2340855eec75a066f04` / `3b8e3f485549b1e479a56438f6a2e5c7`
**Type**: C# script + AudioMixer
**Why inspect it**: Simple, portable day/night audio crossfade pattern and mixer group routing.
**Inspect alongside**: `Assets/HappyHarvest/Prefabs/Managers/SoundManager.prefab`
**What to understand**: linear volume crossfade in `Update()`, mixer exposed params (`MainVolume`/`SFXVolume`/`BGMVolume`)

**Asset**: `Assets/HappyHarvest/Scripts/Effects/LightInterpolator.cs`
**GUID**: `aa1d2dc24a28c8d4092dedf85da3bd0f`
**Type**: C# script
**Why inspect it**: Demonstrates animating a Light2D's freeform **shape**, not just color/intensity — an underused technique worth knowing about.
**Inspect alongside**: `Assets/HappyHarvest/Prefabs/Light 2D_Warehouse.prefab`
**What to understand**: keyframe-pair blending, `SetShapePath` usage

**Asset**: `Assets/Settings/UniversalRP.asset` / `Assets/Settings/Renderer2D.asset` / `Assets/HappyHarvest/Scenes/Volume Profiles/Volume_Profile.asset`
**GUID**: `681886c5eb7344803b6206f758bf0b1c` / `424799608f7334c24bf367e4bbfa7f9a` / `81b2d2e143e313845a66e51cf0baed1f`
**Type**: Pipeline Asset / Renderer2DData / Volume Profile
**Why inspect it**: Baseline rendering context that everything above renders through — confirms 2D Renderer, no custom Renderer Features, Bloom-only post-fx.
**Inspect alongside**: `ProjectSettings/TagManager.asset` (Sorting Layers)
**What to understand**: default material fallback (Sprite-Lit-Default), 2D Light Blend Styles, Bloom-only Volume (no color grading anywhere)

### PRIORITY 3 — POTENTIALLY USEFUL LATER

**Asset**: `Assets/HappyHarvest/VFX/Ambient Dust/VFX_DustParticles.vfx`
**GUID**: `231f13b595b0e4fc3959ce91d53d6c1f`
**Type**: VFX Graph
**Why inspect it**: Camera-bound "TileWrap" ambient-particle technique, reusable regardless of camera perspective.
**Inspect alongside**: n/a
**What to understand**: the TileWrap position-binding node and `ActivateTurbulence` toggle

**Asset**: `Assets/HappyHarvest/ShaderGraphs/ShaderGraph_MoveVertices.shadergraph`
**GUID**: `de89a77b947988849a9d2d1ed2a707c3`
**Type**: Shader Graph
**Why inspect it**: Vegetation wind-sway shader — portable technique, though remember there's no dynamic wind driver behind it (static per-material values only).
**Inspect alongside**: `Assets/HappyHarvest/Materials/Material_Plants.mat`
**What to understand**: the 3-layer wave/transform subgraph stack

**Asset**: `Assets/HappyHarvest/VFX/Water/VFX_WaterLinesStorm.vfx`
**GUID**: `78591f2702ad70e4e9bacf5d9b3a79fb`
**Type**: VFX Graph
**Why inspect it**: A "storm" variant exists for river ripple lines but its activation trigger is unconfirmed — worth checking in-editor whether it's manually placed or has some undiscovered runtime hook.
**Inspect alongside**: `Assets/HappyHarvest/VFX/Water/VFX_WaterLines.vfx` (base variant)
**What to understand**: whether/how it's actually toggled at runtime

---

## 17. SIDE-SCROLLER ADAPTATION NOTES

- **`2DwaterSplashFlipbook.shadergraph` + `RainSplashFlipbook.png`**: perspective-agnostic — the flipbook shader just samples a texture grid by particle-driven frame index. Translates directly; only the *placement* of splash-spawning geometry needs side-view-appropriate collision/ground-plane logic.
- **Rain streak VFX (`VFX_2DRain.vfx`, `VFX_RainForeground.vfx`)**: `RainDirection` is an authored Vector2, so re-angling for a side-scroller camera is just a value change, not a redesign — but the **spawn-volume geometry and extents** (not fully resolved from raw YAML in this pass) are presumably sized/positioned for the top-down farm's camera framing and will need re-tuning to cover a side-scrolling camera's frustum. The foreground layer (`VFX_RainForeground.vfx`, Sorting Layer "Foreground", order 90) is effectively a screen-space overlay already and should adapt with the least effort.
- **`VFX_WaterDrop.vfx` drip/splash**: authored as a **fixed-position, per-object** effect (placed under a lamp, or loose at fixed world coordinates) rather than tied to a floor collision system — so there is no "top-down ground plane" assumption to break here; it's already just "an effect anchored to a point," which transfers fine to any perspective. The main adaptation is re-placing anchor points appropriately (e.g. under eaves/ledges instead of farm props).
- **`DayCycleHandler`'s `LightsRoot` 360° rotation**: this is the one clearly top-down-flavored assumption — rotating a "sun rig" transform a full 360° in the Z axis implies a top-down/plan view where the sun's screen-space position doesn't matter much visually (only the *lights'* colors matter, since the rig's rotation is mostly a legacy/vestigial visual flourish rather than a repositioning of a visible sun sprite — confirm in-editor whether anything visible is actually attached to `LightsRotator` besides the Point lights themselves). In a side view, a literal 360° rotating light rig could look wrong if anything is visually anchored to it; the gradient-evaluation core (color/intensity per Light2D) is unaffected and fully portable.
- **`ShadowInstance`'s fake shadow rotation/scale**: explicitly assumes a horizontal top-down ground plane (a flat shadow blob rotating/stretching under an object as if tracking a sun's angle). In a side-scroller with vertical walls/platforms, this ground-plane assumption breaks down significantly — treat as reference-only (C) for the specific technique, though the underlying idea (curve-driven fake shadow instead of real-time shadow casting) could inspire an adapted approach for a side view.
- **Sorting Layers / Y-sorting**: this project uses a small fixed Sorting Layer list (`Bottom/Default/Objects/ObjectsFront/Foreground`) with **no per-frame Y-sort algorithm found** in the traced weather/lighting scripts — rain and lighting assets don't participate in Y-sorting logic at all (they're either background/global or explicit Foreground-layer overlays). This means the rain/lighting techniques themselves carry no Y-sort coupling to adapt — the destination project's own Y-sort system (if any) is orthogonal.
- **Volume/post-processing**: only Bloom is authored (no color grading, vignette, or storm-specific Volume overrides), so there's no complex Volume-based storm treatment to port — a destination project wanting camera-flash-style lightning would need to author that from scratch; nothing to adapt here.
- **Audio (`AmbienceBlender`, `MainMixer`, `RainSound`/`ThunderSound`)**: entirely perspective-agnostic — direct reuse candidates regardless of camera style.
- **Wind/vegetation shader**: perspective-agnostic vertex displacement; no camera-relative or ground-plane assumptions found.

---

## 18. UNKNOWNS / UNCERTAINTIES

1. **Internal VFX Graph node logic for the "Lightnings" flash inside `VFX_RainForeground.vfx`** was traced only to context/spawner-graph granularity from raw YAML; the specific blocks/operators that produce the visual flash (color/alpha animation, if any) were not fully unpacked. Requires opening the graph in the Unity Editor's VFX Graph view.
2. **Exact spawn-volume shapes/sizes and world-space extents** for `VFX_2DRain.vfx`'s background rain coverage were not resolved (only exposed parameters were traced, not internal spawn-context bounds) — needed before judging how much re-tuning a side-scroller camera would require.
3. **`VFX_WaterLinesStorm.vfx`** (storm variant of river ripple lines) has no confirmed runtime activation trigger — it is not a `WeatherSystemElement` and no script references it. Unclear if it's manually swapped by a level designer, a leftover, or activated by some mechanism not found in this pass.
4. **`DayCycleHandler.prefab`'s orphaned `SunHeight: 0.8` field** has no matching field in the current `DayCycleHandler.cs` — confirmed harmless (Unity ignores unmatched serialized data) but its original purpose/removed-feature history is unknown.
5. **Orphaned `RainDirectionV3`/`CollisionPlaneNoSplashes_position`/`_normal`/`RainTintColor` PropertySheet overrides** on the scene's rain VFX instances don't match any currently exposed property name in the shipped graphs — evidence of a prior graph iteration, but that iteration's design is not recoverable from current assets.
6. **`Assets/HappyHarvest/Audio/Ambience/Water flowing.wav`** appears completely unreferenced — confirmed absent from all scene/prefab searches, but not confirmed absent from any dynamically-loaded/Resources-based path (not exhaustively checked against runtime string-based loading).
7. **`WeatherSystem.StartingWeather`'s exact authored enum value** in the scene was not directly printed by any agent pass (inferred to likely be `Sun`/none-active from GameObject `m_IsActive` states at edit time, since Unity re-evaluates `Start()` at runtime regardless) — not confirmed with certainty.
8. **Full parent-transform hierarchy paths** (e.g., exact GameObject nesting under "LightsRotator", or which specific prop each `VFX_WaterDrop` instance sits near) were only partially resolved — several are confirmed root-level with fixed world positions, but their in-context placement (e.g., under which roof/ledge) was not visually confirmed against the scene view.
9. **Whether anything is visually attached to `LightsRoot`/"LightsRotator" besides the Point lights** (e.g., a sun/moon sprite) was not confirmed — relevant to judging how load-bearing the 360° rotation behavior actually is (see §17).
10. Two official Unity blog articles are referenced by the project's own ReadMe data (`Assets/HappyHarvest/ReadMe/BlocksInfoData/article2dlightshadow.asset` → "2D lights techniques", `.../articlevfx.asset` → "2D special FX with VFX Graph") — these are documentation pointers authored by the original developers describing some of the exact techniques covered in this report; their URLs are stored in-project (not independently verified/fetched during this investigation).

---

## FINAL QUALITY CHECK

- Every major asset listed has an exact path. ✅
- GUIDs recorded wherever a `.meta` file exists. ✅
- Significant GUID references resolved to paths (VFX asset refs, script refs, texture refs, audio clip refs, prefab source refs). ✅
- Rain / Storm / Clear Weather / Day-Night each have a usable file manifest. ✅
- Astra Inspection Manifest points directly at exact assets with GUIDs and "what to understand" guidance. ✅
- Adjacent atmospherics, wind, and audio assets were actively searched and documented, not ignored. ✅
- No files were modified, created, generated, copied, committed, or pushed during this investigation. ✅
