using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AbilityPickupTests
{
    [Test]
    public void OnTriggerEnter2D_HeroDetected_GrantsAbilityAndRecordsPickupConsumption()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        GameObject heroGo = new GameObject("Hero");
        try
        {
            Assert.That(abilityState.IsUnlocked(AbilityId.SpiritCast), Is.False);

            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_1", abilityState, AbilityId.SpiritCast, registry);
            BoxCollider2D heroCollider = AddHeroBox(heroGo);

            InvokePrivate(pickup, "OnTriggerEnter2D", heroCollider);

            Assert.That(abilityState.IsUnlocked(AbilityId.SpiritCast), Is.True);
            Assert.That(registry.IsPickupCollected("pickup_1"), Is.True);
            Assert.That(pickupGo.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(heroGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    // Inconsistent direction 1: PlayerAbilityState says unlocked, WorldStateRegistry has no
    // collectedPickupIds record. PlayerAbilityState is authoritative, so the pickup is suppressed
    // and the registry is brought into line -- never the other way around.
    [Test]
    public void Awake_AbilityAlreadyUnlocked_ReconcilesMissingPickupRecordWithoutReplayingFeedback()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        try
        {
            abilityState.Unlock(AbilityId.SpiritCast);
            int changedCount = 0;
            abilityState.AbilityChanged += (_, _) => changedCount++;

            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_2", abilityState, AbilityId.SpiritCast, registry);
            Assert.That(registry.IsPickupCollected("pickup_2"), Is.False, "Precondition: registry has no record yet.");

            // The automatic Awake fired by AddComponent above ran with unset fields (a harmless
            // no-op); invoke it again now that the real fields are assigned, exactly mirroring how
            // a scene-placed pickup's single real Awake would see fully-assigned Inspector fields.
            InvokePrivate(pickup, "Awake");

            Assert.That(registry.IsPickupCollected("pickup_2"), Is.True, "Missing pickup record must be reconciled.");
            Assert.That(pickupGo.activeSelf, Is.False, "Already-owned ability must suppress the pickup.");
            Assert.That(changedCount, Is.EqualTo(0), "Reconciliation must not fire AbilityChanged (no feedback replay).");
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_ConsistentlyConsumed_SuppressesQuietly()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        try
        {
            abilityState.Unlock(AbilityId.SpiritCast);
            registry.MarkPickupCollected("pickup_3");

            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_3", abilityState, AbilityId.SpiritCast, registry);
            InvokePrivate(pickup, "Awake");

            Assert.That(pickupGo.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_NeitherStateSet_InitializesNormally()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        try
        {
            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_4", abilityState, AbilityId.SpiritCast, registry);
            InvokePrivate(pickup, "Awake");

            Assert.That(pickupGo.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_IsIdempotentAcrossRepeatedInitialization()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        try
        {
            abilityState.Unlock(AbilityId.SpiritCast);

            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_5", abilityState, AbilityId.SpiritCast, registry);
            InvokePrivate(pickup, "Awake");
            InvokePrivate(pickup, "Awake");

            Assert.That(registry.IsPickupCollected("pickup_5"), Is.True);
            Assert.That(pickupGo.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void OnTriggerEnter2D_CalledTwice_CannotGrantTwice()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        GameObject heroGo = new GameObject("Hero");
        try
        {
            int changedCount = 0;
            abilityState.AbilityChanged += (_, _) => changedCount++;

            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_6", abilityState, AbilityId.SpiritCast, registry);
            BoxCollider2D heroCollider = AddHeroBox(heroGo);

            InvokePrivate(pickup, "OnTriggerEnter2D", heroCollider);
            // Called again despite the pickup now being inactive -- reflection bypasses Unity's own
            // physics message dispatch, so this proves the collection logic itself is idempotent.
            InvokePrivate(pickup, "OnTriggerEnter2D", heroCollider);

            Assert.That(changedCount, Is.EqualTo(1), "Ability must only be granted once even if collection logic runs twice.");
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(heroGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    // Inconsistent direction 2: WorldStateRegistry says the pickup was consumed, PlayerAbilityState
    // says the ability was never granted. PlayerAbilityState is still authoritative here -- the fix
    // is to clear the stale WorldStateRegistry record (never to silently grant the ability) so the
    // pickup is genuinely reacquirable and a later real collection notifies normally.
    [Test]
    public void Awake_PickupConsumedButAbilityMissing_ClearsStaleRecordAndStaysActive()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        try
        {
            registry.MarkPickupCollected("pickup_7");
            Assert.That(abilityState.IsUnlocked(AbilityId.SpiritCast), Is.False);

            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_7", abilityState, AbilityId.SpiritCast, registry);

            int changedCount = 0;
            abilityState.AbilityChanged += (_, _) => changedCount++;

            LogAssert.Expect(LogType.Warning, new Regex("is not unlocked"));
            InvokePrivate(pickup, "Awake");

            Assert.That(pickupGo.activeSelf, Is.True, "PlayerAbilityState is authoritative -- the pickup must remain collectible.");
            Assert.That(abilityState.IsUnlocked(AbilityId.SpiritCast), Is.False, "Reconciliation must never silently grant the ability.");
            Assert.That(changedCount, Is.EqualTo(0), "Reconciliation must never fire AbilityChanged.");
            Assert.That(registry.IsPickupCollected("pickup_7"), Is.False, "The stale physical-consumption record must be cleared so the pickup can be genuinely reacquired.");
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    // Idempotence: once the stale record is cleared, repeated initialization must not warn or
    // drift again -- the two states only become consistent (both unlocked/consumed) after an
    // actual collection through OnTriggerEnter2D, never merely from re-running initialization.
    [Test]
    public void Awake_PickupConsumedButAbilityMissing_StaysConsistentAcrossReinitializationUntilActualCollection()
    {
        PlayerAbilityState abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject pickupGo = new GameObject("Pickup");
        GameObject heroGo = new GameObject("Hero");
        try
        {
            registry.MarkPickupCollected("pickup_8");
            AbilityPickup pickup = SetUpPickup(pickupGo, "pickup_8", abilityState, AbilityId.SpiritCast, registry);

            LogAssert.Expect(LogType.Warning, new Regex("is not unlocked"));
            InvokePrivate(pickup, "Awake");
            Assert.That(registry.IsPickupCollected("pickup_8"), Is.False, "Precondition: first initialization already cleared the stale record.");

            // Re-running initialization now sees a genuinely consistent "not yet collected" state
            // (locked + unconsumed) -- no warning, no re-clearing, no state change.
            InvokePrivate(pickup, "Awake");
            Assert.That(abilityState.IsUnlocked(AbilityId.SpiritCast), Is.False);
            Assert.That(registry.IsPickupCollected("pickup_8"), Is.False);
            Assert.That(pickupGo.activeSelf, Is.True);

            // Only an actual collection brings the two states into agreement.
            BoxCollider2D heroCollider = AddHeroBox(heroGo);
            InvokePrivate(pickup, "OnTriggerEnter2D", heroCollider);

            Assert.That(abilityState.IsUnlocked(AbilityId.SpiritCast), Is.True);
            Assert.That(registry.IsPickupCollected("pickup_8"), Is.True);
            Assert.That(pickupGo.activeSelf, Is.False);

            // A later initialization now sees consistent unlocked/consumed state and stays
            // suppressed without re-marking or warning.
            InvokePrivate(pickup, "Awake");
            Assert.That(abilityState.IsUnlocked(AbilityId.SpiritCast), Is.True);
            Assert.That(registry.IsPickupCollected("pickup_8"), Is.True);
            Assert.That(pickupGo.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(pickupGo);
            Object.DestroyImmediate(heroGo);
            Object.DestroyImmediate(abilityState);
            Object.DestroyImmediate(registry);
        }
    }

    // AddComponent<AbilityPickup> fires an automatic Awake immediately (the GameObject is active
    // by default), before any fields below are assigned -- that first call always no-ops (abilityState
    // is still null at that point), so it is harmless. Callers that need to observe
    // ReconcileOnInitialization's real behavior must invoke Awake again via reflection afterward.
    private static AbilityPickup SetUpPickup(GameObject go, string id, PlayerAbilityState abilityState, AbilityId ability, WorldStateRegistry registry)
    {
        AbilityPickup pickup = go.AddComponent<AbilityPickup>();
        SetPrivateField(pickup, "worldObjectId", id);
        SetPrivateField(pickup, "abilityState", abilityState);
        SetPrivateField(pickup, "ability", ability);
        SetPrivateField(pickup, "registry", registry);
        SetPrivateField(pickup, "disableAfterPickup", true);
        return pickup;
    }

    // HeroBox requires a Collider2D (RequireComponent can't auto-satisfy it since Collider2D is
    // abstract -- AddComponent<HeroBox> silently returns null without one already present).
    // AbilityPickup only checks for HeroBox's presence via GetComponentInParent, which is true as
    // soon as the component is attached regardless of whether/when its own Awake has run, so
    // HeroBox's internal "no parent HeroHealthComponent" warning (whose firing is not
    // deterministically synchronous with AddComponent in this harness) is irrelevant here.
    private static BoxCollider2D AddHeroBox(GameObject heroGo)
    {
        BoxCollider2D collider = heroGo.AddComponent<BoxCollider2D>();
        heroGo.AddComponent<HeroBox>();
        return collider;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on '{target.GetType().Name}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' not found on '{target.GetType().Name}'.");
        method.Invoke(target, arguments);
    }
}
