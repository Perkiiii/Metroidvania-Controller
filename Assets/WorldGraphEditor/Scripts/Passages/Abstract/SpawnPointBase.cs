using UnityEngine;

namespace WorldGraphEditor
{
    public abstract class SpawnPointBase : MonoBehaviour
    {
        public abstract Vector3 GetPosition();
    }
}