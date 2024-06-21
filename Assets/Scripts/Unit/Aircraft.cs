using DG.Tweening;
using System.Collections;
using System.Net.Security;
using UnityEngine;
namespace rts.Unit
{
    public class Aircraft : MonoBehaviour
    {
        [SerializeField] AircraftHangar[] aircraftHangar;
        [SerializeField] Transform[] helicopterBlade;
        [SerializeField] float speed = 10;
        [SerializeField] float onTheGroundSpeed = 3;
        [SerializeField] float nearTakeOffSpeed = 7;
        [SerializeField] float rotationSpeed;
        bool unitsToDrop = false;
        float height = 20;
        float airportAltitude = 5;
        public AircraftState aircraftState { get; private set; } = AircraftState.inHangar;
        int airIndex;
        public Aircraft airport { get; private set; }
        public float altitude { get; private set; } = 0;
        public Unit unit { get; private set; }
        public Vector3 prevPosition { get; private set; }
        Transform t;
        Orders orders;
        [System.Serializable]
        public struct AircraftHangar
        {
            public Transform hangarDoor;
            public Transform inHangarPosition;
            public Transform nearHangarPosition;
            public Transform aircraftRunwayPosition;
            public Transform outHangarPosition;
            [HideInInspector] public Aircraft aircraftForHangar;
        }
        public enum AircraftState
        {
            inHangar,
            nearHangar,
            takeoff,
            flying,
            nearLanding,
            landing,
            nearHangarBack
        }
        void Start()
        {
            t = transform;
            unit = GetComponent<Unit>();
            orders = GetComponent<Orders>();
        }

