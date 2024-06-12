using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "UnitSettings", menuName = "Scriptable Objects/UnitSettings")]
public class UnitSettings : ScriptableObject
{
    public AssetReference asset;
    public UnitButton uB;
    public Sprite unitImage;
    public int occupyPSlots;
    public int type = 1;
    public float cost = 1;
    public float maxHealth = 100;
    public float stoppingDistance = 1;
    public float buildPointsNeeded = 100;
    public float whenBuildOffset = 3;
    public float spawnSpeed = 3;
    public bool rotateVisualToTarget;
    public bool animDeath;
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
    public UnitType unitType;
    public Vector3 wheelsAxis = new Vector3(0, 0, 1);
    public Vector3 size = new Vector3(1, 1, 1);
    public int[] spawnOnBuild;
}
