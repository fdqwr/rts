using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
public class MenuUI : NetworkBehaviour
{
    [SerializeField] Button hostButton;
    [SerializeField] Button clientButton;
    [SerializeField] Button[] unitButtons;
    [SerializeField] Button[] unitQueueButtons;
    [SerializeField] TextMeshProUGUI fpsUI;
    [SerializeField] TextMeshProUGUI moneyUI;
    [SerializeField] GameObject unitButtonParent;
    [SerializeField] Transform unitUIParent;
    [SerializeField] Transform minimapUIParent;
    public static MenuUI i;
    public Player player { get; private set; }
    public NetworkVariable<int> map { get; private set; } = new NetworkVariable<int>(0);
    int id;
    int spawnQueueButtons;
    public Transform UnitUIParent() => unitUIParent;
    public Transform MinimapUIParent() => minimapUIParent;

    Unit u;
    void Awake()
    {
        if (i)
            Destroy(gameObject);
        i = this;
        DontDestroyOnLoad(gameObject);
        map.OnValueChanged += OnMapChanged;
        hostButton.onClick.AddListener(() => { i.HostGame(); });
        clientButton.onClick.AddListener(() => { i.JoinGame(); });
        if (GameManager.i)
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
        if (u && u.SpawnSpeed > 0)
        {
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
                    unitQueueButtons[i].GetComponent<Image>().sprite = GameManager.i.unitSettings[u.spawnQueue[i]].unitImage;
                }
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
        if(IsServer)
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
        ulong _localID = NetworkManager.Singleton.LocalClientId;
        unitButtonParent.SetActive(GameManager.i.IsSelectedBuild(_localID));
        int _i = 0;
        foreach (Button _b in unitButtons)
            _b.gameObject.SetActive(false);
        foreach (Button _b in unitQueueButtons)
            _b.gameObject.SetActive(false);
        if (_id == -1 || GameManager.i.unitSettings[_id].uB == null) return;
        u = GameManager.i.GetUnit(GameManager.i.GetPlayer(_localID).selectedUnitList[0]);
        for (int i = 0; i < u.spawnQueue.Count; i++)
        {
            unitQueueButtons[i].gameObject.SetActive(true);
            unitQueueButtons[i].GetComponent<Image>().sprite = GameManager.i.unitSettings[u.spawnQueue[i]].unitImage;
        }
        foreach (UnitButton.UnitButtonSettings _uBS in GameManager.i.unitSettings[_id].uB.unitButtons)
        {
            List<int> _uList = GameManager.i.InsideSelectedUnit(_localID);
            if (_uBS.buttonType == UnitButton.UnitButtonSettings.btype.InsideUnit)
            {
                unitButtons[_i].gameObject.SetActive((_uList.Count > _i && _uList[_i] != -1));
                if (_uList[_i] != -1)
                {
                    unitButtons[_i].GetComponent<Image>().sprite = GameManager.i.unitSettings[GameManager.i.GetUnit(_uList[_i]).Type].unitImage;
                    unitButtons[_i].GetComponentInChildren<TextMeshProUGUI>().text = "";
                }
            }
            else
            {
                unitButtons[_i].gameObject.SetActive(true);
                if (_uBS.buttonType == UnitButton.UnitButtonSettings.btype.Spawn || _uBS.buttonType == UnitButton.UnitButtonSettings.btype.Build)
                {
                    unitButtons[_i].GetComponentInChildren<TextMeshProUGUI>().text = "";
                    unitButtons[_i].GetComponent<Image>().sprite = GameManager.i.unitSettings[_uBS.spawnID].unitImage;
                }
                else unitButtons[_i].GetComponentInChildren<TextMeshProUGUI>().text = _uBS.name;
            }
            _i++;
        }
        OnMoneyChange(-1, player.money.Value);
    }
    public void OnMoneyChange(float _prev, float _curr)
    {
        if (id == -1)
            return;
        UnitButton.UnitButtonSettings[] _uB = GameManager.i.unitSettings[id].uB.unitButtons;
        for (int _i = 0; _i < unitButtons.Length; _i++)
        {
            float _cost = 0;
            if (_uB.Length > _i)
                _cost = GameManager.i.unitSettings[_uB[_i].spawnID].cost;
            if (_cost != 0 && _cost > _curr)
                unitButtons[_i].GetComponent<Image>().color = Color.grey;
            else unitButtons[_i].GetComponent<Image>().color = Color.white;
        } 
    }
    public void SetPlayer(Player _player)
    {
        player = _player;
    }
    void UpdateIsBuildUI()
    {
        unitButtonParent.SetActive(GameManager.i.IsSelectedBuild(NetworkManager.Singleton.LocalClientId));
    }
    public void PressUnitButton(int _i)
    {
        GameManager.i.PressUnitButtonRpc(_i);
    }
    public void PressUnitRemoveQueueButton(int _i)
    {
        GameManager.i.PressUnitQueueButtonRpc(_i);
    }
    private void Update()
    {
        fpsUI.text = ((int)(1 / Time.deltaTime)).ToString();
    }
}
