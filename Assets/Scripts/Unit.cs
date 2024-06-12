using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class Unit : NetworkBehaviour, IDamageable
{
    [SerializeField] UnitSettings unitSettings;
    [SerializeField] Animator characterAnimator;
    [SerializeField] GameObject onDeathEffect;
    [SerializeField] GameObject visualObject;
    [SerializeField] GameObject constructionBox;
    [SerializeField] Transform targetTransform;
    [SerializeField] Transform newUnitPosition;
    [SerializeField] Vector3 wheelsAxis = new Vector3(0,0,1);
    [SerializeField] Transform[] wheels;
    [SerializeField] MeshRenderer[] unitRendererColor;
    [SerializeField] SkinnedMeshRenderer[] unitSkinnedRendererColor;
    [SerializeField] UnitWeaponStats[] unitWeapons;
    public NetworkVariable<float> currentBuildPoints { get; private set; } = new NetworkVariable<float>(0);
    public NetworkVariable<int> team { get; private set; } = new NetworkVariable<int>(0);
    public NetworkVariable<int> id { get; private set; } = new NetworkVariable<int>(0);
    public NetworkVariable<ulong> playerID { get; private set; } = new NetworkVariable<ulong>(99999);
    public NetworkVariable<float> health { get; private set; } = new NetworkVariable<float>(100);
    public NetworkVariable<int> insideUnitID { get; private set; } = new NetworkVariable<int>(-1);
    public UnitAir unitAir { get; private set; }
    public Collider col { get; private set; }
    public Rigidbody rb { get; private set; }
    public UnitResources unitResources { get; private set; }
    public UnitBuilder unitBuilder { get; private set; }
    public UnitCarrier unitCarrier { get; private set; }
    public int unitTargetID { get; private set; } = -1;
    public bool destroyed { get; private set; }
    public bool attackTarget { get; private set; } = false;
    public Unit unitTargetClass { get; private set; } = null;
    public Vector3 targetPos { get; private set; }
    public UnitUI unitUI { get; private set; }
    public List<int> spawnQueue { get; private set; } = new List<int>();
    NavMeshAgent agent;
    Vector3 buildFinished;
    Vector3 buildStarted;
    Vector3 prevModelPosition;
    Quaternion defaultVisualRotation;
    Quaternion targetRotation;
    float destroyedTime;
    float maxRange;
    float minRange;
    float currSpawnTime = 0;
    int frame;
    bool owned;
    Transform t;
    Player player;
    Unit nearbyUTS = null;
    Unit insideUnit = null;
    public bool nearTarget { get; private set; } = false;
    public GameObject ConstructionBox => constructionBox;
    public GameObject VisualObject => visualObject;
    public Transform TargetTransform => targetTransform;
    public bool IsInvulnerable => unitSettings.maxHealth > 999999;
    public bool IsBuild => currentBuildPoints.Value == unitSettings.buildPointsNeeded || unitSettings.unitType != UnitSettings.UnitType.building;
    public float SpawnProgress => currSpawnTime / unitSettings.spawnSpeed;
    public UnitWeaponStats[] UnitWeapons => unitWeapons;
    public UnitButton UB => unitSettings.uB;
    public Sprite UnitImage => unitSettings.unitImage;
    public int OccupyPSlots => unitSettings.occupyPSlots;
    public int Type => unitSettings.type;
    public float Cost => unitSettings.cost;
    public float SpawnSpeed => unitSettings.spawnSpeed;
    public UnitSettings.UnitType UnitType => unitSettings.unitType;
    [System.Serializable]
    public struct UnitWeaponStats
    {
        public UnitAim unitAim;
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
        public bool projectileHoming;
        public float projectileSpeed;
        public float maxDistance;
        public float minDistance;
    }
    private void Awake()
    {
        foreach (UnitWeaponStats _uW in unitWeapons)
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
        unitAir = GetComponent<UnitAir>();
        unitCarrier = GetComponent<UnitCarrier>();
        unitBuilder = GetComponent<UnitBuilder>();
        unitResources = GetComponent<UnitResources>();
        agent = GetComponent<NavMeshAgent>();
        t = transform;
        defaultVisualRotation = visualObject.transform.localRotation;
        buildFinished = visualObject.transform.localPosition;
        buildStarted = buildFinished - Vector3.up * unitSettings.whenBuildOffset;
        currentBuildPoints.OnValueChanged += OnBuildChanged;
        health.OnValueChanged += OnHealthChanged;
        insideUnitID.OnValueChanged += OnInsideUnitIDChange;
    }
    void Start()
    {
        UIActive(false);
        if(!t.parent || !t.parent.GetComponent<Player>())
        OnBuildChanged(0, 0);
        if (unitSettings.unitType == UnitSettings.UnitType.building && newUnitPosition)
            targetPos = newUnitPosition.position + newUnitPosition.forward * 3;
    }
    private void FixedUpdate()
    {
        if (IsServer && insideUnitID.Value > -1)
            t.position = GameManager.i.GetUnit(insideUnitID.Value).t.position;
        frame++;
        if (agent && frame % 3 == 0 && (Physics.Raycast(new Vector3(t.position.x, t.position.y, t.position.z), -t.up, out RaycastHit _hit, 5)))
        {
            targetRotation = Quaternion.FromToRotation(visualObject.transform.up, _hit.normal) * t.rotation;
            visualObject.transform.rotation = Quaternion.Lerp(visualObject.transform.rotation, targetRotation, 0.1f);
        }
        if (!((frame % 10 == 0 || (nearbyUTS && nearbyUTS.destroyed)) && !unitTargetClass))
            return;
        if (nearbyUTS && nearbyUTS.destroyed)
            nearbyUTS = null;
        if (nearbyUTS)
        {
            if (Vector3.Distance(t.position, nearbyUTS.transform.position) > maxRange * 1.3)
                nearbyUTS = null;
            return;
        }
        float _lowestDistance = 99999;
        Unit _u = null;
        foreach (Unit _unit in GameManager.i.allUnits)
            if (!_unit.destroyed && _unit.team.Value > 0 && _unit.team.Value != team.Value)
            {
                float _distance = Vector3.Distance(t.position, _unit.transform.position);
                if (_distance < maxRange * 1.2 && _lowestDistance > _distance)
                {
                    _u = _unit;
                    _lowestDistance = _distance;
                }
            }
        if (_lowestDistance != 99999)
            nearbyUTS = _u;
    }
    void Update()
    {
        if (destroyed)
        {
            destroyedTime += Time.deltaTime;
            if (destroyedTime > 10)
                t.position += t.up * -1 * Time.deltaTime;
            return;
        }
        Unit _u = unitTargetClass;
        if (!_u)
            _u = nearbyUTS;
        for (int i = 0; i < unitWeapons.Length; i++)
        {
            UnitAim _uA = unitWeapons[i].unitAim;
            if (!_uA || unitWeapons[i].unitAimParent)
                continue;
            if (!_u || _u.destroyed)
                _uA.SetTarget(new Vector3(-99999, -99999, -99999));
            else
            {
                if (_uA.BallisticTrajectory)
                    _uA.SetTarget(_u.col.ClosestPoint(t.position));
                else
                    _uA.SetTarget(_u.targetTransform.position);
            }
        }
        Vector3 _velocity = visualObject.transform.InverseTransformDirection(visualObject.transform.position - prevModelPosition) * Time.deltaTime * 5000;
        foreach (Transform _wheel in wheels)
            _wheel.Rotate(wheelsAxis *_velocity.z * Time.deltaTime * 50);
        if (unitSettings.rotateVisualToTarget)
        {
            float _x = Mathf.Clamp(_velocity.z, -1, 1);
            float _z = Mathf.Clamp(_velocity.x, -1, 1);
            characterAnimator.SetFloat("X", _x);
            characterAnimator.SetFloat("Z", _z);
            if (_u && !_u.destroyed && (nearbyUTS || attackTarget))
            {
                Vector3 targetDirection = _u.targetTransform.position - visualObject.transform.position;
                visualObject.transform.rotation = Quaternion.LookRotation(targetDirection);
                visualObject.transform.eulerAngles = new Vector3(0, visualObject.transform.eulerAngles.y, 0);
            }
            else
                visualObject.transform.localRotation = Quaternion.Lerp(visualObject.transform.localRotation, defaultVisualRotation, 0.2f);
        }
        UpdTimers();
        if (SpawnSpeed > 0 && currSpawnTime > unitSettings.spawnSpeed && IsServer && IsBuild)
            Spawn();
        if (_u && !_u.destroyed && IsServer)
            UpdAction();
        else if (agent && IsServer)
        {
            if (unitTargetClass && unitTargetClass.destroyed)
                agent.stoppingDistance = 99999;
            else agent.stoppingDistance = unitSettings.stoppingDistance;
        }
        prevModelPosition = visualObject.transform.position;
    }
    public override void OnNetworkSpawn()
    {
        if (team.Value != 0)
            player = GameManager.i.GetPlayer(playerID.Value);
        owned = (playerID.Value == NetworkManager.Singleton.LocalClientId);
        GameManager.i.AddUnit(id.Value, this);
        if (team.Value != 0)
            GameManager.i.GetPlayer(playerID.Value).AddUnit(this);
        if (IsServer)
            health.Value = unitSettings.maxHealth;
        if (unitSettings.unitType == UnitSettings.UnitType.building)
            GameManager.i.AddBuilding(playerID.Value, this);
        foreach (MeshRenderer _unitRenderer in unitRendererColor)
            _unitRenderer.materials[_unitRenderer.materials.Length - 1].color = GameManager.i.GetColor(team.Value);
        foreach (SkinnedMeshRenderer _unitRenderer in unitSkinnedRendererColor)
            _unitRenderer.materials[_unitRenderer.materials.Length - 1].color = GameManager.i.GetColor(team.Value);
        if (team.Value != 0)
            player.AddUnitToCategory(this);
    }
    public void AddToQueue(int _type, float _cost)
    {
        if (spawnQueue.Count > 8) 
            return;
        if (unitAir && GameManager.i.unitSettings[_type].unitType != UnitSettings.UnitType.helicopter) {
            int _i = unitAir.FreeHangars();
            foreach (int __i in spawnQueue)
                if (GameManager.i.unitSettings[_type].unitType == UnitSettings.UnitType.helicopter)
                    _i--;
            if (_i <= 0)
                return;
        }
        if (_cost <= player.money.Value)
            player.money.Value -= _cost;
        else return;
        spawnQueue.Add(_type);
        SyncQueueRpc(spawnQueue.ToArray(),false);
    }
    public void RemoveFromQueue(int _slot)
    {
        if (spawnQueue.Count <= _slot) 
            return;
        GameManager.i.GetPlayer(playerID.Value).money.Value += GameManager.i.unitSettings[_slot].cost;
        spawnQueue.RemoveAt(_slot);
        SyncQueueRpc(spawnQueue.ToArray(), true);
    }
    [Rpc(SendTo.Everyone)]
    public void SyncQueueRpc(int[] _types, bool _resetTimer)
    {
        if (!IsServer)
        {
            if (_resetTimer)
                currSpawnTime = 0;
            spawnQueue = _types.ToList();
        }
    }
    public void Setup(int _team, int _id, ulong _playerID, bool _instaBuild)
    {
        team.Value = _team;
        id.Value = _id;
        playerID.Value = _playerID;
        if (_instaBuild)
            currentBuildPoints.Value = unitSettings.buildPointsNeeded;
        else currentBuildPoints.Value = 0;
    }
    void OnBuildChanged(float _prev, float _current)
    {
        if (unitSettings.unitType != UnitSettings.UnitType.building)
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
        if (owned)
        {
            GameEvents.i.UpdateIsBuildUI();
            if (IsBuild)
                unitUI.SetBuildUI(100);
            else unitUI.SetBuildUI(_current / unitSettings.buildPointsNeeded * 100);
        }
        if (IsServer && IsBuild)
            foreach (int _i in unitSettings.spawnOnBuild)
                StartCoroutine(GameManager.i.SpawnUnit(playerID.Value, team.Value, id.Value, newUnitPosition.position, newUnitPosition.rotation, _i, false));
    }
    void OnHealthChanged(float _prev, float _current)
    {
        float _ratio = _current / unitSettings.maxHealth;
        unitUI.SetHealthUI(_ratio);
    }
    void OnInsideUnitIDChange(int _prev, int _current)
    {
        visualObject.SetActive(_current < 0 || !insideUnit.unitCarrier.HideInside);
        unitUI.gameObject.SetActive(_current < 0 || !insideUnit.unitCarrier.HideInside);
        unitTargetClass = null;
        nearTarget = false;
        if (agent)
            agent.enabled = (_current < 0);
        if (!owned)
            return;
        foreach (int _i in GameManager.i.GetPlayer(playerID.Value).selectedUnitList)
            if (_i == _current || _i == _prev)
                GameEvents.i.UnitSelection(GameManager.i.GetUnit(_i).unitSettings.type);
    }
    [Rpc(SendTo.Everyone)]
    public void SetTargetRpc(int _unitTarget, bool _attackTarget)
    {
        nearTarget = false;
        unitTargetID = _unitTarget;
        attackTarget = _attackTarget;
        if (_unitTarget < 0)
        {
            unitTargetClass = null;
            return;
        }
        unitTargetClass = GameManager.i.GetUnit(unitTargetID);
        nearbyUTS = null;
    }
    [Rpc(SendTo.Everyone)]
    public void SetTargetPosRpc(Vector3 _targetPos)
    {
        for (int i = 0; i < unitWeapons.Length; i++)
        {
            UnitAim _uA = unitWeapons[i].unitAim;
            if (_uA && !unitWeapons[i].unitAimParent)
                _uA.SetTarget(new Vector3(-99999, -99999, -99999));
        }
        nearTarget = false;
        targetPos = _targetPos;
        if (agent && agent.enabled && gameObject.activeSelf)
            agent.destination = _targetPos;
        else if (unitAir && unitSettings.unitType != UnitSettings.UnitType.building) 
            unitAir.flyDestination = _targetPos;
        unitTargetID = -1;
        unitTargetClass = null;
        attackTarget = false;
    }
    public void UIActive(bool _true)
    {
        if (unitUI)
            unitUI.SActive(_true, -1);
    }
    public void SetUI(UnitUI _unitUI)
    { 
        unitUI = _unitUI; 
    }
    void UpdAction()
    {
        Unit _u = unitTargetClass;
        if (!_u)
            _u = nearbyUTS;
        Vector3 _targetClosestPoint = _u.col.ClosestPoint(transform.position);
        if (agent && agent.enabled && unitTargetID != -1 && !nearbyUTS)
        {
            if (_u.unitAir)
                _targetClosestPoint.y = t.position.y;
            agent.destination = _targetClosestPoint;
            if (unitResources && unitResources.currResDelay > 0)
            {
                agent.destination = t.position;
                return;
            }
        }
        else if (unitAir && unitSettings.unitType != UnitSettings.UnitType.building && _u)
            unitAir.flyDestination = _u.transform.position;
        if (unitResources && unitResources.returnToStockpile && nearTarget)
            unitResources.GetToStockpile();
        float _distance = Vector3.Distance(_targetClosestPoint, t.position);
        if (unitTargetClass && unitSettings.unitType == UnitSettings.UnitType.builder && unitTargetClass.currentBuildPoints.Value < unitTargetClass.unitSettings.buildPointsNeeded && nearTarget)
        {
            unitTargetClass.currentBuildPoints.Value = Mathf.Clamp(unitTargetClass.currentBuildPoints.Value + Time.deltaTime, 0, unitTargetClass.unitSettings.buildPointsNeeded);
            if (unitTargetClass.currentBuildPoints.Value == unitTargetClass.unitSettings.buildPointsNeeded)
                SetTargetPosRpc(t.position);
        }
        else if (!attackTarget && unitTargetClass && unitSettings.occupyPSlots > 0 && insideUnitID.Value == -1 && unitTargetClass.unitCarrier)
        {
            if (unitTargetClass.unitCarrier.FreeUnitSlots() == 0)
            {
                SetTargetPosRpc(t.position);
                return;
            }
            if (unitTargetClass.unitAir)
            {
                Vector3 _uPos = _targetClosestPoint;
                _uPos.y = transform.position.y;
                if (Vector3.Distance(_uPos, t.position) < 7)
                    unitTargetClass.SetTargetRpc(id.Value, false);
            }
            else if (nearTarget)
                unitTargetClass.unitCarrier.GetInsideUnit(this);
        }
        if (unitWeapons.Length > 0 && _distance < maxRange && (attackTarget || nearbyUTS) && !_u.IsInvulnerable && (!insideUnit || insideUnit.unitCarrier.ShootFromInside))
        {
            if (agent && agent.enabled && unitTargetClass)
                agent.stoppingDistance = unitWeapons[0].maxDistance;
            for (int i = 0; i < unitWeapons.Length; i++)
            {
                if (unitWeapons.Length > 0 && unitWeapons[i].cFirerateReload <= 0 && (!unitWeapons[i].unitAim || unitWeapons[i].unitAim.onTheTarget) &&
                    _distance < unitWeapons[i].maxDistance && _distance > unitWeapons[i].minDistance && (unitWeapons[i].ammoSize == 0 || unitWeapons[i].currentAmmo > 0) &&
                    (!unitAir || (unitAir.aircraftState == UnitAir.AircraftState.flying && unitAir.altitude == 1)))
                    Attack(i);
            }
        }
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
            else if (unitAir && unitAir.aircraftState == UnitAir.AircraftState.inHangar)
                unitWeapons[i].currentAmmo = unitWeapons[i].ammoSize;
            if (unitWeapons[i].currentAmmo > 0)
                _weaponsWithAmmo++;
        }
        if (IsServer && unitAir && unitAir.airport)
        {
            if (unitAir.aircraftState == UnitAir.AircraftState.inHangar)
                health.Value += Time.deltaTime * 10;
            else if (_weaponsWithAmmo == 0)
                SetTargetRpc(unitAir.airport.unit.id.Value, false);
        }
        if (spawnQueue.Count > 0 && IsBuild)
            currSpawnTime += Time.deltaTime;
    }
    void Spawn()
    {
        currSpawnTime = 0;
        SpawnNewUnit(playerID.Value, team.Value, id.Value, newUnitPosition.position, newUnitPosition.rotation, spawnQueue[0]);
        spawnQueue.RemoveAt(0);
        SyncQueueRpc(spawnQueue.ToArray(), true);
    }
    public void UnitSetup(Unit _u)
    {
        if (unitAir && _u.GetComponent<UnitAir>())
            unitAir.SetupAirUnit(_u.GetComponent<UnitAir>());
        else
            _u.GetComponent<NavMeshAgent>().destination = targetPos;
    }
    void Attack(int _slot)
    {
        if (IsServer && (unitTargetClass || nearbyUTS))
            AttackRpc(_slot);
    }
    public void InsideUnitID(int _id)
    {
        if (_id == -1)
            insideUnit = null;
        else 
            insideUnit = GameManager.i.GetUnit(_id);
        insideUnitID.Value = _id;
    }
    void SpawnNewUnit(ulong _playerID, int _team, int _spawnerID, Vector3 _pos, Quaternion _rot, int _type)
    {
        StartCoroutine(GameManager.i.SpawnUnit(_playerID, _team, _spawnerID, _pos, _rot, _type, false));
    }
    public void GetDamage(float _d, bool _explosive)
    {
        if (IsServer && !destroyed && !IsInvulnerable && (!insideUnit || insideUnit.unitCarrier.DamageReduction < 100))
            GetDamageRpc(_d, _explosive);
    }
    [Rpc(SendTo.Everyone)]
    void GetDamageRpc(float _d, bool _explosive)
    {
        if (insideUnit)
            _d *= (100 - insideUnit.unitCarrier.DamageReduction);
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
        if (IsServer && unitCarrier)
            unitCarrier.OnDie();
        if (IsServer)
            GameManager.i.GetPlayer(playerID.Value).RemoveUnit(id.Value);
        destroyed = true;
        if (characterAnimator && unitSettings.animDeath)
        {
            characterAnimator.SetFloat("X", 0);
            characterAnimator.SetFloat("Z", 0);
            characterAnimator.SetTrigger("Die");
        }
        else if (unitSettings.unitType == UnitSettings.UnitType.building)
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
        if (GetComponent<NavMeshObstacle>())
            GetComponent<NavMeshObstacle>().enabled = false;
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
        if (team.Value != 0)
            player.RemoveUnitFromCategory(this);
    }

    [Rpc(SendTo.Everyone)]
    void AttackRpc(int _slot)
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
        Unit _u = unitTargetClass;
        if (!_u)
            _u = nearbyUTS;
        if (unitWeapons[_slot].shootParticles)
            unitWeapons[_slot].shootParticles.Play();
        if (characterAnimator)
            characterAnimator.SetTrigger("Shoot");
        if (unitWeapons[_slot].projectile)
        {
            int _pS = 0;
            if (unitWeapons[_slot].projectileSpawn.Length > 1)
                _pS = _slot;
            Rigidbody _rb = Instantiate(unitWeapons[_slot].projectile, unitWeapons[_slot].projectileSpawn[_pS].position, unitWeapons[_slot].projectileSpawn[_currentAmmo].rotation);
            if (!unitWeapons[_slot].projectileHoming)
                _rb.linearVelocity = unitWeapons[_slot].projectileSpeed * unitWeapons[_slot].projectileSpawn[_currentAmmo].forward;
            if (unitWeapons[_slot].projectileHoming)
                _rb.GetComponent<Projectile>().Setup(_u.transform, _u, unitWeapons[_slot].damage, unitWeapons[_slot].projectileSpeed);
            else _rb.GetComponent<Projectile>().Setup(null, null, unitWeapons[_slot].damage, unitWeapons[_slot].projectileSpeed);
        }
        else if (IsServer) _u.GetDamage(unitWeapons[_slot].damage, false);
    }
    private void OnTriggerEnter(Collider collision)
    {
        Unit _u = collision.gameObject.GetComponent<Unit>();
        if (!_u) 
            return;
        if (unitTargetClass && _u == unitTargetClass)
            nearTarget = true;
    }
}