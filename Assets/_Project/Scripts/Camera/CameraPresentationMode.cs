public enum CameraPresentationMode
{
    // Frame one Transform (plus framing offset).
    FocusTarget,

    // Frame one authored world position (plus framing offset). The basic authored-pan case.
    FocusWorldPoint,

    // Frame the region enclosing every valid target (plus framing offset).
    FrameTargets
}
