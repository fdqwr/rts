using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;

[RequireComponent(typeof(NetworkObject))]
public class Player : NetworkBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] Texture2D selectTexture;
    [SerializeField] Material buildingMaterial;
    [SerializeField] float camSpeed = 10;
    [SerializeField] float zoomSpeed = 30;
    public bool bot { get; private set; } = false;
    public int[] selectedUnitList { get; private set; } = new int[0];
    public List<Unit> playerUnits { get; private set; } = new List<Unit>();
    public List<Unit> commandCenters { get; private set; } = new List<Unit>();
    public List<Unit> resourceCenters { get; private set; } = new List<Unit>();
    public List<Unit> airfields { get; private set; } = new List<Unit>();
    public List<Unit> barracks { get; private set; } = new List<Unit>();
    public List<Unit> factories { get; private set; } = new List<Unit>();
    public List<Unit> groundUnits { get; private set; } = new List<Unit>();
    public List<Unit> groundCarriers { get; private set; } = new List<Unit>();
    public List<Unit> airCarriers { get; private set; } = new List<Unit>();
    public List<Unit> infantry { get; private set; } = new List<Unit>();
    public List<Unit> vehicles { get; private set; } = new List<Unit>();
    public List<Unit> buildings { get; private set; } = new List<Unit>();
    public List<Unit> airUnits { get; private set; } = new List<Unit>();
    public List<Unit> builders { get; private set; } = new List<Unit>();
    public List<Unit> supplyTracks { get; private set; } = new List<Unit>();
    NetworkVariable<int> buildSelected = new NetworkVariable<int>(1);
    NetworkVariable<int> team = new NetworkVariable<int>(0);
    NetworkVariable<float> buildCost = new NetworkVariable<float>(0);
    public NetworkVariable<float> money { get; private set; } = new NetworkVariable<float>(10000);
    public NetworkVariable<ulong> playerID { get; private set; } = new NetworkVariable<ulong>(0);
    public Vector3 spawnPosition { get; private set; }
    Vector2 selectionStart;
    Vector2 selectionEnd;
    Transform p1;
    Transform p2;
    int obstructionsInBuilding = 0;
    List<int[]> hotKey = new List<int[]>();
    GameObject buildingSelected;
    public int GetBuildID => buildSelected.Value;
    public int GetTeam => team.Value;
    public Camera PlayerCamera => cam;
    private void Awake()
    {
        buildSelected.OnValueChanged += OnBuildIDChange;
        for (int i = 0; i < 9; i++)
            hotKey.Add(new int[0]);
        p1 = cam.transform.parent;
        p2 = p1.parent;
    }
    void Start()
    {
        if (!IsOwner || bot) 
            return;
        cam.gameObject.SetActive(true);
        Application.targetFrameRate = 600;
        money.OnValueChanged += MenuUI.i.OnMoneyChange;
    }
    void Update()
    {
        if (!IsOwner || bot) 
            return;
        Hotkeys();
        CameraMove();
        UnitSelection();
        if (buildingSelected && !Input.GetMouseButton(2))
        {
            Vector3 _sPos = new Vector3(0, -10000, 0);
            int _layerMask = 1;
            if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit _hit, 1000, _layerMask))
                _sPos = _hit.point;
            buildingSelected.transform.position = _sPos;
        }
    }
    public void OnSpawn(int _team, ulong _playerID)
    {
        team.Value = _team;
        playerID.Value = _playerID;
    }
    public override void OnNetworkSpawn()
    {
        if (GameManager.i.players.ContainsKey(playerID.Value))
            GameManager.i.players[playerID.Value] = this;
        else GameManager.i.players.Add(playerID.Value, this);
        spawnPosition = MapSettings.i.playerSpawn[team.Value - 1].position;
        if (IsOwner && !bot)
        {
            OnMoneyChanged(0, money.Value);
            GameEvents.i.JoinGame();
            MenuUI.i.SetPlayer(this);
        }
        money.OnValueChanged += OnMoneyChanged;
    }
    void OnBuildIDChange(int _prev, int _curr)
    {
        if (IsOwner)
        {
            if (buildingSelected)
            {
                obstructionsInBuilding = 0;
                Destroy(buildingSelected);
            }
            if (_curr != -1)
            {
                StartCoroutine(BuildPreview(_curr));
            }
            else
                obstructionsInBuilding = 0;
        }
    }
    IEnumerator BuildPreview(int _id)
    {
        Vector3 _previewPosition = new Vector3(0, -10000, 0);
        int _layerMask = 1;
        if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit _hit, 1000, _layerMask))
            _previewPosition = _hit.point;
        AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(GameManager.i.unitSettings[_id].asset, _previewPosition, cam.transform.parent.parent.rotation,transform);
        yield return _handle;
        if (_handle.Result != null)
            buildingSelected = _handle.Result;
        Destroy(buildingSelected.GetComponent<UnitAim>());
        Destroy(buildingSelected.GetComponent<UnitResources>());
        Destroy(buildingSelected.GetComponent<UnitAir>());
        Destroy(buildingSelected.GetComponent<UnitBuilder>());
        Destroy(buildingSelected.GetComponent<UnitCarrier>());
        Destroy(buildingSelected.GetComponent<Unit>());
        Destroy(buildingSelected.GetComponent<NetworkObject>());
        Destroy(buildingSelected.GetComponent<NetworkTransform>());
        Destroy(buildingSelected.GetComponent<NavMeshObstacle>());
        buildingSelected.transform.Rotate(0, 180, 0);
        foreach (MeshRenderer _mR in buildingSelected.GetComponentsInChildren<MeshRenderer>())
        {
            _mR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mR.material = buildingMaterial;
            foreach (Material _mat in _mR.materials)
                _mat.color = new Color(1, 1, 1, 0.5f);
        }
    }
    void Hotkeys()
    {
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKeyDown((i + 1).ToString()))
                {
                    foreach (int _i in hotKey[i])
                        GameManager.i.GetUnit(_i).unitUI.SetHotkeyUI(0);
                    hotKey[i] = selectedUnitList;
                    foreach (int _i in hotKey[i])
                        GameManager.i.GetUnit(_i).unitUI.SetHotkeyUI(i + 1);
                }
            }
            else if (Input.GetKeyDown((i + 1).ToString()) && hotKey[i].Length > 0)
            {
                foreach (int _i in selectedUnitList)
                    GameManager.i.GetUnit(_i).UIActive(false);
                SetSelectedUnitsRpc(hotKey[i], false);
            }
        }
    }
    void CameraMove()
    {
        Vector3 _move = new Vector3(Input.GetAxis("Horizontal") * camSpeed, 0, Input.GetAxis("Vertical") * camSpeed);
        Vector3 _zoom = new Vector3(0, 0, Input.mouseScrollDelta.y * zoomSpeed);
        if (Input.mouseScrollDelta.y > 0)
        {
            int _layerMask = 1 << 7;
            _layerMask = ~_layerMask;
            if (Physics.Raycast(cam.ScreenPointToRay(cam.transform.forward), 20, _layerMask))
                _zoom = new Vector3(0, 0, 0);
        }
        p1.localPosition += (_move * Time.deltaTime * 10);
        cam.transform.localPosition += (_zoom * Time.deltaTime * 10);
        if (Input.GetMouseButton(2))
        {
            if (buildingSelected)
                buildingSelected.transform.RotateAround(buildingSelected.transform.position, Vector3.up, Input.GetAxis("Mouse X") * -500 * Time.deltaTime);
            else p2.RotateAround(p1.position, Vector3.up, Input.GetAxis("Mouse X") * 200 * Time.deltaTime);
        }
    }
    void UnitSelection()
    {
        if (Input.GetMouseButtonDown(0) && EventSystem.current.currentSelectedGameObject == null)
            selectionStart = Input.mousePosition;
        if (Input.GetMouseButton(0))
            selectionEnd = Input.mousePosition;
        if (Input.GetMouseButtonUp(0))
        {
            if (Mathf.Max(selectionStart.x, selectionEnd.x) - Mathf.Min(selectionStart.x, selectionEnd.x) < 40 && Mathf.Max(selectionStart.y, selectionEnd.y) - Mathf.Min(selectionStart.y, selectionEnd.y) < 40)
                Select();
            else if (EventSystem.current.currentSelectedGameObject == null)
                ReleaseSelectionBox();
            selectionEnd = selectionStart = Vector2.zero;
        }
        if (Input.GetMouseButtonDown(1))
        {
            foreach (int _unit in selectedUnitList)
                GameManager.i.GetUnit(_unit).UIActive(false);
            SetSelectedUnitsRpc(new int[0], false);
        }
        if (buildingSelected && Input.GetMouseButtonUp(2))
            Select();
    }
    void Select()
    {
        if (obstructionsInBuilding > 0)
            return;
        RaycastHit _hit;
        int _layerMask = 1 << 7;
        _layerMask = ~_layerMask;
        if (!Physics.Raycast(cam.ScreenPointToRay(selectionStart), out _hit, 1000, _layerMask))
            return;
        Unit sUnit = _hit.collider.GetComponent<Unit>();
        if (!sUnit)
        {
            foreach (int _unit in selectedUnitList)
                if (buildingSelected)
                    SetTargetPositionRpc(buildingSelected.transform.position, buildingSelected.transform.rotation, Input.GetKey(KeyCode.LeftShift));
                else SetTargetPositionRpc(_hit.point, cam.transform.parent.parent.rotation, false);
            return;
        }
        if (selectedUnitList.ToList().Count > 0 && !Input.GetKey(KeyCode.LeftShift))
        {
            if (sUnit.unitResources && sUnit.unitResources.currentResource.Value > 0 && sUnit.team.Value == 0)
                SetTargetUnitRpc(sUnit.id.Value, false);
            else if (!sUnit.IsInvulnerable)
                SetTargetUnitRpc(sUnit.id.Value, (team.Value != sUnit.team.Value || Input.GetKey(KeyCode.LeftControl)));
        }
        else
        {
            int[] _unSel = new int[1] { sUnit.id.Value };
            SetSelectedUnitsRpc(_unSel, Input.GetKey(KeyCode.LeftShift));
        }
    }
    void ReleaseSelectionBox()
    {
        Vector2 _min = selectionStart;
        Vector2 _max = Input.mousePosition;
        List<int> _selectedUnitList = new List<int>();
        foreach (Unit _unit in playerUnits)
        {
            Vector3 _screenPos = cam.WorldToScreenPoint(_unit.transform.position);
            if (_screenPos.x > Mathf.Min(_min.x, _max.x) && _screenPos.x < Mathf.Max(_min.x, _max.x) &&
                _screenPos.y > Mathf.Min(_min.y, _max.y) && _screenPos.y < Mathf.Max(_min.y, _max.y))
                _selectedUnitList.Add(_unit.id.Value);
        }
        if (_selectedUnitList.Count > 0)
        {
            if (!Input.GetKey(KeyCode.LeftShift))
                foreach (int _i in selectedUnitList)
                    GameManager.i.GetUnit(_i).UIActive(false);
            SetSelectedUnitsRpc(_selectedUnitList.ToArray(), Input.GetKey(KeyCode.LeftShift));
        }
    }
    public void RemoveUnit(int _id)
    {
        List<int> _uL = selectedUnitList.ToList();
        if (!_uL.Contains(_id)) 
            return;
        _uL.Remove(_id);
        selectedUnitList = _uL.ToArray();
        SetSelectedUnitsClientRRpc(_uL.ToArray(), false);
    }
    [Rpc(SendTo.Server)]
    public void SetSelectedUnitsRpc(int[] _units, bool _combine)
    {
        List<int> _uL = new List<int>();
        if (_combine)
            _uL = selectedUnitList.ToList();
        foreach (int _u in _units)
            if (team.Value == GameManager.i.GetUnit(_u).team.Value && !_uL.Contains(_u))
                _uL.Add(_u);
        selectedUnitList = _uL.ToArray();
        buildSelected.Value = -1;
        buildCost.Value = -1;
        if (IsOwner && !bot)
            foreach (int _u in _units)
                GameManager.i.GetUnit(_u).UIActive(true);
        SetSelectedUnitsClientRRpc(selectedUnitList, _combine);
    }

    [Rpc(SendTo.Server)]
    public void SetTargetPositionRpc(Vector3 _pos, Quaternion _rot, bool _keep)
    {
        if (buildSelected.Value > -1)
            foreach (int _unit in selectedUnitList)
            {
                Unit _u = GameManager.i.GetUnit(_unit);
                _u.unitBuilder.BuildFoundation(_pos, _rot, buildSelected.Value, buildCost.Value);
                if (!_keep)
                    buildSelected.Value = -1;
            }
        else
            foreach (int _unit in selectedUnitList)
                GameManager.i.GetUnit(_unit).SetTargetPosRpc(_pos);
    }
    [Rpc(SendTo.Server)]
    void SetTargetUnitRpc(int _id, bool _attackTarget)
    {
        foreach (int _unit in selectedUnitList)
            GameManager.i.GetUnit(_unit).SetTargetRpc(_id, _attackTarget);
    }
    public void SetBuildIDRpc(int _id, float _cost)
    {
        buildSelected.Value = _id;
        buildCost.Value = _cost;
    }
    [Rpc(SendTo.Owner)]
    void SetSelectedUnitsClientRRpc(int[] _units, bool _combine)
    {
        if (!IsServer)
        {
            if (buildingSelected)
                Destroy(buildingSelected);
            if (!_combine)
                selectedUnitList = _units;
            else selectedUnitList.Concat(_units);
            if (IsOwner && !bot)
                foreach (int _u in selectedUnitList)
                    GameManager.i.GetUnit(_u).UIActive(true);
        }
        UpdateUI();
    }
    public void UpdateUI()
    {
        if (bot)
            return;
        if (IsOwner)
            foreach (int _u in selectedUnitList)
                GameManager.i.GetUnit(_u).UIActive(true);
        if (selectedUnitList.Length > 0)
            GameEvents.i.UnitSelection(GameManager.i.GetUnit(selectedUnitList[0]).Type);
        else GameEvents.i.UnitSelection(-1);
    }
    public void AddMoney(float _money)
    {
        money.Value += _money;
    }
    void OnMoneyChanged(float _prev, float _current)
    {
        if (IsOwner && !bot)
            GameEvents.i.ChangeMoney(_current);
    }
    public void AddUnit(Unit _unit)
    {
        playerUnits.Add(_unit);
    }
    public void RemoveUnit(Unit _unit)
    {
        playerUnits.Remove(_unit);
    }
    void OnObstructionChange(int _value)
    {
        if (_value == -1)
            obstructionsInBuilding--;
        else if (_value == 1)
            obstructionsInBuilding++;
        else obstructionsInBuilding = 0;
        Color _c = new Color(1, 1, 1, 0.5f);
        if (obstructionsInBuilding > 0)
            _c = new Color(1, 0, 0, 0.5f);
        if (buildingSelected)
            foreach (MeshRenderer _uR in buildingSelected.GetComponentsInChildren<MeshRenderer>())
            {
                _uR.material = buildingMaterial;
                foreach (Material _mat in _uR.materials)
                    _mat.color = _c;
            }
    }
    public void AddUnitToCategory(Unit _u)
    {
        if (_u.UnitType == UnitSettings.UnitType.building)
            buildings.Add(_u);
        else
        {
            if (_u.UnitType == UnitSettings.UnitType.vehicle)
                vehicles.Add(_u);
            else
                infantry.Add(_u);
        }
        if (_u.unitAir)
        {
            airUnits.Add(_u);
            if (_u.unitCarrier)
                airCarriers.Add(_u);
        }
        else
        {
            groundUnits.Add(_u);
            airUnits.Add(_u);
            if (_u.unitCarrier)
                groundCarriers.Add(_u);
        }
        if (_u.UnitType == UnitSettings.UnitType.builder)
            builders.Add(_u);
        if (_u.unitResources)
        {
            if (_u.unitResources.ResourceCarryCapacity > 0)
                supplyTracks.Add(_u);
            else
                resourceCenters.Add(_u);
        }
        if (_u.Type == 0)
            commandCenters.Add(_u);
        if (_u.Type == 1)
            barracks.Add(_u);
        if (_u.Type == 2)
            factories.Add(_u);
        if (_u.Type == 3)
            airfields.Add(_u);
        if (_u.Type == 4)
            resourceCenters.Add(_u);
    }
    public void RemoveUnitFromCategory(Unit _u)
    {
        if (_u.UnitType == UnitSettings.UnitType.building)
            buildings.Remove(_u);
        else
        {
            if (_u.UnitType == UnitSettings.UnitType.vehicle)
                vehicles.Remove(_u);
            else
                infantry.Remove(_u);
        }
        if (_u.unitAir)
        {
            airUnits.Remove(_u);
            if (_u.unitCarrier)
                airCarriers.Remove(_u);
        }
        else
        {
            groundUnits.Remove(_u);
            airUnits.Remove(_u);
            if (_u.unitCarrier)
                groundCarriers.Remove(_u);
        }
        if (_u.UnitType == UnitSettings.UnitType.builder)
            builders.Remove(_u);
        if (_u.unitResources)
        {
            if (_u.unitResources.ResourceCarryCapacity > 0)
                supplyTracks.Remove(_u);
            else
                resourceCenters.Remove(_u);
        }
        if (_u.Type == 0)
            commandCenters.Remove(_u);
        if (_u.Type == 1)
            barracks.Remove(_u);
        if (_u.Type == 2)
            factories.Remove(_u);
        if (_u.Type == 3)
            airfields.Remove(_u);
        if (_u.Type == 4)
            resourceCenters.Remove(_u);
    }
    public void SetupPlayer(ulong _playerID, Vector3 _spawnPosition, bool _bot)
    {
        playerID.Value = _playerID;
        spawnPosition = _spawnPosition;
        bot = _bot;
    }
    private void OnTriggerEnter(Collider other)
    {
        Unit _u = other.GetComponent<Unit>();
        if (_u && _u.UnitType == UnitSettings.UnitType.building)
            OnObstructionChange(1);
    }
    private void OnTriggerExit(Collider other)
    {
        Unit _u = other.GetComponent<Unit>();
        if (_u && _u.UnitType == UnitSettings.UnitType.building)
            OnObstructionChange(-1);
    }
    void OnGUI()
    {
        if (selectionStart == Vector2.zero || selectionEnd == Vector2.zero) 
            return;
        var _rect = new Rect(selectionStart.x, Screen.height - selectionStart.y, selectionEnd.x - selectionStart.x, -1 * (selectionEnd.y - selectionStart.y));
        GUI.DrawTexture(_rect, selectTexture);
    }
}

