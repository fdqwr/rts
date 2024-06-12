using UnityEngine;
public class UnitSpawner : MonoBehaviour
{
    [SerializeField] Transform newUnitPos;
    [SerializeField] int team = 0;
    [SerializeField] int nextSpawn = 2;
    [SerializeField] bool spawnOnStart;
    [SerializeField] bool alwaysSpawn;
    [SerializeField] float spawnSpeed = 3;
    [SerializeField] float currSpawnTime = 0;
    public void OnSpawn()
    {
        if(spawnOnStart && GameManager.i.IsServer)
            StartCoroutine(GameManager.i.SpawnUnit(99999, team, -1, newUnitPos.position, newUnitPos.rotation, nextSpawn,true));
    }

    void Update()
    {
        currSpawnTime += Time.deltaTime;
        if (alwaysSpawn && GameManager.i.IsServer && currSpawnTime > spawnSpeed)
        {
            currSpawnTime = 0;
            StartCoroutine(GameManager.i.SpawnUnit(99999, team, -1, newUnitPos.position, newUnitPos.rotation, nextSpawn,true));
        }
    }
}
