using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace rts.UI
{
    using rts.Unit;
    using rts.Player;
    using rts.GameLogic;
    using Zenject;
    using DG.Tweening.Core.Easing;

    public class UnitUI : MonoBehaviour
    {
        Image healthImage;
        [SerializeField] Slider healthBar;
        [SerializeField] TextMeshProUGUI buildingUI;
        [SerializeField] TextMeshProUGUI hotkeyUI;
        [SerializeField] RectTransform minimapImage;
        RectTransform rectTransform;
        Unit unit;
        float showHealthTime;
        bool healthActive;
        bool buildActivce;
        Camera cam;
        GameData gameData;
        GameManager gameManager;
        MenuUI menuUI;
        [Inject]
        public void Construct(GameManager _gameManager, GameData _gameData, MenuUI _menuUI)
        {
            gameManager = _gameManager;
            gameData = _gameData;
            menuUI = _menuUI;
        }
        private void OnDisable()
        {
            minimapImage.gameObject.SetActive(false);
        }

        private void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            healthImage = healthBar.fillRect.GetComponent<Image>();
            minimapImage.SetParent(menuUI.minimapUIParent);
            minimapImage.GetComponent<Image>().color = gameData.GetColor(unit.team.Value);
        }

        private void Update()
        {
            if (!unit)
                return;
            minimapImage.position = new Vector3(unit.transform.position.x, unit.transform.position.z, 0) * gameManager.mapSettings.maps[menuUI.map.Value].minimapScale;
            if (showHealthTime > 0)
            {
                showHealthTime -= Time.deltaTime;
                if (showHealthTime <= 0)
                    healthBar.gameObject.SetActive(false);
            }
            if (!healthActive && !buildActivce)
                return;
            if (!cam)
                cam = Player.localPlayerClass.cam;
            else
                rectTransform.position = cam.WorldToScreenPoint(unit.transform.position + Vector3.up * 5);
        }

        public void SetHealthUI(float _health)
        {
            if (!healthImage)
                return;
            healthBar.value = _health;
            if (_health > 0.7f)
                healthImage.color = Color.green;
            else if (_health > 0.3f)
                healthImage.color = Color.yellow;
            else
                healthImage.color = Color.red;
        }

        public void SetBuildUI(float _build)
        {
            buildingUI.gameObject.SetActive((_build < 100));
            buildActivce = (_build < 100);
            buildingUI.text = ((int)(_build)).ToString() + "%";
        }

        public void SetHotkeyUI(int _hotkey)
        {
            hotkeyUI.gameObject.SetActive((_hotkey > 0));
            if (_hotkey == 0)
                hotkeyUI.text = "";
            else hotkeyUI.text = (_hotkey).ToString();
        }

        public void SActive(bool _active, float _time)
        {
            healthActive = _active;
            hotkeyUI.gameObject.SetActive(_active);
            healthBar.gameObject.SetActive(healthActive);
            showHealthTime = _time;
        }

        public void SetUnit(Unit _unit)
        {
            unit = _unit;
        }

        public void SelfDestruct()
        {
            minimapImage.SetParent(transform);
            Destroy(gameObject);
        }
    }
}