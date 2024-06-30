using System.Collections.Generic;
using UnityEngine;
using rts.UI;
using Zenject;
namespace rts.GameLogic
{
    using rts.Unit;
    using rts.Player;
    public class GameData : MonoBehaviour
    {
        public Dictionary<int, Settings> unitSettings { get; private set; } = new Dictionary<int, Settings>();
        public Dictionary<int, Unit> playerUnits { get; private set; } = new Dictionary<int, Unit>();
        public List<Unit> allUnits { get; private set; } = new List<Unit>();
        public List<Supply> supplyStockpiles { get; private set; } = new List<Supply>();
        public Dictionary<int, List<Supply>> supplyCenters { get; private set; } = new Dictionary<int, List<Supply>>();
        public Dictionary<int, List<Unit>> buildingList { get; private set; } = new Dictionary<int, List<Unit>>();
        public Dictionary<int, Player> players { get; private set; } = new Dictionary<int, Player>();
        [SerializeField]
        Color[] teamColor = new Color[9] { Color.white, Color.blue * 0.5f, Color.red * 0.5f, Color.green * 0.5f,
        Color.cyan * 0.5f, Color.magenta * 0.5f,Color.yellow*0.5f,Color.black*0.5f,Color.Lerp(Color.red, Color.blue,0.5f)*0.5f };
        public GameObject playerPrefab { get; private set; }
        public GameObject botPrefab { get; private set; }
        public GameObject unitUIPrefab { get; private set; }
        public MenuUI menuUI;

        [Inject]
        public void Construct(MenuUI _menuUI)
        {
            menuUI = _menuUI;
        }

        private void Awake()
        {
            foreach (Settings _uS in Resources.LoadAll("UnitSettings", typeof(Settings)))
                unitSettings.Add(_uS.id, _uS);
            playerPrefab = (GameObject)Resources.Load("Prefab/Player");
            botPrefab = (GameObject)Resources.Load("Prefab/Bot");
            unitUIPrefab = (GameObject)Resources.Load("Prefab/UnitUI");
        }

        public void ResetData()
        {
            unitSettings.Clear();
            playerUnits.Clear();
            allUnits.Clear();
            supplyStockpiles.Clear();
            supplyCenters.Clear();
            buildingList.Clear();
            players.Clear();
        }

        public void AddUnit(int _id, Unit _unit)
        {
            playerUnits.Add(_id, _unit);
            allUnits.Add(_unit);
        }

        public void RemoveUnit(int _id, Unit _unit)
        {
            if (playerUnits.ContainsKey(_id))
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

        public void AddSupplyCenter(int _id, Supply _unitResources)
        {
            if (!supplyCenters.ContainsKey(_id))
                supplyCenters.Add(_id, new List<Supply>());
            supplyCenters[_id].Add(_unitResources);
        }

        public void RemoveSupplyCenter(int _id, Supply _unitResources)
        {
            if (supplyCenters.ContainsKey(_id))
                supplyCenters[_id].Remove(_unitResources);
        }

        public void AddToSupplyStockpile(Supply _unitResources)
        {
            supplyStockpiles.Add(_unitResources);
        }

        public void RemoveFromSupplyStockpile(Supply _unitResources)
        {
            if (supplyStockpiles.Contains(_unitResources))
                supplyStockpiles.Remove(_unitResources);
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
            if (!_player || _player.selectedUnitList.Count == 0)
                return false;
            Unit _unit = GetUnit(players[_id].selectedUnitList[0]);
            if (!_unit)
                return false;
            else return _unit.IsConstructionFinished;
        }

        public List<int> InsideSelectedUnit(int _id)
        {
            Player _player = players[_id];
            if (!_player || _player.selectedUnitList.Count == 0)
                return new List<int>();
            Unit _unit = GetUnit(players[_id].selectedUnitList[0]);
            if (!_unit || !_unit.carrier)
                return new List<int>();
            else return _unit.carrier.unitsInside;
        }

        public Unit GetUnit(int _id)
        {
            if (playerUnits.TryGetValue(_id, out Unit _unit))
                return _unit;
            return null;
        }

        public Color GetColor(int _id)
        {
            return teamColor[_id];
        }

        public Player GetPlayer(int _id)
        {
            if (players.TryGetValue(_id, out Player _player))
                return _player;
            return null;
        }
    }
}