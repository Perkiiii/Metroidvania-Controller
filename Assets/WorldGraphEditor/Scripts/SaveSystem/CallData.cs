using System;

namespace WorldGraphEditor
{
    [Serializable]
    public struct CallData
    {
        public EventType EventType;
        public string EventGuid;
    }
}