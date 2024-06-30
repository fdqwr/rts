using UnityEngine;
using Zenject;
namespace rts.GameLogic
{
    public class Spawner : MonoBehaviour
    {
        [SerializeField] Transform newUnitPosition;
        [SerializeField] int team = 0;
        [SerializeField] int spawnType = 2;
        [SerializeField] bool spawnOnStart;
        [SerializeField] bool spawnRepeatadly;
        [SerializeField] float spawnSpeed = 3;
        float spawnProgress = 0;
        GameManager gameManager;
        public void Start()
        {
            gameManager = (GameManager)FindFirstObjectByType(typeof(GameManager));
        }

        public void OnSpawn()
        {
            if (spawnOnStart && gameManager.IsServer)
                StartCoroutine(gameManager.SpawnUnit(99999, team, -1, newUnitPosition.position, newUnitPosition.rotation, spawnType, true));
        }

        void Update()
        {
            spawnProgress += Time.deltaTime;
            if (spawnRepeatadly && gameManager.IsServer && spawnProgress > spawnSpeed)
            {
                spawnProgress = 0;
                StartCoroutine(gameManager.SpawnUnit(99999, team, -1, newUnitPosition.position, newUnitPosition.rotation, spawnType, true));
            }
        }
    }
}