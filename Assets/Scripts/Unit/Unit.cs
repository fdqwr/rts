using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Zenject;
namespace rts.Unit
{
    using rts.UI;
    using rts.Player;
    using rts.GameLogic;
    [RequireComponent(typeof(ZenAutoInjecter))]
    public class Unit : NetworkBehaviour
    {
        [field: SerializeField] public Settings settings { get; private set; }
        [field: SerializeField] public Animator animator { get; private set; }
        [field: SerializeField] public GameObject onDeathEffect { get; private set; }
        [field: SerializeField] public GameObject visualObject { get; private set; }
        [field: SerializeField] public GameObject constructionBox { get; private set; }
        [field: SerializeField] public Transform targetTransform { get; private set; }
        [SerializeField] Transform newUnitSpawnPosition;
        [field: SerializeField] public Transform newUnitAfterSpawnPosition { get; private set; }
        [SerializeField] Transform newUnitSpawnDoor;
        [SerializeField] float doorOffset;
        [field: SerializeField] public Vector3 wheelsAxis { get; private set; } = new Vector3(0, 0, 1);
        [field: SerializeField] public Transform[] wheels { get; private set; }
        [SerializeField] MeshRenderer[] unitRendererColor;
        [SerializeField] SkinnedMeshRenderer[] unitSkinnedRendererColor;
        [field: SerializeField] public UnitWeaponStatsStruct[] unitWeapons { get; private set; }
        public NetworkVariable<float> currentBuildPoints { get; private set; } = new NetworkVariable<float>(0);
        public NetworkVariable<int> team { get; private set; } = new NetworkVariable<int>(0);
        public NetworkVariable<int> id { get; private set; } = new NetworkVariable<int>(0);
        public NetworkVariable<int> playerID { get; private set; } = new NetworkVariable<int>(99999);
        public NetworkVariable<int> insideID { get; private set; } = new NetworkVariable<int>(-1);
        public Aircraft aircraft { get; private set; }
        public Airfield airfield { get; private set; }
        public Health healthClass { get; private set; }
        public Collider col { get; private set; }
        public Rigidbody rb { get; private set; }
        public Supply supply { get; private set; }
        public Builder builder { get; private set; }
        public Carrier carrier { get; private set; }
        public Orders orders { get; private set; }
        public UnitUI unitUI { get; private set; }
        public List<int> spawnQueue { get; private set; } = new List<int>();
        NavMeshAgent agent;
        Vector3 buildFinished;
        Vector3 buildStarted;
        Vector3 originalDoorPositon;
        public Quaternion defaultVisualRotation { get; private set; }
        Quaternion targetRotation;
        public float maxRange { get; private set; }
        float minRange;
        float spawnProgress = 0;
        bool isOwned;
        Transform t;
        Player player;
        GameData gameData;
        GameManager gameManager;
        MenuUI menuUI;
        public Unit insideClass { get; private set; } = null;
        SetupDataStruct setupData;
        public bool IsInvulnerable => settings.maxHealth > 999999;
        public bool IsConstructionFinished => currentBuildPoints.Value == settings.buildPointsNeeded || !settings.IsBuilding;
        public float SpawnProgressPercent => spawnProgress / SpawnTime;
        public float SpawnTime => settings.spawnSpeedModifier * gameData.unitSettings[spawnQueue[0]].buildPointsNeeded;
        public bool IsWeaponReady(int _i, float _d) => unitWeapons.Length > 0
        && unitWeapons[_i].firerateProgress <= 0
        && (!unitWeapons[_i].unitAim || unitWeapons[_i].unitAim.onTheTarget)
        && _d < unitWeapons[_i].maxDistance && _d > unitWeapons[_i].minDistance
        && (unitWeapons[_i].maxAmmo == 0 || unitWeapons[_i].currentAmmo > 0);
        [System.Serializable]
        public struct UnitWeaponStatsStruct
        {
            public Turret unitAim;
            public bool unitAimParent;
            public float damage;
            public float firerate;
            public int maxAmmo;
            public float reloadSpeed;
            public ParticleSystem shootParticles;
            public Rigidbody projectile;
            public Transform[] projectileSpawn;
            public bool isProjectileHoming;
            public float projectileSpeed;
            public float maxDistance;
            public float minDistance;
            [HideInInspector] public float firerateProgress;
            [HideInInspector] public float reloadProgress;
            [HideInInspector] public int currentAmmo;
            public void RemoveAmmo()
            {
                firerateProgress = firerate;
                if (maxAmmo > 0)
                {
                    currentAmmo--;
                    if (reloadSpeed > 0)
                        reloadProgress = reloadSpeed;
                }
            }

        }
        struct SetupDataStruct
        {
            public int team;
            public int id;
            public int playerID;
            public bool isInstaBuild;
        }