        void Update()
        {
            prevPosition = t.position;
            if (unit.unitSettings.unitType == Settings.UnitType.helicopter)
                foreach (Transform _t in helicopterBlade)
                    _t.Rotate(0, 0, 1000 * Time.deltaTime);
            if (!unit.IsServer || unit.unitSettings.unitType == Settings.UnitType.building)
                return;
            if (unit.unitSettings.unitType == Settings.UnitType.aircraft
                && orders.unitTargetClass
                && unit.team.Value == orders.unitTargetClass.team.Value
                && unit.orders.unitTargetClass.unitAir
                && aircraftState == AircraftState.flying
                && unit.orders.unitTargetClass.unitSettings.unitType == Settings.UnitType.building
                && !orders.isAttackingTarget
                && (!airport || airport.unit != orders.unitTargetClass))
            {
                orders.unitTargetClass.unitAir.SetupAirUnit(this);
                orders.SetTargetRpc(airport.unit.id.Value, false, -1);
            }
            if (unit.unitSettings.unitType == Settings.UnitType.helicopter)
            {
                AircraftFlyingHover();
                return;
            }
            if (airport && airport.unit.isDestroyed)
            {
                if (aircraftState < AircraftState.takeoff || aircraftState > AircraftState.nearLanding)
                    unit.DieRpc(false);
                else airport = null;
            }
            else if (aircraftState >= AircraftState.flying)
            {
                if ((orders.unitTargetClass && orders.unitTargetClass.unitAir == airport) || aircraftState >= AircraftState.nearLanding)
                    AircraftLanding();
                else AircraftFlying();
            }
            else if ((orders.unitTargetID > -1 || orders.targetPosition != t.position))
                AircraftTakeoff();
        }
        void AircraftLanding()
        {
            if (aircraftState < AircraftState.nearLanding)
            {
                altitude += Time.deltaTime / 1;
                altitude = Mathf.Clamp(altitude, 0, 1);
                Vector3 _nearLanding = airport.aircraftHangar[airIndex].aircraftRunwayPosition.position + airport.aircraftHangar[airIndex].aircraftRunwayPosition.forward * 50;
                _nearLanding.y = height;
                Vector3 _targetDirection = _nearLanding - t.position;
                Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, Time.deltaTime * rotationSpeed, 0);
                t.position += t.forward * (altitude + 1f) / 2 * speed * Time.deltaTime;
                t.position = new Vector3(t.position.x, Mathf.Lerp(airportAltitude, height, altitude), t.position.z);
                Vector3 _rotationEuler = airport.aircraftHangar[airIndex].aircraftRunwayPosition.rotation.eulerAngles;
                _rotationEuler = new Vector3(_rotationEuler.x, _rotationEuler.y + 180, _rotationEuler.z);
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(_newDirection), Time.deltaTime * rotationSpeed);
                if (Vector3.Distance(t.position, _nearLanding) < 1 && Quaternion.Angle(t.rotation, Quaternion.Euler(_rotationEuler)) < 100)
                    aircraftState = AircraftState.nearLanding;
            }
            else if (aircraftState == AircraftState.nearLanding)
            {
                StartCoroutine(Land());
            }
        }
        IEnumerator Land()
        {
            aircraftState = AircraftState.landing;
            t.DOMove(airport.aircraftHangar[airIndex].aircraftRunwayPosition.position, nearTakeOffSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(airport.aircraftHangar[airIndex].aircraftRunwayPosition.rotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, airport.aircraftHangar[airIndex].aircraftRunwayPosition.position) / nearTakeOffSpeed);
            t.DOMove(airport.aircraftHangar[airIndex].outHangarPosition.position, nearTakeOffSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(airport.aircraftHangar[airIndex].outHangarPosition.rotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, airport.aircraftHangar[airIndex].outHangarPosition.position) / nearTakeOffSpeed);
            t.DOMove(airport.aircraftHangar[airIndex].nearHangarPosition.position, onTheGroundSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(airport.aircraftHangar[airIndex].nearHangarPosition.rotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, airport.aircraftHangar[airIndex].nearHangarPosition.position) / onTheGroundSpeed);
            orders.FinishOrderRpc(false);
            aircraftState = AircraftState.inHangar;
        }
        IEnumerator TakeOff()
        {
            aircraftState = AircraftState.nearHangar;
            aircraftState = AircraftState.landing;
            t.DOMove(airport.aircraftHangar[airIndex].outHangarPosition.position, onTheGroundSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(airport.aircraftHangar[airIndex].outHangarPosition.rotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, airport.aircraftHangar[airIndex].outHangarPosition.position) /onTheGroundSpeed);
            t.DOMove(airport.aircraftHangar[airIndex].aircraftRunwayPosition.position, nearTakeOffSpeed).SetEase(Ease.Linear).SetSpeedBased();
            t.DORotate(airport.aircraftHangar[airIndex].aircraftRunwayPosition.rotation.eulerAngles, 0.5f);
            yield return new WaitForSeconds(Vector3.Distance(t.position, airport.aircraftHangar[airIndex].aircraftRunwayPosition.position) / nearTakeOffSpeed);
            aircraftState = AircraftState.flying;
        }
        void AircraftFlyingHover()
        {
            float _altitude = airportAltitude;
            if (unit.unitCarrier && orders.unitTargetClass && orders.unitTargetClass.unitSettings.occupyPSlots > 0 && orders.unitTargetClass.insideUnitID.Value < 0)
            {
                Vector3 _targetPos = orders.unitTargetClass.transform.position;
                _targetPos.y = t.position.y;
                if (Vector3.Distance(_targetPos, transform.position) < 3)
                {
                    _altitude = orders.unitTargetClass.transform.position.y;
                    altitude -= Time.deltaTime;
                    if (t.position.y - orders.unitTargetClass.transform.position.y < 1)
                    {
                        unit.unitCarrier.GetInsideUnit(orders.unitTargetClass);
                        orders.FinishOrderRpc(false);
                    }
                }
            }
            else if (unitsToDrop)
            {
                altitude -= Time.deltaTime;
                _altitude = orders.targetPosition.Value.y;
            }
            else
                altitude += Time.deltaTime / 1;
            altitude = Mathf.Clamp(altitude, 0, 1);
            Vector3 _positionWithoutY = new Vector3(orders.targetPosition.Value.x, Mathf.Lerp(airportAltitude, height, altitude), orders.targetPosition.Value.z);
            Vector3 _targetDirection = _positionWithoutY - t.position;
            float _singleStep = Time.deltaTime * altitude;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, _singleStep, 0);
            t.rotation = Quaternion.LookRotation(_newDirection);
            t.position = Vector3.MoveTowards(t.position, _positionWithoutY, speed * Time.deltaTime * altitude);
            t.position = new Vector3(t.position.x, Mathf.Lerp(_altitude, height, altitude), t.position.z);
            t.eulerAngles = new Vector3(0, t.eulerAngles.y, 0);
        }
        void AircraftFlying()
        {
            altitude += Time.deltaTime / 1;
            altitude = Mathf.Clamp(altitude, 0, 1);
            Vector3 _positionWithoutY = new Vector3(orders.targetPosition.Value.x, Mathf.Lerp(airportAltitude, height, altitude), orders.targetPosition.Value.z);
            Vector3 _targetDirection = _positionWithoutY - t.position;
            float _singleStep = Time.deltaTime * altitude;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, _singleStep, 0);
            t.rotation = Quaternion.LookRotation(_newDirection);
            t.position += t.forward * (altitude + 1f) / 2 * speed * Time.deltaTime;
            t.position = new Vector3(t.position.x, Mathf.Lerp(airportAltitude, height, altitude), t.position.z);
            t.eulerAngles = new Vector3(0, t.eulerAngles.y, 0);
        }
        void AircraftTakeoff()
        {
            if (aircraftState == AircraftState.inHangar && orders.unitOrderQueue.Count != 0)
                StartCoroutine(TakeOff());
        }
        public void AddToDrop(bool _drop)
        {
            unitsToDrop = _drop;
        }
        public void SetupAirUnit(Aircraft _u)
        {
            if (_u.unit.unitSettings.unitType == Settings.UnitType.building)
                return;
            for (int _i = 0; _i < aircraftHangar.Length; _i++)
            {
                if (aircraftHangar[_i].aircraftForHangar != null)
                    continue;
                aircraftHangar[_i].hangarDoor.DOMove(aircraftHangar[_i].hangarDoor.position + Vector3.down * 6, 1);
                if (_u.airport)
                    _u.airport.RemoveFromHangar(_u.airIndex);
                else
                    _u.transform.SetPositionAndRotation(aircraftHangar[_i].inHangarPosition.position, aircraftHangar[_i].inHangarPosition.rotation);
                aircraftHangar[_i].aircraftForHangar = _u;
                _u.airIndex = _i;
                _u.airport = this;
                _u.airportAltitude = aircraftHangar[_i].nearHangarPosition.position.y;
                aircraftHangar[_i].hangarDoor.DOMove(aircraftHangar[_i].hangarDoor.position + Vector3.down * 6, 1);
                _u.transform.DOMove(aircraftHangar[_i].nearHangarPosition.position, onTheGroundSpeed).SetDelay(1).SetEase(Ease.Linear).SetSpeedBased();
                break;
            }
        }
        public void RemoveFromHangar(int _index)
        {
            aircraftHangar[_index].aircraftForHangar = null;
            aircraftHangar[_index].hangarDoor.DOMove(aircraftHangar[_index].hangarDoor.position + Vector3.up * 6, 1);
        }
        public int FreeHangarsLeft()
        {
            int _i = 0;
            for (int __i = 0; __i < aircraftHangar.Length; __i++)
                if (aircraftHangar[__i].aircraftForHangar != null)
                    _i++;
            return aircraftHangar.Length - _i;
        }
    }
}