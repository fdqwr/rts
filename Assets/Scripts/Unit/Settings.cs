using UnityEngine;
using UnityEngine.AddressableAssets;

namespace rts.Unit
{
    [CreateAssetMenu(fileName = "UnitSettings", menuName = "Scriptable Objects/UnitSettings")]
    public class Settings : ScriptableObject
    {
        [field: SerializeField] public UnitButton[] unitButtons { get; private set; } = new UnitButton[14];
        [field: SerializeField] public AssetReference asset { get; private set; }
        [field: SerializeField] public Sprite unitImage { get; private set; }
        [field: SerializeField] public int occupyPSlots { get; private set; }
        [field: SerializeField] public int id { get; private set; } = 1;
        [field: SerializeField] public float cost { get; private set; } = 1;
        [field: SerializeField] public float maxHealth { get; private set; } = 100;
        [field: SerializeField] public float stoppingDistance { get; private set; } = 1;
        [field: SerializeField] public float buildPointsNeeded { get; private set; } = 100;
        [field: SerializeField] public float constructionOffset { get; private set; } = 0;
        [field: SerializeField] public float spawnSpeedModifier { get; private set; } = 1;
        [field: SerializeField] public bool rotateVisualToTarget { get; private set; }
        [field: SerializeField] public bool animDeath { get; private set; }
        [field: SerializeField] public UnitType unitType { get; private set; }
        [field: SerializeField] public Vector3 wheelsAxis { get; private set; } = new Vector3(0, 0, 1);
        [field: SerializeField] public Vector3 size { get; private set; } = new Vector3(1, 1, 1);
        [field: SerializeField] public int[] spawnOnBuild { get; private set; }
        public bool IsBuilding => unitType == UnitType.commandCenter || unitType == UnitType.barracks
            || unitType == UnitType.factory || unitType == UnitType.airfield || unitType == UnitType.supplyCenter || unitType == UnitType.defence;
        public bool IsVehicle => unitType == UnitType.vehicle || unitType == UnitType.builder || unitType == UnitType.truck;
        public bool IsInfantry => unitType == UnitType.aT || unitType == UnitType.rifleman;
        public bool IsAircraft => unitType == UnitType.airplane || unitType == UnitType.helicopter;
        public enum UnitType
        {
            commandCenter,
            factory,
            barracks,
            airfield,
            supplyCenter,
            defence,
            rifleman,
            aT,
            vehicle,
            builder,
            truck,
            airplane,
            helicopter,
            supplyStockpile
        }
        [System.Serializable]
        public struct UnitButton
        {
            public enum btype
            {
                Empty,
                Spawn,
                Build,
                DismountAllUnits,
                DismountUnit,
                Cancel
            }
            public btype buttonType;
            public int iD;
        }
    }
}