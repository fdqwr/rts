using UnityEngine;
[RequireComponent(typeof(Unit))]
public class UnitBuilder : MonoBehaviour
{
    Unit unit;
    private void Awake()
    {
        unit = GetComponent<Unit>();
    }

    public void BuildFoundation(Vector3 _buildPos, Quaternion _rot, int _buildType, float _cost)
    {
        Player _player = GameManager.i.GetPlayer(unit.playerID.Value);
        if (_player.money.Value >= _cost)
            _player.money.Value -= _cost;
        else return;
        StartCoroutine(GameManager.i.SpawnUnit(unit.playerID.Value, unit.team.Value, unit.id.Value, _buildPos, _rot, _buildType,false));
    }
}
