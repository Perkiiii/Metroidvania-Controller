using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HeroLedgeClimbTests
{
    private readonly List<Object> created = new List<Object>();
    private HeroConfig config;
    private GameObject hero;
    private HeroSensors sensors;
    private HeroStateBlackboard blackboard;
    private HeroMotor motor;
    private HeroInputReader input;
    private HeroAbilityConfig abilityConfig;

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
            {
                Object.DestroyImmediate(created[i]);
            }
        }

        created.Clear();
    }

    [Test]
    public void TryFindLedge_AcceptsWideStaticSurfaceAndStoresTargetLocalPositions()
    {
        CreateHeroAndSensors();
        BoxCollider2D ledge = CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out LedgeProbeResult result), Is.True, sensors.LastLedgeFailure.ToString());
        Assert.That(result.PrimaryCollider, Is.EqualTo(ledge));
        Assert.That(result.TargetBody, Is.Null);
        Assert.That(result.TargetFrame, Is.EqualTo(ledge.transform));

        Vector2 originalStanding = result.ResolveStandingPosition();
        ledge.transform.position += new Vector3(2f, 3f, 0f);
        Physics2D.SyncTransforms();

        Assert.That(result.ResolveStandingPosition(), Is.EqualTo(originalStanding + new Vector2(2f, 3f)));
    }

    [Test]
    public void TryFindLedge_AcceptsAlignedAdjacentStaticTerrain()
    {
        CreateHeroAndSensors();
        BoxCollider2D primary = CreateBox("Primary Terrain", new Vector2(0.36f, 0.75f), new Vector2(0.18f, 1.5f));
        BoxCollider2D secondary = CreateBox("Secondary Terrain", new Vector2(0.975f, 0.75f), new Vector2(1.05f, 1.5f));
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out LedgeProbeResult result), Is.True, sensors.LastLedgeFailure.ToString());
        Assert.That(result.PrimaryCollider, Is.EqualTo(primary));
        Assert.That(result.SecondarySupportCollider, Is.EqualTo(secondary));
    }

    [Test]
    public void TryFindLedge_AcceptsAdjacentStaticTerrainWithinSupportGapTolerance()
    {
        CreateHeroAndSensors();
        BoxCollider2D primary = CreateBox("Primary Terrain", new Vector2(0.355f, 0.75f), new Vector2(0.17f, 1.5f));
        BoxCollider2D secondary = CreateBox("Secondary Terrain", new Vector2(0.98f, 0.75f), new Vector2(1.04f, 1.5f));
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out LedgeProbeResult result), Is.True, sensors.LastLedgeFailure.ToString());
        Assert.That(result.PrimaryCollider, Is.EqualTo(primary));
        Assert.That(result.SecondarySupportCollider, Is.EqualTo(secondary));
    }

    [Test]
    public void TryFindLedge_AcceptsCompositeColliderWithRequiredStaticBody()
    {
        CreateHeroAndSensors();
        GameObject ledge = new GameObject("Composite Terrain");
        created.Add(ledge);
        ledge.layer = 0;
        ledge.transform.position = new Vector3(1.135f, 0.75f, 0f);
        Rigidbody2D targetBody = ledge.AddComponent<Rigidbody2D>();
        targetBody.bodyType = RigidbodyType2D.Static;
        CompositeCollider2D composite = ledge.AddComponent<CompositeCollider2D>();
        BoxCollider2D source = ledge.AddComponent<BoxCollider2D>();
        source.size = new Vector2(1.73f, 1.5f);
        source.compositeOperation = Collider2D.CompositeOperation.Merge;
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out LedgeProbeResult result), Is.True, sensors.LastLedgeFailure.ToString());
        Assert.That(result.PrimaryCollider, Is.EqualTo(composite));
        Assert.That(result.TargetBody, Is.EqualTo(targetBody));
        Assert.That(result.TargetFrame, Is.EqualTo(composite.transform));
    }

    [Test]
    public void TryFindLedge_RejectsUnsupportedGapAcrossStandingFootprint()
    {
        CreateHeroAndSensors();
        CreateBox("Primary Terrain", new Vector2(0.36f, 0.75f), new Vector2(0.18f, 1.5f));
        CreateBox("Secondary Terrain", new Vector2(1.025f, 0.75f), new Vector2(0.95f, 1.5f));
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out _), Is.False);
        Assert.That(sensors.LastLedgeFailure, Is.EqualTo(LedgeProbeFailure.UnsupportedGap));
    }

    [Test]
    public void TryFindLedge_RejectsMismatchedSurfaceHeights()
    {
        CreateHeroAndSensors();
        CreateBox("Primary Terrain", new Vector2(0.36f, 0.75f), new Vector2(0.18f, 1.5f));
        CreateBox("Raised Terrain", new Vector2(0.975f, 0.85f), new Vector2(1.05f, 1.5f));
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out _), Is.False);
        Assert.That(sensors.LastLedgeFailure, Is.EqualTo(LedgeProbeFailure.SurfaceHeightMismatch));
    }

    [Test]
    public void TryFindLedge_RejectsRigidbodyBackedSurfaceExplicitly()
    {
        CreateHeroAndSensors();
        GameObject ledge = CreateBox("Rigidbody Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f)).gameObject;
        Rigidbody2D targetBody = ledge.AddComponent<Rigidbody2D>();
        targetBody.bodyType = RigidbodyType2D.Static;
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out _), Is.False);
        Assert.That(sensors.LastLedgeFailure, Is.EqualTo(LedgeProbeFailure.UnsupportedRigidbodySurface));
    }

    [Test]
    public void TryFindLedge_RejectsEnabledNoLedgeClimbVolumeAndAcceptsAfterDisable()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        GameObject restriction = new GameObject("No Ledge");
        created.Add(restriction);
        restriction.layer = 0;
        restriction.transform.position = new Vector3(0.47f, 1.97f, 0f);
        BoxCollider2D volumeCollider = restriction.AddComponent<BoxCollider2D>();
        volumeCollider.size = new Vector2(0.5f, 0.5f);
        volumeCollider.isTrigger = true;
        NoLedgeClimbVolume volume = restriction.AddComponent<NoLedgeClimbVolume>();
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out _), Is.False);
        Assert.That(sensors.LastLedgeFailure, Is.EqualTo(LedgeProbeFailure.RestrictedVolume));

        volume.enabled = false;
        Physics2D.SyncTransforms();
        Assert.That(sensors.TryFindLedge(1, out _), Is.True, sensors.LastLedgeFailure.ToString());
    }

    [Test]
    public void LedgeProbeResult_DisabledTargetIsNotUsable()
    {
        CreateHeroAndSensors();
        BoxCollider2D ledge = CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        Physics2D.SyncTransforms();
        Assert.That(sensors.TryFindLedge(1, out LedgeProbeResult result), Is.True, sensors.LastLedgeFailure.ToString());

        ledge.enabled = false;

        Assert.That(result.HasUsableTarget(), Is.False);
        Assert.That(sensors.ValidateLedgePath(result), Is.False);
        Assert.That(sensors.LastLedgeFailure, Is.EqualTo(LedgeProbeFailure.TargetInvalid));
    }

    [Test]
    public void HeroMotor_LedgeCleanupRestoresGravityAndStopsScriptedMovement()
    {
        CreateHeroAndSensors();
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        float originalGravity = body.gravityScale;

        motor.BeginLedgeClimb();
        motor.SetLedgeClimbPosition(new Vector2(2f, 3f));
        motor.EndLedgeClimb(false, Vector2.zero);

        Assert.That(body.position, Is.EqualTo(new Vector2(2f, 3f)));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(body.gravityScale, Is.EqualTo(originalGravity));
    }

    [Test]
    public void LedgeAction_RequiresAirborneStateAndRejectsEstablishedWallSlide()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        SetMoveInput(1f);
        blackboard.touchingWallFront = true;
        Physics2D.SyncTransforms();
        HeroLedgeClimbAction action = CreateLedgeAction(out _);

        blackboard.grounded = true;
        Assert.That(action.FixedTick(0.02f), Is.False);

        blackboard.grounded = false;
        blackboard.wallSliding = true;
        Assert.That(action.FixedTick(0.02f), Is.False);
        Assert.That(blackboard.ledgeClimbing, Is.False);
    }

    [TestCase("attacking")]
    [TestCase("binding")]
    [TestCase("controlLocked")]
    [TestCase("wallJumping")]
    public void LedgeAction_RejectsIncompatibleHeroState(string stateField)
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        SetMoveInput(1f);
        blackboard.grounded = false;
        blackboard.touchingWallFront = true;
        typeof(HeroStateBlackboard)
            .GetField(stateField, BindingFlags.Instance | BindingFlags.Public)
            .SetValue(blackboard, true);
        Physics2D.SyncTransforms();

        Assert.That(CreateLedgeAction(out _).FixedTick(0.02f), Is.False);
        Assert.That(blackboard.ledgeClimbing, Is.False);
    }

    [Test]
    public void LedgeAction_AllowsSlowRiseButRejectsConfiguredMaximumRise()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        SetMoveInput(1f);
        blackboard.grounded = false;
        blackboard.touchingWallFront = true;
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        body.linearVelocity = new Vector2(0f, config.ledgeMaxUpwardSpeed);
        Physics2D.SyncTransforms();

        Assert.That(CreateLedgeAction(out _).FixedTick(0.02f), Is.False);

        body.linearVelocity = new Vector2(0f, config.ledgeMaxUpwardSpeed - 0.5f);
        Assert.That(CreateLedgeAction(out _).FixedTick(0.02f), Is.True, sensors.LastLedgeFailure.ToString());
    }

    [Test]
    public void LedgeAction_AirDashHandoffPreservesDashResourceState()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        blackboard.grounded = false;
        blackboard.touchingWallFront = true;
        blackboard.dashing = true;
        HeroDashAction dash = CreateDashAction();
        SetPrivateField(dash, "airDashUsed", true);
        SetPrivateField(dash, "cooldownTimer", 0.37f);
        Physics2D.SyncTransforms();
        HeroLedgeClimbAction action = CreateLedgeAction(out _, dash);

        Assert.That(action.FixedTick(0.02f), Is.True, sensors.LastLedgeFailure.ToString());
        Assert.That(blackboard.ledgeClimbing, Is.True);
        Assert.That(blackboard.dashing, Is.False);
        Assert.That(GetPrivateField<bool>(dash, "airDashUsed"), Is.True);
        Assert.That(GetPrivateField<float>(dash, "cooldownTimer"), Is.EqualTo(0.37f));
    }

    [Test]
    public void LedgeAction_InvalidDashCandidateDoesNotCancelDash()
    {
        CreateHeroAndSensors();
        blackboard.grounded = false;
        blackboard.touchingWallFront = true;
        blackboard.dashing = true;
        HeroDashAction dash = CreateDashAction();
        HeroLedgeClimbAction action = CreateLedgeAction(out _, dash);
        Physics2D.SyncTransforms();

        Assert.That(action.FixedTick(0.02f), Is.False);
        Assert.That(blackboard.dashing, Is.True);
        Assert.That(blackboard.ledgeClimbing, Is.False);
    }

    [Test]
    public void LedgeAction_GroundedDashIntoWallDoesNotMantleOrCancelDash()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        blackboard.grounded = true;
        blackboard.touchingWallFront = true;
        blackboard.dashing = true;
        HeroDashAction dash = CreateDashAction();
        Physics2D.SyncTransforms();

        Assert.That(CreateLedgeAction(out _, dash).FixedTick(0.02f), Is.False);
        Assert.That(blackboard.dashing, Is.True);
        Assert.That(blackboard.ledgeClimbing, Is.False);
    }

    [Test]
    public void LedgeAction_TimerFallbackCompletesAndRestoresMotorState()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        SetMoveInput(1f);
        blackboard.grounded = false;
        blackboard.touchingWallFront = true;
        Physics2D.SyncTransforms();
        HeroLedgeClimbAction action = CreateLedgeAction(out System.Func<bool> animationStopped);

        Assert.That(action.FixedTick(0.02f), Is.True, sensors.LastLedgeFailure.ToString());
        action.FixedTick(config.ledgeCatchDuration);
        action.FixedTick(config.ledgePullUpDuration);
        action.FixedTick(config.ledgeSettleDuration);

        Assert.That(action.IsActive, Is.False);
        Assert.That(blackboard.ledgeClimbing, Is.False);
        Assert.That(animationStopped(), Is.True);
        Assert.That(hero.GetComponent<Rigidbody2D>().gravityScale, Is.Not.Zero);
    }

    [Test]
    public void LedgeAction_DisabledTargetCancelsThroughCommonCleanup()
    {
        CreateHeroAndSensors();
        BoxCollider2D ledge = CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        SetMoveInput(1f);
        blackboard.grounded = false;
        blackboard.touchingWallFront = true;
        Physics2D.SyncTransforms();
        HeroLedgeClimbAction action = CreateLedgeAction(out System.Func<bool> animationStopped);
        Assert.That(action.FixedTick(0.02f), Is.True, sensors.LastLedgeFailure.ToString());

        ledge.enabled = false;
        action.FixedTick(0.02f);

        Assert.That(action.IsActive, Is.False);
        Assert.That(action.LastCancelReason, Is.EqualTo(HeroLedgeClimbCancelReason.TargetInvalid));
        Assert.That(blackboard.ledgeClimbing, Is.False);
        Assert.That(animationStopped(), Is.True);
        Assert.That(hero.GetComponent<Rigidbody2D>().gravityScale, Is.Not.Zero);
    }

    [Test]
    public void LedgeAction_RepeatedCancellationIsIdempotent()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        SetMoveInput(1f);
        blackboard.grounded = false;
        blackboard.touchingWallFront = true;
        Physics2D.SyncTransforms();
        HeroLedgeClimbAction action = CreateLedgeAction(out System.Func<bool> animationStopped);
        Assert.That(action.FixedTick(0.02f), Is.True, sensors.LastLedgeFailure.ToString());

        action.Cancel(HeroLedgeClimbCancelReason.ControlLock);
        action.Cancel(HeroLedgeClimbCancelReason.ExternalState);

        Assert.That(action.IsActive, Is.False);
        Assert.That(action.LastCancelReason, Is.EqualTo(HeroLedgeClimbCancelReason.ControlLock));
        Assert.That(animationStopped(), Is.True);
        Assert.That(hero.GetComponent<Rigidbody2D>().gravityScale, Is.Not.Zero);
    }

    [Test]
    public void TryFindLedge_RejectsLowCeiling()
    {
        CreateHeroAndSensors();
        CreateBox("Wide Terrain", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        CreateBox("Low Ceiling", new Vector2(0.47f, 2.15f), new Vector2(1f, 0.2f));
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out _), Is.False);
        Assert.That(
            sensors.LastLedgeFailure,
            Is.EqualTo(LedgeProbeFailure.StandingBlocked)
                .Or.EqualTo(LedgeProbeFailure.CrestBlocked)
                .Or.EqualTo(LedgeProbeFailure.CorridorBlocked)
                .Or.EqualTo(LedgeProbeFailure.HeightOutOfRange));
    }

    [Test]
    public void TryFindLedge_RejectsNarrowSurface()
    {
        CreateHeroAndSensors();
        CreateBox("Narrow Terrain", new Vector2(0.37f, 0.75f), new Vector2(0.2f, 1.5f));
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out _), Is.False);
        Assert.That(sensors.LastLedgeFailure, Is.EqualTo(LedgeProbeFailure.NoTopSurface));
    }

    [Test]
    public void TryFindLedge_RejectsUnsupportedSurfaceLayer()
    {
        CreateHeroAndSensors();
        config.terrainLayers = (1 << 0) | (1 << 22);
        BoxCollider2D ledge = CreateBox("Breakable", new Vector2(1.135f, 0.75f), new Vector2(1.73f, 1.5f));
        ledge.gameObject.layer = 22;
        Physics2D.SyncTransforms();

        Assert.That(sensors.TryFindLedge(1, out _), Is.False);
        Assert.That(sensors.LastLedgeFailure, Is.EqualTo(LedgeProbeFailure.UnsupportedLayer));
    }

    private void CreateHeroAndSensors()
    {
        config = ScriptableObject.CreateInstance<HeroConfig>();
        created.Add(config);
        config.terrainLayers = 1 << 0;
        config.ledgeSurfaceLayers = 1 << 0;
        config.wallProbeDistance = 0.1f;
        config.sensorInset = 0.02f;
        config.ledgeMinimumHeightFromFeet = 0.25f;
        config.ledgeMaximumHeightFromFeet = 1.35f;
        config.ledgeTopProbeExtraHeight = 0.3f;
        config.ledgeTopSampleInset = 0.04f;
        config.ledgeSurfaceHeightTolerance = 0.08f;
        config.ledgeMinimumUpNormal = 0.85f;
        config.ledgeSupportGapTolerance = 0.08f;
        config.ledgePlacementSkin = 0.02f;
        config.ledgeCatchDrop = 0.1f;
        config.ledgeCatchDuration = 0.08f;
        config.ledgePullUpDuration = 0.28f;
        config.ledgeSettleDuration = 0.05f;

        hero = new GameObject("Ledge Test Hero");
        created.Add(hero);
        hero.layer = LayerMask.NameToLayer("Player");
        hero.transform.position = new Vector3(0f, 1f, 0f);
        Rigidbody2D body = hero.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        BoxCollider2D bodyCollider = hero.AddComponent<BoxCollider2D>();
        bodyCollider.size = new Vector2(0.36f, 0.9f);
        blackboard = hero.AddComponent<HeroStateBlackboard>();
        input = hero.AddComponent<HeroInputReader>();
        sensors = hero.AddComponent<HeroSensors>();
        sensors.Initialize(config, blackboard, body, bodyCollider);
        abilityConfig = ScriptableObject.CreateInstance<HeroAbilityConfig>();
        created.Add(abilityConfig);
        motor = hero.AddComponent<HeroMotor>();
        motor.Initialize(config, abilityConfig, blackboard, body, bodyCollider, null, hero.transform);
    }

    private BoxCollider2D CreateBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = new GameObject(name);
        created.Add(box);
        box.layer = 0;
        box.transform.position = position;
        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        collider.size = size;
        return collider;
    }

    private HeroDashAction CreateDashAction()
    {
        return new HeroDashAction(config, abilityConfig, blackboard, input, motor, null, null);
    }

    private HeroLedgeClimbAction CreateLedgeAction(out System.Func<bool> animationStopped, HeroDashAction dash = null)
    {
        bool stopped = false;
        animationStopped = () => stopped;
        return new HeroLedgeClimbAction(
            config,
            blackboard,
            input,
            motor,
            sensors,
            dash ?? CreateDashAction(),
            () => stopped = true);
    }

    private void SetMoveInput(float x)
    {
        PropertyInfo property = typeof(HeroInputReader).GetProperty(
            nameof(HeroInputReader.MoveVector),
            BindingFlags.Instance | BindingFlags.Public);
        property.SetValue(input, new Vector2(x, 0f));
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }
}
