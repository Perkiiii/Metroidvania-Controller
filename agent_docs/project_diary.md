# Project Diary

## Decisions and Lessons

- Weather presentation is scene-local: `RoomWeatherPresentation` queries `WorldWeatherState` using
  `RoomClimateContext.RegionId`, subscribes to `WeatherChanged`, and never mutates or generates
  weather. `WorldTimeState` and `WorldWeatherState` ownership is unchanged.
- This slice presents only Outdoor Rain and Storm. Clear, Cloudy, Fog, Sheltered, and Indoor resolve
  to the clear presentation baseline without changing the actual weather value.
- `RoomRainPresentation` is the only rain-effect owner. It follows `CameraInfoCache` for gameplay-view
  coverage while existing drops simulate in world space; authored splash emitters avoid universal
  collision or terrain scanning.
- `AudioManager` now owns one ambience channel. Ambience requests are owner-scoped: transferring the
  active request prevents a stale room teardown from stopping the replacement room's loop. This bug
  was reproduced during independent review, repaired, and covered by regression tests.
- `RoomStormPresentation` owns one nonpersisted strike schedule with a bounded multi-pulse flash and
  delayed `AudioManager.PlaySFX` thunder. Rain/Clear/disable/unload cancels pending presentation work;
  an already-fired one-shot may finish naturally.
- Reusing project-owned `SpriteFlash.mat` and `white_fader.png` avoided a new shader, Light2D writer,
  renderer change, or VFX Graph dependency.
- Happy Harvest remains presentation reference material only. Accepted splash/rain/thunder content
  was copied through Unity into `_Project` with new GUIDs; production references to the donor tree
  are forbidden.
- Full EditMode reproduced the historical camera-axis assertion as the sole failure. No evidence
  connects it to weather work; the focused weather/climate/time union and PlayMode lifecycle passed.
- Automated lifecycle and asset validation cannot approve density, readability, audio balance, or
  flash intensity. The vertical slice stops for human visual review before further weather polish.
- Manual weather forcing stays in an Editor-only `WeatherDebugWindow`. It resolves the active
  room's existing `RoomClimateContext` and invokes the normal exact-slot `SetOverride` /
  `ClearOverride` APIs, so `WeatherChanged` remains the only presentation-driving path.
- The active override key is not always the clock's current absolute day: before the first daily
  slot, the prior day's final slot remains active. The debug window mirrors this rule and exposes
  both the clock day and override-key day; day zero before the first slot disables actions.