        [Inject]
        public void Construct(GameManager _gameManager, GameData _gameData, MenuUI _menuUI)
        {
            gameManager = _gameManager;
            gameData = _gameData;
            menuUI = _menuUI;
        }

        private void Awake()
        {
            foreach (UnitWeaponStatsStruct _uW in unitWeapons)
            {
                if (_uW.maxDistance > maxRange)
                    maxRange = _uW.maxDistance;
                if (_uW.minDistance > minRange)
                    minRange = _uW.minDistance;
            }
            for (int i = 0; i < unitWeapons.Length; i++)
                unitWeapons[i].currentAmmo = unitWeapons[i].maxAmmo;
            col = GetComponent<Collider>();
            rb = GetComponent<Rigidbody>();
            aircraft = GetComponent<Aircraft>();
            airfield = GetComponent<Airfield>();
            carrier = GetComponent<Carrier>();
            if (settings.unitType == Settings.UnitType.builder)
                builder = gameObject.AddComponent<Builder>();
            if (unitWeapons.Length != 0)
                gameObject.AddComponent<Attack>();
            if (!settings.IsBuilding)
                gameObject.AddComponent<Movement>();
            if (!IsInvulnerable)
                healthClass = gameObject.AddComponent<Health>();
            orders = gameObject.AddComponent<Orders>();
            supply = GetComponent<Supply>();
            agent = GetComponent<NavMeshAgent>();
            t = transform;
            defaultVisualRotation = visualObject.transform.localRotation;
            buildFinished = visualObject.transform.localPosition;
            buildStarted = buildFinished + Vector3.down * settings.constructionOffset;
            if (newUnitSpawnDoor)
                originalDoorPositon = newUnitSpawnDoor.position;
        }

        void Start()
        {
            if (!t.parent || !t.parent.GetComponent<Player>())
                OnBuildProgressChanged(0, 0);
        }

        void FixedUpdate()
        {
            RotateVisualToGround();
        }

        void Update()
        {
            if (healthClass && healthClass.isDestroyed)
                return;
            UpdTimers();
            if (settings.spawnSpeedModifier > 0 && spawnQueue.Count > 0 && spawnProgress > SpawnTime && IsConstructionFinished)
                ProduceNewUnit();
            if (!agent || !IsServer)
                return;
            if (orders.targetClass && orders.targetClass.healthClass && orders.targetClass.healthClass.isDestroyed)
                agent.stoppingDistance = 99999;
            else agent.stoppingDistance = settings.stoppingDistance;
        }

        public override void OnNetworkSpawn()
        {
            OnAddingUnit();
        }

        public void AddToQueue(int _type, float _cost)
        {
            if (spawnQueue.Count > 8)
                return;
            if (airfield && gameData.unitSettings[_type].unitType == Settings.UnitType.airplane)
            {
                int _i = airfield.FreeHangarsLeft();
                foreach (int __i in spawnQueue)
                    if (gameData.unitSettings[_type].unitType == Settings.UnitType.airplane)
                        _i--;
                if (_i < 1)
                    return;
            }
            if (player.money.Value > _cost)
                player.money.Value -= _cost;
            else return;
            spawnQueue.Add(_type);
            SyncQueueRpc(spawnQueue.ToArray(), false);
        }

        public void RemoveFromQueue(int _slot)
        {
            if (spawnQueue.Count <= _slot)
                return;
            gameData.GetPlayer(playerID.Value).money.Value += gameData.unitSettings[spawnQueue[_slot]].cost;
            spawnQueue.RemoveAt(_slot);
            SyncQueueRpc(spawnQueue.ToArray(), true);
        }

