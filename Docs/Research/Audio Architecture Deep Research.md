# Deep research on audio architecture for your Unity metroidvania

## Scope and verification

I could not retrieve a queryable GitHub connector or repository files in this chat, so I cannot truthfully claim a line-by-line audit of the just-updated repo. The repo-specific comments below are therefore a **provisional audit of the architecture you described**, not a verified inspection of every current script. The recommendations are grounded in Unity’s current audio stack: persistent root objects across scene loads, AudioMixer groups and snapshots, per-source mixer routing, ScriptableObject asset workflows, animation-timed events, and Unity’s built-in pooling support. citeturn12view0turn8view0turn9view0turn8view2turn13view0turn17view0

That limitation matters, because the difference between a clean architecture and technical debt usually appears in the **call sites**: who is allowed to call `AudioSource.Play`, `PlayOneShot`, `Stop`, `PlayClipAtPoint`, or music-start logic. Unity’s own APIs make those seams very explicit: `PlayOneShot` layers on a source without cancelling current playback, `Play()` on a currently assigned clip restarts it, `Stop()` resets playback to the beginning next time, and `PlayClipAtPoint` creates and disposes a temporary source automatically. Those details are exactly why ownership boundaries matter. citeturn8view1turn19view0turn19view1turn19view2

## Current architecture audit

From your description, the broad shape is already good. A Boot-created, persistent `AudioManager` handles cross-scene concerns, while a hero-local `HeroAudioController` owns a small fixed palette of authored movement and body sounds. That fits Unity’s scene-persistence model, because a preserved root object survives scene loads with its children, and it still leaves every local hero source free to route into a central mixer group later through `outputAudioMixerGroup`. citeturn12view0turn9view0turn8view0

**AudioManager.** The current API you described is a sensible minimal service surface: a shared `PlaySFX(AudioClip)` path for simple one-shots, a pooled pitch-varied path for layered combat or repeated transient sounds, and a music path that does **not** restart the same clip if it is already playing. That last guard is especially sound, because Unity documents that calling `Play()` on the currently assigned clip makes it sound restarted. The main risks I would look for in the actual file are missing null guards, duplicated defaults between pooled and non-pooled sources, inconsistent mixer routing, or any direct reference from `AudioManager` into hero/enemy gameplay classes. citeturn19view0turn8view1turn8view0turn9view0

**HeroAudioController.** This is the right owner for fixed hero-local sources such as jump, land, dash, wall jump, wall slide, hurt, death, footsteps, and terrain impact. Looping sources are particularly natural here, because loop state belongs to the hero’s movement state, and Unity exposes looping directly on `AudioSource`. The debt trap is not “hero-local audio” itself; the trap is making the controller a thin wrapper around publicly exposed child sources so that action classes, controller code, or random helpers can start poking those sources directly. If only `HeroAudioController` is allowed to play and stop those sources, the exception stays clean. citeturn9view2turn9view0

**HeroAttackModule.** This is the correct place for slash whooshes and for enforcing a “primary impact once per swing” rule. A whoosh belongs to the attack’s authored timing; a hurt or death cue belongs to the target after damage is accepted. If `HeroAttackModule` also starts enemy hurt/death or if targets also emit generic swing-impact sounds for every hit, you will get duplicated layers fast. In a code audit, I would treat any direct enemy-side audio playback from the attack module as suspicious unless it is merely requesting a generic `AudioManager` one-shot through a narrow API. citeturn8view1turn13view0

**HeroController and plain action classes.** Your constraint that `HeroController` remain a coordinator is correct. The audio implication is that action classes should request sound by intent—“jump committed”, “dash entered”, “start wall-slide loop”, “stop wall-slide loop”—rather than own raw `AudioSource`s. For movement sounds that need exact sync with animation, Unity’s event model exists precisely so you can call a function at a specific point in a clip, which is why footsteps and some attack whooshes are usually cleaner as animation-timed requests than as free-running timers. citeturn13view0

