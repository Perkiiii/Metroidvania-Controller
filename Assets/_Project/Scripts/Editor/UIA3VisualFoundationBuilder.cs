using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Repeatable Package A3 authoring pass. It edits prefabs/scenes through Unity serialization,
/// leaving gameplay ownership untouched and keeping all artwork seams as ordinary Image fields.
/// </summary>
public static class UIA3VisualFoundationBuilder
{
    private const string FontPath =
        "Assets/Plugins/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string SandboxPath = "Assets/_Project/Scenes/UISandbox.unity";
    private const string GameCamerasPath = "Assets/_Project/Prefabs/Managers/_GameCameras.prefab";

    private static readonly string[] StandaloneUiPrefabs =
    {
        "Assets/_Project/Prefabs/UI/GameplayMenu/GameplayMenuTabButton.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/GearEntryView.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/GearTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/CombatLoadoutTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/SatchelTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/FieldNotesTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenu/MapTab.prefab",
        "Assets/_Project/Prefabs/UI/GameplayMenuScreen.prefab",
        "Assets/_Project/Prefabs/UI/PauseMenuScreen.prefab",
        "Assets/_Project/Prefabs/UI/ConfirmationModal.prefab",
        "Assets/_Project/Prefabs/UI/HealthSlotView.prefab"
    };

    private static TMP_FontAsset font;

    private static readonly Color Charcoal = C(11, 14, 17);
    private static readonly Color Smoke = C(25, 28, 31);
    private static readonly Color Raised = C(39, 38, 36);
    private static readonly Color Brass = C(151, 111, 55);
    private static readonly Color Amber = C(224, 174, 77);
    private static readonly Color Parchment = C(224, 213, 183);
    private static readonly Color Muted = C(151, 153, 146);
    private static readonly Color Berry = C(154, 24, 48);
    private static readonly Color BerryBright = C(215, 48, 76);
    private static readonly Color Teal = C(28, 151, 151);
    private static readonly Color TealBright = C(91, 218, 203);
    private static readonly Color Track = C(12, 19, 21);

