using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
[RequireComponent(typeof(Unit))]
public class UnitCarrier : MonoBehaviour
{
    Unit unit;
    [SerializeField] Transform[] dismountPositions;
    [SerializeField] bool hideInside;
    [SerializeField] bool shootFromInside;
    [SerializeField] bool transportVehicles;
    [SerializeField] float damageReduction = 100;
    public List<int> unitsInside { get; private set; } = new List<int>();
    List<int> unitsToDrop = new List<int>();
    public bool HideInside => hideInside;
    public bool ShootFromInside => shootFromInside;
    public float DamageReduction => damageReduction;
    void Awake()
    {
        unit = GetComponent<Unit>();
        foreach (Transform _t in dismountPositions)
            unitsInside.Add(-1);
    }
    private void Update()
    {
        if ((unit.unitAir && unit.unitAir.altitude < 0.05f) && unitsToDrop.Count > 0)
        {
            unit.unitAir.AddToDrop(false);
            foreach (int _i in unitsToDrop)
                DropUnit(_i);
            unitsToDrop = new List<int>();
        }
    }
    public void GetInsideUnit(Unit _unit)
    {
        if (_unit.OccupyPSlots == 0 || unitsInside.Contains(_unit.id.Value)) 
            return;
        int _currOccup = 0;
        foreach (int _u in unitsInside)
            if (_u != -1)
                _currOccup += GameManager.i.GetUnit(_u).OccupyPSlots;
        if (_currOccup >= dismountPositions.Length)
            return;
        int _i = -1;
        for (int i = 0; i < unitsInside.Count; i++)
            if (_i == -1 && unitsInside[i] == -1)
                _i = i;
        if (_i != -1)
            ChangeInsideListRpc(_i, _unit.id.Value);
        _unit.InsideUnitID(unit.id.Value);
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
        if (unit.unitAir && unit.unitAir.altitude > 0.1f)
        {
            int _layerMask = 1 << 2;
            _layerMask = ~_layerMask;
            RaycastHit _hit;
            if (Physics.Raycast(transform.position, -transform.up, out _hit, 25, _layerMask))
            {
                if (!unitsToDrop.Contains(_slot))
                    unitsToDrop.Add(_slot);
                unit.unitAir.AddToDrop(true);
                unit.SetTargetPosRpc(_hit.point);
            }
            return;
        }
        Unit _u = GameManager.i.GetUnit(unitsInside[_slot]);
        _u.transform.SetPositionAndRotation(dismountPositions[_slot].position, dismountPositions[_slot].rotation);
        ChangeInsideListRpc(_slot, -1);
        _u.InsideUnitID(-1);
        _u.SetTargetPosRpc(_u.transform.position);
    }
    public void OnDie()
    {
        foreach (int _i in unitsInside)
            if (_i != -1)
                GameManager.i.GetUnit(_i).DieRpc(false);
    }
    public int FreeUnitSlots()
    {
        int _i = dismountPositions.Length;
        foreach (int _u in unitsInside)
            if (_u != -1)
                _i--;
        return _i;
    }
}