**Enemy health, recoil, and death.** Target-side enemy systems should decide whether damage was accepted, whether hit reactions were suppressed, and whether death occurred. That is where hurt and death audio should be requested. The coupling risk here is duplicated consequence logic: one class plays hurt, another plays recoil, a third plays death, and the result is two or three audio events for one confirmed hit. Your interface-driven combat helps here, because it naturally separates “attack was confirmed” from “target decided what happened next”. citeturn8view1turn19view1

**GameManager, Boot, and scene flow.** Music ownership belongs here much more than in random scene objects. Unity documents that `SceneManager.sceneLoaded` fires after `OnEnable` but before `Start`, which makes it a strong seam for centralised scene cue application. At the same time, Unity also documents that `AudioMixer.SetFloat` should **not** be called in `Awake` or `OnEnable`, but in `Start` or later. Taken together, that argues for a central flow model: Boot instantiates the persistent audio root, `AudioManager.Start` applies volume/mixer state, and scene flow or transition logic applies scene cues afterwards. citeturn12view1turn12view2turn10view0

**Audio-related ScriptableObjects and config.** ScriptableObjects are an excellent fit for reusable cue/config assets because they exist as project assets independent of GameObjects and are specifically intended as shared data stores. They are **not** a good place to persist mutable player settings at runtime in a standalone build. In other words: use them for `SfxCue`, `MusicCue`, `SceneAudioCue`, and `AtmosphereCue`; do not use them as your player’s save file. citeturn8view2

My overall audit judgement, based on the architecture you described, is that the current hero-local exception is **clean and defensible** if two rules are enforced consistently: first, only `HeroAudioController` touches the hero’s child sources; second, the hero prefab remains a fixed body/movement palette, not a dumping ground for attacks, enemy reactions, world sounds, UI, or music. The highest-probability routing violations in a real repo audit would be stray `PlayOneShot`/`Play`/`PlayClipAtPoint` calls outside approved classes, because those are exactly the sorts of convenience calls that erode architecture over time. citeturn8view1turn19view0turn19view2turn18view0

## Whether to keep the hybrid split

You should **keep the hybrid split** and make it stricter, not migrate everything into `AudioManager`. Unity’s routing model already gives you the best practical compromise: local hero sources can still feed the central mix through `outputAudioMixerGroup`, while global systems can keep using a central manager for music, pooled SFX, ambient beds, settings, and scene cues. That means you can preserve responsive, prefab-authored hero movement audio without sacrificing future bus structure, ducking, or user volume control. This is a design inference from Unity’s mixer-and-source model rather than an engine requirement, but it is the right inference for your project constraints. citeturn9view0turn8view0

An “everything through `AudioManager`” approach is attractive for purity, but in your case it creates more indirection than value. Fixed hero loops such as wall slide and tightly coupled movement sounds become awkward if a global manager has to know when to start, stop, retrigger, or preserve those states without learning too much about hero internals. You either over-expand the manager’s surface area or end up smuggling hero state into a supposedly generic audio service. citeturn9view2turn8view0

An “everything local” approach is the opposite failure mode. It can work for one character or one scene, but music ownership, cross-scene persistence, user volume settings, ducking, and ambience transitions become fragmented quickly. Unity’s AudioMixer exists specifically to provide a bus structure and mixer-level control across many sources, so throwing all ownership down to scene-local `AudioSource`s leaves too much system-level behaviour without a clear owner. citeturn8view0turn9view0

A full event bus or middleware-style event graph is unnecessary right now. The middle ground that fits your project well is **data-driven cues without a full bus rewrite**: keep `AudioManager` as the runtime playback service, keep `HeroAudioController` for fixed hero-local sources, and introduce small ScriptableObject cue assets for global one-shots, music, and scene/zone audio. That gets you authoring flexibility and lower coupling while staying very close to Unity’s native strengths as an asset-based engine. citeturn8view2turn8view0

