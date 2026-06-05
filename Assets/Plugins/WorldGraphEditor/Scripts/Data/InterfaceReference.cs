using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WorldGraphEditor
{
    [Serializable]
    public class InterfaceReference<TInterface, TObject> where TObject : Object where TInterface : class
    {
        [SerializeField, HideInInspector] private TObject _value;

        public TInterface Value
        {
            get => _value switch
            {
                TInterface i => i,
                _ => null
            };
            set => _value = value switch
            {
                TObject newValue => newValue,
                _ => null
            };
        }

        public TObject UnderlyingValue
        {
            get => _value;
            set => _value = value;
        }

        public InterfaceReference()
        {
            
        }

        public InterfaceReference(TObject target)
        {
            _value = target;
        }

        public InterfaceReference(TInterface @interface)
        {
            _value = @interface as TObject;
        }
    }

    [Serializable]
    public class InterfaceReference<TInterface> : InterfaceReference<TInterface, Object> where TInterface : class
    {
    }
}