using System;

// Neutral, presentation-only phase notification raised by a concrete boss actor.
//
// It deliberately carries no gameplay meaning, no camera detail, and no encounter authority: the
// encounter lifecycle, phase thresholds, and completion gates are unchanged. A scene-side
// presentation adapter subscribes to it to bias framing for a beat; nothing else consumes it.
public interface IBossPresentationPhaseSource
{
    // Argument is the 1-based presentation phase the actor is entering.
    event Action<int> PresentationPhaseChanged;

    int CurrentPresentationPhase { get; }
}
