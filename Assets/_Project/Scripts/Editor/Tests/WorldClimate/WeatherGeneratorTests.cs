using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WeatherGeneratorTests
{
    private const int WeatherCount = 5;
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void SplitMix64DerivationMatchesApprovedGoldenValues()
    {
        Assert.That(DeterministicWeatherRng.DeriveSeasonSeed(0x00000001u, 0L), Is.EqualTo(0x67AE5B22u));
        Assert.That(DeterministicWeatherRng.DeriveSeasonSeed(0x12345678u, 0L), Is.EqualTo(0xCC2A6A1Au));
        Assert.That(DeterministicWeatherRng.DeriveSeasonSeed(0x12345678u, 1L), Is.EqualTo(0x4C6FB65Du));
        Assert.That(DeterministicWeatherRng.DeriveSeasonSeed(0xFFFFFFFFu, 123456789L), Is.EqualTo(0xDF754397u));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicWeatherRng.DeriveSeasonSeed(0u, 0L));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeterministicWeatherRng.DeriveSeasonSeed(1u, -1L));
    }

    [Test]
    public void XorShift32MatchesApprovedGoldenSequence()
    {
        DeterministicWeatherRng rng = new DeterministicWeatherRng(0xCC2A6A1Au);
        uint[] expected =
        {
            0xAC443F6Eu,
            0x5E94BDFAu,
            0xEC58B48Fu,
            0xA3EC148Bu,
            0x0ED1E295u
        };

        for (int i = 0; i < expected.Length; i++)
            Assert.That(rng.NextUInt(), Is.EqualTo(expected[i]), $"Golden draw {i} changed.");

        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicWeatherRng(0u));
    }

    [Test]
    public void NextBelowMatchesApprovedGoldenValuesAndRejectsZeroBound()
    {
        DeterministicWeatherRng rng = new DeterministicWeatherRng(0x12345678u);
        Assert.That(rng.NextBelow(1u), Is.EqualTo(0u));
        Assert.That(rng.NextBelow(2u), Is.EqualTo(1u));
        Assert.That(rng.NextBelow(3u), Is.EqualTo(1u));
        Assert.That(rng.NextBelow(10u), Is.EqualTo(2u));
        Assert.That(rng.NextBelow(uint.MaxValue), Is.EqualTo(0x703A0788u));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextBelow(0u));

        DeterministicWeatherRng rejectionRng = new DeterministicWeatherRng(0x12345678u);
        Assert.That(rejectionRng.NextBelow(2147483649u), Is.EqualTo(0x07985AA4u));
        Assert.That(rejectionRng.NextBelow(2147483649u), Is.EqualTo(0x01B3AC97u));
        Assert.That(rejectionRng.NextBelow(2147483649u), Is.EqualTo(0x09CA4F1Cu));
    }

    [Test]
    public void WeightedGenerationMatchesGoldenSequenceAndConfiguredShape()
    {
        int[] weights =
        {
            1, 2, 3, 4, 5,
            5, 4, 3, 2, 1,
            1, 1, 8, 1, 1,
            0, 0, 0, 1, 1,
            10, 0, 0, 0, 1
        };
        SeasonDefinition season = CreateSeason(WeatherType.Cloudy, weights);
        ClimateRegionDefinition region = CreateRegion("golden_region", CreateTrack(season));
        WeatherGenerator generator = new WeatherGenerator(4, 2);

        IReadOnlyList<WeatherType> generated = generator.GenerateSeason(region, 0x12345678u, 2L);

        Assert.That(generated.Count, Is.EqualTo(8));
        CollectionAssert.AreEqual(
            new[]
            {
                WeatherType.Fog,
                WeatherType.Clear,
                WeatherType.Clear,
                WeatherType.Fog,
                WeatherType.Clear,
                WeatherType.Fog,
                WeatherType.Clear,
                WeatherType.Rain
            },
            generated);
        Assert.That(generator.ResolveSlot(region, 0x12345678u, 2L, 4, 1), Is.EqualTo(WeatherType.Rain));
    }

    [Test]
    public void FixedSlotEmitsAuthoredWeatherAndBecomesNextTransitionSource()
    {
        int[] nextTypeWeights = new int[WeatherCount * WeatherCount];
        for (int from = 0; from < WeatherCount; from++)
            nextTypeWeights[from * WeatherCount + ((from + 1) % WeatherCount)] = 1;

        SeasonDefinition season = CreateSeason(
            WeatherType.Clear,
            nextTypeWeights,
            new FixedWeatherSlot(1, 0, WeatherType.Storm));
        ClimateRegionDefinition region = CreateRegion("fixed_region", CreateTrack(season));
        WeatherGenerator generator = new WeatherGenerator(1, 3);

        CollectionAssert.AreEqual(
            new[] { WeatherType.Storm, WeatherType.Fog, WeatherType.Clear },
            generator.GenerateSeason(region, 0x10203040u, 0L));
    }

    [Test]
    public void FixedSlotDoesNotConsumeTheRandomStream()
    {
        int[] weights = UniformWeights();
        SeasonDefinition generatedSeason = CreateSeason(WeatherType.Cloudy, weights);
        SeasonDefinition fixedSeason = CreateSeason(
            WeatherType.Cloudy,
            weights,
            new FixedWeatherSlot(1, 0, WeatherType.Cloudy));
        WeatherGenerator generator = new WeatherGenerator(1, 4);

        IReadOnlyList<WeatherType> baseline = generator.GenerateSeason(
            CreateRegion("generated_region", CreateTrack(generatedSeason)),
            0xABCDEF01u,
            7L);
        IReadOnlyList<WeatherType> withFixed = generator.GenerateSeason(
            CreateRegion("fixed_region", CreateTrack(fixedSeason)),
            0xABCDEF01u,
            7L);

        Assert.That(withFixed[0], Is.EqualTo(WeatherType.Cloudy));
        for (int i = 1; i < withFixed.Count; i++)
            Assert.That(withFixed[i], Is.EqualTo(baseline[i - 1]), $"Slot {i} consumed a draw at the fixed slot.");
    }

    [Test]
    public void ResultsAreDeterministicRegardlessOfQueryOrder()
    {
        SeasonDefinition spring = CreateSeason(WeatherType.Clear, UniformWeights());
        SeasonDefinition summer = CreateSeason(WeatherType.Rain, UniformWeights());
        ClimateRegionDefinition region = CreateRegion("order_region", CreateTrack(spring, summer));
        WeatherGenerator forward = new WeatherGenerator(3, 2);
        WeatherGenerator reverse = new WeatherGenerator(3, 2);

        IReadOnlyList<WeatherType> forward0 = forward.GenerateSeason(region, 0x11223344u, 0L);
        IReadOnlyList<WeatherType> forward1 = forward.GenerateSeason(region, 0x11223344u, 1L);
        IReadOnlyList<WeatherType> forward2 = forward.GenerateSeason(region, 0x11223344u, 2L);

        IReadOnlyList<WeatherType> reverse2 = reverse.GenerateSeason(region, 0x11223344u, 2L);
        IReadOnlyList<WeatherType> reverse0 = reverse.GenerateSeason(region, 0x11223344u, 0L);
        IReadOnlyList<WeatherType> reverse1 = reverse.GenerateSeason(region, 0x11223344u, 1L);

        CollectionAssert.AreEqual(forward0, reverse0);
        CollectionAssert.AreEqual(forward1, reverse1);
        CollectionAssert.AreEqual(forward2, reverse2);

        IReadOnlyList<WeatherType> differentSeed = reverse.GenerateSeason(region, 0x55667788u, 0L);
        Assert.That(SequenceEquals(forward0, differentSeed), Is.False);
        Assert.That(SequenceEquals(forward0, forward1), Is.False);
    }

    [Test]
    public void SeasonsResolveByAbsoluteInstanceAndRestartFromTheirAuthoredEntryWeather()
    {
        int[] identityWeights = new int[WeatherCount * WeatherCount];
        for (int weatherIndex = 0; weatherIndex < WeatherCount; weatherIndex++)
            identityWeights[weatherIndex * WeatherCount + weatherIndex] = 1;

        SeasonDefinition spring = CreateSeason(WeatherType.Clear, identityWeights);
        SeasonDefinition summer = CreateSeason(WeatherType.Storm, identityWeights);
        ClimateRegionDefinition region = CreateRegion("season_region", CreateTrack(spring, summer));
        WeatherGenerator generator = new WeatherGenerator(2, 2);

        CollectionAssert.AreEqual(
            new[] { WeatherType.Clear, WeatherType.Clear, WeatherType.Clear, WeatherType.Clear },
            generator.GenerateSeason(region, 0xCAFEBABEu, 0L));
        CollectionAssert.AreEqual(
            new[] { WeatherType.Storm, WeatherType.Storm, WeatherType.Storm, WeatherType.Storm },
            generator.GenerateSeason(region, 0xCAFEBABEu, 1L));
        CollectionAssert.AreEqual(
            new[] { WeatherType.Clear, WeatherType.Clear, WeatherType.Clear, WeatherType.Clear },
            generator.GenerateSeason(region, 0xCAFEBABEu, 2L));
    }

    [Test]
    public void CacheKeepsThreeImmutablePeriodsPerRegionUsingLruEviction()
    {
        SeasonDefinition season = CreateSeason(WeatherType.Clear, UniformWeights());
        SeasonTrackDefinition track = CreateTrack(season);
        ClimateRegionDefinition regionA = CreateRegion("cache_A", track);
        ClimateRegionDefinition regionB = CreateRegion("cache_B", track);
        WeatherGenerator generator = new WeatherGenerator(2, 2);

        IReadOnlyList<WeatherType> a0 = generator.GenerateSeason(regionA, 0x13572468u, 0L);
        IReadOnlyList<WeatherType> a1 = generator.GenerateSeason(regionA, 0x13572468u, 1L);
        IReadOnlyList<WeatherType> a2 = generator.GenerateSeason(regionA, 0x13572468u, 2L);
        IReadOnlyList<WeatherType> b0 = generator.GenerateSeason(regionB, 0x24681357u, 0L);
        Assert.That(generator.GenerateSeason(regionA, 0x13572468u, 0L), Is.SameAs(a0));

        generator.GenerateSeason(regionA, 0x13572468u, 3L);

        Assert.That(generator.GenerateSeason(regionA, 0x13572468u, 2L), Is.SameAs(a2));
        Assert.That(generator.GenerateSeason(regionB, 0x24681357u, 0L), Is.SameAs(b0));
        IReadOnlyList<WeatherType> regeneratedA1 = generator.GenerateSeason(regionA, 0x13572468u, 1L);
        Assert.That(regeneratedA1, Is.Not.SameAs(a1), "The least-recently-used fourth period must evict period 1.");
        CollectionAssert.AreEqual(a1, regeneratedA1, "Eviction must not change deterministic output.");

        IList<WeatherType> mutableView = (IList<WeatherType>)a0;
        Assert.That(mutableView.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = WeatherType.Storm);
    }

    [Test]
    public void InvalidDimensionsAndQueryKeysFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherGenerator(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherGenerator(2, 0));

        SeasonDefinition season = CreateSeason(WeatherType.Clear, UniformWeights());
        ClimateRegionDefinition region = CreateRegion("validation_region", CreateTrack(season));
        WeatherGenerator generator = new WeatherGenerator(2, 2);

        Assert.Throws<ArgumentNullException>(() => generator.GenerateSeason(null, 1u, 0L));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateSeason(region, 0u, 0L));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateSeason(region, 1u, -1L));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.ResolveSlot(region, 1u, 0L, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.ResolveSlot(region, 1u, 0L, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.ResolveSlot(region, 1u, 0L, 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.ResolveSlot(region, 1u, 0L, 1, 2));
    }

    private SeasonDefinition CreateSeason(
        WeatherType entryWeather,
        int[] weights,
        params FixedWeatherSlot[] fixedSlots)
    {
        SeasonDefinition season = Create<SeasonDefinition>();
        SetPrivateField(season, "seasonId", $"season_{createdObjects.Count}");
        SetPrivateField(season, "displayName", "Fixture Season");
        SetPrivateField(season, "entryWeatherType", entryWeather);
        SetPrivateField(season, "transitionWeights", (int[])weights.Clone());
        SetPrivateField(season, "fixedWeatherSlots", new List<FixedWeatherSlot>(fixedSlots));
        return season;
    }

    private SeasonTrackDefinition CreateTrack(params SeasonDefinition[] seasons)
    {
        SeasonTrackDefinition track = Create<SeasonTrackDefinition>();
        SetPrivateField(track, "seasons", new List<SeasonDefinition>(seasons));
        return track;
    }

    private ClimateRegionDefinition CreateRegion(string id, SeasonTrackDefinition track)
    {
        ClimateRegionDefinition region = Create<ClimateRegionDefinition>();
        SetPrivateField(region, "regionId", id);
        SetPrivateField(region, "displayName", id);
        SetPrivateField(region, "seasonTrack", track);
        SetPrivateField(region, "initialWeatherType", WeatherType.Clear);
        return region;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(instance);
        return instance;
    }

    private static int[] UniformWeights()
    {
        int[] weights = new int[WeatherCount * WeatherCount];
        for (int i = 0; i < weights.Length; i++)
            weights[i] = 1;
        return weights;
    }

    private static bool SequenceEquals(IReadOnlyList<WeatherType> left, IReadOnlyList<WeatherType> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