        [Rpc(SendTo.Everyone)]
        public void SyncQueueRpc(int[] _types, bool _resetTimer)
        {
            if (IsServer)
                return;
            if (_resetTimer)
                spawnProgress = 0;
            spawnQueue = _types.ToList();
        }

        public void SetData(int _team, int _id, int _playerID, bool _isInstaBuild)
        {
            setupData = new SetupDataStruct { team = _team, id = _id, playerID = _playerID, isInstaBuild = _isInstaBuild };
        }

        public void UIActive(bool _active)
        {
            if (unitUI)
                unitUI.SActive(_active, -1);
        }

        public void SetUI(UnitUI _unitUI)
        {
            unitUI = _unitUI;
        }

        public async void SpawnedUnitSetup(Unit _u, bool _isInstaBuild)
        {
            Aircraft _aircraft = _u.GetComponent<Aircraft>();
            if (airfield && _aircraft)
                airfield.SetupAirUnit(_aircraft);
            else
            {
                NavMeshAgent _agent = _u.GetComponent<NavMeshAgent>();
                if (!_isInstaBuild)
                {
                    float _time = Vector3.Distance(newUnitAfterSpawnPosition.position, newUnitSpawnPosition.position) / _agent.speed;
                    _u.transform.DOMove(newUnitAfterSpawnPosition.position, _agent.speed).SetEase(Ease.Linear).SetSpeedBased();
                    await Task.Delay((int)(_time * 1000));
                }
                _agent.enabled = true;
                foreach (Orders.UnitOrderStruct _uO in orders.unitOrderQueue)
                    if (_uO.position.HasValue)
                        _u.orders.SetTargetPos(_uO.position.Value, false, 1);
            }
        }

        public void SetInsideUnitID(int _id)
        {
            if (_id == -1)
                insideClass = null;
            else
                insideClass = gameData.GetUnit(_id);
            insideID.Value = _id;
        }

        void OnAddingUnit()
        {
            currentBuildPoints.OnValueChanged += OnBuildProgressChanged;
            insideID.OnValueChanged += OnInsideUnitIDChange;
            if (IsServer)
            {
                team.Value = setupData.team;
                id.Value = setupData.id;
                playerID.Value = setupData.playerID;
                if (setupData.isInstaBuild)
                    currentBuildPoints.Value = settings.buildPointsNeeded;
                else currentBuildPoints.Value = 0;
            }
            gameManager.AddUnit(id.Value, this);
            if (IsServer && healthClass)
                healthClass.HealthSetup();
            if (playerID.Value != 99999)
            {
                player = gameData.GetPlayer(playerID.Value);
                player.AddUnit(this);
                player.AddUnitToCategory(this);
            }
            isOwned = playerID.Value == Player.localPlayerID;
            if (settings.IsBuilding)
                gameData.AddBuilding(playerID.Value, this);
            foreach (MeshRenderer _unitRenderer in unitRendererColor)
                _unitRenderer.materials[_unitRenderer.materials.Length - 1].color = gameData.GetColor(team.Value);
            foreach (SkinnedMeshRenderer _unitRenderer in unitSkinnedRendererColor)
                _unitRenderer.materials[_unitRenderer.materials.Length - 1].color = gameData.GetColor(team.Value);
            UIActive(false);
        }

        public void OnRemovingUnit()
        {
            currentBuildPoints.OnValueChanged -= OnBuildProgressChanged;
            insideID.OnValueChanged -= OnInsideUnitIDChange;
            if (unitUI)
                unitUI.SelfDestruct();
            gameData.RemoveUnit(id.Value, this);
            if (team.Value > 0)
            {
                player = gameData.GetPlayer(playerID.Value);
                player.RemoveUnit(this);
                player.RemoveUnitFromCategory(this);
            }
            if (settings.IsBuilding)
                gameData.RemoveBuilding(playerID.Value, this);
        }

