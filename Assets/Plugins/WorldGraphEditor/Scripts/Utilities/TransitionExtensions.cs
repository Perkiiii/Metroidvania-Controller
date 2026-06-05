namespace WorldGraphEditor
{
    public static class TransitionExtensions
    {
        public static TransitionDelayData ResolveDelay(this EventCallConfig config, in TransitionDelayData overrideData)
        {
            return overrideData ?? config?.DelayData ?? new TransitionDelayData(DelayType.ThisFrame);
        }

        public static bool IsOutput(this ITransitionComponent component)
        {
            if (TransitionManager.Instance?.OutputTransitionComponent == null)
                return false;
            
            return component.GetGuid() == TransitionManager.Instance.OutputTransitionComponent.GetGuid();
        }

        public static bool IsInput(this ITransitionComponent component)
        {
            if (TransitionManager.Instance?.InputTransitionComponent == null)
                return false;

            return component.GetGuid() == TransitionManager.Instance.InputTransitionComponent.GetGuid();
        }

        public static bool IsShortcutOutput(this ITransitionComponent component)
        {
            if (TransitionManager.Instance?.Container == null)
                return false;
            
            return TransitionManager.Instance.Container.IsShortcutOutput(component.GetGuid());
        }

        public static bool IsShortcutInput(this ITransitionComponent component)
        {
            if (TransitionManager.Instance?.Container == null)
                return false;
            
            return TransitionManager.Instance.Container.IsShortcutInput(component.GetGuid());
        }
    }
}