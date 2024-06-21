using System;
using UnityEngine;

public class GameEvents : MonoBehaviour
{
    public static GameEvents i;

    void Awake()
    {
        i = this;
    }
    public event Action<int> onUnitSelection;
    public event Action<float> onMoneyChanged;
    public event Action onJoinGame;
    public event Action onUpdateIsBuildUI;
    public void UnitSelection(int _id)
    {
        if(onUnitSelection != null)
            onUnitSelection(_id);
    }
    public void JoinGame()
    {
        if (onJoinGame != null)
            onJoinGame();
    }
    public void UpdateIsBuildUI()
    {
        if (onUpdateIsBuildUI != null)
            onUpdateIsBuildUI();
    }
    public void ChangeMoney(float _amount)
    {
        if(onMoneyChanged!=null)
            onMoneyChanged(_amount);
    }
}
