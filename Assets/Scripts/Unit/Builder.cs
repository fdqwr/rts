using UnityEngine;
namespace rts.Unit 
{
    using rts.Player;
    public class Builder : MonoBehaviour
    {
        Unit unit;
        private void Awake()
        {
            unit = GetComponent<Unit>();
        }

        public void BuildFoundation(Vector3 _buildPos, Quaternion _rot, int _buildType, float _cost)
        {
            Player _player = GameData.i.GetPlayer(unit.playerID.Value);
            if (_player.money.Value >= _cost)
                _player.money.Value -= _cost;
            else return;
            StartCoroutine(GameData.i.SpawnUnit(unit.playerID.Value, unit.team.Value, unit.id.Value, _buildPos, _rot, _buildType, false));
        }
    }
}
