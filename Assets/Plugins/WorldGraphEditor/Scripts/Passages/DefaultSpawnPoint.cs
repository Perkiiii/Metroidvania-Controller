using UnityEngine;

namespace WorldGraphEditor
{
    public class DefaultSpawnPoint : SpawnPointBase
    {
        [SerializeField] private Transform _spawnPoint;
        
        public override Vector3 GetPosition() => _spawnPoint.position;
    }
}