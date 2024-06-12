using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    public static GameManager i;
    public Dictionary<int, UnitSettings> unitSettings { get; private set; } = new Dictionary<int, UnitSettings>();
    public Dictionary<int, Unit> playerUnits { get; private set; } = new Dictionary<int, Unit>();
    public List<Unit> allUnits { get; private set; } = new List<Unit>();
    public List<UnitResources> resources { get; private set; } = new List<UnitResources>();
    public Dictionary<ulong, List<UnitResources>> resourceStockpile { get; private set; } = new Dictionary<ulong, List<UnitResources>>();
    public Dictionary<ulong, List<Unit>> buildingList { get; private set; } = new Dictionary<ulong, List<Unit>>();
    public Dictionary<ulong, Player> players { get; private set; } = new Dictionary<ulong, Player>();
    Color[] teamColor = new Color[6] { Color.white, Color.blue * 0.5f, Color.red * 0.5f, Color.green * 0.5f, Color.cyan * 0.5f, Color.magenta * 0.5f };
    public GameObject playerPrefab { get; private set; }
    public GameObject botPrefab { get; private set; }
    public GameObject unitUIPrefab { get; private set; }
    int team = 0;
    private void Awake()
    {
        i = this;
        foreach (UnitSettings _uS in Resources.LoadAll("UnitSettings", typeof(UnitSettings)))
            unitSettings.Add(_uS.type, _uS);
        playerPrefab = (GameObject)Resources.Load("Prefabs/Player");
        botPrefab = (GameObject)Resources.Load("Prefabs/Bot");
        unitUIPrefab = (GameObject)Resources.Load("Prefabs/UnitUI");
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            SpawnPlayerServerRpc((ulong)players.Count+1,true);
        }
    }
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;
        NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
        foreach (ulong _id in NetworkManager.ConnectedClientsIds)
            SpawnPlayerServerRpc(_id, false);
        foreach (UnitSpawner _u in MapSettings.i.unitSpawner)
            _u.OnSpawn();
    }
    void OnClientConnectedCallback(ulong _playerID)
    {
        SpawnPlayerServerRpc(_playerID, false);
    }
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SpawnPlayerServerRpc(ulong _playerID,bool _isBot)
    {
        if (players.ContainsKey(_playerID))
            return;
        team++;
        Bot _bot;
        Player _player;
        if (_isBot)
        {
            _bot = Instantiate(botPrefab, MapSettings.i.playerSpawn[team - 1].position, MapSettings.i.playerSpawn[team - 1].rotation).GetComponent<Bot>();
            _player = _bot.GetComponent<Player>();
        }
        else 
            _player = Instantiate(playerPrefab, MapSettings.i.playerSpawn[team - 1].position, MapSettings.i.playerSpawn[team - 1].rotation).GetComponent<Player>();
        _player.OnSpawn(team, _playerID);
        if (!_isBot)
            _player.GetComponent<NetworkObject>().SpawnAsPlayerObject(_playerID);
        else
            _player.GetComponent<NetworkObject>().Spawn();
        StartCoroutine(SpawnUnit(_playerID, team, -1, MapSettings.i.playerSpawn[team - 1].position, MapSettings.i.playerSpawn[team - 1].rotation, 0, true));
    }
    [Rpc(SendTo.Server)]
    public void PressUnitButtonRpc(int _button, RpcParams _serverRpcParams = default)
    {
        ulong _playerID = _serverRpcParams.Receive.SenderClientId;
        PressUnitButton(_button,_playerID);
    }
    public void PressUnitButton(int _button, ulong _playerID)
    {
        Player _player = GetPlayer(_playerID);
        int[] _selUn = _player.selectedUnitList;
        if (_selUn.Length == 0)
            return;
        UnitButton _ub = unitSettings[GetUnit(_selUn[0]).Type].uB;
        if (!_ub || !(_ub.unitButtons.Length > _button))
            return;
        UnitButton.UnitButtonSettings _s = _ub.unitButtons[_button];
        if (_s.buttonType == UnitButton.UnitButtonSettings.btype.Build)
            _player.SetBuildIDRpc(_s.spawnID, unitSettings[_s.spawnID].cost);
        else if (_s.buttonType == UnitButton.UnitButtonSettings.btype.Spawn)
        {
            bool _sameUnit = true;
            foreach (int _i in _selUn)
                if (_i != _selUn[0])
                    _sameUnit = false;
            if (!_sameUnit) return;
            foreach (int _i in _selUn)
                GetUnit(_i).AddToQueue(_s.spawnID, unitSettings[_s.spawnID].cost);
        }
        else if (_s.buttonType == UnitButton.UnitButtonSettings.btype.InsideUnit)
        {
            foreach (int _i in _selUn)
            {
                Unit _u = GetUnit(_i);
                if (_u.unitCarrier) _u.unitCarrier.DropUnit(_button);
            }
        }
    }
    [Rpc(SendTo.Server)]
    public void PressUnitQueueButtonRpc(int _button, RpcParams serverRpcParams = default)
    {
        ulong _playerID = serverRpcParams.Receive.SenderClientId;
        Player _player = GetPlayer(_playerID);
        int[] _selUn = _player.selectedUnitList;
        if (_selUn.Length == 0) return;
        foreach (int _i in _selUn)
        {
            GetUnit(_i).RemoveFromQueue(_button);
        }
    }
    public IEnumerator SpawnUnit(ulong _playerID, int _team, int _spawnerID, Vector3 _pos, Quaternion _rot, int _type, bool _instaBuild)
    {
        AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(unitSettings[_type].asset, _pos, _rot, transform);
        yield return _handle;
        Unit _unit = null;
        if (_handle.Result != null)
            _unit = _handle.Result.GetComponent<Unit>();
        int _c = playerUnits.Count;
        _unit.Setup(_team, _c, _playerID, _instaBuild);
        _unit.GetComponent<NetworkObject>().Spawn();
        if (_spawnerID > -1)
        {
            Unit unitSpawner = GetUnit(_spawnerID);
            if (unitSpawner.UnitType == UnitSettings.UnitType.builder)
                GetUnit(_spawnerID).SetTargetRpc(_unit.id.Value, false);
            else
                GetUnit(_spawnerID).UnitSetup(_unit);
        }
    }
    public void AddUnit(int _id, Unit _unit)
    {
        playerUnits.Add(_id, _unit);
        allUnits.Add(_unit);
        UnitUI _uUI = Instantiate(unitUIPrefab, MenuUI.i.UnitUIParent()).GetComponent<UnitUI>();
        _uUI.SetUnit(_unit);
        _unit.SetUI(_uUI);
        if (_unit.IsInvulnerable)
            _uUI.gameObject.SetActive(false);
    }
    public void AddStockpile(ulong _id, UnitResources _unitResources)
    {
        if (!resourceStockpile.ContainsKey(_id))
            resourceStockpile.Add(_id, new List<UnitResources>());
        resourceStockpile[_id].Add(_unitResources);
    }
    public void AddToResources(UnitResources _unitResources)
    {
        resources.Add(_unitResources);
    }
    public void AddBuilding(ulong _id, Unit _unit)
    {
        if (!buildingList.ContainsKey(_id))
            buildingList.Add(_id, new List<Unit>());
        buildingList[_id].Add(_unit);
    }
    public bool IsSelectedBuild(ulong _id)
    {
        Player _player = players[_id];
        if (_player == null || _player.selectedUnitList.Length == 0)
            return false;
        Unit _u = GetUnit(players[_id].selectedUnitList[0]);
        if (_u == null)
            return false;
        else return _u.IsBuild;
    }
    public List<int> InsideSelectedUnit(ulong _id)
    {
        Player _player = players[_id];
        if (_player == null || _player.selectedUnitList.Length == 0)
            return new List<int>();
        Unit _u = GetUnit(players[_id].selectedUnitList[0]);
        if (_u == null || _u.unitCarrier == null)
            return new List<int>();
        else return _u.unitCarrier.unitsInside;
    }
    public Unit GetUnit(int _id)
    {
        return playerUnits[_id];
    }
    public Color GetColor(int _id)
    {
        return teamColor[_id];
    }
    public Player GetPlayer(ulong _id)
    {
        return players[_id];
    }
}