The right progression for this project, then, is not “migrate hero sounds into `AudioManager`”. It is “formalise the boundary”: hero body/movement audio stays local; transient combat/world/UI/music/ambience stays global; all of it routes through the same mixer tree; and nothing outside the approved owners calls playback APIs directly. citeturn9view0turn8view0turn8view1

## Target architecture for the next milestones

The clean target shape is this. `AudioManager` owns cross-scene audio services: pooled SFX playback, music playback, future ambience beds, mixer references, exposed volume parameters, scene/zone cue application, and perhaps later ducking/snapshots. `HeroAudioController` owns the hero’s fixed local sources and only the hero’s fixed local sources. `HeroAttackModule` owns whoosh timing and the single primary impact for each swing. Enemy damage/death systems own their own hurt/death consequences after damage resolution. UI calls `AudioManager` directly. World one-shots call `AudioManager`; persistent positional loops, if you add them later, are better represented by generic reusable emitter components that still route through the mixer instead of teaching `AudioManager` every local world loop in the game. citeturn8view0turn9view0turn9view1turn9view2

For actual playback categories, I would draw the line like this. Fixed hero body and locomotion sounds live in `HeroAudioController`. Looping hero states such as wall slide also live there. Slash whooshes live through `AudioManager` on pooled one-shot voices, because they need overlap and pitch variance more than they need fixed authored sources. Enemy hurt and death sounds live through `AudioManager`, requested by the target after accepting damage. UI and menu sounds live through `AudioManager`. Music is triggered by scene flow or `GameManager.BeginSceneTransition`, never by arbitrary scene-object lifecycle methods. Global ambience beds also belong under `AudioManager`, but positional environmental loops can be local emitter components once you need them. citeturn8view1turn9view2turn12view1turn8view0

For data, introduce a small ScriptableObject cue layer. A `SfxCue` asset can hold one or more clips, pitch range, volume, optional cooldown, and target bus/category. A `MusicCue` can hold clip, loop, fade settings, and perhaps a snapshot name or reference. A `SceneAudioCue` can map a scene or transition destination to baseline music and ambience. An `AtmosphereCue` can describe cave/wind/water/interior beds and layer weights. ScriptableObjects are a strong fit because Unity positions them as shared assets independent of scene object lifetimes, which is exactly what audio cue data is. citeturn8view2

To avoid hard references from audio systems into hero/enemy internals, keep gameplay code dependent on **small intent APIs**, not on audio implementation. A jump action asks `HeroAudioController.PlayJump()`. An enemy health component asks `AudioManager.PlaySfx(enemyHurtCue)`. A zone trigger asks `AudioManager.PushAtmosphere(cue, ownerToken)`. None of those callers need to know whether the underlying playback happens on a pooled transient source, a local hero source, or a crossfade channel. Unity’s mixer routing model makes that separation practical because routing sits on `AudioSource`s and groups, not in your gameplay call sites. citeturn9view0turn8view0

A sensible folder and naming layout would look like this:

```text
Assets/_Project/Audio/
  Scripts/
    AudioManager.cs
    HeroAudioController.cs
    WorldAudioEmitter.cs
    AudioSettingsService.cs
  Cues/
    SFX/
      SfxCue_HeroSlash.asset
      SfxCue_EnemyHurt.asset
      SfxCue_UIConfirm.asset
    Music/
      MusicCue_ForgottenCrossroads.asset
      MusicCue_BossPrototype.asset
    Scenes/
      SceneAudioCue_Greenpath.asset
      SceneAudioCue_TestRoom.asset
    Atmosphere/
      AtmosphereCue_Cave.asset
      AtmosphereCue_WindHall.asset
  Mixers/
    MainAudioMixer.mixer
  Clips/
    SFX/Hero/
    SFX/Enemy/
    SFX/World/
    SFX/UI/
    Music/
    Ambience/
  Prefabs/
    AudioManager.prefab
```

