using UnityEngine;

namespace rts.Unit
{
    public class Helicopter : Aircraft
    {
        [SerializeField] Transform[] helicopterBlade;
        private void Update()
        {
            foreach (Transform _t in helicopterBlade)
                _t.Rotate(0, 0, 1000 * Time.deltaTime);
            Flying();
        }
        void Flying()
        {
            Vector3? _targetPosition = orders.TargetPosition();
            if (!_targetPosition.HasValue)
            {
                altitude += Time.deltaTime / 1;
                altitude = Mathf.Clamp(altitude, 0, 1);
                return;
            }
            float _altitude = airportAltitude;
            if (unit.carrier && orders.targetClass && orders.targetClass.settings.occupyPSlots > 0 && orders.targetClass.insideID.Value < 0)
            {
                Vector3 _targetPos = _targetPosition.Value;
                _targetPos.y = t.position.y;
                if (Vector3.Distance(_targetPos, transform.position) < 3)
                {
                    _altitude = _targetPosition.Value.y;
                    altitude -= Time.deltaTime;
                    if (t.position.y - _targetPosition.Value.y < 1)
                    {
                        unit.carrier.GetInsideUnit(orders.targetClass);
                        orders.FinishOrderRpc(false);
                    }
                }
            }            else if (unitsToDrop)
            {
                altitude -= Time.deltaTime;
                _altitude = _targetPosition.Value.y;
            }
            else
                altitude += Time.deltaTime / 1;
            altitude = Mathf.Clamp(altitude, 0, 1);
            Vector3 _positionWithoutY = new Vector3(_targetPosition.Value.x, Mathf.Lerp(airportAltitude, height, altitude), _targetPosition.Value.z);
            Vector3 _targetDirection = _positionWithoutY - t.position;
            float _singleStep = Time.deltaTime * altitude;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, _singleStep, 0);
            t.rotation = Quaternion.LookRotation(_newDirection);
            t.position = Vector3.MoveTowards(t.position, _positionWithoutY, speed * Time.deltaTime * altitude);
            t.position = new Vector3(t.position.x, Mathf.Lerp(_altitude, height, altitude), t.position.z);
            t.eulerAngles = new Vector3(0, t.eulerAngles.y, 0);
        }
    }
}
