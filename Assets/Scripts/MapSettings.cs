using UnityEngine;

public class MapSettings : MonoBehaviour
{
    public static MapSettings i;
    public float minimapScale = 1;
    [SerializeField] public Transform[] playerSpawn;
    public Camera cam;
    public UnitSpawner[] unitSpawner;
    private void Awake()
    {
        i = this;
    }
}
