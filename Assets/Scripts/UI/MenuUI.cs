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
    using Zenject;
    using rts.GameLogic;
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
        Image[] queueImages;
        public Player player { get; private set; }
        public NetworkVariable<int> map { get; private set; } = new NetworkVariable<int>(-1);
        int id;
        int spawnQueueButtons;
        Camera cam;
        Unit firstSelectedUnit;
        GameData gameData;
        GameManager gameManager;
        [Inject]
        public void Construct(GameManager _gameManager, GameData _gameData)
        {
            gameManager = _gameManager;
            gameData = _gameData;
        }
        void Awake()
        {
            buttonImages = new Image[unitButtons.Length];
            for (int _i = 0; _i < unitButtons.Length; _i++)
                buttonImages[_i] = unitButtons[_i].GetComponent<Image>();
            queueImages = new Image[unitQueueButtons.Length];
            for (int _i = 0; _i < unitQueueButtons.Length; _i++)
                queueImages[_i] = unitQueueButtons[_i].GetComponent<Image>();
            map.OnValueChanged += OnMapChanged;
            hostButton.onClick.AddListener(() => { HostGame(); });
            clientButton.onClick.AddListener(() => { JoinGame(); });
            cam = Camera.main;
        }
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        private void Update()
        {
            fpsUI.text = ((int)(1 / Time.deltaTime)).ToString();
            if (Input.GetKeyDown(KeyCode.V))
            {
                NetworkManager.Singleton.Shutdown();
                map.Value = -1;
            }
        }
        private void FixedUpdate()
        {
            if (!firstSelectedUnit || firstSelectedUnit.settings.spawnSpeedModifier <= 0)
                return;
            if (firstSelectedUnit.spawnQueue.Count > 0)
                queueImages[0].fillAmount = firstSelectedUnit.SpawnProgressPercent;
            if (firstSelectedUnit.spawnQueue.Count != spawnQueueButtons)
            {
                foreach (Button _b in unitQueueButtons)
                    _b.gameObject.SetActive(false);
                spawnQueueButtons = firstSelectedUnit.spawnQueue.Count;
                for (int _i = 0; _i < spawnQueueButtons; _i++)
                {
                    unitQueueButtons[_i].gameObject.SetActive(true);
                    queueImages[_i].sprite = gameData.unitSettings[firstSelectedUnit.spawnQueue[_i]].unitImage;
                }
            }
        }

        public void HostGame()
        {
            NetworkManager.Singleton.StartHost();
            map.Value = 0;
        }

        public void JoinGame()
        {
            NetworkManager.Singleton.StartClient();
        }

        void OnMapChanged(int _prev, int _curr)
        {
            hostButton.gameObject.SetActive(_curr == -1);
            clientButton.gameObject.SetActive(_curr == -1);
            cam.gameObject.SetActive(_curr == -1);
            if (_curr > -1)
                SceneManager.LoadScene(gameManager.mapSettings.maps[_curr].scene, LoadSceneMode.Additive);
            else
            {
                gameData.ResetData();
                SceneManager.UnloadSceneAsync(gameManager.mapSettings.maps[_prev].scene);
                foreach (UnitUI _unitUI in GetComponentsInChildren<UnitUI>())
                    _unitUI.SelfDestruct();
            }
        }

        private void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
        {
            if (_scene.name == "Menu" || _scene.name == "Entities Scene")
                return;
            gameManager.OnSpawn();
        }

        public void UpdateUnitSelectionUI(int _id)
        {
            firstSelectedUnit = null;
            id = _id;
            int _localID = Player.localPlayerID;
            unitButtonParent.SetActive(gameData.IsSelectedBuild(_localID));
            int _i = 0;
            foreach (Button _b in unitButtons)
                _b.gameObject.SetActive(false);
            foreach (Button _b in unitQueueButtons)
                _b.gameObject.SetActive(false);
            if (_id == -1)
                return;
            firstSelectedUnit = gameData.GetUnit(gameData.GetPlayer(_localID).selectedUnitList[0]);
            for (int i = 0; i < firstSelectedUnit.spawnQueue.Count; i++)
            {
                unitQueueButtons[i].gameObject.SetActive(true);
                unitQueueButtons[i].GetComponent<Image>().sprite = gameData.unitSettings[firstSelectedUnit.spawnQueue[i]].unitImage;
            }
            foreach (Settings.UnitButton _uBS in gameData.unitSettings[_id].unitButtons)
            {
                List<int> _unitList = gameData.InsideSelectedUnit(_localID);
                if (_uBS.buttonType == Settings.UnitButton.btype.DismountUnit)
                {
                    unitButtons[_i].gameObject.SetActive((_unitList.Count > _i && _unitList[_i] != -1));
                    if (_unitList[_i] != -1)
                    {
                        buttonImages[_i].sprite = gameData.unitSettings[gameData.GetUnit(_unitList[_i]).settings.id].unitImage;
                    }
                }
                else
                {
                    unitButtons[_i].gameObject.SetActive(_uBS.buttonType != Settings.UnitButton.btype.Empty && gameManager.ButtonIsDublicatated(_localID, _i));
                    if (_uBS.buttonType == Settings.UnitButton.btype.Spawn || _uBS.buttonType == Settings.UnitButton.btype.Build)
                    {
                        buttonImages[_i].sprite = gameData.unitSettings[_uBS.iD].unitImage;
                    }
                }
                _i++;
            }
            OnMoneyChange(-1, player.money.Value);
        }

        public void OnMoneyChange(float _prev, float _curr)
        {
            moneyUI.text = _curr.ToString() + "$";
            if (id == -1)
                return;
            Settings.UnitButton[] _uB = gameData.unitSettings[id].unitButtons;
            for (int _i = 0; _i < unitButtons.Length; _i++)
            {
                float _cost = 0;
                if (_uB.Length > _i)
                    _cost = gameData.unitSettings[_uB[_i].iD].cost;
                if (_cost != 0 && _cost > _curr)
                    buttonImages[_i].color = Color.grey;
                else buttonImages[_i].color = Color.white;
            }
        }

        public void SetPlayer(Player _player)
        {
            player = _player;
        }

        public void UpdateIsBuildUI()
        {
            unitButtonParent.SetActive(gameData.IsSelectedBuild(Player.localPlayerID));
        }

        public void PressUnitButton(int _i)
        {
            Player.localPlayerClass.PressUnitButtonRpc(_i);
        }

        public void PressUnitRemoveQueueButton(int _i)
        {
            Player.localPlayerClass.PressUnitQueueButtonRpc(_i);
        }
    }
}