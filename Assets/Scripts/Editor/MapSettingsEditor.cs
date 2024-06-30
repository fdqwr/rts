using UnityEngine;

namespace rts.Editor
{
    using rts.GameLogic;
    using UnityEngine.SceneManagement;
    public class MapSettingsEditor : MonoBehaviour
    {
        [SerializeField] MapSettings mapSettings;
        private void OnDrawGizmos()
        {
            foreach (MapSettings.Map _map in mapSettings.maps)
            {
                if (_map.scene != SceneManager.GetActiveScene().name)
                    continue;
                Gizmos.color = Color.blue;
                foreach (MapSettings.PlayerSpawn _playerSpawn in _map.playerSpawn)
                    Gizmos.DrawSphere(_playerSpawn.position + Vector3.up*1.5f, 3);
                Gizmos.color = Color.red;
                foreach (MapSettings.SupplySpawn _supplySpawn in _map.supplySpawn)
                    Gizmos.DrawSphere(_supplySpawn.position + Vector3.up*1.5f, 3);
            }
        }
    }
}
