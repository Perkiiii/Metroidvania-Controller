# Project Diary

## Decisions and Lessons

- Package 3 keeps `WorldWeatherState` as the sole actual-weather owner and `WorldTimeState` as the
  sole clock owner. Generator cache state is nonpersisted and cannot affect deterministic output.
- Slot mutation and actual-history recording use one atomic path. Same-result resolutions do not
  emit events or fragment history; active override edits use the current minute as the boundary.
- Catalog-changing recompletion must snapshot current runtime weather/history/overrides before
  reconciliation. Reusing the original pending DTO loses post-load runtime changes.
- Forecast and history surfaces are immutable queries. Forecasts emit a current clipped segment and
  one segment for every strictly future configured slot, including repeated weather types.
- Headless Unity repeatedly removed `SENTIS_ANALYTICS_ENABLED`; restore only that proven test-run
  side effect and verify `ProjectSettings.asset` against Git after every gate.
- Explicit fixture unions are required for climate/time validation. A broad literal
  `WorldClimate;WorldTime` filter under-selects tests.
- Package 4 is room context and authoring tooling only. Presentation, particles, lighting,
  post-processing, and weather audio remain later consumers even when a task brief mentions them
  as contextual possibilities.
- `RoomClimateContext` stays passive scene metadata: an authored region ID plus independent
  `EnvironmentExposure`. It has no clock/weather/catalog/save reference and no lifecycle work.
- Matrix enum growth requires a two-dimensional row-major remap; naïve array resize moves logical
  FROM/TO cells. Destructive shrink/corrupt reset paths require explicit confirmation.
- Climate validation allows many rooms to share a region while enforcing one context per enabled
  gameplay scene, no context in Boot, and catalog membership for every authored region ID.
- The current four sample rooms use provisional `region_underbrew` / `Outdoor` assignments; future
  content review may reauthor exposure without changing simulation.
- A live Unity MCP Editor can remain usable even when `unity status` has no Pipeline instance. The
  real Test Runner and scene APIs authored and verified Package 4 without raw scene-YAML edits.
- The historical camera assertion failure did not reproduce in the Package 4 full EditMode run;
  report current evidence rather than carrying an old failure forward as permanent baseline.
