using System.Collections;
using UnityEngine;

public class Bot : Player
{
    float time;
    Quaternion rotation;
    private void Start()
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
            yield return new WaitForSeconds(2);
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
            foreach (Unit _u in factories)
            {
                SetSelectedUnitsRpc(new int[] { _u.id.Value }, false);
                GameManager.i.PressUnitButton(Random.Range(0, _u.UB.unitButtons.Length - 1),playerID.Value);
            }
            if(time > 100 && groundUnits.Count > 10 && groundUnits.Count > GameManager.i.players[0].groundUnits.Count)
                foreach (Unit _u in groundUnits)
                {
                    if (_u.UnitWeapons.Length > 0)
                        _u.SetTargetPosRpc(GameManager.i.players[0].spawnPosition);
                }
        }
    }
    bool Build(int _id)
    {
        foreach (Unit _u in builders)
        {
            if (GameManager.i.unitSettings[_id].cost >= money.Value || _u.unitTargetClass != null)
                continue;
            float _distance = 999999f;
            Vector3 _pos = spawnPosition;
            if (_id == 4)
            {
                foreach (UnitResources _r in GameManager.i.resources)
                {
                    float _d = Vector3.Distance(spawnPosition, _r.transform.position);
                    if (_d < _distance)
                    {
                        Collider[] _colList = Physics.OverlapSphere(_r.transform.position, 100);
                        bool _otherResC = false;
                        foreach (Collider _col in _colList)
                        {
                            Unit _un = _col.GetComponent<Unit>();
                            if (_un && ((_un.team.Value != GetTeam && _un.team.Value != 0) ||
                                (_un.unitResources && _un.unitResources.currentResource.Value == 0 && _un.unitResources.ResourceCarryCapacity == 0)))
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
            _pos = GetFreePos((_pos), GameManager.i.unitSettings[_id].size, rotation);
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
            SetBuildIDRpc(_id, GameManager.i.unitSettings[4].cost);
            SetTargetPositionRpc(_pos, rotation, false);
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
            for (float _shiftZ = 0; _shiftZ < _shiftX; _shiftZ+=2)
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
            _shiftX+=2;
        }
    }
    static bool BuildingCollision(Vector3 _startingPos, Vector3 _size, Quaternion _q)
    {
        Collider[] _colList = Physics.OverlapBox(_startingPos, _size, _q);
        foreach (Collider _col in _colList)
        {
            Unit _u = _col.GetComponent<Unit>();
            if (_u && _u.UnitType == UnitSettings.UnitType.building)
                return false;
        }
        return true;
    }
    private void Update()
    {
        time += Time.deltaTime;
    }
}
