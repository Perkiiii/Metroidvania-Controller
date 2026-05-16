\# Pogo / Downslash Bounce Reference Research

This document is reference research only. Do not implement the recommended code snippets directly.

The reference project uses a more complex downspike state machine with animation-timed bounce events, jump-step counters, recovery states, invulnerability frames, and NonBouncer opt-outs.

For this project, the first implementation should be much smaller:

\- Trigger pogo only from a successful downslash hit.  
\- Start with enemies only.  
\- Apply upward bounce through \`HeroMotor\`.  
\- Store tuning in \`HeroConfig\`.  
\- Prevent multiple pogo bounces from one attack swing.  
\- Do not add hazard pogo yet.  
\- Do not add downspike anticipation yet.  
\- Do not add special bounce animation yet.  
\- Do not add new recovery states yet.  
\- Do not call animation logic from \`HeroMotor\`.  
\- Do not copy the reference state machine directly.

Use the research below for design context, not as a direct implementation plan.

\---  
\# Pogo\_DownslashBounce\_Reference

Author: research summary of Hollow Knight: Silksong reference pogo/downslash handoff    
Purpose: A compact, implementation-useful reference for creating a baseline pogo/downslash bounce mechanic in another Unity project. Contains flow, concepts to adapt, mapping to a custom architecture, recommended first-implementation snippets, tuning, and testing checklist.

\---

\#\# Summary  
This doc condenses a reverse-engineering of Silksong's downslash bounce (pogo) behavior into a minimal, portable design you can adapt. Key points:  
\- Bounce is triggered by an animation frame event (not directly by collision callback).  
\- A queued flag prevents multiple bounces per single slash animation.  
\- Bounce uses a frame-counted jump (countdown \`jump\_steps\`) to apply a fixed-duration upward velocity rather than impulse based on incoming velocity.  
\- A simple \`NonBouncer\` component opt-outs targets from being bounceable.  
\- The hero enters a short invuln window on bounce start and goes into recovery afterwards.

Key reference files (in the workspace)  
\- HeroController (reference): HeroController.cs    
\- Slash / attack: NailSlash.cs    
\- Non-bounce opt-out: NonBouncer.cs    
\- Animation controller: HeroAnimationController.cs

\---

\#\# Baseline Reference Pogo Flow  
1\. Player issues Down \+ Attack while airborne. Controller goes into downslash animation.  
2\. Slash component activates collider and damage logic (attacker \-\> \`DamageEnemies\` / \`Damage\` subsystem).  
3\. Animation plays; the slash animation contains a frame marker named "Bounce".  
4\. When the "Bounce" animation event fires, \`NailSlash\` logic checks a \`queuedDownspikeBounce\` flag and whether the target is bounceable. If OK, it calls the hero controller method that applies the bounce.  
5\. \`HeroController.DownspikeBounce(...)\` sets \`downSpikeBouncing\` state, initializes \`jump\_steps\` (e.g., 30), sets a small invulnerability counter, optionally plays bounce animation variant, and starts the upward motion logic.  
6\. In physics (FixedUpdate), while \`jump\_steps \> 0\` the hero's vertical velocity is forcibly set to a configured upward speed each fixed step; \`jump\_steps\` decrements.  
7\. When \`jump\_steps\` reaches 0, the bouncing state ends and a downspike recovery state/timer is entered.  
8\. Multi-bounce from a single slash prevented by \`queuedDownspikeBounce\` boolean and by clearing/consuming the queue on use.

\---

\#\# Essential Concepts To Adapt (minimum for a good feel)  
\- Animation-timed trigger: fire bounce based on animation event, not collision timing.  
\- Queue/consume flag: prevents multiple bounce activations per swing.  
\- Jump-steps countdown: apply upward velocity for N FixedUpdate frames to get consistent bounce height.  
\- Bounce state(s): flags to track \`bouncing\`, \`downSpiking\`, \`downSpikeRecovery\`.  
\- NonBouncer opt-out: component or flag on targets to disable bounce.  
\- Short invulnerability at bounce start: prevent immediate re-hits or stacked collisions.  
\- Gravity control/clamping during antic and bounce phases.

\---

\#\# Optional / Defer  
(Implement later; not required for a baseline)  
\- Antic phase fidelity (the pre-thrust "antic" animation).  
\- Multiple bounce variants (short/sightly-short/full).  
\- Harpoon/crest/weapon-specific recoil or special-case logic.  
\- Hit-stop/frame-freeze.  
\- Camera shake / fancy particles and VFX.  
\- Per-crest gameplay interactions (warrior/reaper/hunter).  
\- Silk/energy generation nuances tied to first-hit conditions.

\---

\#\# Mapping To A Typical Custom Architecture (your project)  
Reference responsibility → suggested equivalent (first pass)  
\- Slash lifecycle \+ collision/damage → \`HeroAttackModule\` / \`HeroAttackAction\`  
\- Damage / hit meta → \`HeroAttackHit\` / \`HeroAttackHit\` struct (minimal fields)  
\- Bounce trigger (animation event) → \`HeroAttackModule\` invokes callback to motor  
\- Bounce application → \`HeroMotor.ApplyDownspikeBounce(...)\`  
\- State storage → \`HeroStateBlackboard\` (add flags: \`isDownspikeBouncing\`, \`downspikeRecoveryTimer\`, \`downspikeInvulFrames\`, \`isBouncePending\`)  
\- Config / tuning → \`HeroConfig\` (fields: \`DownspikeBounceSteps\`, \`DownspikeBounceVelocity\`, \`DownspikeRecoveryTime\`)  
\- Non-bounce opt-out → small \`NonBouncer\` component or \`IBounceable\` flag on \`IHeroAttackReceiver\` targets  
\- Visuals → your anim system (\`YourAnimController\`) should play bounce animation when state set

\---

\#\# Minimal Data Structures (recommended)

Example \`HeroAttackHit\` minimal fields (C\#):

\`\`\`csharp  
public struct HeroAttackHit {  
    public bool IsFirstHit;        // true for first application on a target per swing (optional)  
    public bool BounceEligible;    // true if the hit should allow pogo  
    public int Damage;             // damage amount  
    public Vector2 HitPosition;  
}  
\`\`\`

Example \`HeroConfig\` additions:

\`\`\`csharp  
public class HeroConfig {  
    public float DownspikeBounceVelocity \= 10f; // upward velocity applied each fixed step  
    public int DownspikeBounceSteps \= 30;       // how many fixed frames to apply upward velocity  
    public int DownspikeInvulnFrames \= 5;       // frames of invuln after bounce start  
    public float DownspikeRecoveryTime \= 0.5f;  // seconds before normal actions resume  
}  
\`\`\`

Hero state blackboard additions:

\`\`\`csharp  
public class HeroStateBlackboard {  
    public bool isDownspikeBouncing \= false;  
    public int downspikeJumpStepsRemaining \= 0;  
    public int downspikeInvulnFrames \= 0;  
    public bool isBouncePending \= false; // queue flag set by attack/animation  
    public float downspikeRecoveryTimer \= 0f;  
}  
\`\`\`

\---

\#\# Recommended First Implementation (concrete steps \+ code snippets)

High-level plan:  
1\. Add config and state fields.  
2\. Make \`HeroAttackModule\` raise an explicit \`OnBounceEvent\` (animation-timed), set \`isBouncePending\` on slash start and clear appropriately.  
3\. On animation event, if \`isBouncePending\` and the hit target is bounceable, call \`HeroMotor.TriggerDownspikeBounce(bounceConfig)\`.  
4\. \`HeroMotor\` sets jump steps and invuln frames and flips to bouncing state; in \`FixedUpdate\` apply upward velocity while steps \> 0\.  
5\. Set recovery timer and disable/enable input as needed.

Key code snippets (adapt to your class names):

HeroAttackModule (animation event handler):

\`\`\`csharp  
// Called by animation event "Bounce" on the slash animation  
public void OnSlashAnimationBounceEvent() {  
    if (\!stateBlackboard.isBouncePending) return;  
    stateBlackboard.isBouncePending \= false;

    // Determine if current hit target allows bounce.  
    if (currentHitTarget \!= null) {  
        var nb \= currentHitTarget.GetComponent\<NonBouncer\>();  
        if (nb \!= null && nb.active) return;  
    }

    heroMotor.TriggerDownspikeBounce(heroConfig.DownspikeBounceSteps, heroConfig.DownspikeBounceVelocity);  
}  
\`\`\`

HeroMotor (apply bounce):

\`\`\`csharp  
public void TriggerDownspikeBounce(int steps, float upVelocity) {  
    if (stateBlackboard.isDownspikeBouncing) return;  
    stateBlackboard.isDownspikeBouncing \= true;  
    stateBlackboard.downspikeJumpStepsRemaining \= steps;  
    stateBlackboard.downspikeInvulnFrames \= heroConfig.DownspikeInvulnFrames;  
    stateBlackboard.downspikeRecoveryTimer \= 0f;  
    this.downspikeUpVelocity \= upVelocity;  
    // force any necessary animation trigger:  
    animController.PlayDownspikeBounceAnim();  
}  
\`\`\`

HeroMotor FixedUpdate skeleton:

\`\`\`csharp  
private void FixedUpdate()  
{  
    // other physics...

    if (stateBlackboard.downspikeInvulnFrames \> 0\) {  
        stateBlackboard.downspikeInvulnFrames--;  
    }

    if (stateBlackboard.isDownspikeBouncing) {  
        if (stateBlackboard.downspikeJumpStepsRemaining \> 0\) {  
            rb2d.velocity \= new Vector2(rb2d.velocity.x, downspikeUpVelocity);  
            stateBlackboard.downspikeJumpStepsRemaining--;  
        } else {  
            // bounce ended  
            stateBlackboard.isDownspikeBouncing \= false;  
            stateBlackboard.downspikeRecoveryTimer \= 0f;  
            stateBlackboard.isDownspikeRecovering \= true;  
        }  
    }

    if (stateBlackboard.isDownspikeRecovering) {  
        stateBlackboard.downspikeRecoveryTimer \+= Time.deltaTime;  
        if (stateBlackboard.downspikeRecoveryTimer \>= heroConfig.DownspikeRecoveryTime) {  
            stateBlackboard.isDownspikeRecovering \= false;  
        } else {  
            // optionally clamp movement/disable certain inputs  
        }  
    }

    // regular Jump() / DoubleJump() logic may also read jump-steps counters  
}  
\`\`\`

Prevent double-bounce per swing:  
\- On \`StartSlash()\` set \`stateBlackboard.isBouncePending \= true\` and store \`slashInstanceId\`.    
\- On \`OnSlashAnimationBounceEvent\`, consume the flag (\`isBouncePending \= false\`) and record \`lastBounceFrame \= Time.frameCount\`.    
\- In \`TriggerDownspikeBounce\` check \`if (Time.frameCount \== lastBounceFrame) return;\` or use a \`bounceConsumed\` boolean per slash instance.

Minimal NonBouncer:

\`\`\`csharp  
public class NonBouncer : MonoBehaviour {  
    public bool active \= true;  
}  
\`\`\`

\---

\#\# System Architecture & Component Interactions (textual)  
\- HeroAttackAction (input) \-\> HeroAttackModule (attack lifecycle)  
  \- Responsible: starting slash animation, enabling collider/damager, tracking \`isBouncePending\`.  
  \- Emits: animation events (Bounce) or calls \`OnSlashAnimationBounceEvent()\`.

\- Damage / Hit system  
  \- Responsible: apply damage to enemy, create a \`HeroAttackHit\` struct for metadata (IsFirstHit, BounceEligible).  
  \- On successful hit, it may call back into \`HeroAttackModule\` to record that a bounce target was hit this swing.

\- HeroMotor (physics)  
  \- Responsible: receiving \`TriggerDownspikeBounce()\` call and applying jump frame-count logic; update states in blackboard; control gravity/clamping; manage recovery timers.

\- HeroStateBlackboard  
  \- Single source of truth for states (bouncing, recovering, invuln frames, queued bounce).

\- Animation system  
  \- Hooks to call \`OnSlashAnimationBounceEvent()\` exactly on the right frame.

\- Enemy components  
  \- May include \`NonBouncer\` to opt out.  
  \- Should expose a simple property to indicate bounce eligibility (\`IBounceable\` interface optional).

Sequence:  
1\. Input \-\> \`HeroAttackModule.StartSlash()\` \-\> sets \`isBouncePending \= true\`.  
2\. Collider/damager hits enemy \-\> \`DamageSystem\` applies damage and marks hit metadata (\`BounceEligible\`).  
3\. At animation bounce frame \-\> \`HeroAttackModule.OnSlashAnimationBounceEvent()\` sees \`isBouncePending\` and \`BounceEligible\` \-\> calls \`HeroMotor.TriggerDownspikeBounce()\`.  
4\. \`HeroMotor\` sets steps/flags and \`FixedUpdate\` applies upward velocity for N steps.

\---

\#\# Tuning parameters (recommended starting values)  
\- \`DownspikeBounceSteps \= 30\` — \~0.45–0.6s depending on FixedUpdate frequency / velocity chosen.  
\- \`DownspikeBounceVelocity \= 10f\` — adjust to match jump feel; ensure terminal velocity clamping afterward.  
\- \`DownspikeInvulnFrames \= 3\` — small window to avoid immediate re-hit.  
\- \`DownspikeRecoveryTime \= 0.5f\` — lockout or reduced options after bounce.

\---

\#\# Testing Checklist  
\- \[ \] Downslash while airborne triggers downward movement and a slash animation.  
\- \[ \] Hitting a bounceable enemy, then the animation "Bounce" frame, causes hero to bounce predictably upward.  
\- \[ \] Bounce height is consistent across approach velocities (uses jump\_steps).  
\- \[ \] Single slash does not produce multiple bounces (double-bounce prevention).  
\- \[ \] Hitting a \`NonBouncer\`-marked enemy does not produce a bounce.  
\- \[ \] Invulnerability frames prevent immediate re-damage when bouncing.  
\- \[ \] Recovery timer blocks normal downslash re-use on landing (if desired).  
\- \[ \] Animation for bounce plays correctly and aligns with physics.

\---

\#\# Implementation pitfalls & tips  
\- Animation timing: prefer an explicit animation event named e.g. \`"Bounce"\`; don't tie bounce to collider hit because hit detection can happen across frames.  
\- FixedUpdate consistency: use FixedUpdate for physics-driven \`jump\_steps\` so the bounce duration is stable across frame rates.  
\- Collider vs animation timing: the game should apply damage on collider, but the bounce trigger is strictly animation-driven (this avoids visual/feel mismatches).  
\- Avoid cumulative velocities: when applying upward velocity each step, set Y directly rather than adding, to prevent stacking with existing vertical velocity.  
\- If using root motion or complex animator overrides, ensure the bounce animation doesn't also modify vertical position (or account for it).  
\- Keep the bounce code isolated: \`HeroMotor.TriggerDownspikeBounce()\` should be the only method that flips the bounce flags — event sources should only call it.

\---

\#\# Appendix — Useful reference locations in the Silksong codebase  
\- Reference hero logic: HeroController.cs    
\- Slash / animation event & queued bounce: NailSlash.cs    
\- Non-bouncer opt-out component: NonBouncer.cs    
\- Animation controller (variants & bounce animation): HeroAnimationController.cs

\---

If you want, I can:  
\- produce a ready-to-save markdown file for you to paste into Pogo\_DownslashBounce\_Reference.md, or  
\- produce a small patch (diff) that creates this file in the repo (I won't modify files unless you permit). Which do you prefer?

Created 7 todos  
