using Zenject;
using UnityEngine;
using rts.UI;
using rts.GameLogic;

namespace rts.Infrastracture
{
    public class LocationInstaller : MonoInstaller
    {
        [SerializeField] MenuUI menuUI;
        [SerializeField] GameData gameData;

        public override void InstallBindings()
        {
            Container.Bind<MenuUI>().FromInstance(menuUI).AsSingle().NonLazy();
            Container.Bind<GameData>().FromInstance(gameData).AsSingle();
            Container.Bind<GameManager>().FromInstance(gameData.GetComponent<GameManager>()).AsSingle();
        }
    }
}
