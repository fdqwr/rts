using System.Collections;
using UnityEngine;
namespace rts.Player
{
    using rts.GameLogic;
    using rts.Unit;
    using Zenject;

    public class Bot : Player
    {
        float time;
        Quaternion rotation;

        public void Start()
        {
            if (!IsServer)
                return;
            rotation = cam.transform.parent.parent.rotation;
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
                if (supplyCenters.Count < time / 200)
                {
                    if (Build(4) || supplyCenters.Count == 0)
                        continue;
                }
                if (factories.Count == 0)
                {
                    if (Build(1))
                        continue;
                }
                if (barracks.Count == 0)
                {
                    if (Build(2))
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
                    gameManager.PressUnitButton(Random.Range(0, factories[_i].settings.unitButtons.Length - 1), playerID.Value);
                }
                for (int _i = 0; _i < barracks.Count; _i++)
                {
                    SetSelectedUnitsRpc(new int[] { barracks[_i].id.Value }, false);
                    gameManager.PressUnitButton(Random.Range(0, barracks[_i].settings.unitButtons.Length - 1), playerID.Value);
                }
                for (int _i = 0; _i < airfields.Count; _i++)
                {
                    SetSelectedUnitsRpc(new int[] { airfields[_i].id.Value }, false);
                    gameManager.PressUnitButton(Random.Range(0, airfields[_i].settings.unitButtons.Length - 1), playerID.Value);
                }
                if (time > 100 && groundUnits.Count > 10 && groundUnits.Count > gameData.players[0].groundUnits.Count)
                    for (int _i = 0; _i < groundUnits.Count; _i++)
                    {
                        if (groundUnits[_i].orders.targetClass)
                            continue;
                        float _closestDistance = 9999999;
                        Unit _targetUnit = null;
                        foreach (Unit _unit in gameData.allUnits)
                        {
                            if (_unit.team.Value != team.Value && _unit.team.Value != 0)
                            {
                                float _distance = Vector3.Distance(groundUnits[_i].transform.position, _unit.transform.position);
                                if (_distance < _closestDistance)
                                {
                                    _closestDistance = _distance;
                                    _targetUnit = _unit;
                                }
                            }
                        }
                        if (_targetUnit && groundUnits[_i].unitWeapons.Length > 0 && !groundUnits[_i].orders.targetClass)
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
                if (gameData.unitSettings[_id].cost >= money.Value || _u.orders.targetClass != null)
                    continue;
                float _distance = 999999f;
                Vector3 _pos = spawnPosition;
                if (_id == 4)
                {
                    foreach (Supply _r in gameData.supplyStockpiles)
                    {
                        float _d = Vector3.Distance(spawnPosition, _r.transform.position);
                        if (_d < _distance)
                        {
                            Collider[] _colList = Physics.OverlapSphere(_r.transform.position, 100);
                            bool _otherResC = false;
                            foreach (Collider _col in _colList)
                            {
                                Unit _unit = _col.GetComponent<Unit>();
                                if ((_unit && ((_unit.team.Value != team.Value && _unit.team.Value != 0)) ||
                                    (_unit && _unit.settings.unitType == Settings.UnitType.supplyStockpile)))
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
                _pos = GetFreePos((_pos), gameData.unitSettings[_id].size, rotation);
                if (_id != 4)
                {
                    Collider[] _colList = Physics.OverlapSphere(_pos, 50);
                    foreach (Collider _col in _colList)
                    {
                        Unit _un = _col.GetComponent<Unit>();
                        if (_un && _un.team.Value != team.Value && _un.team.Value != 0)
                            return false;
                    }
                }
                SetSelectedUnitsRpc(new int[] { _u.id.Value }, false);
                SetBuildIDRpc(_id, gameData.unitSettings[4].cost);
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
                if (_u && _u.settings.IsBuilding)
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