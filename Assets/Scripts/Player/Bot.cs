using System.Collections;
using UnityEngine;
namespace rts.Player
{
    using rts.Unit;
    public class Bot : Player
    {
        float time;
        Quaternion rotation;
        public void Start()
        {
            if (!IsServer)
                return;
            rotation = PlayerCamera.transform.parent.parent.rotation;
            rotation.eulerAngles += new Vector3(0, 180, 0);
            StartCoroutine(OrderLoop());
        }
        IEnumerator OrderLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);
                if (commandCenters.Count == 0)
                {
                    if (Build(0))
                        continue;
                }
                if (resourceCenters.Count < time / 200)
                {
                    if (Build(4) || resourceCenters.Count == 0)
                        continue;
                }
                if (factories.Count == 0)
                {
                    if (Build(2))
                        continue;
                }
                if (barracks.Count == 0)
                {
                    if (Build(1))
                        continue;
                }
                if (airfields.Count == 0)
                {
                    if (Build(3))
                        continue;
                }
                for (int _i = 0; _i < factories.Count; _i++)
                {
                    SetSelectedUnitsRpc(new int[] { factories[_i].id.Value }, false);
                    GameData.i.PressUnitButton(Random.Range(0, factories[_i].unitSettings.unitButtons.Length - 1), playerID.Value);
                }
                if (time > 100 && groundUnits.Count > 10 && groundUnits.Count > GameData.i.players[0].groundUnits.Count)
                    for (int _i = 0; _i < groundUnits.Count; _i++)
                    {
                        if (groundUnits[_i].orders.unitTargetClass)
                            continue;
                        float _closestDistance = 9999999;
                        Unit _targetUnit = null;
                        foreach (Unit _unit in GameData.i.allUnits)
                        {
                            if (_unit.team.Value != GetTeam && _unit.team.Value != 0)
                            {
                                float _distance = Vector3.Distance(groundUnits[_i].transform.position, _unit.transform.position);
                                if (_distance < _closestDistance)
                                {
                                    _closestDistance = _distance;
                                    _targetUnit = _unit;
                                }
                            }
                        }
                        if (_targetUnit && groundUnits[_i].unitWeapons.Length > 0 && !groundUnits[_i].orders.unitTargetClass)
                            groundUnits[_i].orders.SetTargetRpc(_targetUnit.id.Value, true, -1);
                        if (_i % 10 == 0)
                            yield return null;
                    }
            }
        }
        bool Build(int _id)
        {
            foreach (Unit _u in builders)
            {
                if (GameData.i.unitSettings[_id].cost >= money.Value || _u.orders.unitTargetClass != null)
                    continue;
                float _distance = 999999f;
                Vector3 _pos = spawnPosition;
                if (_id == 4)
                {
                    foreach (rts.Unit.Supply _r in GameData.i.supplies)
                    {
                        float _d = Vector3.Distance(spawnPosition, _r.transform.position);
                        if (_d < _distance)
                        {
                            Collider[] _colList = Physics.OverlapSphere(_r.transform.position, 100);
                            bool _otherResC = false;
                            foreach (Collider _col in _colList)
                            {
                                Unit _unit = _col.GetComponent<Unit>();
                                if ((_unit && ((_unit.team.Value != GetTeam && _unit.team.Value != 0)) ||
                                    (_unit && _unit.unitResources && _unit.unitResources.isResourceStockpile)))
                                    _otherResC = true;
                            }
                            if (!_otherResC)
                            {
                                _pos = _r.transform.position;
                                _distance = _d;
                            }
                        }
                    }
                    if (_pos == spawnPosition)
                        return false;
                    _pos = (spawnPosition - _pos).normalized * 20 + _pos;
                }
                _pos = GetFreePos((_pos), GameData.i.unitSettings[_id].size, rotation);
                if (_id != 4)
                {
                    Collider[] _colList = Physics.OverlapSphere(_pos, 50);
                    foreach (Collider _col in _colList)
                    {
                        Unit _un = _col.GetComponent<Unit>();
                        if (_un && _un.team.Value != GetTeam && _un.team.Value != 0)
                            return false;
                    }
                }
                SetSelectedUnitsRpc(new int[] { _u.id.Value }, false);
                SetBuildIDRpc(_id, GameData.i.unitSettings[4].cost);
                SetTargetPositionServerRpc(_pos, rotation, false, false);
                return true;
            }
            return false;
        }
        static Vector3 GetFreePos(Vector3 _startingPos, Vector3 _size, Quaternion _q)
        {
            if (BuildingCollision(_startingPos, _size, _q))
                return _startingPos;
            float _shiftX = 1;
            while (true)
            {
                for (float _shiftZ = 0; _shiftZ < _shiftX; _shiftZ += 2)
                {
                    if (BuildingCollision(_startingPos + new Vector3(_shiftX, 0, _shiftZ), _size, _q))
                        return _startingPos + new Vector3(_shiftX, 0, _shiftZ);
                    if (BuildingCollision(_startingPos + new Vector3(-_shiftX, 0, _shiftZ), _size, _q))
                        return _startingPos + new Vector3(-_shiftX, 0, _shiftZ);
                    if (BuildingCollision(_startingPos + new Vector3(_shiftX, 0, -_shiftZ), _size, _q))
                        return _startingPos + new Vector3(_shiftX, 0, -_shiftZ);
                    if (BuildingCollision(_startingPos + new Vector3(-_shiftX, 0, -_shiftZ), _size, _q))
                        return _startingPos + new Vector3(-_shiftX, 0, -_shiftZ);
                }
                _shiftX += 2;
            }
        }
        static bool BuildingCollision(Vector3 _startingPos, Vector3 _size, Quaternion _q)
        {
            Collider[] _colList = Physics.OverlapBox(_startingPos, _size, _q);
            foreach (Collider _col in _colList)
            {
                Unit _u = _col.GetComponent<Unit>();
                if (_u && _u.unitSettings.unitType == Settings.UnitType.building)
                    return false;
            }
            return true;
        }
        private void Update()
        {
            time += Time.deltaTime;
        }
    }
}