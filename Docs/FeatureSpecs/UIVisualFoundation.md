# Feature Spec — UI Visual Foundation

**Status:** Package A3 implemented on 2026-07-29. This is a production-ready visual foundation
using provisional project-owned shapes and the bundled TextMesh Pro Liberation Sans SDF asset.
Final illustration, bespoke fonts, icons, localization, accessibility, and audio remain content
passes; they do not block the current hierarchy or presentation contracts.

## Purpose and boundaries

Package A3 gives the existing HUD, Pause menu, five-tab Gameplay Menu, Gear screen, confirmation
modal, and UI Sandbox a coherent presentation language. It does not add inventory, Loadout,
Field Notes, Map, notification, settings, frontend, encounter, save, or input ownership.

Presentation code may read a normalized value or view state and animate local graphics with
unscaled time. It must never mutate gameplay state, pause state, input maps, encounter state, or
save data.

## Visual language

| Role | RGB | Use |
|---|---:|---|
| Charcoal | `11, 14, 17` | Primary frames and HUD backplates |
| Smoke | `25, 28, 31` | Modal and diagnostic surfaces |
| Raised | `39, 38, 36` | Buttons and selectable rows |
| Brass | `151, 111, 55` | Borders, rules, selection accents |
| Amber | `224, 174, 77` | Active labels and emphasis |
| Parchment | `224, 213, 183` | Primary text |
| Muted | `151, 153, 146` | Secondary text |
| Berry | `154, 24, 48` | Health and boss-health fill |
| Teal | `28, 151, 151` | Resource fill |
| Track | `12, 19, 21` | Empty bar tracks |

Frames are dark and quiet; information is separated by spacing, value, and thin brass accents
instead of large saturated panels. The working spacing rhythm is 8 px, with 12–16 px local
padding, 18–24 px group separation, and 32–44 px primary interactive rows at the 1920×1080
reference resolution.

## Typography

All Package A UI uses TextMesh Pro. The current font is
`Assets/Plugins/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`; it is a
temporary bundled font and `Assets/Plugins/` must not be edited. A future font swap should assign a
project-owned TMP font asset to the existing TMP fields without changing view ownership.

Primary screen titles are 28–36 px, section titles 18–24 px, body text 16–18 px, and compact
labels 14–16 px at the reference resolution. Active/selected text uses Amber or Parchment; body
copy uses Parchment or Muted.

## Component anatomy and states

### Health

`HealthSlotView` owns layered empty, filled, bonus, and highlight graphics. Damage, healing, and
bonus changes trigger brief local unscaled scale/highlight responses. `StateApplied` and `Reset`
remain neutral. Slot graphics do not receive raycasts.

### Resource

`ResourceBarView` owns a continuous bar with a framed track, masked artwork, optional leading-edge
pulse, and short unscaled gain/spend interpolation. Clear/death is deliberately quiet.

`HorizontalMaskedFillView` changes the width of a left-anchored `RectMask2D` viewport. The artwork
inside remains at the full track width, so gradients and later texture art are cropped rather than
compressed. Values are finite-clamped to `[0,1]`; zero capacity and rect changes are safe.
Production must not use `Image.fillAmount` for this bar.

### Boss health

`BossHealthDisplay` retains source-token and aggregate-roster ownership. `BossHealthBarView` owns
only the name/icon rendering, show motion, main masked fill, and delayed trailing damage fill.
Healing moves both fills together. All timing is unscaled and no boss/enemy reference is stored by
the presentation view.

### Menus and selection

Pause, confirmation, and Gameplay Menu roots use dark scrims, bounded frames, brass rules, and
short unscaled fade/scale transitions. The five Gameplay Menu cells are always present. Active-tab
state is a persistent amber underline/accent; hover or navigation focus is a separate raised
surface, so focus and selection never look identical.

Gear uses a 38/62 collection/details split. The selected stable Gear key drives the persistent
row accent; EventSystem focus remains an independent highlight. The details art area is an
intentional replacement seam, not invented production item art. Empty states are framed,
player-facing compositions rather than debug placeholders.

## Safe area and scaling

Production HUD content is wrapped by `SafeAreaContent` with `SafeAreaInset`. The wrapper applies
`Screen.safeArea` anchors only when the screen or safe area changes. The HUD and Sandbox use
`CanvasScaler.ScaleWithScreenSize`, 1920×1080 reference resolution, and a 0.5 width/height match.

The Sandbox safe-area guide is a preview aid only. Final shipping device cutouts and platform
accessibility targets still require device review.

## UI Sandbox workbench

The Sandbox keeps production prefabs and isolated fixtures, but Package A3 separates authoring
chrome from the preview:

- a compact top toolbar exposes Fixtures, Diagnostics, Safe Area, and Backdrop controls;
- the fixture drawer is a narrow, scrollable left workbench and can be toggled with F1;
- diagnostics are neutral and hidden by default;
- opening a production root auto-collapses the fixture drawer and closing it restores the prior
  state;
- the preview background cycles through dark, mid-value, and light checks;
- no workbench component is permitted in `_GameCameras.prefab`.

## Artwork replacement workflow

Current shapes are intentionally replaceable. To integrate final art:

1. Import sprites under `Assets/_Project/` (never `Assets/Plugins/` or `Assets/ThirdParty/`).
2. Use Sprite (2D and UI), sRGB enabled, alpha transparency enabled, no crunch compression for
   small UI assets, and a max size appropriate to the authored pixel dimensions.
3. Use 9-sliced sprites for scalable frames and buttons; set borders in Sprite Editor before
   assigning them to `Image` components with Type `Sliced`.
4. Replace `Artwork`, `Highlight`, frame, motif, icon, and detail-art `Image.sprite` fields in the
   existing prefabs. Do not replace masked bars with filled Images.
5. Keep decorative `Graphic.raycastTarget` disabled. Only intentional blocking scrims and
   Selectable surfaces receive raycasts.
6. Re-run `Tools/Project/Validate UI Foundation` and the focused UI tests.

## Authoring and validation

`Tools/Project/UI/Apply Package A3 Visual Foundation` is a repeatable Editor authoring pass for the
current prefabs and Sandbox scene. It is a migration/build tool, not runtime ownership. The
validator enforces TMP/font assignment, masked-fill wiring, safe-area composition, Sandbox-only
chrome exclusion, hidden diagnostics, and decorative HUD raycast safety in addition to the Package
A1/A2 architecture checks.

Focused coverage includes `HorizontalMaskedFillViewTests`, `HudDisplayTests`,
`BossHealthDisplayTests`, `UISandboxControllerFixtureTests`, `GameplayMenuScreenTests`,
`GameplayMenuProductionAssetTests`, and `GearScreenTests`.

## Remaining content and manual review

- Replace Liberation Sans with the approved project type family and generate TMP atlas/fallbacks
  for all supported languages.
- Replace provisional geometry with approved 9-sliced frames, health states, resource/boss
  artwork, tab motifs, Gear art, icons, and controller glyphs.
- Review contrast, text scaling, reduced-motion behavior, color-blind differentiation, and
  localization overflow.
- Validate keyboard, controller, and mouse focus at 16:9, 16:10, 21:9, 4:3, and target-device safe
  areas.
- Validate the persistent production composition through Boot and room transitions.

## Related specifications

- `Docs/FeatureSpecs/UIArchitecture.md`
- `Docs/FeatureSpecs/HUD.md`
- `Docs/FeatureSpecs/GameplayMenu.md`
- `Docs/FeatureSpecs/Gear.md`
- `Docs/FeatureSpecs/UISandbox.md`
