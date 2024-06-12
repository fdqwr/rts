using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitButton", menuName = "Scriptable Objects/UnitButton")]
[Serializable]
public class UnitButton : ScriptableObject
{
    [System.Serializable]
    public struct UnitButtonSettings
    {
        public enum btype
        {
            None,
            Spawn,
            Build,
            InsideUnit,
            Cancel,
            Empty
        }
        public btype buttonType;
        public int spawnID;
        public string name;
        public float time;
        public Texture buttonTexture;
    }
    public UnitButtonSettings[] unitButtons;
}
