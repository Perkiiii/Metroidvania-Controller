using UnityEngine;

namespace WorldGraphEditor
{
    [CreateAssetMenu(fileName = "New Event Call Config", menuName = "World Graph Editor/Event Call Config", order = 0)]
    public class EventCallConfig : ScriptableObject
    {
        [SerializeField] private DelayType _delayType = DelayType.CustomDelay;
        [SerializeField, EnableIfField(nameof(_delayType), DelayType.CustomDelay)] private float _delay = .2f;

        public TransitionDelayData DelayData => new(_delayType, _delay);
    }
}