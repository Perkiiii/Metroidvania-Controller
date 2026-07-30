using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossHealthDisplayTests
{
    private GameObject displayObject;
    private BossHealthDisplay display;
    private CanvasGroup canvasGroup;
    private Image fillImage;
    private BossHealthBarView presentation;
    private HorizontalMaskedFillView mainFill;
    private HorizontalMaskedFillView trailingFill;
    private GameObject healthRoot;
    private EnemyConfig[] configs;

    [SetUp]
    public void SetUp()
    {
        displayObject = new GameObject("Boss Health Display Test");
        canvasGroup = displayObject.AddComponent<CanvasGroup>();
        fillImage = displayObject.AddComponent<Image>();
        presentation = displayObject.AddComponent<BossHealthBarView>();
        mainFill = CreateMaskedFill("Main Fill");
        trailingFill = CreateMaskedFill("Trailing Fill");
        SetPrivateField(presentation, "visibilityGroup", canvasGroup);
        SetPrivateField(presentation, "mainFill", mainFill);
        SetPrivateField(presentation, "trailingFill", trailingFill);
        presentation.ConfigureDurations(0f, 0f, 0f, 0f);
        display = displayObject.AddComponent<BossHealthDisplay>();
        SetPrivateField(display, "presentation", presentation);
        SetPrivateField(display, "visibilityGroup", canvasGroup);
        SetPrivateField(display, "fillImage", fillImage);
        InvokePrivate(display, "OnEnable");

        healthRoot = new GameObject("Boss Health Sources");
        configs = new EnemyConfig[2];
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(displayObject);
        Object.DestroyImmediate(healthRoot);
        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i] != null)
            {
                Object.DestroyImmediate(configs[i]);
            }
        }
    }

    [Test]
    public void ShowReadsInitialAggregateAndAcceptedDamageUpdatesIt()
    {
        EnemyHealthComponent first = CreateHealth("First", 3, 0);
        EnemyHealthComponent second = CreateHealth("Second", 5, 1);
        object source = new object();

        BossHudEventService.RequestShow(source, null, new[] { first, second });

        Assert.That(display.IsVisible, Is.True);
        Assert.That(display.ActiveSource, Is.SameAs(source));
        Assert.That(display.BoundHealthSourceCount, Is.EqualTo(2));
        Assert.That(display.CurrentHealth, Is.EqualTo(8));
        Assert.That(display.MaximumHealth, Is.EqualTo(8));
        Assert.That(display.FillAmount01, Is.EqualTo(1f));
        Assert.That(canvasGroup.alpha, Is.EqualTo(1f));

        first.ReceiveHeroAttack(new HeroAttackHit(null, HeroAttackDirection.Side, 1, Vector2.zero, Vector2.right));

        Assert.That(display.CurrentHealth, Is.EqualTo(7));
        Assert.That(display.FillAmount01, Is.EqualTo(0.875f).Within(0.0001f));
        Assert.That(fillImage.fillAmount, Is.EqualTo(0.875f).Within(0.0001f));
        Assert.That(mainFill.FillAmount01, Is.EqualTo(0.875f).Within(0.0001f));
        Assert.That(trailingFill.FillAmount01, Is.EqualTo(0.875f).Within(0.0001f));
    }

    [Test]
    public void MismatchedHideDoesNotClearActiveBinding()
    {
        EnemyHealthComponent health = CreateHealth("Boss", 4, 0);
        object source = new object();
        BossHudEventService.RequestShow(source, null, new[] { health });

        BossHudEventService.RequestHide(new object());

        Assert.That(display.IsVisible, Is.True);
        Assert.That(display.ActiveSource, Is.SameAs(source));
        Assert.That(display.BoundHealthSourceCount, Is.EqualTo(1));
    }

    [Test]
    public void MatchingHideClearsSceneReferencesAndSubscriptions()
    {
        EnemyHealthComponent health = CreateHealth("Boss", 4, 0);
        object source = new object();
        BossHudEventService.RequestShow(source, null, new[] { health });

        BossHudEventService.RequestHide(source);

        Assert.That(display.IsVisible, Is.False);
        Assert.That(display.ActiveSource, Is.Null);
        Assert.That(display.BoundHealthSourceCount, Is.Zero);
        Assert.That(display.CurrentHealth, Is.Zero);
        Assert.That(canvasGroup.alpha, Is.Zero);

        health.ReceiveHeroAttack(new HeroAttackHit(null, HeroAttackDirection.Side, 1, Vector2.zero, Vector2.right));
        Assert.That(display.CurrentHealth, Is.Zero);
    }

    [Test]
    public void DisableClearsBindingAndStopsReceivingRequests()
    {
        EnemyHealthComponent health = CreateHealth("Boss", 4, 0);
        BossHudEventService.RequestShow(new object(), null, new[] { health });

        InvokePrivate(display, "OnDisable");
        BossHudEventService.RequestShow(new object(), null, new[] { health });

        Assert.That(display.IsVisible, Is.False);
        Assert.That(display.BoundHealthSourceCount, Is.Zero);
        Assert.That(canvasGroup.alpha, Is.Zero);
    }

    private EnemyHealthComponent CreateHealth(string objectName, int maximum, int configIndex)
    {
        GameObject actor = new GameObject(objectName);
        actor.transform.SetParent(healthRoot.transform);
        Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
        EnemyStateBlackboard blackboard = actor.AddComponent<EnemyStateBlackboard>();
        EnemyHealthComponent health = actor.AddComponent<EnemyHealthComponent>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        config.maxHealth = maximum;
        configs[configIndex] = config;
        health.Initialize(config, blackboard, body, null);
        return health;
    }

    private HorizontalMaskedFillView CreateMaskedFill(string objectName)
    {
        GameObject trackObject = new GameObject(objectName + " Track", typeof(RectTransform));
        trackObject.transform.SetParent(displayObject.transform, false);
        RectTransform track = (RectTransform)trackObject.transform;
        track.sizeDelta = new Vector2(200f, 20f);

        GameObject viewportObject = new GameObject(objectName + " Viewport",
            typeof(RectTransform), typeof(RectMask2D), typeof(HorizontalMaskedFillView));
        viewportObject.transform.SetParent(track, false);
        RectTransform viewport = (RectTransform)viewportObject.transform;

        GameObject artworkObject = new GameObject(objectName + " Artwork", typeof(RectTransform));
        artworkObject.transform.SetParent(viewport, false);
        RectTransform artwork = (RectTransform)artworkObject.transform;

        HorizontalMaskedFillView view = viewportObject.GetComponent<HorizontalMaskedFillView>();
        view.Configure(track, viewport, artwork);
        return view;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }
}
