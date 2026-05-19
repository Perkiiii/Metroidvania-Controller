\# Ability Spec — Drift Cloak / Updraft Riding

\#\# Status

Planned / Future Ability.

Do not implement this ability until the core hero feel, ability unlock system, and current traversal kit are stable.

This document describes the intended design for a future traversal ability inspired by the idea of holding Jump in mid-air to glide, slow-fall, and ride environmental updrafts.

\---

\#\# High-Level Concept

The Drift Cloak is a traversal ability that gives the hero controlled aerial descent and lets them interact with authored wind/updraft zones in the world.

Once unlocked, the ability has two related behaviours:

1\. \*\*Glide / Slow Fall\*\*  
   \- While airborne, holding Jump slows the hero’s fall.  
   \- This gives the player more control when descending from high places.  
   \- It does not give infinite height by itself.

2\. \*\*Updraft Riding\*\*  
   \- When the hero is inside an authored updraft/wind zone, holding Jump lets them ride the current upward.  
   \- This turns wind columns into traversal routes, shortcuts, puzzle elements, and optional exploration gates.  
   \- The player can enter and exit the updraft behaviour freely by holding or releasing Jump.

The ability should feel like a controlled drift, not full flight.

\---

\#\# Design Goals

\- Add a new traversal verb without replacing jump, wall jump, double jump, or air dash.  
\- Let level design create vertical routes using wind/updraft volumes.  
\- Give the player more control while falling from high places.  
\- Preserve tight input feel: tap Jump still means jump/double jump; hold Jump means drift/glide/updraft.  
\- Avoid hidden input forgiveness or automatic movement.  
\- Keep the ability readable, intentional, and easy to author in levels.  
\- Keep velocity writes inside \`HeroMotor\`.

\---

\#\# Player Fantasy

The hero gains a cloak, mantle, wing, feather, leaf, or similar movement tool that catches the air.

Outside wind zones, it lets the hero slow their fall.

Inside wind zones, it lets the hero rise with the current and reach new areas.

This should feel like the hero is using the environment, not simply flying.

\---

\#\# Input Model

The ability uses the existing Jump input.

\#\#\# Core Input Rules

| Input | Result |  
|---|---|  
| Press Jump while grounded | Normal jump |  
| Tap Jump while airborne and double jump is unused | Double jump |  
| Hold Jump while airborne and falling | Glide / slow fall |  
| Hold Jump while airborne inside an updraft zone | Ride the updraft upward |  
| Release Jump while gliding/updrafting | Exit glide/updraft and return to normal air movement |  
| Hold Jump again while airborne | Re-enter glide/updraft if conditions are valid |

The key distinction is:

\`\`\`text  
Tap Jump \= spend a jump.  
Hold Jump \= drift/glide/updraft mode.

The Drift Cloak should read `JumpHeld`, but it should not consume the jump buffer.

---

## **Important Interaction Rule**

Updraft riding and glide must **not consume double jump**.

If the hero enters an updraft before using double jump, the double jump should remain available.

Example:

Ground jump  
→ hold Jump inside updraft  
→ hero rides upward  
→ release Jump  
→ tap Jump again  
→ double jump triggers  
→ hold Jump again  
→ hero re-enters glide/updraft

This means updraft riding is an environmental movement mode, not an air resource.

---

## **Re-Entry Rule**

The player should be able to enter and exit glide/updraft repeatedly while airborne.

Example:

Hero has used all jumps  
→ hero is falling  
→ player holds Jump  
→ hero glides or rides updraft  
→ player releases Jump  
→ hero falls normally  
→ player holds Jump again  
→ hero re-enters glide/updraft

There should be no “drift used” flag by default.

The ability is controlled by current conditions:

* ability unlocked  
* airborne  
* Jump held  
* not dashing  
* not hurt/dead/control locked  
* not wall latched  
* valid drift or updraft conditions

---

## **Ability Priority**

The ability must sit below discrete jump actions in priority.

Recommended action priority:

1. Hurt / dead / control locked  
2. Dash  
3. Wall latch / aimed wall launch  
4. Wall jump  
5. Ground jump  
6. Double jump  
7. Drift / glide / updraft

This prevents held Jump from stealing input that should become a jump, wall jump, or double jump.

---

## **Interaction With Other Abilities**

### **Normal Jump**

Normal jump always has priority over drift.

If the player jumps from the ground and keeps holding Jump, the hero should not immediately enter slow-fall while still rising.

Recommended rule:

Normal glide can only start once the hero is falling or near the apex.

A useful config value:

driftStartMaxUpwardVelocity \= 0f;

This means glide only starts when the hero’s vertical velocity is less than or equal to zero.

### **Double Jump**

Double jump is triggered by a fresh Jump press while airborne.

Drift/updraft does not consume double jump.

If the player is currently holding Jump to ride an updraft, they must release and press Jump again to perform double jump, unless the input system later supports a deliberate “fresh press while held” rule.

Recommended behaviour:

Fresh Jump press \= double jump if available.  
Jump held \= drift/updraft if available.

After double jump, if Jump is still held and the hero is inside an updraft, updraft riding may resume.

Important: updraft should not clamp away the upward speed from double jump. If the hero’s current upward velocity is greater than the updraft rise speed, the updraft should not reduce it.

### **Air Dash**

Air dash cancels drift/updraft while active.

After dash ends, if the player is still holding Jump and is still airborne, drift/updraft may resume if conditions are valid.

Recommended rule:

Dash owns velocity while active.  
Drift/updraft resumes only after dash ends.

### **Wall Slide**

Wall slide should take priority over drift when the hero is touching and engaging with a wall.

If the hero leaves the wall and is still holding Jump, drift/updraft can activate again.

### **Wall Jump**

Wall jump has priority over drift/updraft.

If the player is wall sliding and presses Jump, the result should be wall jump, not glide.

### **Wall Latch / Aimed Wall Launch**

Wall latch fully suppresses drift/updraft while active.

Once the hero launches or exits latch, drift/updraft can resume if the player is airborne and holding Jump.

### **Pogo / Downslash Bounce**

Pogo bounce should override drift/updraft momentarily because it applies a deliberate upward bounce.

Recommended rule:

Downslash bounce sets vertical velocity.  
Drift/updraft can resume afterward if Jump is held and conditions are valid.

Optional later rule:

Successful pogo may refresh air dash or double jump.

Do not add that interaction in the first implementation.

### **Attacks**

Air attacks should not consume drift/updraft.

There are two possible design options:

1. **Allow drift while attacking**  
   * Air combat remains expressive.  
   * The hero can glide while slashing.  
   * Attack animation takes visual priority, but drift still affects physics.  
2. **Disable drift while attacking**  
   * Combat commitment is clearer.  
   * Air attacks are less safe.  
   * Easier first implementation.

Recommended first-pass rule:

Allow drift/updraft during normal air attacks, but let dash, hurt, wall latch, and pogo override it.

If this feels too safe, add a config option later.

---

## **Updraft Zone Design**

Updrafts should be authored as world trigger volumes.

Suggested component:

UpdraftZone

The zone should use a `BoxCollider2D` or `PolygonCollider2D` set as trigger.

### **UpdraftZone Fields**

Suggested fields:

public float riseSpeed \= 5f;  
public float acceleration \= 20f;  
public float horizontalInfluence \= 0f;  
public bool requiresDriftAbility \= true;  
public int priority \= 0;

### **Behaviour**

When the hero is inside an updraft zone:

* If Drift Cloak is unlocked and Jump is held, the hero rides upward.  
* If Jump is released, the hero is no longer actively riding the wind.  
* The hero may still be visually affected by wind particles, but should not be lifted strongly unless Jump is held.  
* If multiple updrafts overlap, the highest priority or strongest zone should win.

### **Why Updrafts Should Require Input**

The wind should not automatically carry the player upward at full strength.

Requiring JumpHeld gives the player agency:

Hold Jump \= engage with wind.  
Release Jump \= drop out of wind.

This makes updraft routes feel skillful instead of automatic elevators.

---

## **Glide Behaviour**

When the hero is airborne, falling, and holding Jump:

* Vertical velocity should move toward a slow fall speed.  
* Gravity should feel softened.  
* The hero should retain normal or slightly reduced horizontal air control.

Suggested starting values:

glideFallSpeed \= \-3f;  
glideAcceleration \= 18f;  
driftStartMaxUpwardVelocity \= 0f;

The glide should not make the hero hover in place. It should slow descent, not remove danger entirely.

---

## **Updraft Behaviour**

When the hero is inside an updraft zone and holding Jump:

* Vertical velocity should move upward toward the zone’s rise speed.  
* Updraft should not reduce stronger upward velocity from jump or double jump.  
* Updraft should feel like a current, not like a teleport or instant launch.

Suggested starting values:

updraftRiseSpeed \= 5f;  
updraftAcceleration \= 20f;

If the hero enters an updraft while already moving upward faster than the rise speed, preserve the current upward velocity until it naturally falls below the updraft target.

---

## **Required Ability Flag**

The ability should be gated through `PlayerAbilityState`.

Suggested flag:

public bool driftCloakUnlocked;

Optional future split:

public bool glideUnlocked;  
public bool updraftRideUnlocked;

Recommended design: keep glide and updraft riding as one unlock unless the game specifically needs them separated.

---

## **Required Blackboard Fields**

Suggested fields:

public bool drifting;  
public bool updraftRiding;  
public bool inUpdraft;

Optional:

public float activeUpdraftStrength;

Keep these as state/readability fields. The actual velocity changes should still happen through `HeroMotor`.

---

## **Required HeroConfig Fields**

Suggested config fields:

\[Header("Drift Cloak")\]  
public float glideFallSpeed \= \-3f;  
public float glideAcceleration \= 18f;  
public float driftStartMaxUpwardVelocity \= 0f;  
public float updraftRiseSpeed \= 5f;  
public float updraftAcceleration \= 20f;

Optional future tuning fields:

public float driftAirControlMultiplier \= 1f;  
public float driftExitCooldown \= 0f;  
public float updraftMaxRiseSpeed \= 7f;  
public float updraftWeakRiseSpeed \= 2f;

Do not add optional fields until they are needed.

---

## **Required Classes**

Future implementation will likely need:

HeroDriftAction  
UpdraftZone  
HeroUpdraftDetector

### **HeroDriftAction**

Plain C\# action class responsible for:

* checking unlock state  
* reading JumpHeld  
* checking airborne state  
* deciding whether to glide or ride updraft  
* writing drift/updraft state to blackboard  
* requesting velocity changes from HeroMotor

It should not write Rigidbody2D velocity directly.

### **UpdraftZone**

Scene-authored trigger volume representing wind/updraft areas.

Responsible for:

* exposing rise speed  
* exposing acceleration  
* exposing priority  
* providing zone data to the hero detector

### **HeroUpdraftDetector**

MonoBehaviour on the hero or hero child.

Responsible for:

* tracking which updraft zones the hero is currently inside  
* selecting the active updraft zone if multiple overlap  
* exposing active zone data to HeroDriftAction

This avoids making HeroDriftAction perform trigger logic.

---

## **Required HeroMotor Methods**

Suggested motor methods:

public void ApplyGlide(float fixedDeltaTime, float fallSpeed, float acceleration)  
public void ApplyUpdraft(float fixedDeltaTime, float riseSpeed, float acceleration)

Rules:

* Do not move velocity writes outside HeroMotor.  
* Glide should only affect vertical velocity.  
* Updraft should only affect vertical velocity unless future level design requires wind to push horizontally.  
* Do not clamp stronger upward velocity downward during updraft.

---

## **Animation Requirements**

Future animation slots may include:

glide  
updraftRide

Visual priority should likely be:

1. Dead  
2. Hurt  
3. Attack  
4. Dash  
5. Wall latch  
6. Wall slide  
7. Wall jump  
8. Updraft ride  
9. Glide  
10. Jump / fall / locomotion

If attack while drifting is allowed, attack animation should display while drift physics continues.

---

## **VFX / Audio Requirements**

### **Glide**

Possible feedback:

* cloak/leaf/wing flutter loop  
* subtle trailing particles  
* light wind sound while held  
* small cloth animation

### **Updraft Ride**

Possible feedback:

* wind column particles  
* stronger cloak flutter  
* upward streaks  
* wind loop volume increases while JumpHeld  
* small camera framing adjustment in tall shafts

### **Enter / Exit**

Optional one-shot effects:

* enter drift puff  
* exit drift flutter  
* enter updraft whoosh

Do not add these until the mechanical behaviour feels good.

---

## **Camera Considerations**

Updraft routes may need camera support because they often move the hero vertically through tall spaces.

Potential future camera signals:

updraftRiding started  
updraftRiding ended  
vertical traversal intent

Camera should stay decoupled from hero internals. Any camera behaviour should go through the existing one-way signal/event pattern rather than direct references from camera scripts into hero actions.

---

## **Level Design Uses**

The Drift Cloak / Updraft ability can support several metroidvania level design patterns.

### **Vertical Wind Shafts**

Tall columns of wind that let the hero rise to new areas.

### **Controlled Descent Rooms**

Large drops where the hero can glide to avoid hazards or reach side platforms.

### **Optional Collectibles**

Coins, seeds, charms, resources, or secrets placed along wind paths.

### **Route Reversal**

A room that was previously a dangerous fall becomes an upward shortcut after the ability is unlocked.

### **Soft Gates**

The player may technically enter an area early, but without glide/updraft they cannot cross safely or climb high enough.

### **Farming / Nature Integration**

If the wider game includes farming or resource systems, updrafts can be tied to:

* giant plants  
* wind flowers  
* spore vents  
* bellows  
* seasonal gusts  
* restored garden machinery  
* environmental upgrades that activate wind routes

This makes the ability fit the world rather than feeling like a generic movement upgrade.

---

## **Progression Role**

Recommended progression placement:

After wall jump, before or around air dash.

Suggested progression chain:

Wall Jump  
→ Drift Cloak / Updraft Riding  
→ Air Dash  
→ Double Jump  
→ Wall Latch / Aimed Wall Launch

Alternative progression chain:

Wall Jump  
→ Double Jump  
→ Drift Cloak / Updraft Riding  
→ Air Dash  
→ Wall Latch / Aimed Wall Launch

The ability should probably not be available from the start. It changes how the player reads vertical space and should open a new class of routes.

---

## **Design Risks**

### **Risk: Too Much Vertical Freedom**

If wall jump, double jump, glide, updraft, wall latch, and air dash all exist, the player may become too powerful in vertical spaces.

Mitigation:

* Use stamina-free glide, but make it descend slowly rather than hover.  
* Make updraft movement require authored zones.  
* Use ability-specific gates instead of relying only on raw jump height.  
* Tune double jump height carefully.

### **Risk: Double Jump Feels Redundant**

If updrafts are common and glide is too strong, double jump may feel less important.

Mitigation:

* Double jump gives instant height anywhere.  
* Updraft gives sustained height only in specific zones.  
* Glide gives descent control, not upward movement outside wind zones.

### **Risk: Holding Jump After Normal Jump Starts Glide Too Early**

If holding Jump immediately triggers glide, jumps may feel floaty or wrong.

Mitigation:

* Normal glide only starts when falling or near apex.  
* Updraft riding can start earlier if inside an updraft zone.

### **Risk: Updraft Cancels Double Jump Feel**

If updraft caps vertical velocity, it may weaken double jump.

Mitigation:

* Updraft should never clamp stronger upward velocity downward.  
* It should only lift the hero if current vertical velocity is below the updraft target.

### **Risk: Player Cannot Tell When Updraft Is Usable**

Mitigation:

* Strong wind visuals.  
* Audio loop.  
* Particles moving upward.  
* Cloak flutter when JumpHeld.  
* Optional UI/animation cue when ability is unlocked.

---

## **First Implementation Scope**

When this ability is eventually implemented, the first pass should be small.

### **First Pass Should Include**

* `driftCloakUnlocked` flag  
* `HeroDriftAction`  
* basic glide while falling and holding Jump  
* basic `UpdraftZone`  
* updraft riding while inside zone and holding Jump  
* blackboard flags for `drifting` and `updraftRiding`  
* motor-owned vertical velocity changes

### **First Pass Should Not Include**

* complex VFX  
* camera changes  
* stamina cost  
* horizontal wind push  
* multiple wind strengths beyond basic zone values  
* glide attack variants  
* special animation blending  
* air dash refresh  
* double jump refresh  
* updraft puzzles with moving zones

---

## **Acceptance Criteria For Future Implementation**

* Holding Jump while falling slows descent.  
* Releasing Jump exits glide immediately.  
* Holding Jump again while still airborne re-enters glide.  
* Holding Jump inside an updraft moves the hero upward.  
* Releasing Jump inside an updraft lets the hero fall normally.  
* Updraft/glide does not consume double jump.  
* If double jump is unused, the player can still double jump after using updraft.  
* Air dash cancels glide/updraft while active.  
* Glide/updraft can resume after dash if Jump is still held and conditions are valid.  
* Wall latch suppresses glide/updraft while active.  
* Pogo bounce overrides drift/updraft vertical velocity.  
* All hero velocity writes remain inside HeroMotor.  
* Missing ability unlock state fails safely.

---

## **Manual Playtest Checklist**

When implemented later, test:

* Jump, hold Jump, fall into glide.  
* Jump, release Jump, fall normally.  
* Jump, fall, hold Jump again, re-enter glide.  
* Jump into updraft while holding Jump, ride upward.  
* Release Jump inside updraft, fall out.  
* Re-hold Jump inside updraft, ride upward again.  
* Jump into updraft, do not use double jump, confirm double jump remains available.  
* Use double jump inside or near updraft, confirm updraft does not weaken the double jump.  
* Use all jumps, fall, hold Jump, confirm glide/updraft still works.  
* Air dash while gliding, confirm dash cancels glide.  
* After dash ends, hold Jump, confirm glide/updraft can resume.  
* Wall slide near updraft, confirm wall slide/wall jump priority is correct.  
* Wall latch near updraft, confirm latch suppresses drift.  
* Downslash pogo while holding Jump, confirm pogo bounce wins.  
* Enter a tall vertical wind shaft, confirm camera remains readable.

---

## **Summary**

The Drift Cloak / Updraft ability is a future traversal unlock that adds controlled falling and environmental vertical movement.

Its core design rule is:

Tap Jump spends jump resources.  
Hold Jump engages drift/updraft movement.

The ability should not consume double jump, should be freely enterable/exitable while airborne, and should rely on authored updraft zones for upward traversal.

It fits the game best as a mid-game traversal unlock that makes the player reinterpret vertical rooms, wind shafts, garden vents, and environmental shortcuts.

The key thing I’d preserve from this spec is the input philosophy:

\*\*tap \= jump resource, hold \= drift/updraft mode.\*\*

That keeps the ability from fighting double jump later.

