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
        [field: SerializeField] public int type { get; private set; } = 1;
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
        public enum UnitType
        {
            building,
            infantry,
            vehicle,
            builder,
            truck,
            aircraft,
            helicopter
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