I would also keep naming very literal: `sfx_hero_jump_01`, `sfx_hero_land_soft_01`, `sfx_enemy_fly_hurt_01`, `mus_greenpath_loop`, `amb_cave_bed_loop`, `cue_scene_greenpath`, `cue_sfx_slash_light`. The point is not elegance; it is making grep, inspector work, and later collaboration painless. That recommendation follows naturally from Unity’s asset-centric `ScriptableObject` and `AudioMixer` workflows. citeturn8view2turn8view0

## Engine-level implementation guidance

**AudioMixer and volume settings.** Introduce the mixer **now**, before you add settings UI and before you add layered ambience. Unity’s AudioMixer is built around a tree of groups/buses, supports effects and send/return routing, and allows snapshots for transitions and ducking. Because any `AudioSource` can route to a target group via `outputAudioMixerGroup`, hero-local sources do not block central bus control at all. For your project, I would use `Master` at the top, with `Music`, `Ambience`, and `UI` as direct children, and an `SFX` branch containing `Hero`, `Enemy`, and `World`. If you later need a reverb/send path, add it after you have a concrete use case, not before. citeturn8view0turn9view0

Expose mixer parameters only where the player will actually want control: `MasterVol`, `MusicVol`, `SfxVol`, optionally `UiVol`, and perhaps `AmbienceVol` once ambience is in. Apply those in `Start` or later from a dedicated settings service, because Unity explicitly warns against calling `AudioMixer.SetFloat` in `Awake` or `OnEnable`. Save the player’s chosen values in your future settings/save system as normal serialised data—ideally normalised linear values converted to dB at the mixer boundary—rather than trying to mutate ScriptableObject assets as runtime save data. citeturn10view0turn8view2

**SFX pooling and pitch variation.** Your current two-path setup is enough for the present slice. A shared `PlaySFX(AudioClip)` path is fine for lower-concurrency cues, especially UI, pickups, and light world interactions, because `PlayOneShot` layers playback without cancelling clips already playing on that source. Use the pooled path for slash whooshes, repeated enemy-hit sounds, and any case where you need per-voice pitch or later per-voice routing/spatial flags. Also, do not use `volumeScale > 1` as a brute-force loudness fix; Unity notes that scales larger than one can clip, so loudness is better solved at the asset/mixer level. citeturn8view1

Expand the pool only when stress testing or profiling shows you actually need it. Unity’s own pooling guidance is very clear: pooling reduces instantiation/destruction overhead and GC pressure, but it also adds lifecycle complexity and can reserve memory you do not need if you introduce it prematurely. In practical terms, keep your pooled combat voices small at first, then raise capacity only if rapid slash/hit stacks expose audible dropouts or source creation churn. citeturn18view0turn17view0

When returning a pooled audio voice, reset it like any other “dirty” pooled object. In your case that means pitch back to `1`, loop back to `false`, volume to the expected default, `spatialBlend` back to your chosen baseline, `outputAudioMixerGroup` back to the correct bus if your code can reassign it, and any assigned clip cleared if you use direct clip assignment rather than `PlayOneShot`. The point is not dogma; it is preventing state leakage between unrelated sounds. Unity’s pooling examples explicitly emphasise resetting reused objects to a clean state. citeturn18view0turn17view0turn9view1turn9view2turn9view3turn9view0

For positional audio, I would keep hero, combat, and UI largely **2D for now**. Unity’s `spatialBlend` exists to choose between fully 2D and fully 3D playback, so you can add spatial treatment later to environmental emitters, far-off hazards, waterfalls, lifts, or machinery if that improves level readability. In a tight 2D/2.5D action prototype, over-spatialising core combat usually hurts clarity more than it helps. citeturn9view1turn8view3

