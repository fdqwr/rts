using UnityEngine;
public class Spawner : MonoBehaviour
{
    [SerializeField] Transform newUnitPosition;
    [SerializeField] int team = 0;
    [SerializeField] int spawnType = 2;
    [SerializeField] bool spawnOnStart;
    [SerializeField] bool spawnRepeatadly;
    [SerializeField] float spawnSpeed = 3;
    float currSpawnTime = 0;
    public void OnSpawn()
    {
        if(spawnOnStart && GameData.i.IsServer)
            StartCoroutine(GameData.i.SpawnUnit(99999, team, -1, newUnitPosition.position, newUnitPosition.rotation, spawnType,true));
    }

    void Update()
    {
        currSpawnTime += Time.deltaTime;
        if (spawnRepeatadly && GameData.i.IsServer && currSpawnTime > spawnSpeed)
        {
            currSpawnTime = 0;
            StartCoroutine(GameData.i.SpawnUnit(99999, team, -1, newUnitPosition.position, newUnitPosition.rotation, spawnType,true));
        }
    }
}