    [MenuItem("Tools/Project/UI/Apply Package A3 Visual Foundation")]
    public static void Apply()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[UIA3VisualFoundationBuilder] TMP font asset not found at '{FontPath}'.");
            return;
        }

        foreach (string path in StandaloneUiPrefabs)
        {
            EditPrefab(path, root =>
            {
                ConvertLegacyText(root, skipNestedPrefabInstances: true);
                ApplySharedPrefabStyle(path, root);
            });
        }

        EditPrefab(GameCamerasPath, root =>
        {
            ConvertLegacyText(root, skipNestedPrefabInstances: true);
            StyleProductionHud(root);
        });

        StyleSandboxScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UIA3VisualFoundationBuilder] Package A3 visual foundation authored successfully.");
    }

    private static void ApplySharedPrefabStyle(string path, GameObject root)
    {
        if (path.EndsWith("HealthSlotView.prefab", StringComparison.Ordinal))
            StyleHealthSlot(root);
        else if (path.EndsWith("GameplayMenuTabButton.prefab", StringComparison.Ordinal))
            StyleTabButton(root);
        else if (path.EndsWith("GearEntryView.prefab", StringComparison.Ordinal))
            StyleGearEntry(root);
        else if (path.EndsWith("GearTab.prefab", StringComparison.Ordinal))
            StyleGearTab(root);
        else if (path.EndsWith("GameplayMenuScreen.prefab", StringComparison.Ordinal))
            StyleGameplayMenu(root);
        else if (path.EndsWith("PauseMenuScreen.prefab", StringComparison.Ordinal))
            StylePauseMenu(root);
        else if (path.EndsWith("ConfirmationModal.prefab", StringComparison.Ordinal))
            StyleConfirmationModal(root);
        else
            StyleEmptyTab(root);
    }

    private static void StyleHealthSlot(GameObject root)
    {
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(34f, 38f);

        Image stateImage = GetOrAdd<Image>(root);
        stateImage.color = Brass;
        stateImage.raycastTarget = false;

        Image empty = EnsureImage(root.transform, "EmptyVisual", C(64, 33, 40), 1);
        Inset(empty.rectTransform, 4f, 4f, 4f, 4f);

        Image filled = EnsureImage(root.transform, "FilledVisual", Berry, 2);
        Inset(filled.rectTransform, 4f, 4f, 4f, 4f);

        Image fillCore = EnsureImage(filled.transform, "Core", BerryBright, 0);
        Inset(fillCore.rectTransform, 4f, 3f, 4f, 10f);

        Image bonus = EnsureImage(root.transform, "BonusVisual", C(184, 131, 48), 3);
        Inset(bonus.rectTransform, 3f, 3f, 3f, 3f);
        Image bonusCore = EnsureImage(bonus.transform, "Core", C(241, 193, 91), 0);
        Inset(bonusCore.rectTransform, 4f, 4f, 4f, 4f);

        Image gloss = EnsureImage(root.transform, "GlassHighlight", new Color(1f, 0.92f, 0.78f, 0.14f), 4);
        SetRect(gloss.rectTransform, Vector2.up * 0.58f, Vector2.one, new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero);

        Image feedback = EnsureImage(root.transform, "FeedbackOverlay", new Color(1f, 0.9f, 0.65f, 0.8f), 5);
        SetFullStretch(feedback.rectTransform);
        CanvasGroup feedbackGroup = GetOrAdd<CanvasGroup>(feedback.gameObject);
        feedbackGroup.alpha = 0f;
        feedbackGroup.interactable = false;
        feedbackGroup.blocksRaycasts = false;

        HealthSlotView view = root.GetComponent<HealthSlotView>();
        SetObject(view, "emptyVisual", empty.gameObject);
        SetObject(view, "filledVisual", filled.gameObject);
        SetObject(view, "bonusVisual", bonus.gameObject);
        SetObject(view, "stateImage", stateImage);
        SetObject(view, "feedbackOverlay", feedbackGroup);
        SetObject(view, "animatedRoot", rt);
        SetColor(view, "emptyColor", Brass);
        SetColor(view, "filledColor", C(94, 39, 48));
        SetColor(view, "bonusColor", C(126, 99, 48));
    }

    private static void StyleTabButton(GameObject root)
    {
        Image background = GetOrAdd<Image>(root);
        background.color = new Color(Raised.r, Raised.g, Raised.b, 0.88f);
        background.raycastTarget = true;
        AddOutline(root, new Color(Brass.r, Brass.g, Brass.b, 0.42f), new Vector2(1f, -1f));

        Button button = GetOrAdd<Button>(root);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = C(68, 61, 48);
        colors.selectedColor = C(77, 68, 51);
        colors.pressedColor = C(94, 74, 43);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Transform activeT = root.transform.Find("ActiveIndicator");
        if (activeT != null)
        {
            Image active = GetOrAdd<Image>(activeT.gameObject);
            active.color = new Color(Amber.r, Amber.g, Amber.b, 0.20f);
            active.raycastTarget = false;
            SetFullStretch(active.rectTransform);
            Image accent = EnsureImage(activeT, "Accent", Amber, 0);
            SetRect(accent.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(0f, 6f));
        }

        Image focus = EnsureImage(root.transform, "FocusIndicator",
            new Color(1f, 0.86f, 0.52f, 0.18f), 1);
        Inset(focus.rectTransform, -3f, -3f, -3f, -3f);
        AddOutline(focus.gameObject, new Color(1f, 0.79f, 0.38f, 0.9f), new Vector2(2f, -2f));
        focus.gameObject.SetActive(false);

        Transform glyphT = root.transform.Find("Stack/Glyph");
        if (glyphT != null)
        {
            RectTransform glyphRect = glyphT as RectTransform;
            glyphRect.sizeDelta = new Vector2(18f, 18f);
            glyphRect.localEulerAngles = new Vector3(0f, 0f, 45f);
            GetOrAdd<Image>(glyphT.gameObject).raycastTarget = false;
        }

        TMP_Text label = Find<TMP_Text>(root.transform, "Stack/Label");
        StyleText(label, 17f, Parchment, TextAlignmentOptions.Center, FontStyles.Bold);

        GameplayMenuTabButton presenter = root.GetComponent<GameplayMenuTabButton>();
        SetObject(presenter, "button", button);
        SetObject(presenter, "label", label);
        SetObject(presenter, "focusIndicator", focus);
    }

    private static void StyleGearEntry(GameObject root)
    {
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 72f);
        LayoutElement layout = GetOrAdd<LayoutElement>(root);
        layout.minHeight = 64f;
        layout.preferredHeight = 72f;

        Image background = GetOrAdd<Image>(root);
        background.color = new Color(Raised.r, Raised.g, Raised.b, 0.90f);
        Button button = GetOrAdd<Button>(root);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = C(63, 58, 49);
        colors.selectedColor = C(70, 62, 49);
        colors.pressedColor = C(80, 66, 45);
        button.colors = colors;

        Image selected = EnsureImage(root.transform, "SelectedIndicator",
            new Color(Amber.r, Amber.g, Amber.b, 0.16f), 0);
        SetFullStretch(selected.rectTransform);
        Image leftAccent = EnsureImage(selected.transform, "LeftAccent", Amber, 0);
        SetRect(leftAccent.rectTransform, Vector2.zero, Vector2.up, Vector2.left,
            new Vector2(3f, 0f), new Vector2(6f, 0f));
        selected.gameObject.SetActive(false);

        Image focus = EnsureImage(root.transform, "FocusIndicator", Color.clear, 1);
        SetFullStretch(focus.rectTransform);
        AddOutline(focus.gameObject, new Color(0.95f, 0.78f, 0.42f, 0.95f), new Vector2(2f, -2f));
        focus.gameObject.SetActive(false);

        TMP_Text name = Find<TMP_Text>(root.transform, "Name");
        TMP_Text category = Find<TMP_Text>(root.transform, "Category");
        StyleText(name, 19f, Parchment, TextAlignmentOptions.Left, FontStyles.Bold);
        StyleText(category, 14f, Muted, TextAlignmentOptions.Left, FontStyles.UpperCase);

        Image icon = Find<Image>(root.transform, "Icon");
        if (icon != null) icon.raycastTarget = false;

        GearEntryView view = root.GetComponent<GearEntryView>();
        SetObject(view, "button", button);
        SetObject(view, "icon", icon);
        SetObject(view, "nameLabel", name);
        SetObject(view, "categoryLabel", category);
        SetObject(view, "selectedIndicator", selected);
        SetObject(view, "focusIndicator", focus);
    }

    private static void StyleGearTab(GameObject root)
    {
        Transform collectionT = root.transform.Find("Content/Populated/Collection");
        Transform dividerT = root.transform.Find("Content/Populated/Divider");
        Transform detailsT = root.transform.Find("Content/Populated/Details");
        if (collectionT != null)
        {
            SetRect(collectionT as RectTransform, new Vector2(0f, 0f), new Vector2(0.38f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(-12f, 0f));
            Image panel = GetOrAdd<Image>(collectionT.gameObject);
            panel.color = new Color(Smoke.r, Smoke.g, Smoke.b, 0.88f);
            AddOutline(collectionT.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.32f), new Vector2(1f, -1f));
        }

        if (dividerT != null)
        {
            SetRect(dividerT as RectTransform, new Vector2(0.38f, 0f), new Vector2(0.38f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, -40f));
            GetOrAdd<Image>(dividerT.gameObject).color = new Color(Brass.r, Brass.g, Brass.b, 0.55f);
        }

        if (detailsT != null)
        {
            SetRect(detailsT as RectTransform, new Vector2(0.40f, 0f), Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image detailPanel = GetOrAdd<Image>(detailsT.gameObject);
            detailPanel.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.48f);

            Transform content = detailsT.Find("DetailsContent");
            if (content != null)
            {
                Image artBack = EnsureImage(content, "ArtworkBackdrop",
                    new Color(Brass.r, Brass.g, Brass.b, 0.10f), 0);
                SetRect(artBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(230f, 210f));
                AddOutline(artBack.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.35f), new Vector2(1f, -1f));
                Transform artwork = content.Find("Artwork");
                if (artwork != null) artwork.SetAsLastSibling();
            }
        }

        StyleText(Find<TMP_Text>(root.transform, "Content/Populated/Collection/SectionLabel"),
            19f, Amber, TextAlignmentOptions.Left, FontStyles.Bold | FontStyles.UpperCase);
        StyleText(Find<TMP_Text>(root.transform, "Content/Populated/Details/DetailsContent/Name"),
            31f, Parchment, TextAlignmentOptions.Left, FontStyles.Bold);
        StyleText(Find<TMP_Text>(root.transform, "Content/Populated/Details/DetailsContent/Category"),
            16f, Amber, TextAlignmentOptions.Left, FontStyles.UpperCase);
        StyleText(Find<TMP_Text>(root.transform, "Content/Populated/Details/DetailsContent/Description"),
            19f, Parchment, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        StyleText(Find<TMP_Text>(root.transform, "Content/Populated/Details/DetailsContent/ControlHint"),
            16f, TealBright, TextAlignmentOptions.Left, FontStyles.Bold);
        StyleText(Find<TMP_Text>(root.transform, "Content/Populated/Details/DetailsContent/Flavour"),
            17f, Muted, TextAlignmentOptions.TopLeft, FontStyles.Italic);
        StyleText(Find<TMP_Text>(root.transform, "Content/EmptyState/Centre/Title"),
            30f, Parchment, TextAlignmentOptions.Center, FontStyles.Bold);
        StyleText(Find<TMP_Text>(root.transform, "Content/EmptyState/Centre/Body"),
            19f, Muted, TextAlignmentOptions.Center, FontStyles.Normal);

        GearScreen screen = root.GetComponent<GearScreen>();
        GearDetailsPanel details = root.GetComponentInChildren<GearDetailsPanel>(true);
        if (details != null)
        {
            SetObject(details, "nameLabel", Find<TMP_Text>(details.transform, "DetailsContent/Name"));
            SetObject(details, "categoryLabel", Find<TMP_Text>(details.transform, "DetailsContent/Category"));
            SetObject(details, "descriptionLabel", Find<TMP_Text>(details.transform, "DetailsContent/Description"));
            SetObject(details, "controlHintLabel", Find<TMP_Text>(details.transform, "DetailsContent/ControlHint"));
            SetObject(details, "flavourLabel", Find<TMP_Text>(details.transform, "DetailsContent/Flavour"));
        }
    }

    private static void StyleEmptyTab(GameObject root)
    {
        TMP_Text title = Find<TMP_Text>(root.transform, "Content/Centre/Title");
        TMP_Text body = Find<TMP_Text>(root.transform, "Content/Centre/Body");
        StyleText(title, 32f, Parchment, TextAlignmentOptions.Center, FontStyles.Bold);
        StyleText(body, 19f, Muted, TextAlignmentOptions.Center, FontStyles.Normal);

        Image motif = Find<Image>(root.transform, "Content/Centre/Motif");
        if (motif != null)
        {
            motif.color = new Color(Amber.r, Amber.g, Amber.b, 0.30f);
            motif.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            motif.raycastTarget = false;
        }

        GameplayMenuEmptyTabView view = root.GetComponent<GameplayMenuEmptyTabView>();
        if (view != null)
        {
            SetObject(view, "titleLabel", title);
            SetObject(view, "bodyLabel", body);
        }
    }

    private static void StyleGameplayMenu(GameObject root)
    {
        Transform panelT = root.transform.Find("Panel");
        Transform frameT = panelT?.Find("Frame");
        if (panelT == null || frameT == null) return;

        Image scrim = GetOrAdd<Image>(panelT.gameObject);
        scrim.color = new Color(0.015f, 0.018f, 0.021f, 0.96f);
        scrim.raycastTarget = true;

        SetRect(frameT as RectTransform, new Vector2(0.045f, 0.055f), new Vector2(0.955f, 0.945f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image frame = GetOrAdd<Image>(frameT.gameObject);
        frame.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.985f);
        AddOutline(frameT.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.70f), new Vector2(2f, -2f));

        Image inner = EnsureImage(frameT, "InnerBorder", Color.clear, 0);
        Inset(inner.rectTransform, 10f, 10f, 10f, 10f);
        AddOutline(inner.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.20f), new Vector2(1f, -1f));

        Transform headerT = frameT.Find("HeaderBar");
        if (headerT != null)
        {
            Image header = GetOrAdd<Image>(headerT.gameObject);
            header.color = new Color(Smoke.r, Smoke.g, Smoke.b, 0.96f);
            RectTransform headerRect = headerT as RectTransform;
            headerRect.offsetMin = new Vector2(headerRect.offsetMin.x, -154f);
            headerRect.offsetMax = new Vector2(headerRect.offsetMax.x, 0f);
        }

        Transform content = frameT.Find("ContentHost");
        if (content != null)
        {
            RectTransform contentRect = content as RectTransform;
            contentRect.offsetMin = new Vector2(34f, 34f);
            contentRect.offsetMax = new Vector2(-34f, -170f);
        }

        Image accent = Find<Image>(frameT, "AccentLine");
        if (accent != null) accent.color = new Color(Amber.r, Amber.g, Amber.b, 0.78f);

        TMP_Text title = Find<TMP_Text>(frameT, "HeaderBar/ActiveTabTitle");
        StyleText(title, 34f, Parchment, TextAlignmentOptions.Center, FontStyles.Bold);
        StyleText(Find<TMP_Text>(frameT, "HeaderBar/PreviousTabHint"),
            15f, Amber, TextAlignmentOptions.Center, FontStyles.Bold);
        StyleText(Find<TMP_Text>(frameT, "HeaderBar/NextTabHint"),
            15f, Amber, TextAlignmentOptions.Center, FontStyles.Bold);

        Transform closeT = frameT.Find("HeaderBar/CloseButton");
        if (closeT != null)
        {
            StyleButton(closeT.gameObject, new Color(Raised.r, Raised.g, Raised.b, 0.95f));
            StyleText(Find<TMP_Text>(closeT, "Label"), 17f, Parchment,
                TextAlignmentOptions.Center, FontStyles.Bold);
            LayoutElement closeLayout = GetOrAdd<LayoutElement>(closeT.gameObject);
            closeLayout.minHeight = 46f;
        }

        CanvasGroup group = GetOrAdd<CanvasGroup>(panelT.gameObject);
        UIVisualTransition transition = GetOrAdd<UIVisualTransition>(root);
        SetObject(transition, "canvasGroup", group);
        SetObject(transition, "animatedRoot", frameT as RectTransform);

        GameplayMenuScreen screen = root.GetComponent<GameplayMenuScreen>();
        SetObject(screen, "activeTabTitleLabel", title);
        SetObject(screen, "visualTransition", transition);
    }

    private static void StylePauseMenu(GameObject root)
    {
        Transform panelT = root.transform.Find("Panel");
        if (panelT == null) return;
        Image panel = GetOrAdd<Image>(panelT.gameObject);
        panel.color = new Color(0.015f, 0.018f, 0.021f, 0.94f);

        Transform buttonsT = panelT.Find("Buttons");
        if (buttonsT != null)
        {
            Image back = EnsureImage(panelT, "PauseCard", new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.96f), 0);
            SetRect(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(430f, 430f));
            AddOutline(back.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.65f), new Vector2(2f, -2f));
            buttonsT.SetAsLastSibling();
        }

        StyleText(Find<TMP_Text>(panelT, "Title"), 40f, Parchment,
            TextAlignmentOptions.Center, FontStyles.Bold);
        foreach (Button button in panelT.GetComponentsInChildren<Button>(true))
        {
            StyleButton(button.gameObject, new Color(Raised.r, Raised.g, Raised.b, 0.95f));
            StyleText(button.GetComponentInChildren<TMP_Text>(true), 20f, Parchment,
                TextAlignmentOptions.Center, FontStyles.Bold);
            LayoutElement layout = GetOrAdd<LayoutElement>(button.gameObject);
            layout.minHeight = 52f;
        }
    }

    private static void StyleConfirmationModal(GameObject root)
    {
        Transform panelT = root.transform.Find("Panel");
        Transform boxT = panelT?.Find("Box");
        if (panelT == null || boxT == null) return;

        GetOrAdd<Image>(panelT.gameObject).color = new Color(0f, 0f, 0f, 0.76f);
        Image box = GetOrAdd<Image>(boxT.gameObject);
        box.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.985f);
        AddOutline(boxT.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.75f), new Vector2(2f, -2f));
        StyleText(Find<TMP_Text>(boxT, "Message"), 24f, Parchment,
            TextAlignmentOptions.Center, FontStyles.Normal);

        foreach (Button button in boxT.GetComponentsInChildren<Button>(true))
        {
            StyleButton(button.gameObject, new Color(Raised.r, Raised.g, Raised.b, 0.96f));
            StyleText(button.GetComponentInChildren<TMP_Text>(true), 18f, Parchment,
                TextAlignmentOptions.Center, FontStyles.Bold);
        }
    }

    private static void StyleProductionHud(GameObject root)
    {
        Transform hudCanvas = root.transform.Find("HUDRoot/HUD Canvas");
        Transform player = hudCanvas?.Find("Player HUD") ?? hudCanvas?.Find("SafeAreaContent/Player HUD");
        if (hudCanvas == null || player == null) return;

        CanvasScaler scaler = hudCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform wrapper = hudCanvas.Find("SafeAreaContent");
        if (wrapper == null)
        {
            wrapper = EnsureRect(hudCanvas, "SafeAreaContent", 0);
            SetFullStretch(wrapper as RectTransform);
            SafeAreaInset inset = GetOrAdd<SafeAreaInset>(wrapper.gameObject);
            SetObject(inset, "target", wrapper as RectTransform);
        }

        if (player.parent != wrapper)
            player.SetParent(wrapper, false);

        RectTransform playerRect = player as RectTransform;
        SetRect(playerRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, -36f), new Vector2(474f, 122f));

        Image backdrop = EnsureImage(player, "ClusterBackdrop",
            new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.90f), 0);
        Inset(backdrop.rectTransform, -14f, -10f, -14f, -10f);
        AddOutline(backdrop.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.62f), new Vector2(2f, -2f));
        backdrop.raycastTarget = false;
        backdrop.enabled = false;

        Transform health = player.Find("Health Display");
        if (health != null)
        {
            SetRect(health as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(430f, 44f));
            HorizontalLayoutGroup layout = health.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.MiddleLeft;
            }
        }

        Transform resource = player.Find("Resource Display");
        if (resource != null)
        {
            SetRect(resource as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(8f, -70f), new Vector2(360f, 32f));
            StyleResourceBar(resource);
        }

        Transform boss = hudCanvas.Find("Boss Health Display") ?? wrapper.Find("Boss Health Display");
        if (boss != null)
        {
            if (boss.parent != wrapper) boss.SetParent(wrapper, false);
            StyleBossBar(boss);
        }

        foreach (Graphic graphic in player.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.GetComponent<Button>() == null)
                graphic.raycastTarget = false;
        }
    }

    private static void StyleResourceBar(Transform resource)
    {
        Transform trackT = FindDirectChild(resource, "Frame / Background");
        Transform viewportT = trackT?.Find("Fill");
        if (trackT == null || viewportT == null) return;

        Image frame = GetOrAdd<Image>(trackT.gameObject);
        frame.color = C(37, 48, 48);
        frame.raycastTarget = false;
        AddOutline(trackT.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.82f), new Vector2(2f, -2f));
        Inset(trackT as RectTransform, 8f, 6f, 8f, 6f);

        HorizontalMaskedFillView fill = ConfigureMaskedFill(
            trackT as RectTransform, viewportT as RectTransform, Teal, TealBright);
        ResourceBarView bar = viewportT.GetComponent<ResourceBarView>();
        SetObject(bar, "maskedFill", fill);
        SetObject(bar, "fillImage", null);
        SetObject(bar, "animatedRoot", resource as RectTransform);

        Transform edge = viewportT.Find("LeadingEdge");
        if (edge != null)
        {
            CanvasGroup pulse = GetOrAdd<CanvasGroup>(edge.gameObject);
            pulse.alpha = 0.9f;
            pulse.interactable = false;
            pulse.blocksRaycasts = false;
            SetObject(bar, "edgePulse", pulse);
        }
    }

    private static void StyleBossBar(Transform boss)
    {
        RectTransform bossRect = boss as RectTransform;
        SetRect(bossRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -34f), new Vector2(760f, 104f));

        CanvasGroup visibility = GetOrAdd<CanvasGroup>(boss.gameObject);
        visibility.alpha = 0f;
        visibility.interactable = false;
        visibility.blocksRaycasts = false;

        Image panel = GetOrAdd<Image>(boss.gameObject);
        panel.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.94f);
        panel.raycastTarget = false;
        AddOutline(boss.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.62f), new Vector2(2f, -2f));

        Transform trackT = boss.Find("Background");
        Transform mainT = trackT?.Find("Fill");
        if (trackT == null || mainT == null) return;
        SetRect(trackT as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(670f, 30f));
        Image trackImage = GetOrAdd<Image>(trackT.gameObject);
        trackImage.color = Track;
        trackImage.raycastTarget = false;
        AddOutline(trackT.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.72f), new Vector2(1f, -1f));

        RectTransform trailingT = EnsureRect(trackT, "TrailingFillViewport", 0) as RectTransform;
        SetFullStretch(trailingT);
        HorizontalMaskedFillView trailing = ConfigureMaskedFill(
            trackT as RectTransform, trailingT, C(104, 39, 45), C(174, 80, 73));

        mainT.SetAsLastSibling();
        SetFullStretch(mainT as RectTransform);
        HorizontalMaskedFillView main = ConfigureMaskedFill(
            trackT as RectTransform, mainT as RectTransform, Berry, BerryBright);

        TMP_Text name = Find<TMP_Text>(boss, "Boss Name");
        if (name == null)
        {
            Transform nameT = EnsureRect(boss, "Boss Name", 0);
            name = GetOrAdd<TextMeshProUGUI>(nameT.gameObject);
        }
        SetRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(640f, 42f));
        StyleText(name, 27f, Parchment, TextAlignmentOptions.Center, FontStyles.Bold);

        Image icon = EnsureImage(boss, "BossIcon", new Color(Brass.r, Brass.g, Brass.b, 0.25f), 1);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(38f, 2f), new Vector2(58f, 58f));
        icon.enabled = false;

        BossHealthBarView view = GetOrAdd<BossHealthBarView>(boss.gameObject);
        SetObject(view, "visibilityGroup", visibility);
        SetObject(view, "animatedRoot", bossRect);
        SetObject(view, "nameLabel", name);
        SetObject(view, "iconImage", icon);
        SetObject(view, "mainFill", main);
        SetObject(view, "trailingFill", trailing);

        BossHealthDisplay display = boss.GetComponent<BossHealthDisplay>();
        SetObject(display, "presentation", view);
        SetObject(display, "visibilityGroup", visibility);
        SetObject(display, "fillImage", null);
        SetObject(display, "nameLabel", name);
        SetObject(display, "iconImage", icon);
    }

    private static HorizontalMaskedFillView ConfigureMaskedFill(
        RectTransform track, RectTransform viewport, Color fillColor, Color edgeColor)
    {
        GetOrAdd<RectMask2D>(viewport.gameObject);
        Image viewportImage = GetOrAdd<Image>(viewport.gameObject);
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = false;
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(0f, 1f);
        viewport.pivot = new Vector2(0f, 0.5f);
        viewport.anchoredPosition = Vector2.zero;

        Image artwork = EnsureImage(viewport, "Artwork", fillColor, 0);
        artwork.rectTransform.anchorMin = new Vector2(0f, 0f);
        artwork.rectTransform.anchorMax = new Vector2(0f, 1f);
        artwork.rectTransform.pivot = new Vector2(0f, 0.5f);
        artwork.rectTransform.anchoredPosition = Vector2.zero;
        Image highlight = EnsureImage(artwork.transform, "Highlight",
            new Color(1f, 1f, 1f, 0.14f), 0);
        SetRect(highlight.rectTransform, new Vector2(0f, 0.58f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);

        Image edge = EnsureImage(viewport, "LeadingEdge", edgeColor, 1);
        SetRect(edge.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 0f));

        HorizontalMaskedFillView fill = GetOrAdd<HorizontalMaskedFillView>(viewport.gameObject);
        SetObject(fill, "track", track);
        SetObject(fill, "viewport", viewport);
        SetObject(fill, "artwork", artwork.rectTransform);
        SetObject(fill, "leadingEdge", edge.rectTransform);
        return fill;
    }

    private static void StyleSandboxScene()
    {
        Scene scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ConvertLegacyText(root, skipNestedPrefabInstances: true);
        }

        Transform sandboxCanvas = FindRoot(scene, "SandboxCanvas")?.transform;
        Transform aspect = sandboxCanvas?.Find("AspectFrame");
        Transform fixture = aspect?.Find("FixtureControlPanel");
        Transform safe = aspect?.Find("SafeAreaGuide");
        GameObject developerRoot = FindRoot(scene, "DeveloperUtilityLayer");
        if (sandboxCanvas == null || aspect == null || fixture == null || developerRoot == null)
        {
            Debug.LogError("[UIA3VisualFoundationBuilder] UISandbox hierarchy is incomplete.");
            return;
        }

        CanvasScaler sandboxScaler = sandboxCanvas.GetComponent<CanvasScaler>();
        if (sandboxScaler != null)
        {
            sandboxScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sandboxScaler.referenceResolution = new Vector2(1920f, 1080f);
            sandboxScaler.matchWidthOrHeight = 0.5f;
        }

        Image previewBackground = GetOrAdd<Image>(aspect.gameObject);
        previewBackground.color = C(15, 18, 21);
        previewBackground.raycastTarget = false;
        Outline aspectOutline = GetOrAdd<Outline>(aspect.gameObject);
        aspectOutline.effectColor = new Color(Brass.r, Brass.g, Brass.b, 0.55f);

        SetRect(fixture as RectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), new Vector2(18f, -18f), new Vector2(340f, -116f));
        Image fixturePanel = GetOrAdd<Image>(fixture.gameObject);
        fixturePanel.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.96f);
        AddOutline(fixture.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.55f), new Vector2(2f, -2f));

        Transform content = fixture.Find("Content");
        if (content != null)
        {
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 8f;
                layout.padding = new RectOffset(12, 12, 14, 18);
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }

            foreach (Transform child in content)
            {
                TMP_Text text = child.GetComponent<TMP_Text>();
                Button button = child.GetComponent<Button>();
                Toggle toggle = child.GetComponent<Toggle>();
                if (text != null)
                {
                    StyleText(text, 16f, Amber, TextAlignmentOptions.Left,
                        FontStyles.Bold | FontStyles.UpperCase);
                    LayoutElement headerLayout = GetOrAdd<LayoutElement>(child.gameObject);
                    headerLayout.minHeight = 28f;
                    headerLayout.preferredHeight = 32f;
                }
                else if (button != null)
                {
                    StyleButton(child.gameObject, new Color(Raised.r, Raised.g, Raised.b, 0.94f));
                    StyleText(child.GetComponentInChildren<TMP_Text>(true), 15f, Parchment,
                        TextAlignmentOptions.Left, FontStyles.Normal);
                    LayoutElement buttonLayout = GetOrAdd<LayoutElement>(child.gameObject);
                    buttonLayout.minHeight = 40f;
                    buttonLayout.preferredHeight = 42f;
                }
                else if (toggle != null)
                {
                    StyleText(child.GetComponentInChildren<TMP_Text>(true), 15f, Parchment,
                        TextAlignmentOptions.Left, FontStyles.Normal);
                    LayoutElement toggleLayout = GetOrAdd<LayoutElement>(child.gameObject);
                    toggleLayout.minHeight = 38f;
                }
            }
        }

        Transform hudPreview = aspect.Find("HUDPreview");
        if (hudPreview != null)
        {
            Transform resource = hudPreview.Find("ResourceDisplay");
            if (resource != null) StyleSandboxResource(resource);
            Transform boss = hudPreview.Find("BossHealthDisplay");
            if (boss != null) StyleSandboxBoss(boss);
        }

        Transform toolbar = EnsureRect(developerRoot.transform, "TopToolbar", 0);
        SetRect(toolbar as RectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(0f, 54f));
        Image toolbarBack = GetOrAdd<Image>(toolbar.gameObject);
        toolbarBack.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.96f);
        AddOutline(toolbar.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.48f), new Vector2(0f, -2f));

        TMP_Text status = EnsureText(toolbar, "Status", "UI WORKBENCH  ·  Preview", 16f,
            Parchment, TextAlignmentOptions.Left, FontStyles.Bold, 0);
        SetRect(status.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f),
            new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

        Button fixtureButton = EnsureToolbarButton(toolbar, "FixturesButton", "FIXTURES", 1, -430f);
        Button diagnosticsButton = EnsureToolbarButton(toolbar, "DiagnosticsButton", "DIAGNOSTICS", 2, -300f);
        Button safeButton = EnsureToolbarButton(toolbar, "SafeAreaButton", "SAFE AREA", 3, -170f);
        Button backgroundButton = EnsureToolbarButton(toolbar, "BackgroundButton", "BACKDROP", 4, -40f);

        Transform devPanel = developerRoot.transform.Find("DevPanel");
        if (devPanel != null)
        {
            SetRect(devPanel as RectTransform, new Vector2(1f, 1f), Vector2.one, Vector2.one,
                new Vector2(-18f, -70f), new Vector2(360f, 200f));
            GetOrAdd<Image>(devPanel.gameObject).color = new Color(Smoke.r, Smoke.g, Smoke.b, 0.98f);
            AddOutline(devPanel.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.55f), new Vector2(2f, -2f));
            StyleText(Find<TMP_Text>(devPanel, "Title"), 15f, Amber,
                TextAlignmentOptions.Left, FontStyles.Bold);
            StyleText(Find<TMP_Text>(devPanel, "Status"), 14f, Parchment,
                TextAlignmentOptions.TopLeft, FontStyles.Normal);
            foreach (Button button in devPanel.GetComponentsInChildren<Button>(true))
            {
                StyleButton(button.gameObject, new Color(Raised.r, Raised.g, Raised.b, 0.96f));
                StyleText(button.GetComponentInChildren<TMP_Text>(true), 14f, Parchment,
                    TextAlignmentOptions.Center, FontStyles.Bold);
            }
            devPanel.gameObject.SetActive(false);
        }

        SandboxWorkbenchChrome chrome = GetOrAdd<SandboxWorkbenchChrome>(developerRoot);
        SetObject(chrome, "uiFlow", FindRoot(scene, "SandboxMenuFlow")?.GetComponent<UIFlowController>());
        SetObject(chrome, "eventSystem", FindRoot(scene, "EventSystem")?.GetComponent<EventSystem>());
        SetObject(chrome, "previewBackground", previewBackground);
        SetObject(chrome, "safeAreaGuide", safe as RectTransform);
        SetObject(chrome, "fixtureDrawer", fixture.gameObject);
        SetObject(chrome, "diagnosticsDrawer", devPanel?.gameObject);
        SetObject(chrome, "toolbarStatus", status);
        SetObject(chrome, "fixtureToggleButton", fixtureButton);
        SetObject(chrome, "diagnosticsToggleButton", diagnosticsButton);
        SetObject(chrome, "safeAreaToggleButton", safeButton);
        SetObject(chrome, "backgroundToggleButton", backgroundButton);
        SetBool(chrome, "autoCollapseChromeWhenRootOpens", true);
        SetBool(chrome, "diagnosticsHiddenByDefault", true);

        SandboxDeveloperUtilityLayer devLayer = developerRoot.GetComponent<SandboxDeveloperUtilityLayer>();
        if (devLayer != null)
        {
            SetObject(devLayer, "statusLabel", Find<TMP_Text>(devPanel, "Status"));
        }

        foreach (SandboxQuitCallbackStatus quitStatus in
                 FindAllInScene<SandboxQuitCallbackStatus>(scene))
        {
            SetObject(quitStatus, "statusText", quitStatus.GetComponent<TMP_Text>());
        }

        foreach (BossHealthDisplay boss in FindAllInScene<BossHealthDisplay>(scene))
        {
            TMP_Text name = boss.GetComponentInChildren<TMP_Text>(true);
            if (name != null) SetObject(boss, "nameLabel", name);
        }

        StyleDirectSandboxModal(scene);
        SetNonInteractiveGraphicsRaycastOff(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void StyleSandboxResource(Transform resource)
    {
        Image panel = GetOrAdd<Image>(resource.gameObject);
        panel.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.88f);
        panel.raycastTarget = false;
        Transform viewport = resource.Find("Fill");
        if (viewport == null) return;
        HorizontalMaskedFillView fill = ConfigureMaskedFill(
            resource as RectTransform, viewport as RectTransform, Teal, TealBright);
        ResourceBarView bar = viewport.GetComponent<ResourceBarView>();
        SetObject(bar, "maskedFill", fill);
        SetObject(bar, "fillImage", null);
    }

    private static void StyleSandboxBoss(Transform boss)
    {
        Image panel = GetOrAdd<Image>(boss.gameObject);
        panel.color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.94f);
        panel.raycastTarget = false;

        Transform mainT = boss.Find("Fill");
        if (mainT == null) return;
        RectTransform trailingT = EnsureRect(boss, "TrailingFillViewport", 0) as RectTransform;
        SetFullStretch(trailingT);
        HorizontalMaskedFillView trailing = ConfigureMaskedFill(
            boss as RectTransform, trailingT, C(104, 39, 45), C(174, 80, 73));
        mainT.SetAsLastSibling();
        HorizontalMaskedFillView main = ConfigureMaskedFill(
            boss as RectTransform, mainT as RectTransform, Berry, BerryBright);

        TMP_Text name = Find<TMP_Text>(boss, "BossName");
        StyleText(name, 25f, Parchment, TextAlignmentOptions.Center, FontStyles.Bold);

        CanvasGroup visibility = GetOrAdd<CanvasGroup>(boss.gameObject);
        BossHealthBarView view = GetOrAdd<BossHealthBarView>(boss.gameObject);
        SetObject(view, "visibilityGroup", visibility);
        SetObject(view, "animatedRoot", boss as RectTransform);
        SetObject(view, "nameLabel", name);
        SetObject(view, "mainFill", main);
        SetObject(view, "trailingFill", trailing);
        BossHealthDisplay display = boss.GetComponent<BossHealthDisplay>();
        SetObject(display, "presentation", view);
        SetObject(display, "visibilityGroup", visibility);
        SetObject(display, "fillImage", null);
        SetObject(display, "nameLabel", name);
    }

    private static void StyleDirectSandboxModal(Scene scene)
    {
        GameObject options = FindByPath(scene, "SandboxCanvas/ModalLayer/OptionsPreviewPanel");
        if (options == null) return;
        GetOrAdd<Image>(options).color = new Color(0f, 0f, 0f, 0.72f);
        Transform box = options.transform.Find("Box");
        if (box != null)
        {
            GetOrAdd<Image>(box.gameObject).color = new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.98f);
            AddOutline(box.gameObject, new Color(Brass.r, Brass.g, Brass.b, 0.68f), new Vector2(2f, -2f));
            StyleText(Find<TMP_Text>(box, "Message"), 20f, Parchment,
                TextAlignmentOptions.Center, FontStyles.Normal);
            Button back = box.GetComponentInChildren<Button>(true);
            if (back != null)
            {
                StyleButton(back.gameObject, Raised);
                StyleText(back.GetComponentInChildren<TMP_Text>(true), 18f, Parchment,
                    TextAlignmentOptions.Center, FontStyles.Bold);
            }
        }
    }

    private static void SetNonInteractiveGraphicsRaycastOff(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                text.raycastTarget = false;

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponent<Button>() == null
                    && image.GetComponent<Toggle>() == null
                    && image.transform.parent?.GetComponent<Toggle>() == null)
                {
                    image.raycastTarget = false;
                }
            }
        }
    }

    private static void ConvertLegacyText(GameObject root, bool skipNestedPrefabInstances)
    {
        Text[] legacy = root.GetComponentsInChildren<Text>(true);
        foreach (Text oldText in legacy)
        {
            if (skipNestedPrefabInstances && PrefabUtility.IsPartOfPrefabInstance(oldText.gameObject))
                continue;

            string text = oldText.text;
            Color color = oldText.color;
            int size = oldText.fontSize;
            FontStyle style = oldText.fontStyle;
            TextAnchor alignment = oldText.alignment;
            bool richText = oldText.supportRichText;
            bool raycast = oldText.raycastTarget;
            float lineSpacing = oldText.lineSpacing;

            GameObject go = oldText.gameObject;
            UnityEngine.Object.DestroyImmediate(oldText, true);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = Mathf.Max(1, size);
            tmp.fontStyle = ConvertStyle(style);
            tmp.alignment = ConvertAlignment(alignment);
            tmp.richText = richText;
            tmp.raycastTarget = raycast;
            tmp.lineSpacing = (lineSpacing - 1f) * 20f;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableAutoSizing = false;
        }
    }

    private static FontStyles ConvertStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold: return FontStyles.Bold;
            case FontStyle.Italic: return FontStyles.Italic;
            case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
            default: return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static void EditPrefab(string path, Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void StyleButton(GameObject go, Color normal)
    {
        Image image = GetOrAdd<Image>(go);
        image.color = normal;
        image.raycastTarget = true;
        AddOutline(go, new Color(Brass.r, Brass.g, Brass.b, 0.34f), new Vector2(1f, -1f));
        Button button = GetOrAdd<Button>(go);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = C(66, 59, 46);
        colors.selectedColor = C(76, 66, 48);
        colors.pressedColor = C(92, 73, 42);
        colors.disabledColor = new Color(0.32f, 0.32f, 0.32f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private static TMP_Text EnsureText(
        Transform parent, string name, string value, float size, Color color,
        TextAlignmentOptions alignment, FontStyles style, int sibling)
    {
        Transform child = EnsureRect(parent, name, sibling);
        TMP_Text text = GetOrAdd<TextMeshProUGUI>(child.gameObject);
        text.text = value;
        StyleText(text, size, color, alignment, style);
        return text;
    }

    private static Button EnsureToolbarButton(
        Transform parent, string name, string label, int sibling, float rightOffset)
    {
        Transform child = EnsureRect(parent, name, sibling);
        SetRect(child as RectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(rightOffset, 0f), new Vector2(116f, 36f));
        StyleButton(child.gameObject, new Color(Raised.r, Raised.g, Raised.b, 0.96f));
        Button button = GetOrAdd<Button>(child.gameObject);
        TMP_Text text = EnsureText(child, "Label", label, 13f, Parchment,
            TextAlignmentOptions.Center, FontStyles.Bold, 0);
        SetFullStretch(text.rectTransform);
        return button;
    }

    private static Image EnsureImage(Transform parent, string name, Color color, int sibling)
    {
        Transform child = EnsureRect(parent, name, sibling);
        Image image = GetOrAdd<Image>(child.gameObject);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Transform EnsureRect(Transform parent, string name, int sibling)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            child = go.transform;
            child.SetParent(parent, false);
        }
        child.SetSiblingIndex(Mathf.Clamp(sibling, 0, parent.childCount - 1));
        return child;
    }

    private static void StyleText(
        TMP_Text text, float size, Color color, TextAlignmentOptions alignment, FontStyles style)
    {
        if (text == null) return;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void SetRect(
        RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null) return;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Inset(RectTransform rect, float left, float bottom, float right, float top)
    {
        SetFullStretch(rect);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static Outline AddOutline(GameObject go, Color color, Vector2 distance)
    {
        Outline outline = GetOrAdd<Outline>(go);
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
        return outline;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static T Find<T>(Transform root, string path) where T : Component
    {
        return root?.Find(path)?.GetComponent<T>();
    }

    private static Transform FindDirectChild(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
        }
        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }
        return null;
    }

    private static GameObject FindByPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject root = FindRoot(scene, parts[0]);
        if (root == null) return null;
        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }
        return current.gameObject;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        List<T> results = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results.ToArray();
    }

    private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[UIA3VisualFoundationBuilder] Missing field '{field}' on {target.GetType().Name}.", target);
            return;
        }
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetColor(UnityEngine.Object target, string field, Color value)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return;
        property.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(UnityEngine.Object target, string field, bool value)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return;
        property.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Color C(int r, int g, int b, int a = 255)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
