public enum CameraTransitionCause
{
    None,
    SceneStart,
    FollowToLock,
    LockToLock,
    LockToFollow,
    OverrideReleased,

    // Camera Phase 3 presentation requests. Kept distinct from OverrideReleased so diagnostics can
    // tell a freeze/free release apart from a presentation release.
    PresentationEntered,
    PresentationChanged,
    PresentationReleased
}
