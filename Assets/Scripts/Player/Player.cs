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
namespace rts.Player
{
    using rts.Unit;
    using rts.UI;
    using rts.GameLogic;
    using Zenject;

    public class Player : NetworkBehaviour
    {
        [field: SerializeField] public Camera cam { get; private set; }
        [SerializeField] Texture2D selectTexture;
        [SerializeField] Material buildingMaterial;
        [SerializeField] float camSpeed = 10;
        [SerializeField] float zoomSpeed = 30;
        public bool isBot { get; private set; } = false;
        NetworkVariable<int> buildSelected = new NetworkVariable<int>(1);
        public NetworkVariable<int> team { get; private set; } = new NetworkVariable<int>(0);
        NetworkVariable<float> buildCost = new NetworkVariable<float>(0);
        public NetworkVariable<float> money { get; private set; } = new NetworkVariable<float>(0);
        public NetworkVariable<int> playerID { get; private set; } = new NetworkVariable<int>(0);
        public Vector3 spawnPosition { get; private set; }
        Vector2 selectionStart;
        Vector2 selectionEnd;
        Transform p1;
        Transform p2;
        int obstructionsInBuilding = 0;
        SetupDataStruct setupData;
        List<int[]> hotKey = new List<int[]>();
        Transform buildingSelected;
        protected MenuUI menuUI { get; private set; }
        protected GameManager gameManager { get; private set; }
        protected GameData gameData { get; private set; }
        string[] hotkeyButtonNames = new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        public static int localPlayerID { get; private set; }
        public static Player localPlayerClass { get; private set; }
        public List<int> selectedUnitList { get; private set; } = new List<int>();
        public List<Unit> playerUnits { get; private set; } = new List<Unit>();
        public List<Unit> commandCenters { get; private set; } = new List<Unit>();
        public List<Unit> supplyCenters { get; private set; } = new List<Unit>();
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
        struct SetupDataStruct
        {
            public int team;
            public int playerID;
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
            buildSelected.OnValueChanged += OnBuildIDChange;
            for (int i = 0; i < 9; i++)
                hotKey.Add(new int[0]);
            p1 = cam.transform.parent;
            p2 = p1.parent;
            isBot = GetComponent<Bot>() != null;
        }

        void Update()
        {
            if (!IsOwner || isBot)
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
                buildingSelected.position = _sPos;
            }
        }

