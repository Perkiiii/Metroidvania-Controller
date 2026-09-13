# Project Progress

## Goal

Add a minimal Editor-only weather override window so the accepted
`Clear -> Rain -> Storm -> Clear` slice can be tested manually through normal weather events.

## Overall Progress

The three presentation packages are implemented and accepted. `RoomWeatherPresentation` consumes
the actual regional weather selected by `WorldWeatherState`; `RoomRainPresentation` owns one
camera-framed world-space rain layer, three authored surface splash emitters, and owner-scoped rain
ambience; `RoomStormPresentation` owns a transient multi-pulse flash/thunder schedule. `SampleScene`
is the only authored slice room. Simulation determinism, save schema, hero, camera behavior,
`GameManager`, renderer, packages, and global sorting layers are unchanged.

`WeatherDebugWindow` is implemented at `Tools/Underbrew/Weather Debug`. It reads the existing world
time/weather assets and active room context, resolves the current simulation slot, and uses only
`WorldWeatherState.SetOverride` / `ClearOverride`; it adds no runtime or player-build code.

## Current Position

- Final focused EditMode union: 169/169.
- New weather-presentation PlayMode lifecycle fixture: 1/1.
- Full EditMode: 764/765; only the established unrelated
  `CameraPhaseOneTests.AxisLocksUseOnlyTheirOwnedLegalAxis` assertion failed.
- Project-owned rain splash, rain ambience, and thunder copies have new production GUIDs; production
  weather dependencies on `Assets/ThirdParty/HappyHarvest/` are zero.
- `SampleScene` contains one weather presentation composition, one rain layer, three surface splash
  emitters, and one initially hidden bounded lightning flash.
- No live screenshot, hands-on Play Mode visual/readability pass, audio-mix judgment, or profiling
  claim was completed. The implementation is visually provisional pending Sam's review.
- Test-run `ProjectSettings.asset` symbol drift was restored; there is no remaining project-settings
  delta.
- Weather debug focused EditMode regression: 54/54. Live menu/window visual smoke remains unclaimed.

## Next Milestone

Use `Tools/Underbrew/Weather Debug` in Play Mode for human visual review of rain density/streak
scale/depth, splash readability, ambience volume, lightning intensity, and gameplay/HUD readability.
Shelter policy, foreground rain, wind, ambient
leaves/motes, camera feedback, wet surfaces, fog, day/night lighting, and broader polish remain
deferred.
