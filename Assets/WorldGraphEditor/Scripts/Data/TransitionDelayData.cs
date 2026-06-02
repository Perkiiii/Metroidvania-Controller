namespace WorldGraphEditor
{
    public class TransitionDelayData
    {
        public DelayType DelayType;
        public float Delay;
        
        public TransitionDelayData(DelayType delayType)
        {
            DelayType = delayType;
            Delay = 0;
        }
        
        public TransitionDelayData(DelayType delayType, float delay)
        {
            DelayType = delayType;
            Delay = delay;
        }
    }
}