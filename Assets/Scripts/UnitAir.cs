using UnityEngine;
public class UnitAir : MonoBehaviour
{
    [SerializeField] Transform[] hangarPositions;
    [SerializeField] Transform[] aircraftRunwayPositions;
    [SerializeField] Transform[] outHangarPositions;
    [SerializeField] Transform[] helicopterBlade;
    [SerializeField] float speed = 10;
    UnitAir[] aircraftForHangars;
    bool unitsToDrop = false;
    float height = 20;
    float airportAltitude = 5;
    public  AircraftState aircraftState { get; private set; } = AircraftState.inHangar;
    int airIndex;
    public UnitAir airport { get; private set; }
    [HideInInspector] public Vector3 flyDestination;
    public float altitude { get; private set; } = 0;
    public Unit unit;
    public Vector3 prevPosition { get; private set; }
    Transform t;
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
        if (unit.UnitType == UnitSettings.UnitType.aircraft || unit.UnitType == UnitSettings.UnitType.helicopter) 
            flyDestination = t.position;
        aircraftForHangars = new UnitAir[hangarPositions.Length];
    }

    void Update()
    {
        prevPosition = t.position;
        if (unit.UnitType == UnitSettings.UnitType.helicopter)
            foreach (Transform _t in helicopterBlade)
                _t.Rotate(0, 0, 1000 * Time.deltaTime);
        if (!unit.IsServer || unit.UnitType == UnitSettings.UnitType.building)
            return;
        if (unit.UnitType != UnitSettings.UnitType.helicopter && unit.unitTargetClass && unit.team.Value == unit.unitTargetClass.team.Value && unit.unitTargetClass.unitAir && aircraftState == AircraftState.flying &&
            unit.unitTargetClass.UnitType == UnitSettings.UnitType.building && !unit.attackTarget && (!airport || airport.unit != unit.unitTargetClass))
        {
            unit.unitTargetClass.unitAir.SetupAirUnit(this);
            unit.SetTargetRpc(airport.unit.id.Value, false);
        }
        if (unit.UnitType == UnitSettings.UnitType.helicopter)
        {
            AircraftFlyingHover();
            return;
        }
        if (airport && airport.unit.destroyed)
        {
            if (aircraftState < AircraftState.takeoff || aircraftState > AircraftState.nearLanding)
                unit.DieRpc(false);
            else airport = null;
        }
        if (hangarPositions.Length > 0 && (aircraftState == AircraftState.inHangar && unit.unitTargetClass && unit.unitTargetClass.unitAir == airport) ||
            aircraftState == AircraftState.inHangar && unit.unitTargetClass == null && flyDestination == t.position && airport &&
            t.rotation != airport.hangarPositions[airIndex].rotation)
               t.rotation = Quaternion.RotateTowards(t.rotation, airport.hangarPositions[airIndex].rotation, Time.deltaTime * 100);
        else if (aircraftState >= AircraftState.flying)
        {
            if ((unit.unitTargetClass && unit.unitTargetClass.unitAir == airport) || aircraftState >= AircraftState.nearLanding)
                AircraftLanding();
            else AircraftFlying();
        }
        else if ((unit.unitTargetID > -1 || flyDestination != t.position))
            AircraftTakeoff();
    }
    void AircraftLanding()
    {
        if (aircraftState < AircraftState.nearLanding)
        {
            altitude += Time.deltaTime / 1;
            altitude = Mathf.Clamp(altitude, 0, 1);
            Vector3 _nearLanding = airport.aircraftRunwayPositions[airIndex].position + airport.aircraftRunwayPositions[airIndex].forward * 50;
            _nearLanding.y = height;
            Vector3 _targetDirection = _nearLanding - t.position;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, Time.deltaTime * 30, 0.0f);
            t.position += t.forward * (altitude + 1f) / 2 * speed * Time.deltaTime;
            t.position = new Vector3(t.position.x, Mathf.Lerp(airportAltitude, height, altitude), t.position.z);
            Vector3 _rotationEuler = airport.aircraftRunwayPositions[airIndex].rotation.eulerAngles;
            _rotationEuler = new Vector3(_rotationEuler.x, _rotationEuler.y + 180, _rotationEuler.z);
            t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(_newDirection),Time.deltaTime * 100);
            if (Vector3.Distance(t.position, _nearLanding) < 2 && Quaternion.Angle(t.rotation, Quaternion.Euler(_rotationEuler)) < 150)
                aircraftState = AircraftState.nearLanding;
        }
        else if (aircraftState == AircraftState.nearLanding)
        {
            Vector3 _targetDirection = airport.aircraftRunwayPositions[airIndex].position - t.position;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, Time.deltaTime, 0.0f);
            t.rotation = Quaternion.LookRotation(_newDirection);
            t.position = Vector3.MoveTowards(t.position, airport.aircraftRunwayPositions[airIndex].position, 30 * Time.deltaTime);
            Vector3 _nearTakeoffPosition = airport.aircraftRunwayPositions[airIndex].position+ airport.aircraftRunwayPositions[airIndex].forward * 50;
            _nearTakeoffPosition.y = height;
            float _ld = Vector3.Distance(_nearTakeoffPosition, airport.aircraftRunwayPositions[airIndex].position);
            altitude = Vector3.Distance(t.position, airport.aircraftRunwayPositions[airIndex].position) / _ld;
            t.position = new Vector3(t.position.x, Mathf.Lerp(airportAltitude, height, altitude), t.position.z);
            t.eulerAngles = new Vector3(0, t.eulerAngles.y, 0);
            if (Vector3.Distance(t.position, airport.aircraftRunwayPositions[airIndex].position) < 0.2)
                aircraftState = AircraftState.landing;
        }
        else if (aircraftState == AircraftState.landing)
        {
            t.position = Vector3.MoveTowards(t.position, airport.outHangarPositions[airIndex].position, 10 * Time.deltaTime);
            if (Vector3.Distance(t.position, airport.outHangarPositions[airIndex].position) < 0.2)
                aircraftState = AircraftState.nearHangarBack;
        }
        else if (aircraftState == AircraftState.nearHangarBack)
        {
            Vector3 _targetDirection = airport.hangarPositions[airIndex].position - t.position;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, Time.deltaTime * 7, 0.0f);
            t.rotation = Quaternion.LookRotation(_newDirection);
            t.position = Vector3.MoveTowards(t.position, airport.hangarPositions[airIndex].position, 5 * Time.deltaTime);
            if (Vector3.Distance(t.position, airport.hangarPositions[airIndex].position) < 0.2)
            {
                flyDestination = t.position;
                unit.SetTargetRpc(-1, false);
                aircraftState = AircraftState.inHangar;
            }
        }
    }
    void AircraftFlyingHover()
    {
        float _altitude = airportAltitude;
        if (unit.unitCarrier && unit.unitTargetClass && unit.unitTargetClass.OccupyPSlots > 0 && unit.unitTargetClass.insideUnitID.Value < 0)
        {
            Vector3 _targetPos = unit.unitTargetClass.transform.position;
            _targetPos.y = t.position.y;
            if (Vector3.Distance(_targetPos, transform.position) < 3)
            {
                _altitude = unit.unitTargetClass.transform.position.y;
                altitude -= Time.deltaTime;
                if (t.position.y - unit.unitTargetClass.transform.position.y < 1)
                {
                    unit.unitCarrier.GetInsideUnit(unit.unitTargetClass);
                    unit.SetTargetPosRpc(t.position);
                }
            }
        }
        else if(unitsToDrop)
        {
            altitude -= Time.deltaTime;
            _altitude = unit.targetPos.y;
        }
        else
            altitude += Time.deltaTime / 1;
        altitude = Mathf.Clamp(altitude, 0, 1);
        flyDestination.y = Mathf.Lerp(airportAltitude, height, altitude);
        Vector3 _targetDirection = flyDestination - t.position;
        float _singleStep = Time.deltaTime * altitude;
        Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, _singleStep, 0.0f);
        t.rotation = Quaternion.LookRotation(_newDirection);
        t.position = Vector3.MoveTowards(t.position, flyDestination, speed * Time.deltaTime * altitude);
        t.position = new Vector3(t.position.x, Mathf.Lerp(_altitude, height, altitude), t.position.z);
        t.eulerAngles = new Vector3(0, t.eulerAngles.y, 0);
    }
    void AircraftFlying()
    {
        altitude += Time.deltaTime / 1;
        altitude = Mathf.Clamp(altitude, 0, 1);
        flyDestination.y = Mathf.Lerp(airportAltitude, height, altitude);
        Vector3 _targetDirection = flyDestination - t.position;
        float _singleStep = Time.deltaTime * altitude;
        Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, _singleStep, 0.0f);
        t.rotation = Quaternion.LookRotation(_newDirection);
        t.position += t.forward * (altitude + 1f) / 2 * speed * Time.deltaTime;
        t.position = new Vector3(t.position.x, Mathf.Lerp(airportAltitude, height, altitude), t.position.z);
        t.eulerAngles = new Vector3(0, t.eulerAngles.y, 0);
    }
    void AircraftTakeoff()
    {
        if (aircraftState == AircraftState.inHangar)
        {
            if (t.rotation != airport.hangarPositions[airIndex].rotation)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, airport.hangarPositions[airIndex].rotation, Time.deltaTime * 100);
                return;
            }
            Vector3 _targetDirection = airport.outHangarPositions[airIndex].position - t.position;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, Time.deltaTime * 2, 0.0f);
            t.rotation = Quaternion.LookRotation(_newDirection);
            t.position = Vector3.MoveTowards(t.position, airport.outHangarPositions[airIndex].position, 5 * Time.deltaTime);
            if (Vector3.Distance(t.position, airport.outHangarPositions[airIndex].position) < 0.2)
                aircraftState = AircraftState.nearHangar;
        }
        else
        {
            Vector3 _targetDirection = airport.aircraftRunwayPositions[airIndex].position - t.position;
            Vector3 _newDirection = Vector3.RotateTowards(t.forward, _targetDirection, Time.deltaTime * 2, 0.0f);
            t.rotation = Quaternion.LookRotation(_newDirection);
            if (Vector3.Angle(_targetDirection, t.forward) < 3)
            {
                t.position = Vector3.MoveTowards(t.position, airport.aircraftRunwayPositions[airIndex].position, 15 * Time.deltaTime);
                if (Vector3.Distance(t.position, airport.aircraftRunwayPositions[airIndex].position) < 0.2)
                    aircraftState = AircraftState.flying;
            }
        }
    }
    public void AddToDrop(bool _drop)
    {
        unitsToDrop = _drop;
    }
    public void SetupAirUnit(UnitAir _u)
    {
        if (_u.unit.UnitType == UnitSettings.UnitType.building) return;
        for (int i = 0; i < hangarPositions.Length; i++)
            if (aircraftForHangars[i] == null)
            {
                if (_u.airport)
                    _u.airport.aircraftForHangars[airIndex] = null;
                else _u.transform.SetPositionAndRotation(hangarPositions[i].position, hangarPositions[i].rotation);
                aircraftForHangars[i] = _u;
                _u.airIndex = i;
                _u.airport = this;
                _u.airportAltitude = hangarPositions[i].position.y;
                break;
            }
    }
    public int FreeHangars()
    {
        int _i = 0;
        foreach (UnitAir _u in aircraftForHangars)
            if (_u)
                _i++;
        return hangarPositions.Length-_i;
    }
}