**Combat audio feel.** For slash whooshes, trigger from animation timing, not from input press. The whoosh should line up with the perceptual start of the weapon motion—usually just before or at the first active frames—because the player reads that motion, not the raw button-down. Unity’s animation-event system exists for exactly this kind of sync point: a function call at a specific moment in an animation. This is one of the main places where animation-timed requests beat timer-based guesses. citeturn13view0turn8view1

For impacts, use a hybrid ownership rule. The attacker side should own one **primary impact** sound for the swing, triggered on the first confirmed successful hit. The target side should own body-specific consequences such as hurt or death after damage has been accepted. That gives you one clear weapon connection sound, prevents one swing from producing three identical impact sounds when it hits three targets, and still lets each target contribute its own reaction if appropriate. In your architecture, that means `HeroAttackModule` should guard the “impact already played this swing” flag, while enemy damage/death code decides whether to request hurt or death. citeturn8view1turn19view1

For pogo/downslash, use the confirmed responder seam—the equivalent of your `IHeroDownslashResponder` path—as the decisive trigger point. The bounce sound should happen when the bounce is actually granted, not merely when the attack was an attempted downslash. For hit-stop and camera shake, fire the audio on the same frame as hit confirmation and any feedback request, not after the freeze begins. If you later add More Mountains Feel, let it be a **consumer** of the same confirmed combat result, not a second audio owner. citeturn13view0turn8view1

To avoid repetitive sounds in rapid combat, combine three light techniques rather than one heavy one: a small clip variant pool, restrained pitch jitter, and a short per-cue cooldown when the same event can fire in bursts. Use narrower pitch variance on recognisable body sounds and broader variance on noisy transients. Unity’s pitch property makes this cheap, but it is still worth being conservative; large pitch swings on footsteps, hurt barks, or signature hero cues sound fake quickly. citeturn9view3

**Hero movement audio.** Jump, land, dash, wall jump, hurt, death, and terrain impact should stay in `HeroAudioController`. Jump, dash, and wall jump should trigger on committed state transitions in their action classes. Land and terrain impact should trigger from grounded/collision transitions in motor or state logic. Wall slide should be a local loop whose lifecycle is controlled by slide state instead of repeated one-shots. Footsteps should be animation-timed when your animation cadence is reliable; Unity’s examples specifically call out footsteps as a good use of animation events. If the animation is not reliable, a movement-distance ticker is the fallback, but not the first choice. citeturn9view2turn13view0

Future hero ability sounds follow the same ownership rule. If a sound is attached to the hero’s body and tightly coupled to a fixed hero state—charge hum, cloak burst, wind-up loop—it belongs in `HeroAudioController`. If it belongs to a spawned projectile, trap, spell area, companion, or remote world interaction, it should go through `AudioManager` or through a generic world emitter. Because every source can still be routed to the correct mixer group, this is an ownership decision, not a technical limitation. citeturn9view0turn8view0

**Music and atmosphere.** Treat music and ambience as separate categories, even if both live under `AudioManager`. Music should be scene- or high-level flow-driven through `SceneAudioCue` assets applied by `GameManager.BeginSceneTransition` or scene flow. Ambience should be zone- or cue-driven through `AtmosphereCue` assets that can layer cave, wind, water, rain, machine hum, or interior beds. Unity’s AudioMixer snapshots are designed for interpolated transitions between mixer states, and that makes them very suitable for ducking, low-health treatment, pause states, or boss-tension emphasis. citeturn8view0turn10view1turn8view2

For clip-to-clip music changes, add a second dedicated music source once you want true crossfades. Snapshots interpolate mixer state; they do not themselves swap one music clip assignment for another, so the simplest crossfade pattern is two music channels under the same bus with central ownership deciding which one is fading in or out. That last implementation detail is an inference from Unity’s snapshot model, but it is the most practical route for a vertical slice. citeturn8view0turn10view1

