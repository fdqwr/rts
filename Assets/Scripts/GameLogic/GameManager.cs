using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Unity.Jobs;
using Unity.Collections;
using Unity.Netcode;
namespace rts.GameLogic
{
    using rts.Unit;
    using rts.Player;
    using rts.UI;
    using Zenject;
    using System.Collections.Generic;

    public class GameManager : NetworkBehaviour
    {
        [field: SerializeField] public MapSettings mapSettings { get; private set; }
        GameData gameData;
        int team = 0;
        MenuUI menuUI;
        [Inject]
        public void Construct(MenuUI _menuUI)
        {
            menuUI = _menuUI;
        }
        void Awake()
        {
            gameData = GetComponent<GameData>();
            StartCoroutine(Loop());
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
                SpawnPlayerServerRpc(true);
        }
        public void OnSpawn()
        {
            if (!IsServer)
                return;
            NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
            foreach (ulong _id in NetworkManager.ConnectedClientsIds)
                SpawnPlayerServerRpc(false);
            foreach (MapSettings.SupplySpawn _spawn in mapSettings.maps[menuUI.map.Value].supplySpawn)
                StartCoroutine(SpawnUnit(99999, 0, -1, _spawn.position, _spawn.rotation, 20, true));
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
            for (int _i = 0; _i < gameData.players.Count + 1; _i++)
            {
                if (!gameData.players.ContainsKey(_i))
                {
                    _lowestNumber = _i;
                    break;
                }
            }
            if (_isBot)
            {
                _bot = Instantiate(gameData.botPrefab, mapSettings.maps[menuUI.map.Value].playerSpawn[_lowestNumber].position
                    , mapSettings.maps[menuUI.map.Value].playerSpawn[_lowestNumber].rotation, transform).GetComponent<Bot>();
                _player = _bot.GetComponent<Player>();
            }
            else
                _player = Instantiate(gameData.playerPrefab, mapSettings.maps[menuUI.map.Value].playerSpawn[_lowestNumber].position
                    , mapSettings.maps[menuUI.map.Value].playerSpawn[_lowestNumber].rotation, transform).GetComponent<Player>();
            _player.SetData(team, _lowestNumber);
            if (!_isBot)
                _player.GetComponent<NetworkObject>().SpawnAsPlayerObject((ulong)_lowestNumber);
            else
                _player.GetComponent<NetworkObject>().Spawn();
            StartCoroutine(SpawnUnit(_lowestNumber, team, -1, mapSettings.maps[menuUI.map.Value].playerSpawn[_lowestNumber].position
                , mapSettings.maps[menuUI.map.Value].playerSpawn[_lowestNumber].rotation, 0, true));
        }

        IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                NativeArray<Vector2> _position = new NativeArray<Vector2>(gameData.allUnits.Count, Allocator.TempJob);
                NativeArray<int> _team = new NativeArray<int>(gameData.allUnits.Count, Allocator.TempJob);
                NativeArray<int> _id = new NativeArray<int>(gameData.allUnits.Count, Allocator.TempJob);
                NativeArray<int> _closestID = new NativeArray<int>(gameData.allUnits.Count, Allocator.TempJob);
                NativeArray<float> _distance = new NativeArray<float>(gameData.allUnits.Count * gameData.allUnits.Count, Allocator.TempJob);
                NativeArray<float> _d2 = new NativeArray<float>(gameData.allUnits.Count * gameData.allUnits.Count, Allocator.TempJob);
                for (int i = 0; i < gameData.allUnits.Count; i++)
                {
                    _position[i] = gameData.allUnits[i].transform.position;
                    _id[i] = gameData.allUnits[i].id.Value;
                    _team[i] = gameData.allUnits[i].team.Value;
                }
                CalculateDistance job = new CalculateDistance
                {
                    Position = _position,
                    Team = _team,
                    ID = _id,
                    ClosestID = _closestID,
                    Distance = _distance,
                    Distance2 = _d2
                };
                JobHandle jobHandle = job.Schedule(gameData.allUnits.Count, 4);
                jobHandle.Complete();
                for (int i = 0; i < gameData.allUnits.Count; i++)
                    gameData.allUnits[i].orders.SetNearbyEnemies(gameData.GetUnit(_closestID[i]), _distance[i]);
                _position.Dispose();
                _distance.Dispose();
                _team.Dispose();
                _id.Dispose();
                _d2.Dispose();
            }
        }
        public void PressUnitButton(int _buttonIndex, int _playerID)
        {
            if (!ButtonIsDublicatated(_playerID, _buttonIndex))
                return;
            Player _player = gameData.GetPlayer(_playerID);
            if (_player.selectedUnitList.Count == 0)
                return;
            Settings.UnitButton[] _allButtons = gameData.unitSettings[gameData.GetUnit(_player.selectedUnitList[0]).settings.id].unitButtons;
            if (_allButtons.Length <= _buttonIndex)
                return;
            Settings.UnitButton _button = _allButtons[_buttonIndex];
            if (_button.buttonType == Settings.UnitButton.btype.Cancel)
                foreach (int _i in _player.selectedUnitList)
                {
                    Unit _u = gameData.GetUnit(_i);
                    if (_u)
                        _u.orders.FinishOrderRpc(true);
                }
            else if (_button.buttonType == Settings.UnitButton.btype.Build)
                _player.SetBuildIDRpc(_button.iD, gameData.unitSettings[_button.iD].cost);
            else if (_button.buttonType == Settings.UnitButton.btype.Spawn)
            {
                foreach (int _i in _player.selectedUnitList)
                    gameData.GetUnit(_i).AddToQueue(_button.iD, gameData.unitSettings[_button.iD].cost);
            }
            else if (_button.buttonType == Settings.UnitButton.btype.DismountUnit)
            {
                foreach (int _i in _player.selectedUnitList)
                {
                    Unit _u = gameData.GetUnit(_i);
                    if (_u.carrier) _u.carrier.DropUnit(_buttonIndex);
                }
            }
        }

        public void PressUnitQueueButton(int _button, int _id)
        {
            Player _player = gameData.GetPlayer(_id);
            if (_player.selectedUnitList.Count == 0)
                return;
            foreach (int _i in _player.selectedUnitList)
                gameData.GetUnit(_i).RemoveFromQueue(_button);
        }
        public void AddUnit(int _id, Unit _unit)
        {
            gameData.AddUnit(_id, _unit);
            UnitUI _unitUI = Instantiate(gameData.unitUIPrefab, menuUI.unitUIParent).GetComponent<UnitUI>();
            _unitUI.SetUnit(_unit);
            _unit.SetUI(_unitUI);
            if (_unit.IsInvulnerable)
                _unitUI.gameObject.SetActive(false);
        }

        public IEnumerator SpawnUnit(int _playerID, int _team, int _spawnerID, Vector3 _pos, Quaternion _rot, int _type, bool _instaBuild)
        {
            AsyncOperationHandle<GameObject> _handle = Addressables.InstantiateAsync(gameData.unitSettings[_type].asset, _pos, _rot, transform);
            yield return _handle;
            Unit _unit = _handle.Result.GetComponent<Unit>();
            int _lowestNumber = gameData.playerUnits.Count;
            for (int _i = 0; _i < gameData.playerUnits.Count + 1; _i++)
            {
                if (!gameData.playerUnits.ContainsKey(_i))
                {
                    _lowestNumber = _i;
                    break;
                }
            }
            _unit.SetData(_team, _lowestNumber, _playerID, _instaBuild);
            _unit.GetComponent<NetworkObject>().Spawn();
            if (_spawnerID > -1)
            {
                Unit _unitSpawner = gameData.GetUnit(_spawnerID);
                if (_unitSpawner.settings.unitType == Settings.UnitType.builder)
                    gameData.GetUnit(_spawnerID).orders.SetTargetRpc(_unit.id.Value, false, 1);
                else
                    gameData.GetUnit(_spawnerID).SpawnedUnitSetup(_unit, _instaBuild);
            }
        }
        public bool ButtonIsDublicatated(int _id, int _button)
        {
            List<int> _unitList = gameData.GetPlayer(_id).selectedUnitList;
            if (_unitList.Count < 2)
                return true;
            Settings.UnitButton.btype _buttonType = gameData.GetUnit(_unitList[0]).settings.unitButtons[_button].buttonType;
            int _spawnID = gameData.GetUnit(_unitList[0]).settings.unitButtons[_button].iD;
            for (int _i = 1; _i < _unitList.Count; _i++)
                if (_buttonType != gameData.GetUnit(_unitList[_i]).settings.unitButtons[_button].buttonType
                    || _spawnID != gameData.GetUnit(_unitList[_i]).settings.unitButtons[_button].iD)
                    return false;
            return true;
        }
    }
}