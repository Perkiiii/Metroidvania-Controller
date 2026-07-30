using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class HorizontalMaskedFillViewTests
{
    private GameObject root;
    private RectTransform track;
    private RectTransform viewport;
    private RectTransform artwork;
    private RectTransform leadingEdge;
    private HorizontalMaskedFillView view;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Masked Fill Test", typeof(RectTransform));
        track = (RectTransform)root.transform;
        track.sizeDelta = new Vector2(240f, 24f);

        GameObject viewportObject = new GameObject("Viewport",
            typeof(RectTransform), typeof(RectMask2D), typeof(HorizontalMaskedFillView));
        viewportObject.transform.SetParent(track, false);
        viewport = (RectTransform)viewportObject.transform;

        GameObject artworkObject = new GameObject("Artwork", typeof(RectTransform));
        artworkObject.transform.SetParent(viewport, false);
        artwork = (RectTransform)artworkObject.transform;

        GameObject edgeObject = new GameObject("Leading Edge", typeof(RectTransform));
        edgeObject.transform.SetParent(viewport, false);
        leadingEdge = (RectTransform)edgeObject.transform;

        view = viewportObject.GetComponent<HorizontalMaskedFillView>();
        view.Configure(track, viewport, artwork, leadingEdge);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void FillCropsViewportWhileArtworkKeepsFullTrackWidth()
    {
        view.SetFill(0.25f);

        Assert.That(view.FillAmount01, Is.EqualTo(0.25f));
        Assert.That(view.VisibleWidth, Is.EqualTo(60f).Within(0.001f));
        Assert.That(view.ArtworkWidth, Is.EqualTo(240f).Within(0.001f));
        Assert.That(viewport.rect.width, Is.EqualTo(60f).Within(0.001f));
        Assert.That(artwork.rect.width, Is.EqualTo(240f).Within(0.001f));
        Assert.That(leadingEdge.anchoredPosition.x, Is.EqualTo(60f).Within(0.001f));
        Assert.That(leadingEdge.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void InvalidAndZeroValuesAreSafeAndHideTheLeadingEdge()
    {
        view.SetFill(float.NaN);
        Assert.That(view.FillAmount01, Is.Zero);
        Assert.That(view.VisibleWidth, Is.Zero);
        Assert.That(leadingEdge.gameObject.activeSelf, Is.False);

        view.SetFill(float.PositiveInfinity);
        Assert.That(view.FillAmount01, Is.Zero);

        view.SetFill(-10f);
        Assert.That(view.FillAmount01, Is.Zero);

        view.SetFill(10f);
        Assert.That(view.FillAmount01, Is.EqualTo(1f));
    }

    [Test]
    public void DimensionChangeReappliesTheCurrentNormalizedFill()
    {
        view.SetFill(0.5f);
        track.sizeDelta = new Vector2(320f, 24f);

        MethodInfo callback = typeof(HorizontalMaskedFillView).GetMethod(
            "OnRectTransformDimensionsChange",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(callback, Is.Not.Null);
        callback.Invoke(view, null);

        Assert.That(view.VisibleWidth, Is.EqualTo(160f).Within(0.001f));
        Assert.That(view.ArtworkWidth, Is.EqualTo(320f).Within(0.001f));
    }
}
