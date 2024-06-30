using UnityEngine;
namespace rts.Unit
{
    public class Aircraft : MonoBehaviour
    {
        [field: SerializeField] protected float speed { get; private set; } = 10;
        [field: SerializeField] protected float rotationSpeed { get; private set; }
        [field: SerializeField] public float onTheGroundSpeed { get; private set; } = 3;
        [field: SerializeField] protected float nearTakeOffSpeed { get; private set; } = 7;
        protected bool unitsToDrop { get; private set; } = false;
        protected float height { get; private set; } = 20;
        protected float airportAltitude { get; private set; } = 5;
        public AircraftState aircraftState { get; protected set; } = AircraftState.nearHangar;
        public int airIndex { get; private set; }
        public Airfield airfield { get; protected set; }
        public float altitude { get; protected set; } = 0;
        public Unit unit { get; private set; }
        public Vector3 prevPosition { get; private set; }
        public Transform t { get; private set; }
        public Orders orders { get; private set; }
        protected Vector3 nearHangarPosition { get; private set; }
        protected Vector3 aircraftRunwayPosition { get; private set; }
        protected Vector3 outHangarPosition { get; private set; }
        protected Quaternion nearHangarRotation { get; private set; }
        protected Quaternion aircraftRunwayRotation { get; private set; }
        protected Quaternion outHangarRotation { get; private set; }
        public enum AircraftState
        {
            nearHangar,
            takeoff,
            flying,
            landing,
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
        }


        public void AddToDrop(bool _drop)
        {
            unitsToDrop = _drop;
        }
        public void SetAirfield(int _i, Airfield _airfield)
        {
            airIndex = _i;
            airfield = _airfield;
            airportAltitude = airfield.aircraftHangar[_i].nearHangarPosition.position.y;
            outHangarPosition = airfield.aircraftHangar[airIndex].outHangarPosition.position;
            nearHangarPosition = airfield.aircraftHangar[airIndex].nearHangarPosition.position;
            aircraftRunwayPosition = airfield.aircraftHangar[airIndex].aircraftRunwayPosition.position;
            outHangarRotation = airfield.aircraftHangar[airIndex].outHangarPosition.rotation;
            nearHangarRotation = airfield.aircraftHangar[airIndex].nearHangarPosition.rotation;
            aircraftRunwayRotation = airfield.aircraftHangar[airIndex].aircraftRunwayPosition.rotation;
        }
    }
}