        public void SetData(int _team, int _playerID)
        {
            setupData = new SetupDataStruct { team = _team, playerID = _playerID };
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner && !isBot)
            {
                money.OnValueChanged += menuUI.OnMoneyChange;
                cam.gameObject.SetActive(true);
                Application.targetFrameRate = 600;
                localPlayerID = playerID.Value;
                localPlayerClass = this;
                menuUI.SetPlayer(this);
            }
            if (IsServer)
            {
                playerID.Value = setupData.playerID;
                team.Value = setupData.team;
                money.Value = 10000;
            }
            gameData.AddPlayer(playerID.Value, this);
            spawnPosition = gameManager.mapSettings.maps[menuUI.map.Value].playerSpawn[playerID.Value].position;
        }

        void OnBuildIDChange(int _prev, int _curr)
        {
            if (!IsOwner || isBot)
                return;
            if (buildingSelected)
            {
                obstructionsInBuilding = 0;
                Destroy(buildingSelected.gameObject);
            }
            if (_curr != -1)
                StartCoroutine(BuildPreview(_curr));
            else
                obstructionsInBuilding = 0;
        }

        IEnumerator BuildPreview(int _id)
        {
            Vector3 _previewPosition = new Vector3(0, -10000, 0);
            int _layerMask = 1;
            if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit _hit, 1000, _layerMask))
                _previewPosition = _hit.point;
            AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(gameData.unitSettings[_id].asset, _previewPosition, p2.rotation, transform);
            yield return _handle;
            buildingSelected = _handle.Result.transform;
            Destroy(buildingSelected.GetComponent<Unit>());
            Destroy(buildingSelected.GetComponent<Turret>());
            Destroy(buildingSelected.GetComponent<Supply>());
            Destroy(buildingSelected.GetComponent<Aircraft>());
            Destroy(buildingSelected.GetComponent<Builder>());
            Destroy(buildingSelected.GetComponent<Carrier>());
            Destroy(buildingSelected.GetComponent<NetworkObject>());
            Destroy(buildingSelected.GetComponent<NetworkTransform>());
            Destroy(buildingSelected.GetComponent<NavMeshObstacle>());
            buildingSelected.Rotate(0, 180, 0);
            foreach (MeshRenderer _meshRenderer in buildingSelected.GetComponentsInChildren<MeshRenderer>())
            {
                _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _meshRenderer.material = buildingMaterial;
                foreach (Material _mat in _meshRenderer.materials)
                    _mat.color = new Color(1, 1, 1, 0.5f);
            }
        }

        void Hotkeys()
        {
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    if (!Input.GetKeyDown(hotkeyButtonNames[i]))
                        continue;
                    foreach (int _i in hotKey[i])
                        gameData.GetUnit(_i).unitUI.SetHotkeyUI(0);
                    hotKey[i] = selectedUnitList.ToArray();
                    foreach (int _i in hotKey[i])
                        gameData.GetUnit(_i).unitUI.SetHotkeyUI(_i + 1);
                }
                else if (Input.GetKeyDown(hotkeyButtonNames[i]) && hotKey[i].Length > 0)
                {
                    foreach (int _i in selectedUnitList)
                        gameData.GetUnit(_i).UIActive(false);
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
                    buildingSelected.RotateAround(buildingSelected.position, Vector3.up, Input.GetAxis("Mouse X") * -500 * Time.deltaTime);
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
                if (Mathf.Max(selectionStart.x, selectionEnd.x) - Mathf.Min(selectionStart.x, selectionEnd.x) < 40 
                    && Mathf.Max(selectionStart.y, selectionEnd.y) - Mathf.Min(selectionStart.y, selectionEnd.y) < 40)
                    Select();
                else if (EventSystem.current.currentSelectedGameObject == null)
                    ReleaseSelectionBox();
                selectionEnd = selectionStart = Vector2.zero;
            }
            if (Input.GetMouseButtonDown(1))
            {
                foreach (int _unit in selectedUnitList)
                    gameData.GetUnit(_unit).UIActive(false);
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
                        SetTargetPositionServerRpc(buildingSelected.position, buildingSelected.rotation, Input.GetKey(KeyCode.LeftControl), Input.GetKey(KeyCode.LeftShift));
                    else SetTargetPositionServerRpc(_hit.point, p2.rotation, Input.GetKey(KeyCode.LeftControl), Input.GetKey(KeyCode.LeftShift));
                return;
            }
            if (selectedUnitList.ToList().Count > 0 && !Input.GetKey(KeyCode.LeftShift))
            {
                if (sUnit.supply && sUnit.supply.supplyResource.Value > 0 && sUnit.team.Value == 0)
                    SetTargetUnitServerRpc(sUnit.id.Value, Input.GetKey(KeyCode.LeftControl), Input.GetKey(KeyCode.LeftShift));
                else if (!sUnit.IsInvulnerable)
                    SetTargetUnitServerRpc(sUnit.id.Value, (team.Value != sUnit.team.Value || Input.GetKey(KeyCode.LeftControl)), Input.GetKey(KeyCode.LeftShift));
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
                        gameData.GetUnit(_i).UIActive(false);
                SetSelectedUnitsRpc(_selectedUnitList.ToArray(), Input.GetKey(KeyCode.LeftShift));
            }
        }

        public void RemoveUnit(int _id)
        {
            if (!selectedUnitList.Contains(_id))
                return;
            selectedUnitList.Remove(_id);
            SetSelectedUnitsClientRRpc(selectedUnitList.ToArray(), false);
        }

        [Rpc(SendTo.Server)]
        public void SetSelectedUnitsRpc(int[] _units, bool _combine)
        {
            if (!_combine)
                selectedUnitList = new List<int>();
            foreach (int _u in _units)
                if (team.Value == gameData.GetUnit(_u).team.Value && !selectedUnitList.Contains(_u))
                    selectedUnitList.Add(_u);
            buildSelected.Value = -1;
            buildCost.Value = -1;
            if (IsOwner && !isBot)
                foreach (int _u in _units)
                    gameData.GetUnit(_u).UIActive(true);
            SetSelectedUnitsClientRRpc(selectedUnitList.ToArray(), _combine);
        }

        [Rpc(SendTo.Server)]
        public void SetTargetPositionServerRpc(Vector3 _pos, Quaternion _rot, bool _attackTarget, bool _queue)
        {
            int _queueInt = -1;
            if (_queue)
                _queueInt = 1;
            if (buildSelected.Value > -1)
                foreach (int _unit in selectedUnitList)
                {
                    Unit _u = gameData.GetUnit(_unit);
                    _u.builder.BuildFoundation(_pos, _rot, buildSelected.Value, buildCost.Value);
                    if (!_queue)
                        buildSelected.Value = -1;
                }
            else
                SetTargetPositionClientRpc(selectedUnitList.ToArray(), _pos, _attackTarget, _queueInt);
        }

        [Rpc(SendTo.Everyone)]
        public void SetTargetPositionClientRpc(int[] _units, Vector3 _pos, bool _attackTarget, int _queueInt)
        {
            for (int _i = 0; _i < _units.Length; _i++)
                gameData.GetUnit(_units[_i]).orders.SetTargetPos(_pos, _attackTarget, _queueInt);
        }

        [Rpc(SendTo.Server)]
        void SetTargetUnitServerRpc(int _id, bool _attackTarget, bool _queue)
        {
            int _queueInt = -1;
            if (_queue)
                _queueInt = 1;
            SetTargetUnitClientRpc(selectedUnitList.ToArray(), _id, _attackTarget, _queueInt);
        }

        [Rpc(SendTo.Everyone)]
        void SetTargetUnitClientRpc(int[] _units, int _id, bool _attackTarget, int _queueInt)
        {
            for (int _i = 0; _i < _units.Length; _i++)
                gameData.GetUnit(_units[_i]).orders.SetTarget(_id, _attackTarget, _queueInt);
        }

        [Rpc(SendTo.Server)]
        public void PressUnitButtonRpc(int _button)
        {
            gameManager.PressUnitButton(_button, playerID.Value);
        }

        [Rpc(SendTo.Server)]
        public void PressUnitQueueButtonRpc(int _button)
        {
            gameManager.PressUnitQueueButton(_button, playerID.Value);
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
                    selectedUnitList = _units.ToList();
                else selectedUnitList.Concat(_units);
                if (IsOwner && !isBot)
                    foreach (int _u in selectedUnitList)
                        gameData.GetUnit(_u).UIActive(true);
            }
            UpdateUI();
        }

        public void UpdateUI()
        {
            if (isBot)
                return;
            if (IsOwner)
                foreach (int _u in selectedUnitList)
                    gameData.GetUnit(_u).UIActive(true);
            if (selectedUnitList.Count > 0)
                menuUI.UpdateUnitSelectionUI(gameData.GetUnit(selectedUnitList[0]).settings.id);
            else menuUI.UpdateUnitSelectionUI(-1);
        }

        public void AddMoney(float _money)
        {
            money.Value += _money;
        }

        public void AddUnit(Unit _unit)
        {
            playerUnits.Add(_unit);
        }

        public void RemoveUnit(Unit _unit)
        {
            if (selectedUnitList.Contains(_unit.id.Value))
                selectedUnitList.Remove(_unit.id.Value);
            for (int _i = 0; _i < hotKey.Count; _i++)
            {
                if (hotKey[_i].Contains(_unit.id.Value))
                {
                    List<int> _hotkey = hotKey[_i].ToList();
                    _hotkey.Remove(_unit.id.Value);
                    hotKey[_i] = _hotkey.ToArray();
                }
            }
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

        public void AddUnitToCategory(Unit _unit)
        {
            if (_unit.settings.IsBuilding)
                buildings.Add(_unit);
            if (_unit.settings.IsVehicle)
                vehicles.Add(_unit);
            if (_unit.settings.IsInfantry)
                infantry.Add(_unit);
            if (_unit.settings.IsAircraft)
            {
                airUnits.Add(_unit);
                if (_unit.carrier)
                    airCarriers.Add(_unit);
            }
            else
            {
                groundUnits.Add(_unit);
                if (_unit.carrier)
                    groundCarriers.Add(_unit);
            }
            if (_unit.settings.unitType == Settings.UnitType.builder)
                builders.Add(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.truck)
                    supplyTracks.Add(_unit);
            else if(_unit.settings.unitType == Settings.UnitType.supplyCenter)
                    supplyCenters.Add(_unit);
            else if(_unit.settings.unitType == Settings.UnitType.commandCenter)
                commandCenters.Add(_unit);
            else if(_unit.settings.unitType == Settings.UnitType.barracks)
                barracks.Add(_unit);
            else if(_unit.settings.unitType == Settings.UnitType.factory)
                factories.Add(_unit);
            else if(_unit.settings.unitType == Settings.UnitType.airfield)
                airfields.Add(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.supplyCenter)
                supplyCenters.Add(_unit);
        }
        public void RemoveUnitFromCategory(Unit _unit)
        {
            if (_unit.settings.IsBuilding)
                buildings.Remove(_unit);
            if (_unit.settings.IsVehicle)
                vehicles.Remove(_unit);
            if (_unit.settings.IsInfantry)
                infantry.Remove(_unit);
            if (_unit.settings.IsAircraft)
            {
                airUnits.Remove(_unit);
                if (_unit.carrier)
                    airCarriers.Remove(_unit);
            }
            else
            {
                groundUnits.Remove(_unit);
                if (_unit.carrier)
                    groundCarriers.Remove(_unit);
            }
            if (_unit.settings.unitType == Settings.UnitType.builder)
                builders.Remove(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.truck)
                supplyTracks.Remove(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.supplyCenter)
                supplyCenters.Remove(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.commandCenter)
                commandCenters.Remove(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.barracks)
                barracks.Remove(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.factory)
                factories.Remove(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.airfield)
                airfields.Remove(_unit);
            else if (_unit.settings.unitType == Settings.UnitType.supplyCenter)
                supplyCenters.Remove(_unit);
        }
        public void SetPlayerData(int _playerID, Vector3 _spawnPosition, bool _bot)
        {
            playerID.Value = _playerID;
            spawnPosition = _spawnPosition;
            isBot = _bot;
        }
        private void OnTriggerEnter(Collider other)
        {
            Unit _u = other.GetComponent<Unit>();
            if (_u && _u.settings.IsBuilding)
                OnObstructionChange(1);
        }
        private void OnTriggerExit(Collider other)
        {
            Unit _u = other.GetComponent<Unit>();
            if (_u && _u.settings.IsBuilding)
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
}