To stop scene objects fighting over music, designate one owner. Scene flow sets baseline music. Zone triggers may request ambience changes. Only explicit high-priority systems—such as a boss-room controller or a temporary narrative beat—are allowed to override music, and even then they should do so through the central service. `sceneLoaded` is the natural seam for establishing the baseline because Unity raises it centrally after load. citeturn12view1turn12view2

What I would **defer** for now is adaptive stem systems, combat-music state graphs, complex reverb send networks, and middleware-style parameter webs. For a polished portfolio slice, disciplined ownership, solid mixer routing, convincing combat timing, one music crossfade path, and one ambience path will produce more player-facing quality than a giant audio framework. Unity’s own pooling advice points in the same strategic direction: do not add lifecycle complexity before gameplay proves you need it. citeturn18view0turn8view0

## Phased plan and Codex prompt

The safest order is to lock down **ownership and routing first**, then add the mixer layer, then add cue assets and scene flow, and only after that widen scope into ambience and richer transitions. That sequence uses Unity’s strengths—asset-based configuration, source-to-group routing, scene callbacks, and pooled reuse—without forcing a rewrite. citeturn8view2turn9view0turn12view1turn17view0

**Phase one — routing audit and guardrails**

- **Goal:** establish a one-owner rule for playback.
- **Files likely touched:** `AudioManager`, `HeroAudioController`, `HeroAttackModule`, hero action classes, enemy damage/death/recoil classes, `GameManager`, Boot/bootstrap scripts, and any file containing `AudioSource`, `PlayOneShot`, `Play`, or `PlayClipAtPoint`.
- **Behaviour to add/change:** remove or encapsulate direct playback calls outside approved owners; add null-safe early returns; add warn-once development logs for missing clips or missing source references.
- **What not to change yet:** mixer asset, settings UI, ambience system, combat crossfades.
- **Acceptance criteria:** only approved classes directly call playback APIs; current game behaviour is unchanged; music no longer risks being started from scene object lifecycle methods.
- **Manual playtest checklist:** boot the game from a cold start; jump/dash/wall slide/attack; kill several enemies quickly; transition scenes; verify no duplicated hurt/death or unexpected music restarts.

**Phase two — mixer integration without call-site churn**

- **Goal:** introduce a stable bus tree before more features pile on.
- **Files likely touched:** `MainAudioMixer`, `AudioManager` prefab, hero prefab audio sources, any pooled SFX source template, settings/bootstrap service.
- **Behaviour to add/change:** route all current sources to mixer groups; expose top-level volume parameters; apply saved/default levels in `Start` or later.
- **What not to change yet:** no new audio event bus, no ambience layering, no boss music logic.
- **Acceptance criteria:** hero-local and global sounds all respond to mixer bus changes; current call sites remain largely unchanged.
- **Manual playtest checklist:** lower `Music` only; lower `SFX` only; confirm hero-local sounds still route through the correct bus; confirm scene transitions preserve mixer state.

**Phase three — formal hero audio API and combat-result ownership**

- **Goal:** make the hybrid split explicit and hard to violate.
- **Files likely touched:** `HeroAudioController`, hero action classes, `HeroAttackModule`, enemy health/death classes.
- **Behaviour to add/change:** replace any raw source access with intent methods such as `PlayJump`, `PlayLand`, `StartWallSlideLoop`, `StopWallSlideLoop`, `PlayDash`, `PlayHurt`; add a “primary impact once per swing” flag in the attack pipeline.
- **What not to change yet:** no cue-asset migration for every existing sound.
- **Acceptance criteria:** movement sounds remain local; slash whooshes remain global; one swing produces one primary weapon impact even when it hits multiple targets.
- **Manual playtest checklist:** spam attacks into one enemy and into a group; test pogo/downslash; verify wall-slide starts and stops cleanly; verify hurt and death do not stack wrongly.

**Phase four — cue assets and scene ownership**

