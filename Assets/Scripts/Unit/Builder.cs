using UnityEngine;
namespace rts.Unit 
{
    using rts.GameLogic;
    using rts.Player;
    using Zenject;

    public class Builder : MonoBehaviour
    {
        Unit unit;
        GameManager gameManager;
        GameData gameData;

        private void Awake()
        {
            unit = GetComponent<Unit>();
        }
        [Inject]
        public void Construct(GameManager _gameManager, GameData _gameData)
        {
            gameManager = _gameManager;
            gameData = _gameData;
        }
        public void BuildFoundation(Vector3 _buildPos, Quaternion _rot, int _buildType, float _cost)
        {
            Player _player = gameData.GetPlayer(unit.playerID.Value);
            if (_player.money.Value >= _cost)
                _player.money.Value -= _cost;
            else return;
            StartCoroutine(gameManager.SpawnUnit(unit.playerID.Value, unit.team.Value, unit.id.Value, _buildPos, _rot, _buildType, false));
        }
    }
}
