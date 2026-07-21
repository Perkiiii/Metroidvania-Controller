using System;

namespace WorldGraphEditor
{
    public static class TransitionExtensions
    {
        public static TransitionDelayData ResolveDelay(this EventCallConfig config, in TransitionDelayData overrideData)
        {
            return overrideData ?? config?.DelayData ?? new TransitionDelayData(DelayType.ThisFrame);
        }
        
        [Obsolete("Pass ITransitionManager explicitly. Use component.IsOutput(manager). Required when using a custom TransitionManager.")]
        public static bool IsOutput(this ITransitionComponent component)
        {
            if (WGEProjectConfig.Instance.IsCustomManagerEnabled)
                throw new InvalidOperationException(
                    "IsOutput() without ITransitionManager is not supported when a custom TransitionManager is enabled. " +
                    "Use component.IsOutput(manager).");
            
            return component.IsOutput(TransitionManager.Instance);
        }
        
        [Obsolete("Pass ITransitionManager explicitly. Use component.IsInput(manager). Required when using a custom TransitionManager.")]
        public static bool IsInput(this ITransitionComponent component)
        {
            if (WGEProjectConfig.Instance.IsCustomManagerEnabled)
                throw new InvalidOperationException(
                    "IsInput() without ITransitionManager is not supported when a custom TransitionManager is enabled. " +
                    "Use component.IsInput(manager).");
            
            return component.IsInput(TransitionManager.Instance);
        }

        public static bool IsOutput(this ITransitionComponent component, ITransitionManager manager)
        {
            if (manager?.OutputTransitionComponent == null)
                return false;

            return component?.GetGuid() == manager.OutputTransitionComponent.GetGuid();
        }

        public static bool IsInput(this ITransitionComponent component, ITransitionManager manager)
        {
            if (manager?.InputTransitionComponent == null)
                return false;

            return component?.GetGuid() == manager.InputTransitionComponent.GetGuid();
        }

        [Obsolete("Use component.IsShortcutDestination instead.")]
        public static bool IsShortcutOutput(this ITransitionComponent component)
        {
            return IsShortcutDestination(component);
        }
        
        [Obsolete("Use component.IsShortcutOrigin instead.")]
        public static bool IsShortcutInput(this ITransitionComponent component)
        {
            return IsShortcutOrigin(component);
        }
        
        public static bool IsShortcutDestination(this ITransitionComponent component)
        {
            var graph = WGEProjectConfig.Instance.GetWorldGraph();
            
            if (graph == null)
                return false;

            return graph.IsShortcutDestination(component.GetGuid());
        }
        
        public static bool IsShortcutOrigin(this ITransitionComponent component)
        {
            var graph = WGEProjectConfig.Instance.GetWorldGraph();
            
            if (graph == null)
                return false;

            return graph.IsShortcutOrigin(component.GetGuid());
        }
        
        public static bool IsOneWayDestination(this ITransitionComponent component)
        {
            var graph = WGEProjectConfig.Instance.GetWorldGraph();
            
            if (graph == null)
                return false;

            return graph.IsOneWayDestination(component.GetGuid());
        }
        
        public static bool IsOneWayOrigin(this ITransitionComponent component)
        {
            var graph = WGEProjectConfig.Instance.GetWorldGraph();
            
            if (graph == null)
                return false;

            return graph.IsOneWayOrigin(component.GetGuid());
        }
    }
}