        void UpdTimers()
        {
            int _weaponsWithAmmo = 0;
            for (int i = 0; i < unitWeapons.Length; i++)
            {
                unitWeapons[i].firerateProgress -= Time.deltaTime;
                if (unitWeapons[i].reloadSpeed > 0 && unitWeapons[i].reloadProgress >= 0)
                {
                    unitWeapons[i].reloadProgress -= Time.deltaTime;
                    if (unitWeapons[i].reloadProgress < 0)
                        unitWeapons[i].currentAmmo = unitWeapons[i].maxAmmo;
                }
                else if (aircraft && aircraft.aircraftState == Aircraft.AircraftState.nearHangar)
                    unitWeapons[i].currentAmmo = unitWeapons[i].maxAmmo;
                if (unitWeapons[i].currentAmmo > 0)
                    _weaponsWithAmmo++;
            }
            if (IsServer && aircraft && aircraft.airfield && aircraft.aircraftState != Aircraft.AircraftState.nearHangar && _weaponsWithAmmo == 0)
                    orders.SetTargetRpc(aircraft.airfield.unit.id.Value, false, -1);
            if (spawnQueue.Count > 0 && IsConstructionFinished)
                spawnProgress += Time.deltaTime;
        }

        void RotateVisualToGround()
        {
            if (!agent || orders.unitOrderQueue.Count <= 0 || !Physics.Raycast(t.position, -t.up, out RaycastHit _hit, 5))
                return;
            targetRotation = Quaternion.FromToRotation(visualObject.transform.up, _hit.normal) * t.rotation;
            visualObject.transform.rotation = Quaternion.Lerp(visualObject.transform.rotation, targetRotation, 0.15f);
        }

        void OnBuildProgressChanged(float _prev, float _current)
        {
            if (!settings.IsBuilding)
                return;
            if (constructionBox)
            {
                constructionBox.SetActive(_current > 0 && _current < settings.buildPointsNeeded);
                if (_current / settings.buildPointsNeeded < 0.01f)
                    constructionBox.transform.localPosition = Vector3.Lerp(buildStarted, buildFinished, (_current / settings.buildPointsNeeded) * 100);
                if (_current / settings.buildPointsNeeded > 0.99f)
                    constructionBox.transform.localPosition = Vector3.Lerp(buildFinished, buildStarted, (_current / settings.buildPointsNeeded - 0.99f) * 100);
            }
            if (unitRendererColor.Length > 0)
            {
                if (IsConstructionFinished)
                    visualObject.transform.localPosition = buildFinished;
                else visualObject.transform.localPosition = Vector3.Lerp(buildStarted, buildFinished, _current / settings.buildPointsNeeded);
            }
            if (isOwned)
            {
                menuUI.UpdateIsBuildUI();
                if (IsConstructionFinished)
                    unitUI.SetBuildUI(100);
                else unitUI.SetBuildUI(_current / settings.buildPointsNeeded * 100);
            }
            if (IsServer && IsConstructionFinished)
                foreach (int _i in settings.spawnOnBuild)
                    StartCoroutine(gameManager.SpawnUnit(playerID.Value, team.Value, id.Value, newUnitAfterSpawnPosition.position, newUnitAfterSpawnPosition.rotation, _i, true));
        }
        void OnInsideUnitIDChange(int _prev, int _current)
        {
            visualObject.SetActive(_current < 0 || !insideClass.carrier.hideInside);
            unitUI.gameObject.SetActive(_current < 0 || !insideClass.carrier.hideInside);
            orders.SetTargetNull();
            if (agent)
                agent.enabled = (_current < 0);
            if (!isOwned)
                return;
            foreach (int _i in gameData.GetPlayer(playerID.Value).selectedUnitList)
                if (_i == _current || _i == _prev)
                    menuUI.UpdateUnitSelectionUI(gameData.GetUnit(_i).settings.id);
        }

        void ProduceNewUnit()
        {
            spawnProgress = 0;
            if (IsServer)
                StartCoroutine(gameManager.SpawnUnit(playerID.Value, team.Value, id.Value, newUnitSpawnPosition.position, newUnitSpawnPosition.rotation, spawnQueue[0], false));
            if (!settings.IsAircraft && newUnitSpawnDoor)
            {
                newUnitSpawnDoor.DOMove(newUnitSpawnDoor.position + Vector3.down * doorOffset, Mathf.Clamp(SpawnTime / 6, 0, 1));
                newUnitSpawnDoor.DOMove(originalDoorPositon, Mathf.Clamp(SpawnTime / 3, 0, 2)).SetDelay(Mathf.Clamp(SpawnTime / 2.5f, 0, 2.5f));
            }
            spawnQueue.RemoveAt(0);
        }
    }
}