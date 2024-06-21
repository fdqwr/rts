using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using rts.Unit;
using rts.Player;
using rts.UI;
public class GameData : NetworkBehaviour
{
    public static GameData i;
    public Dictionary<int, Settings> unitSettings { get; private set; } = new Dictionary<int, Settings>();
    public Dictionary<int, Unit> playerUnits { get; private set; } = new Dictionary<int, Unit>();
    public List<Unit> allUnits { get; private set; } = new List<Unit>();
    public List<Supply> supplies { get; private set; } = new List<Supply>();
    public Dictionary<int, List<Supply>> supplyStockpile { get; private set; } = new Dictionary<int, List<Supply>>();
    public Dictionary<int, List<Unit>> buildingList { get; private set; } = new Dictionary<int, List<Unit>>();
    public Dictionary<int, Player> players { get; private set; } = new Dictionary<int, Player>();
    [SerializeField] Color[] teamColor = new Color[9] { Color.white, Color.blue * 0.5f, Color.red * 0.5f, Color.green * 0.5f, 
        Color.cyan * 0.5f, Color.magenta * 0.5f,Color.yellow*0.5f,Color.black*0.5f,Color.Lerp(Color.red, Color.blue,0.5f)*0.5f };
    public GameObject playerPrefab { get; private set; }
    public GameObject botPrefab { get; private set; }
    public GameObject unitUIPrefab { get; private set; }
    int team = 0;
    private void Awake()
    {
        i = this;
        foreach (Settings _uS in UnityEngine.Resources.LoadAll("UnitSettings", typeof(Settings)))
            unitSettings.Add(_uS.type, _uS);
        playerPrefab = (GameObject)UnityEngine.Resources.Load("Prefabs/Player");
        botPrefab = (GameObject)UnityEngine.Resources.Load("Prefabs/Bot");
        unitUIPrefab = (GameObject)UnityEngine.Resources.Load("Prefabs/UnitUI");
        StartCoroutine(Loop());
    }
    IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            NativeArray<Vector2> _position = new NativeArray<Vector2>(allUnits.Count, Allocator.TempJob);
            NativeArray<int> _team = new NativeArray<int>(allUnits.Count, Allocator.TempJob);
            NativeArray<int> _id = new NativeArray<int>(allUnits.Count, Allocator.TempJob);
            NativeArray<int> _closestID = new NativeArray<int>(allUnits.Count, Allocator.TempJob);
            NativeArray<float> _distance = new NativeArray<float>(allUnits.Count * allUnits.Count, Allocator.TempJob);
            NativeArray<float> _d2 = new NativeArray<float>(allUnits.Count * allUnits.Count, Allocator.TempJob);
            for (int i = 0; i < allUnits.Count; i++)
            {
                _position[i] = allUnits[i].transform.position;
                _id[i] = allUnits[i].id.Value;
                _team[i] = allUnits[i].team.Value;
            }
            CalculateDistance job = new CalculateDistance
            {
                Position = _position,
                Team = _team,
                ID = _id,
                ClosestID = _closestID,
                Distance = _distance,
                d2 = _d2
            };
            JobHandle jobHandle = job.Schedule(allUnits.Count, 4);
            jobHandle.Complete();
            for (int i = 0; i < allUnits.Count; i++)
                allUnits[i].orders.SetNearbyEnemies(GetUnit(_closestID[i]), _distance[i]);
            _position.Dispose();
            _distance.Dispose();
            _team.Dispose();
            _id.Dispose();
            _d2.Dispose();
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
            SpawnPlayerServerRpc(true);
    }
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;
        NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
        foreach (ulong _id in NetworkManager.ConnectedClientsIds)
            SpawnPlayerServerRpc(false);
        foreach (Spawner _u in MapSettings.i.unitSpawner)
            _u.OnSpawn();
    }
    void OnClientConnectedCallback(ulong _playerID)
    {
        SpawnPlayerServerRpc(false);
    }
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SpawnPlayerServerRpc(bool _isBot)
    {
        team++;
        Bot _bot;
        Player _player;
        int _lowestNumber = 0;
        for (int _i = 0; _i < players.Count + 1; _i++)
        {
            if (!players.ContainsKey(_i))
            {
                _lowestNumber = _i;
                break;
            }
        }
        if (_isBot)
        {
            _bot = Instantiate(botPrefab, MapSettings.i.playerSpawn[_lowestNumber].position, MapSettings.i.playerSpawn[_lowestNumber].rotation).GetComponent<Bot>();
            _player = _bot.GetComponent<Player>();
        }
        else 
            _player = Instantiate(playerPrefab, MapSettings.i.playerSpawn[_lowestNumber].position, MapSettings.i.playerSpawn[_lowestNumber].rotation).GetComponent<Player>();
        _player.OnSpawn(team, _lowestNumber);
        if (!_isBot)
            _player.GetComponent<NetworkObject>().SpawnAsPlayerObject((ulong)_lowestNumber);
        else
            _player.GetComponent<NetworkObject>().Spawn();
        StartCoroutine(SpawnUnit(_lowestNumber, team, -1, MapSettings.i.playerSpawn[_lowestNumber].position, MapSettings.i.playerSpawn[_lowestNumber].rotation, 0, true));
    }
    public void PressUnitButton(int _button, int _playerID)
    {
        if (!MenuUI.i.ButtonIsDublicatated(_playerID, _button))
            return;
        Player _player = GetPlayer(_playerID);
        if (_player.selectedUnitList.Count == 0)
            return;
        Settings.UnitButton[] _ub = unitSettings[GetUnit(_player.selectedUnitList[0]).unitSettings.type].unitButtons;
        if (_ub.Length <= _button)
            return;
        Settings.UnitButton _s = _ub[_button];
        if (_s.buttonType == Settings.UnitButton.btype.Cancel)
            foreach (int _i in _player.selectedUnitList)
            {
                Unit _u = GetUnit(_i);
                if (_u)
                    _u.orders.FinishOrderRpc(true);
            }
        else if (_s.buttonType == Settings.UnitButton.btype.Build)
            _player.SetBuildIDRpc(_s.iD, unitSettings[_s.iD].cost);
        else if (_s.buttonType == Settings.UnitButton.btype.Spawn)
        {
            foreach (int _i in _player.selectedUnitList)
                GetUnit(_i).AddToQueue(_s.iD, unitSettings[_s.iD].cost);
        }
        else if (_s.buttonType == Settings.UnitButton.btype.DismountUnit)
        {
            foreach (int _i in _player.selectedUnitList)
            {
                Unit _u = GetUnit(_i);
                if (_u.unitCarrier) _u.unitCarrier.DropUnit(_button);
            }
        }
    }
    public void PressUnitQueueButton(int _button, int _id)
    {
        Player _player = GetPlayer(_id);
        if (_player.selectedUnitList.Count == 0) 
            return;
        foreach (int _i in _player.selectedUnitList)
            GetUnit(_i).RemoveFromQueue(_button);
    }
    public IEnumerator SpawnUnit(int _playerID, int _team, int _spawnerID, Vector3 _pos, Quaternion _rot, int _type, bool _instaBuild)
    {
        AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(unitSettings[_type].asset, _pos, _rot, transform);
        yield return _handle;
        Unit _unit = _handle.Result.GetComponent<Unit>();
        int _lowestNumber = playerUnits.Count;
        for (int _i = 0; _i < playerUnits.Count+1; _i++)
        {
            if(!playerUnits.ContainsKey(_i))
            {
                _lowestNumber = _i;
                break;
            }
        }
        _unit.SetupData(_team, _lowestNumber, _playerID, _instaBuild);
        _unit.GetComponent<NetworkObject>().Spawn();
        if (_spawnerID > -1)
        {
            Unit unitSpawner = GetUnit(_spawnerID);
            if (unitSpawner.unitSettings.unitType == Settings.UnitType.builder)
                GetUnit(_spawnerID).orders.SetTargetRpc(_unit.id.Value, false,1);
            else
                GetUnit(_spawnerID).UnitSetup(_unit, _instaBuild);
        }
    }
    public void AddUnit(int _id, Unit _unit)
    {
        playerUnits.Add(_id, _unit);
        allUnits.Add(_unit);
        UnitUI _uUI = Instantiate(unitUIPrefab, MenuUI.i.unitUIParent).GetComponent<UnitUI>();
        _uUI.SetUnit(_unit);
        _unit.SetUI(_uUI);
        if (_unit.IsInvulnerable)
            _uUI.gameObject.SetActive(false);
    }
    public void RemoveUnit(int _id, Unit _unit)
    {
        if(playerUnits.ContainsKey(_id))
            playerUnits.Remove(_id);
        if (allUnits.Contains(_unit))
            allUnits.Remove(_unit);
    }
    public void AddPlayer(int _id, Player _player)
    {
        players.Add(_id, _player);
    }
    public void RemovePlayer(int _id)
    {
        if (players.ContainsKey(_id))
            players.Remove(_id);
    }
    public void AddStockpile(int _id, Supply _unitResources)
    {
        if (!supplyStockpile.ContainsKey(_id))
            supplyStockpile.Add(_id, new List<Supply>());
        supplyStockpile[_id].Add(_unitResources);
    }
    public void RemoveStockpile(int _id, Supply _unitResources)
    {
        if (supplyStockpile.ContainsKey(_id))
            supplyStockpile[_id].Remove(_unitResources);
    }
    public void AddToSupply(Supply _unitResources)
    {
        supplies.Add(_unitResources);
    }
    public void RemoveFromSupply(Supply _unitResources)
    {
        if (supplies.Contains(_unitResources))
            supplies.Remove(_unitResources);
    }
    public void AddBuilding(int _id, Unit _unit)
    {
        if (!buildingList.ContainsKey(_id))
            buildingList.Add(_id, new List<Unit>());
        buildingList[_id].Add(_unit);
    }
    public void RemoveBuilding(int _id, Unit _unit)
    {
        if (buildingList.ContainsKey(_id) && buildingList[_id].Contains(_unit))
            buildingList[_id].Remove(_unit);
    }
    public bool IsSelectedBuild(int _id)
    {
        Player _player = players[_id];
        if (_player == null || _player.selectedUnitList.Count == 0)
            return false;
        Unit _u = GetUnit(players[_id].selectedUnitList[0]);
        if (_u == null)
            return false;
        else return _u.IsBuild;
    }
    public List<int> InsideSelectedUnit(int _id)
    {
        Player _player = players[_id];
        if (_player == null || _player.selectedUnitList.Count == 0)
            return new List<int>();
        Unit _u = GetUnit(players[_id].selectedUnitList[0]);
        if (_u == null || _u.unitCarrier == null)
            return new List<int>();
        else return _u.unitCarrier.unitsInside;
    }
    public Unit GetUnit(int _id)
    {
        if (!playerUnits.ContainsKey(_id))
            return null;
        return playerUnits[_id];
    }
    public Color GetColor(int _id)
    {
        return teamColor[_id];
    }
    public Player GetPlayer(int _id)
    {
        if (!players.ContainsKey(_id))
            return null;
        return players[_id];
    }
}