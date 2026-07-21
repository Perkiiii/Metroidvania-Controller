using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WorldGraphEditor
{
    internal class EventManager : IDisposable
    {
        private readonly Dictionary<EventType, SortedList<int, List<Func<CancellationToken, Task>>>> _eventHandlers = new();
        
        public void Add(EventType eventType, Func<CancellationToken, Task> func, int priority)
        {
            if (!_eventHandlers.TryGetValue(eventType, out var priorityDict))
            {
                priorityDict = new SortedList<int, List<Func<CancellationToken, Task>>>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
                _eventHandlers[eventType] = priorityDict;
            }

            if (!priorityDict.TryGetValue(priority, out var list))
            {
                list = new List<Func<CancellationToken, Task>>();
                priorityDict[priority] = list;
            }
            
            list.Add(func);
        }

        public void Remove(EventType eventType, Func<CancellationToken, Task> func)
        {
            try
            {
                if (!_eventHandlers.TryGetValue(eventType, out var handlerGroups))
                    return;
                
                foreach (var key in handlerGroups.Keys.ToList())
                {
                    var handlers = handlerGroups[key];
                    if (handlers.Remove(func) && handlers.Count == 0)
                    {
                        handlerGroups.Remove(key);
                    }
                }

                if (handlerGroups.Count == 0)
                {
                    _eventHandlers.Remove(eventType);
                }
            }
            catch (Exception e)
            {
                WGEConsole.Log(e.Message);
                throw;
            }
        }

        public async Task Invoke(EventType eventType, CancellationToken cancellationToken)
        {
            try
            {
                if (!_eventHandlers.TryGetValue(eventType, out var eventHandler))
                    return;
                
                foreach (var priorityGroup in eventHandler)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tasks = priorityGroup.Value.Select(handler => handler(cancellationToken));
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception e)
            {
                WGEConsole.Log(e.Message);
                throw;
            }
        }

        public void Dispose()
        {
            foreach (var handlerGroup in _eventHandlers.Values)
            {
                foreach (var priorityGroup in handlerGroup.Values)
                {
                    priorityGroup.Clear();
                }
                handlerGroup.Clear();
            }
            _eventHandlers.Clear();
        }
    }
}
