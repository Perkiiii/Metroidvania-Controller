using System;
using UnityEngine;

namespace WorldGraphEditor
{
    [Serializable]
    public struct Optional<T>
    {
        [SerializeField] private bool _enabled;
        [SerializeField] private T _value;

        public bool Enabled => _enabled;
        public T Value => _value;

        public Optional(T value)
        {
            _value = value;
            _enabled = true;
        }
    }
}