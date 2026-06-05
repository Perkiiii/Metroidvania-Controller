using UnityEngine;

namespace WorldGraphEditor
{
    public readonly struct PushData
    {
        public readonly Vector3 Force;
        public readonly bool Exists;

        public PushData(Vector3 force)
        {
            Force = force;
            Exists = true;
        }
    }
}