- **Goal:** move global audio configuration out of scattered clip fields.
- **Files likely touched:** new `SfxCue`, `MusicCue`, `SceneAudioCue`, and `AtmosphereCue` assets and their supporting scripts; `AudioManager`; `GameManager`.
- **Behaviour to add/change:** migrate global/slash/enemy/UI/music clips to cue assets; let scene flow apply a baseline scene cue; keep hero-local fixed sources unchanged.
- **What not to change yet:** no complex adaptive music, no zone-stack priority system beyond what is needed immediately.
- **Acceptance criteria:** a scene’s baseline music can be changed by editing a cue asset rather than rewriting scene object code; enemy/UI/slash cues become easier to rebalance centrally.
- **Manual playtest checklist:** swap a scene music cue and verify the new clip plays without code edits; retune a slash pitch range in one place and verify all relevant attacks inherit it.

**Phase five — ambience baseline and simple crossfades**

- **Goal:** add atmosphere without destabilising the slice.
- **Files likely touched:** `AudioManager`, music source setup, any new ambience source setup, `SceneAudioCue`, `AtmosphereCue`, optional zone trigger scripts.
- **Behaviour to add/change:** add a second music source for clip crossfade; add one ambience channel or A/B ambience pair; let scene/zone cues request ambience changes centrally.
- **What not to change yet:** no middleware-like state graph, no elaborate stem system.
- **Acceptance criteria:** scene transitions can crossfade music; atmosphere can change by zone or cue without arbitrary scene object ownership conflicts.
- **Manual playtest checklist:** transition between two scenes with different music; move into and out of an ambience zone; verify ambience and music volumes remain independently controllable.

**Codex prompt**

```text
Audit this Unity metroidvania repository’s audio architecture before making any code changes.

Important constraints:
- Do NOT modify files yet.
- Do NOT propose a large rewrite.
- Preserve the modular architecture.
- HeroController must remain a coordinator.
- Hero actions should stay plain C# action classes where possible.
- HeroMotor owns Rigidbody2D velocity writes.
- AudioManager must not reference HeroController, EnemyController, or gameplay MonoBehaviours directly.
- Music must not be started from random scene object Awake/Start methods.

Repository audit targets:
- AudioManager
- HeroAudioController
- HeroAttackModule
- HeroController
- hero action classes such as jump, dash, wall slide, wall jump, attack
- enemy health / recoil / death classes
- GameManager
- boot / bootstrap setup
- any audio-related ScriptableObjects or config fields

Search specifically for:
- AudioSource
- Play()
- PlayOneShot()
- Stop()
- PlayClipAtPoint()
- DontDestroyOnLoad
- SceneManager.sceneLoaded
- AudioMixer
- outputAudioMixerGroup
- music start / scene transition / cue logic

Produce exactly these outputs:

1. Current audio map
- Which classes currently own audio playback
- Which classes request audio
- Which systems are persistent vs scene-local
- Which sounds are hero-local vs global

2. Direct AudioSource usage audit
- List every file that directly calls AudioSource.Play / PlayOneShot / Stop / PlayClipAtPoint
- For each call site, say whether it matches intended routing rules

3. Routing rule violations
- Identify any direct playback outside AudioManager and HeroAudioController
- Identify any music-start logic outside central scene flow / GameManager
- Identify any coupling where AudioManager depends on gameplay controllers
- Identify duplicate or inconsistent audio consequence logic

4. Risk assessment
- Null-safety issues
- Missing fallback behaviour
- Pooled-source dirty-state risks
- Likely duplicate impact / hurt / death trigger paths
- Whether the hero-local audio exception is currently clean or becoming debt

5. Smallest safe next implementation task
- Recommend the single smallest, safest PR-sized task
- Explain why it is the best next step
- List the files likely touched
- State what should explicitly NOT be changed yet

6. Manual playtest checklist
- Give a concise Unity playtest checklist for verifying the recommended task

Prefer grep-friendly evidence and concrete file names over general advice.
If the repo contains no clear audio entry points, say so explicitly rather than guessing.
```
