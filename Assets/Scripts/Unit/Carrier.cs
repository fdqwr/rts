using rts.GameLogic;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Zenject;
namespace rts.Unit
{
    public class Carrier : MonoBehaviour
    {
        Unit unit;
        Orders orders;
        [SerializeField] Transform[] dismountPositions;
        [field: SerializeField] public bool hideInside { get; private set; }
        [field: SerializeField] public bool shootFromInside { get; private set; }
        [SerializeField] bool transportVehicles;
        [field: SerializeField] public float damageReduction { get; private set; } = 100;
        public List<int> unitsInside { get; private set; } = new List<int>();
        List<int> unitsToDrop = new List<int>();
        public int freeUnitSlots { get; private set; }
        GameData gameData;
        [Inject]
        public void Construct(GameData _gameData)
        {
            gameData = _gameData;
        }
        void Awake()
        {
            unit = GetComponent<Unit>();
            orders = GetComponent<Orders>();
            foreach (Transform _t in dismountPositions)
                unitsInside.Add(-1);
            freeUnitSlots = dismountPositions.Length;
        }

        private void Update()
        {
            if (unit.settings.IsAircraft && unit.aircraft.altitude < 0.05f && unitsToDrop.Count > 0)
            {
                unit.aircraft.AddToDrop(false);
                foreach (int _i in unitsToDrop)
                    DropUnit(_i);
                unitsToDrop = new List<int>();
            }
        }

        public void GetInsideUnit(Unit _unit)
        {
            if (_unit.settings.occupyPSlots == 0 || unitsInside.Contains(_unit.id.Value))
                return;
            int _currOccup = 0;
            foreach (int _u in unitsInside)
                if (_u != -1)
                    _currOccup += gameData.GetUnit(_u).settings.occupyPSlots;
            if (_currOccup >= dismountPositions.Length)
                return;
            int _i = -1;
            for (int i = 0; i < unitsInside.Count; i++)
                if (_i == -1 && unitsInside[i] == -1)
                    _i = i;
            if (_i != -1)
                ChangeInsideListRpc(_i, _unit.id.Value);
            _unit.SetInsideUnitID(unit.id.Value);
            freeUnitSlots -= _unit.settings.occupyPSlots;
        }

        [Rpc(SendTo.Everyone)]
        void ChangeInsideListRpc(int _index, int _id)
        {
            unitsInside[_index] = _id;
        }

        public void DropUnit(int _slot)
        {
            if (unitsInside[_slot] == -1)
                return;
            if (unit.settings.IsAircraft && unit.aircraft.altitude > 0.1f)
            {
                int _layerMask = 1 << 2;
                _layerMask = ~_layerMask;
                RaycastHit _hit;
                if (Physics.Raycast(transform.position, -transform.up, out _hit, 25, _layerMask))
                {
                    if (!unitsToDrop.Contains(_slot))
                        unitsToDrop.Add(_slot);
                    unit.aircraft.AddToDrop(true);
                    orders.SetTargetPosRpc(_hit.point, false, -1);
                }
                return;
            }
            Unit _unit = gameData.GetUnit(unitsInside[_slot]);
            freeUnitSlots += _unit.settings.occupyPSlots;
            _unit.transform.SetPositionAndRotation(dismountPositions[_slot].position, dismountPositions[_slot].rotation);
            ChangeInsideListRpc(_slot, -1);
            _unit.SetInsideUnitID(-1);
            _unit.orders.SetTargetPosRpc(_unit.transform.position, false, -1);
        }

        public void OnDiyng()
        {
            foreach (int _i in unitsInside)
                if (_i != -1)
                    gameData.GetUnit(_i).healthClass.DieRpc(false);
        }
    }
}