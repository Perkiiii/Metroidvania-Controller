namespace WorldGraphEditor
{
    public class TransitionContext : ITransitionContext
    {
        public TransitionDelayData PortEnteredDelay;
        public TransitionDelayData TransitionStartedDelay;
        public TransitionDelayData SceneLoadedDelay;
        public TransitionDelayData TransitionEndedDelay;
    }
}