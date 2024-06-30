using DG.Tweening;
using UnityEngine;

namespace rts.Unit
{
    public class Airfield : MonoBehaviour
    {
        [field: SerializeField] public AirfieldHangar[] aircraftHangar { get; private set; }
        public Unit unit { get; private set; }
        [System.Serializable]
        public struct AirfieldHangar
        {
            public Transform hangarDoor;
            public Transform inHangarPosition;
            public Transform nearHangarPosition;
            public Transform aircraftRunwayPosition;
            public Transform outHangarPosition;
            [HideInInspector] public Aircraft aircraftForHangar;
        }
        private void Awake()
        {
            unit = GetComponent<Unit>();
        }
        public void SetupAirUnit(Aircraft _u)
        {
            if (_u.unit.settings.unitType != Settings.UnitType.airplane)
                return;
            for (int _i = 0; _i < aircraftHangar.Length; _i++)
            {
                if (aircraftHangar[_i].aircraftForHangar != null)
                    continue;
                aircraftHangar[_i].hangarDoor.DOMove(aircraftHangar[_i].hangarDoor.position + Vector3.down * 6, 1);
                if (_u.airfield)
                    _u.airfield.RemoveFromHangar(_u.airIndex);
                else
                    _u.transform.SetPositionAndRotation(aircraftHangar[_i].inHangarPosition.position, aircraftHangar[_i].inHangarPosition.rotation);
                _u.SetAirfield(_i, this);
                aircraftHangar[_i].aircraftForHangar = _u;
                aircraftHangar[_i].hangarDoor.DOMove(aircraftHangar[_i].hangarDoor.position + Vector3.down * 6, 1);
                _u.transform.DOMove(aircraftHangar[_i].nearHangarPosition.position, _u.onTheGroundSpeed).SetDelay(1).SetEase(Ease.Linear).SetSpeedBased();
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
