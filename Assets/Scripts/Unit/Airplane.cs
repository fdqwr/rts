using DG.Tweening;
using System.Collections;
using UnityEngine;
namespace rts.Unit
{
    public class Airplane : Aircraft
    {
        private void Update()
        {
            if (!unit.IsServer)
                return;
            Unit _targetClass = orders.targetClass;
            if (_targetClass
                && unit.team.Value == _targetClass.team.Value
                && aircraftState == AircraftState.flying
                && _targetClass.airfield
                && !orders.isAttackingTarget
                && (!airfield || airfield.unit != _targetClass))
            {
                _targetClass.airfield.SetupAirUnit(this);
                orders.SetTargetRpc(airfield.unit.id.Value, false, -1);
            }
            if (airfield && airfield.unit.healthClass.isDestroyed)
            {
                if (aircraftState != AircraftState.flying)
                    unit.healthClass.DieRpc(false);
                else airfield = null;
            }
            else if (aircraftState >= AircraftState.flying)
                Flying();
            else if ((orders.targetID > -1 || orders.targetVector != t.position))
                AirplaneTakeoff();
        }
        void AirplaneLanding()
        {
            if (false)
            {
                altitude += Time.deltaTime / 1;
                altitude = Mathf.Clamp(altitude, 0, 1);
                Vector3 _nearLanding = aircraftRunwayPosition + airfield.aircraftHangar[airIndex].aircraftRunwayPosition.forward * 50;
                _nearLanding.y = height;
                Vector3 _targetDirection = _nearLanding - t.position;
                Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, Time.deltaTime * rotationSpeed, 0);
                t.position += t.forward * (altitude + 1f) / 2 * speed * Time.deltaTime;
                t.position = new Vector3(t.position.x, Mathf.Lerp(airportAltitude, height, altitude), t.position.z);
                Vector3 _rotationEuler = aircraftRunwayRotation.eulerAngles;
                _rotationEuler = new Vector3(_rotationEuler.x, _rotationEuler.y + 180, _rotationEuler.z);
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(_newDirection), Time.deltaTime * rotationSpeed);
              //  if (Vector3.Distance(t.position, _nearLanding) < 1 && Quaternion.Angle(t.rotation, Quaternion.Euler(_rotationEuler)) < 100)
                   // aircraftState = AircraftState.nearLanding;
            }
           // if (aircraftState == AircraftState.nearLanding)
           // {
           //     StartCoroutine(Land());
           // }
        }
        IEnumerator Land()
        {
            aircraftState = AircraftState.landing;
            t.DOMove(aircraftRunwayPosition, nearTakeOffSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(aircraftRunwayRotation.eulerAngles + new Vector3(0, 180, 0), 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, aircraftRunwayPosition) / nearTakeOffSpeed);
            t.DOMove(outHangarPosition, nearTakeOffSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(outHangarRotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, outHangarPosition) / nearTakeOffSpeed);
            t.DOMove(nearHangarPosition, onTheGroundSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(nearHangarRotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, nearHangarPosition) / onTheGroundSpeed);
            orders.FinishOrderRpc(false);
            aircraftState = AircraftState.nearHangar;
        }
        IEnumerator TakeOff()
        {
            aircraftState = AircraftState.takeoff;
            if ((airIndex == 1 || airIndex == 2)
                && (airfield.aircraftHangar[0].aircraftForHangar && airfield.aircraftHangar[0].aircraftForHangar.aircraftState < AircraftState.flying
                || airfield.aircraftHangar[3].aircraftForHangar && airfield.aircraftHangar[3].aircraftForHangar.aircraftState < AircraftState.flying))
            {
                if (airIndex == 1)
                    yield return new WaitForSeconds(5.5f);
                else
                    yield return new WaitForSeconds(3);
            }
            t.DOMove(outHangarPosition, onTheGroundSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(outHangarRotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, outHangarPosition) / onTheGroundSpeed);
            t.DORotate(aircraftRunwayRotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(0.5f);
            t.DOMove(aircraftRunwayPosition, nearTakeOffSpeed).SetEase(Ease.Linear).SetSpeedBased();
            yield return new WaitForSeconds(Vector3.Distance(t.position, aircraftRunwayPosition) / nearTakeOffSpeed);
            aircraftState = AircraftState.flying;
        }


        void Flying()
        {
            Vector3? _targetPosition = orders.TargetPosition();
            if (!_targetPosition.HasValue)
                return;
            altitude += Time.deltaTime / 1;
            altitude = Mathf.Clamp(altitude, 0, 1);
            Vector3 _positionWithoutY = new Vector3(_targetPosition.Value.x, Mathf.Lerp(airportAltitude, height, altitude), _targetPosition.Value.z);
            Vector3 _targetDirection = _positionWithoutY - t.position;
            float _singleStep = Time.deltaTime * altitude;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, _singleStep, 0);
            t.rotation = Quaternion.LookRotation(_newDirection);
            t.position += t.forward * (altitude + 1f) / 2 * speed * Time.deltaTime;
            t.position = new Vector3(t.position.x, Mathf.Lerp(airportAltitude, height, altitude), t.position.z);
            t.eulerAngles = new Vector3(0, t.eulerAngles.y, 0);
        }

        void AirplaneTakeoff()
        {
            if (aircraftState == AircraftState.nearHangar && orders.unitOrderQueue.Count != 0)
                StartCoroutine(TakeOff());
        }
    }
}