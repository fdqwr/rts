using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace rts.Unit
{
    using rts.UI;
    using rts.Player;
    public class Unit : NetworkBehaviour, IDamageable
    {
        [field: SerializeField] public Settings unitSettings { get; private set; }
        [field: SerializeField] public Animator characterAnimator { get; private set; }
        [SerializeField] GameObject onDeathEffect;
        [field: SerializeField] public GameObject visualObject { get; private set; }
        [field: SerializeField] public GameObject constructionBox { get; private set; }
        [field: SerializeField] public Transform targetTransform { get; private set; }
        [SerializeField] Transform newUnitSpawnPosition;
        [field: SerializeField] public Transform newUnitAfterSpawnPosition { get; private set; }
        [SerializeField] Transform newUnitSpawnDoor;
        [SerializeField] float doorOffset;
        [SerializeField] Vector3 wheelsAxis = new Vector3(0, 0, 1);
        [SerializeField] Transform[] wheels;
        [SerializeField] MeshRenderer[] unitRendererColor;
        [SerializeField] SkinnedMeshRenderer[] unitSkinnedRendererColor;
        [field: SerializeField] public UnitWeaponStatsStruct[] unitWeapons { get; private set; }
        public NetworkVariable<float> currentBuildPoints { get; private set; } = new NetworkVariable<float>(0);
        public NetworkVariable<int> team { get; private set; } = new NetworkVariable<int>(0);
        public NetworkVariable<int> id { get; private set; } = new NetworkVariable<int>(0);
        public NetworkVariable<int> playerID { get; private set; } = new NetworkVariable<int>(99999);
        public NetworkVariable<float> health { get; private set; } = new NetworkVariable<float>(100);
        public NetworkVariable<int> insideUnitID { get; private set; } = new NetworkVariable<int>(-1);
        public Aircraft unitAir { get; private set; }
        public Collider col { get; private set; }
        public Rigidbody rb { get; private set; }
        public Supply unitResources { get; private set; }
        public Builder unitBuilder { get; private set; }
        public Carrier unitCarrier { get; private set; }
        public Orders orders { get; private set; }
        public bool isDestroyed { get; private set; }
        public UnitUI unitUI { get; private set; }
        public List<int> spawnQueue { get; private set; } = new List<int>();
        NavMeshAgent agent;
        NavMeshObstacle obstacle;
        Vector3 buildFinished;
        Vector3 buildStarted;
        Vector3 prevModelPosition;
        Vector3 originalDoorPositon;
        Quaternion defaultVisualRotation;
        Quaternion targetRotation;
        float destroyedTime;
        public float maxRange { get; private set; }
        float minRange;
        float currSpawnTime = 0;
        int fixedFrame;
        bool isOwned;
        Transform t;
        Player player;
        public Unit insideUnitClass { get; private set; } = null;
        SetupDataStruct setupData;
        public bool IsInvulnerable => unitSettings.maxHealth > 999999;
        public bool IsBuild => currentBuildPoints.Value == unitSettings.buildPointsNeeded || unitSettings.unitType != Settings.UnitType.building;
        public float SpawnProgress => currSpawnTime / SpawnTime;
        public float SpawnTime => unitSettings.spawnSpeedModifier * GameData.i.unitSettings[spawnQueue[0]].buildPointsNeeded;
        public bool WeaponReady(int _i, float _d) => unitWeapons.Length > 0
        && unitWeapons[_i].cFirerateReload <= 0
        && (!unitWeapons[_i].unitAim || unitWeapons[_i].unitAim.onTheTarget)
        && _d < unitWeapons[_i].maxDistance && _d > unitWeapons[_i].minDistance
        && (unitWeapons[_i].ammoSize == 0 || unitWeapons[_i].currentAmmo > 0);
        [System.Serializable]
        public struct UnitWeaponStatsStruct
        {
            public Turret unitAim;
            public bool unitAimParent;
            public float damage;
            public float firerate;
            public int ammoSize;
            public float reloadSpeed;
            [HideInInspector] public float cFirerateReload;
            [HideInInspector] public float cReloadSpeed;
            [HideInInspector] public int currentAmmo;
            public ParticleSystem shootParticles;
            public Rigidbody projectile;
            public Transform[] projectileSpawn;
            public bool isProjectileHoming;
            public float projectileSpeed;
            public float maxDistance;
            public float minDistance;
        }
        struct SetupDataStruct
        {
            public int team;
            public int id;
            public int playerID;
            public bool isInstaBuild;
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
                unitWeapons[i].currentAmmo = unitWeapons[i].ammoSize;
            col = GetComponent<Collider>();
            rb = GetComponent<Rigidbody>();
            unitAir = GetComponent<Aircraft>();
            unitCarrier = GetComponent<Carrier>();
            if (unitSettings.unitType == Settings.UnitType.builder)
                unitBuilder = gameObject.AddComponent<Builder>();
            if (unitWeapons.Length != 0)
                gameObject.AddComponent<Attack>();
            if (unitSettings.unitType != Settings.UnitType.building)
                gameObject.AddComponent<Movement>();
            orders = gameObject.AddComponent<Orders>();
            unitResources = GetComponent<Supply>();
            agent = GetComponent<NavMeshAgent>();
            obstacle = GetComponent<NavMeshObstacle>();
            t = transform;
            defaultVisualRotation = visualObject.transform.localRotation;
            buildFinished = visualObject.transform.localPosition;
            buildStarted = buildFinished - Vector3.up * unitSettings.constructionOffset;
            if (newUnitSpawnDoor)
                originalDoorPositon = newUnitSpawnDoor.position;
        }
        void Start()
        {
            if (!t.parent || !t.parent.GetComponent<Player>())
                OnBuildChanged(0, 0);
        }
        void FixedUpdate()
        {
            fixedFrame++;
            RotateVisualToGround();
        }
        void Update()
        {
            if (isDestroyed)
            {
                destroyedTime += Time.deltaTime;
                if (destroyedTime > 10)
                    t.position += t.up * -1 * Time.deltaTime;
                return;
            }
            VisualRotation();
            UpdTimers();
            if (unitSettings.spawnSpeedModifier > 0 && spawnQueue.Count > 0 && currSpawnTime > SpawnTime && IsBuild)
                ProduceNewUnit();
            prevModelPosition = visualObject.transform.position;
            if (!agent || !IsServer)
                return;
            if (orders.unitTargetClass && orders.unitTargetClass.isDestroyed)
                agent.stoppingDistance = 99999;
            else agent.stoppingDistance = unitSettings.stoppingDistance;
        }

        public override void OnNetworkSpawn()
        {
            OnAddingUnit();
        }
        public void AddToQueue(int _type, float _cost)
        {
            if (spawnQueue.Count > 8)
                return;
            if (unitAir && GameData.i.unitSettings[_type].unitType != Settings.UnitType.helicopter)
            {
                int _i = unitAir.FreeHangarsLeft();
                foreach (int __i in spawnQueue)
                    if (GameData.i.unitSettings[_type].unitType == Settings.UnitType.helicopter)
                        _i--;
                if (_i <= 0)
                    return;
            }
            if (_cost <= player.money.Value)
                player.money.Value -= _cost;
            else return;
            spawnQueue.Add(_type);
            SyncQueueRpc(spawnQueue.ToArray(), false);
        }
        public void RemoveFromQueue(int _slot)
        {
            if (spawnQueue.Count <= _slot)
                return;
            GameData.i.GetPlayer(playerID.Value).money.Value += GameData.i.unitSettings[spawnQueue[_slot]].cost;
            spawnQueue.RemoveAt(_slot);
            SyncQueueRpc(spawnQueue.ToArray(), true);
        }
        [Rpc(SendTo.Everyone)]
        public void SyncQueueRpc(int[] _types, bool _resetTimer)
        {
            if (IsServer)
                return;
            if (_resetTimer)
                currSpawnTime = 0;
            spawnQueue = _types.ToList();
        }
        public void SetupData(int _team, int _id, int _playerID, bool _isInstaBuild)
        {
            setupData = new SetupDataStruct { team = _team, id = _id, playerID = _playerID, isInstaBuild = _isInstaBuild };
        }
        public void RemoveAmmo(int _slot)
        {
            unitWeapons[_slot].cFirerateReload = unitWeapons[_slot].firerate;
            int _currentAmmo = 0;
            if (unitWeapons[_slot].ammoSize > 0)
            {
                unitWeapons[_slot].currentAmmo--;
                if (unitWeapons[_slot].reloadSpeed > 0)
                    unitWeapons[_slot].cReloadSpeed = unitWeapons[_slot].reloadSpeed;
                _currentAmmo = unitWeapons[_slot].currentAmmo;
            }
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
        public async void UnitSetup(Unit _u, bool _isInstaBuild)
        {
            Aircraft _unitAir = _u.GetComponent<Aircraft>();
            if (unitAir && _unitAir)
                unitAir.SetupAirUnit(_unitAir);
            else
            {
                NavMeshAgent _uAgent = _u.GetComponent<NavMeshAgent>();
                if (!_isInstaBuild)
                {
                    float _time = Vector3.Distance(newUnitAfterSpawnPosition.position, newUnitSpawnPosition.position) / _uAgent.speed;
                    _u.transform.DOMove(newUnitAfterSpawnPosition.position, _uAgent.speed).SetEase(Ease.Linear).SetSpeedBased();
                    await Task.Delay((int)(_time * 1000));
                }
                _uAgent.enabled = true;
                foreach (Orders.UnitOrderStruct _uO in orders.unitOrderQueue)
                    if (_uO.position.HasValue)
                        _u.orders.SetTargetPos(_uO.position.Value, false, 1);
            }
        }
        public void SetInsideUnitID(int _id)
        {
            if (_id == -1)
                insideUnitClass = null;
            else
                insideUnitClass = GameData.i.GetUnit(_id);
            insideUnitID.Value = _id;
        }
        public void GetDamage(float _d, bool _explosive)
        {
            if (IsServer && !isDestroyed && !IsInvulnerable && (!insideUnitClass || insideUnitClass.unitCarrier.damageReduction < 100))
                GetDamageRpc(_d, _explosive);
        }
        [Rpc(SendTo.Everyone)]
        public void GetDamageRpc(float _d, bool _explosive)
        {
            if (insideUnitClass)
                _d *= (100 - insideUnitClass.unitCarrier.damageReduction);
            unitUI.SActive(true, 3);
            if (!IsServer)
                return;
            health.Value -= _d;
            if (health.Value <= 0)
                DieRpc(_explosive);
        }
        [Rpc(SendTo.Everyone)]
        public void DieRpc(bool _explosive)
        {
            if (isDestroyed)
                return;
            if (IsServer && unitCarrier)
                unitCarrier.OnDiyng();
            if (IsServer)
                GameData.i.GetPlayer(playerID.Value).RemoveUnit(id.Value);
            isDestroyed = true;
            if (characterAnimator && unitSettings.animDeath)
            {
                characterAnimator.SetFloat("X", 0);
                characterAnimator.SetFloat("Z", 0);
                characterAnimator.SetTrigger("Die");
            }
            else if (unitSettings.unitType == Settings.UnitType.building)
                visualObject.SetActive(false);
            else foreach (MeshRenderer _unitRenderer in GetComponentsInChildren<MeshRenderer>())
                    foreach (Material _mat in _unitRenderer.materials)
                        _mat.color = Color.black;
            for (int i = 0; i < unitWeapons.Length; i++)
                if (unitWeapons[i].unitAim)
                    unitWeapons[i].unitAim.enabled = false;
            if (unitAir)
                unitAir.enabled = false;
            if (unitBuilder)
                unitBuilder.enabled = false;
            if (unitCarrier)
                unitCarrier.enabled = false;
            if (unitResources)
                unitResources.enabled = false;
            if (obstacle)
                obstacle.enabled = false;
            if (GetComponent<NetworkTransform>())
                GetComponent<NetworkTransform>().enabled = false;
            if (agent)
                agent.enabled = false;
            if (rb)
                rb.isKinematic = true;
            if (unitAir && rb)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = (t.position - unitAir.prevPosition) / Time.deltaTime;
                col.isTrigger = false;
            }
            else if (col)
                col.enabled = false;
            if (onDeathEffect && _explosive)
            {
                visualObject.SetActive(false);
                onDeathEffect.SetActive(true);
            }
            unitUI.gameObject.SetActive(false);
            OnRemovingUnit();
            Invoke("Despawn", 25);
        }
        void Despawn()
        {
            NetworkObject.Despawn();
        }
        void VisualRotation()
        {
            Unit _u = orders.unitTargetClass;
            if (!_u)
                _u = orders.nearbyUnitTargetClass;
            Vector3 _velocity = visualObject.transform.InverseTransformDirection(visualObject.transform.position - prevModelPosition) * Time.deltaTime * 5000;
            foreach (Transform _wheel in wheels)
                _wheel.Rotate(wheelsAxis * _velocity.z * Time.deltaTime * 50);
            if (unitSettings.rotateVisualToTarget)
            {
                characterAnimator.SetFloat("X", Mathf.Clamp(_velocity.z, -1, 1));
                characterAnimator.SetFloat("Z", Mathf.Clamp(_velocity.x, -1, 1));
                if (_u && !_u.isDestroyed && (orders.nearbyUnitTargetClass || orders.isAttackingTarget))
                {
                    Vector3 targetDirection = _u.targetTransform.position - visualObject.transform.position;
                    visualObject.transform.rotation = Quaternion.LookRotation(targetDirection);
                    visualObject.transform.eulerAngles = new Vector3(0, visualObject.transform.eulerAngles.y, 0);
                }
                else
                    visualObject.transform.localRotation = Quaternion.Lerp(visualObject.transform.localRotation, defaultVisualRotation, 0.2f);
            }
        }
        void OnAddingUnit()
        {
            currentBuildPoints.OnValueChanged += OnBuildChanged;
            insideUnitID.OnValueChanged += OnInsideUnitIDChange;
            if (IsServer)
            {
                team.Value = setupData.team;
                id.Value = setupData.id;
                playerID.Value = setupData.playerID;
                if (setupData.isInstaBuild)
                    currentBuildPoints.Value = unitSettings.buildPointsNeeded;
                else currentBuildPoints.Value = 0;
            }
            GameData.i.AddUnit(id.Value, this);
            if (IsServer)
                health.Value = unitSettings.maxHealth;
            health.OnValueChanged += OnHealthChanged;
            if (playerID.Value != 99999)
            {
                player = GameData.i.GetPlayer(playerID.Value);
                player.AddUnit(this);
                player.AddUnitToCategory(this);
            }
            isOwned = playerID.Value == Player.localPlayer;
            if (unitSettings.unitType == Settings.UnitType.building)
                GameData.i.AddBuilding(playerID.Value, this);
            foreach (MeshRenderer _unitRenderer in unitRendererColor)
                _unitRenderer.materials[_unitRenderer.materials.Length - 1].color = GameData.i.GetColor(team.Value);
            foreach (SkinnedMeshRenderer _unitRenderer in unitSkinnedRendererColor)
                _unitRenderer.materials[_unitRenderer.materials.Length - 1].color = GameData.i.GetColor(team.Value);
            UIActive(false);
        }
        void OnRemovingUnit()
        {
            health.OnValueChanged -= OnHealthChanged;
            currentBuildPoints.OnValueChanged -= OnBuildChanged;
            insideUnitID.OnValueChanged -= OnInsideUnitIDChange;
            if (unitUI)
                unitUI.SelfDestruct();
            GameData.i.RemoveUnit(id.Value, this);
            if (team.Value > 0)
            {
                player = GameData.i.GetPlayer(playerID.Value);
                player.RemoveUnit(this);
                player.RemoveUnitFromCategory(this);
            }
            if (unitSettings.unitType == Settings.UnitType.building)
                GameData.i.RemoveBuilding(playerID.Value, this);
        }
        void UpdTimers()
        {
            int _weaponsWithAmmo = 0;
            for (int i = 0; i < unitWeapons.Length; i++)
            {
                unitWeapons[i].cFirerateReload -= Time.deltaTime;
                if (unitWeapons[i].reloadSpeed > 0 && unitWeapons[i].cReloadSpeed >= 0)
                {
                    unitWeapons[i].cReloadSpeed -= Time.deltaTime;
                    if (unitWeapons[i].cReloadSpeed < 0)
                        unitWeapons[i].currentAmmo = unitWeapons[i].ammoSize;
                }
                else if (unitAir && unitAir.aircraftState == Aircraft.AircraftState.inHangar)
                    unitWeapons[i].currentAmmo = unitWeapons[i].ammoSize;
                if (unitWeapons[i].currentAmmo > 0)
                    _weaponsWithAmmo++;
            }
            if (IsServer && unitAir && unitAir.airport)
            {
                if (unitAir.aircraftState == Aircraft.AircraftState.inHangar)
                    health.Value += Time.deltaTime * 10;
                else if (_weaponsWithAmmo == 0)
                    orders.SetTargetRpc(unitAir.airport.unit.id.Value, false, -1);
            }
            if (spawnQueue.Count > 0 && IsBuild)
                currSpawnTime += Time.deltaTime;
        }
        void RotateVisualToGround()
        {
            if (!agent || orders.unitOrderQueue.Count <= 0 || !Physics.Raycast(new Vector3(t.position.x, t.position.y, t.position.z), -t.up, out RaycastHit _hit, 5))
                return;
            targetRotation = Quaternion.FromToRotation(visualObject.transform.up, _hit.normal) * t.rotation;
            visualObject.transform.rotation = Quaternion.Lerp(visualObject.transform.rotation, targetRotation, 0.15f);
        }
        void OnBuildChanged(float _prev, float _current)
        {
            if (unitSettings.unitType != Settings.UnitType.building)
                return;
            if (constructionBox)
            {
                constructionBox.SetActive(_current > 0 && _current < unitSettings.buildPointsNeeded);
                if (_current / unitSettings.buildPointsNeeded < 0.01f)
                    constructionBox.transform.localPosition = Vector3.Lerp(buildStarted, buildFinished, (_current / unitSettings.buildPointsNeeded) * 100);
                if (_current / unitSettings.buildPointsNeeded > 0.99f)
                    constructionBox.transform.localPosition = Vector3.Lerp(buildFinished, buildStarted, (_current / unitSettings.buildPointsNeeded - 0.99f) * 100);
            }
            if (unitRendererColor.Length > 0)
            {
                if (IsBuild)
                    visualObject.transform.localPosition = buildFinished;
                else visualObject.transform.localPosition = Vector3.Lerp(buildStarted, buildFinished, _current / unitSettings.buildPointsNeeded);
            }
            if (isOwned)
            {
                GameEvents.i.UpdateIsBuildUI();
                if (IsBuild)
                    unitUI.SetBuildUI(100);
                else unitUI.SetBuildUI(_current / unitSettings.buildPointsNeeded * 100);
            }
            if (IsServer && IsBuild)
                foreach (int _i in unitSettings.spawnOnBuild)
                    StartCoroutine(GameData.i.SpawnUnit(playerID.Value, team.Value, id.Value, newUnitAfterSpawnPosition.position, newUnitAfterSpawnPosition.rotation, _i, true));
        }
        void OnHealthChanged(float _prev, float _current)
        {
            float _ratio = _current / unitSettings.maxHealth;
            unitUI.SetHealthUI(_ratio);
        }
        void OnInsideUnitIDChange(int _prev, int _current)
        {
            visualObject.SetActive(_current < 0 || !insideUnitClass.unitCarrier.hideInside);
            unitUI.gameObject.SetActive(_current < 0 || !insideUnitClass.unitCarrier.hideInside);
            orders.SetTargetNull();
            if (agent)
                agent.enabled = (_current < 0);
            if (!isOwned)
                return;
            foreach (int _i in GameData.i.GetPlayer(playerID.Value).selectedUnitList)
                if (_i == _current || _i == _prev)
                    GameEvents.i.UnitSelection(GameData.i.GetUnit(_i).unitSettings.type);
        }
        void ProduceNewUnit()
        {
            currSpawnTime = 0;
            if (IsServer)
                StartCoroutine(GameData.i.SpawnUnit(playerID.Value, team.Value, id.Value, newUnitSpawnPosition.position, newUnitSpawnPosition.rotation, spawnQueue[0], false));
            spawnQueue.RemoveAt(0);
            if (unitAir || !newUnitSpawnDoor)
                return;
            newUnitSpawnDoor.DOMove(newUnitSpawnDoor.position + Vector3.down * doorOffset, 1);
            newUnitSpawnDoor.DOMove(originalDoorPositon, 2).SetDelay(2.5f);
        }
    }
}