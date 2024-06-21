using UnityEngine;

public class MapSettings : MonoBehaviour
{
    public static MapSettings i;
    public float minimapScale = 1;
    [SerializeField] public Transform[] playerSpawn;
    public Camera cam;
    public Spawner[] unitSpawner;
    private void Awake()
    {
        i = this;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        foreach (Transform _t in playerSpawn) {
            Gizmos.DrawSphere(_t.position+Vector3.up, 1);
        }
        Gizmos.color = Color.red;
        foreach (Spawner _unitSpawner in unitSpawner)
        {
            Gizmos.DrawSphere(_unitSpawner.transform.position+Vector3.up, 1);
        }
    }
}
