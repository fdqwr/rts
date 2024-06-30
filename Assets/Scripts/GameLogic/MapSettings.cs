using UnityEditor;
using UnityEngine;
namespace rts.GameLogic
{
    [CreateAssetMenu(fileName = "MapSettings", menuName = "Scriptable Objects/MapSettings")]
    public class MapSettings : ScriptableObject
    {
        [field: SerializeField] public Map[] maps { get; private set; }
        [System.Serializable]
        public struct Map
        {
            [field: SerializeField] public string scene { get; private set; }
            [field: SerializeField] public float minimapScale { get; private set; }
            [field: SerializeField] public PlayerSpawn[] playerSpawn { get; private set; }
            [field: SerializeField] public SupplySpawn[] supplySpawn { get; private set; }
        }
        [System.Serializable]
        public struct PlayerSpawn
        {
            [field: SerializeField] public Vector3 position { get; private set; }
            [field: SerializeField] public Quaternion rotation { get; private set; }
        }
        [System.Serializable]
        public struct SupplySpawn
        {
            [field: SerializeField] public Vector3 position { get; private set; }
            [field: SerializeField] public Quaternion rotation { get; private set; }
        }
    }
}