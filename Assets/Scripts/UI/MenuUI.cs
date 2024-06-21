using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
namespace rts.UI
{
    using rts.Unit;
    using rts.Player;
    public class MenuUI : NetworkBehaviour
    {
        [SerializeField] Button hostButton;
        [SerializeField] Button clientButton;
        [SerializeField] Button[] unitButtons;
        [SerializeField] Button[] unitQueueButtons;
        [SerializeField] TextMeshProUGUI fpsUI;
        [SerializeField] TextMeshProUGUI moneyUI;
        [SerializeField] GameObject unitButtonParent;
        [field: SerializeField] public Transform unitUIParent { get; private set; }
        [field: SerializeField] public Transform minimapUIParent { get; private set; }
        Image[] buttonImages;
        public static MenuUI i;
        public Player player { get; private set; }
        public NetworkVariable<int> map { get; private set; } = new NetworkVariable<int>(0);
        int id;
        int spawnQueueButtons;

        Unit u;
        void Awake()
        {
            if (i)
                Destroy(gameObject);
            i = this;
            buttonImages = new Image[unitButtons.Length];
            for (int _i = 0; _i < unitButtons.Length; _i++)
                buttonImages[_i] = unitButtons[_i].GetComponent<Image>();
            DontDestroyOnLoad(gameObject);
            map.OnValueChanged += OnMapChanged;
            hostButton.onClick.AddListener(() => { i.HostGame(); });
            clientButton.onClick.AddListener(() => { i.JoinGame(); });
            if (GameData.i)
            {
                GameEvents.i.onUnitSelection += SetUnitSelectionUI;
                GameEvents.i.onJoinGame += JoinGameUI;
                GameEvents.i.onMoneyChanged += MoneyChangeUI;
                GameEvents.i.onUpdateIsBuildUI += UpdateIsBuildUI;
            }
            else
                SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void FixedUpdate()
        {
            if (!u || u.unitSettings.spawnSpeedModifier <= 0)
                return;
            if (u.spawnQueue.Count > 0)
                unitQueueButtons[0].GetComponent<Image>().fillAmount = u.SpawnProgress;
            if (u.spawnQueue.Count != spawnQueueButtons)
            {
                foreach (Button _b in unitQueueButtons)
                    _b.gameObject.SetActive(false);
                spawnQueueButtons = u.spawnQueue.Count;
                for (int i = 0; i < spawnQueueButtons; i++)
                {
                    unitQueueButtons[i].gameObject.SetActive(true);
                    unitQueueButtons[i].GetComponent<Image>().sprite = GameData.i.unitSettings[u.spawnQueue[i]].unitImage;
                }
            }
        }
        public void HostGame()
        {
            NetworkManager.Singleton.StartHost();
            map.Value = 1;
        }
        public void JoinGame()
        {
            NetworkManager.Singleton.StartClient();
        }
        void OnMapChanged(int _prev, int _curr)
        {
            SceneManager.LoadScene(_curr);
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Menu" || scene.name == "Entities Scene")
                return;
            if (IsServer)
                Instantiate((GameObject)Resources.Load("Prefabs/GameManager")).GetComponent<NetworkObject>().Spawn();
            GameEvents.i.onUnitSelection += SetUnitSelectionUI;
            GameEvents.i.onJoinGame += JoinGameUI;
            GameEvents.i.onMoneyChanged += MoneyChangeUI;
            GameEvents.i.onUpdateIsBuildUI += UpdateIsBuildUI;
            hostButton.gameObject.SetActive(false);
            clientButton.gameObject.SetActive(false);
            MapSettings.i.cam.gameObject.SetActive(false);
        }
        void MoneyChangeUI(float _amount)
        {
            moneyUI.text = _amount.ToString() + "$";
        }
        void JoinGameUI()
        {
        }
        void SetUnitSelectionUI(int _id)
        {
            u = null;
            id = _id;
            int _localID = Player.localPlayer;
            unitButtonParent.SetActive(GameData.i.IsSelectedBuild(_localID));
            int _i = 0;
            foreach (Button _b in unitButtons)
                _b.gameObject.SetActive(false);
            foreach (Button _b in unitQueueButtons)
                _b.gameObject.SetActive(false);
            if (_id == -1)
                return;
            u = GameData.i.GetUnit(GameData.i.GetPlayer(_localID).selectedUnitList[0]);
            for (int i = 0; i < u.spawnQueue.Count; i++)
            {
                unitQueueButtons[i].gameObject.SetActive(true);
                unitQueueButtons[i].GetComponent<Image>().sprite = GameData.i.unitSettings[u.spawnQueue[i]].unitImage;
            }
            foreach (Settings.UnitButton _uBS in GameData.i.unitSettings[_id].unitButtons)
            {
                List<int> _uList = GameData.i.InsideSelectedUnit(_localID);
                if (_uBS.buttonType == Settings.UnitButton.btype.DismountUnit)
                {
                    unitButtons[_i].gameObject.SetActive((_uList.Count > _i && _uList[_i] != -1));
                    if (_uList[_i] != -1)
                    {
                        buttonImages[_i].sprite = GameData.i.unitSettings[GameData.i.GetUnit(_uList[_i]).unitSettings.type].unitImage;
                    }
                }
                else
                {
                    unitButtons[_i].gameObject.SetActive(_uBS.buttonType != Settings.UnitButton.btype.Empty && ButtonIsDublicatated(_localID, _i));
                    if (_uBS.buttonType == Settings.UnitButton.btype.Spawn || _uBS.buttonType == Settings.UnitButton.btype.Build)
                    {
                        buttonImages[_i].sprite = GameData.i.unitSettings[_uBS.iD].unitImage;
                    }
                }
                _i++;
            }
            OnMoneyChange(-1, player.money.Value);
        }
        public void OnMoneyChange(float _prev, float _curr)
        {
            if (id == -1)
                return;
            Settings.UnitButton[] _uB = GameData.i.unitSettings[id].unitButtons;
            for (int _i = 0; _i < unitButtons.Length; _i++)
            {
                float _cost = 0;
                if (_uB.Length > _i)
                    _cost = GameData.i.unitSettings[_uB[_i].iD].cost;
                if (_cost != 0 && _cost > _curr)
                    buttonImages[_i].color = Color.grey;
                else buttonImages[_i].color = Color.white;
            }
        }
        public void SetPlayer(Player _player)
        {
            player = _player;
        }
        void UpdateIsBuildUI()
        {
            unitButtonParent.SetActive(GameData.i.IsSelectedBuild(Player.localPlayer));
        }
        public void PressUnitButton(int _i)
        {
            GameData.i.GetPlayer(Player.localPlayer).PressUnitButtonRpc(_i);
        }
        public void PressUnitRemoveQueueButton(int _i)
        {
            GameData.i.GetPlayer(Player.localPlayer).PressUnitQueueButtonRpc(_i);
        }
        public bool ButtonIsDublicatated(int _id, int _button)
        {
            List<int> _unitList = GameData.i.GetPlayer(_id).selectedUnitList;
            if (_unitList.Count < 2)
                return true;
            Settings.UnitButton.btype _buttonType = GameData.i.GetUnit(_unitList[0]).unitSettings.unitButtons[_button].buttonType;
            int _spawnID = GameData.i.GetUnit(_unitList[0]).unitSettings.unitButtons[_button].iD;
            for (int _i = 1; _i < _unitList.Count; _i++)
                if (_buttonType != GameData.i.GetUnit(_unitList[_i]).unitSettings.unitButtons[_button].buttonType
                    || _spawnID != GameData.i.GetUnit(_unitList[_i]).unitSettings.unitButtons[_button].iD)
                    return false;
            return true;
        }
        private void Update()
        {
            fpsUI.text = ((int)(1 / Time.deltaTime)).ToString();
        }
    }
}