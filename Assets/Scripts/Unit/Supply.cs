using UnityEngine;
using Unity.Netcode;
using rts.GameLogic;
using Zenject;
namespace rts.Unit
{
    public class Supply : NetworkBehaviour
    {
        [field: SerializeField] public float supplyStart { get; private set; }
        [field: SerializeField] public float supplyCarryCapacity { get; private set; }

        [SerializeField] float supplyCarryDelay;
        [SerializeField] GameObject[] supplyVisual;
        public NetworkVariable<float> supplyResource { get; private set; } = new NetworkVariable<float>(0);
        public bool returnToStockpile { get; private set; }
        Supply stockpileUnit = null;
        public float loadingSupplyProgress { get; private set; }
        Unit unit;
        Orders orders;
        const int maximumSupplyDistance = 100;
        GameData gameData;
        [Inject]
        public void Construct(GameData _gameData)
        {
            gameData = _gameData;
        }
        private void Awake()
        {
            unit = GetComponent<Unit>();
            orders = GetComponent<Orders>();
            if (supplyStart > 0)
                supplyResource.OnValueChanged += OnResourceChange;
        }

        void OnResourceChange(float _prev, float _curr)
        {
            int _amount = Mathf.RoundToInt((supplyResource.Value / supplyStart) * supplyVisual.Length);
            for (int i = 0; i < supplyVisual.Length; i++)
                supplyVisual[i].SetActive(i < _amount);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                supplyResource.Value = supplyStart;
            if (unit.settings.unitType == Settings.UnitType.supplyStockpile)
                gameData.AddToSupplyStockpile(this);
            if (unit.settings.unitType == Settings.UnitType.supplyCenter)
                gameData.AddSupplyCenter(unit.playerID.Value, this);
            if (!IsServer || supplyCarryCapacity <= 0)
                return;
            Supply _resources = null;
            float _closestDistance = maximumSupplyDistance;
            foreach (Supply _r in gameData.supplyStockpiles)
            {
                float _distance = Vector3.Distance(transform.position, _r.transform.position);
                if (_distance < _closestDistance)
                {
                    _closestDistance = _distance;
                    _resources = _r;
                }
            }
            if (_resources != null && unit.IsServer)
                orders.SetTargetRpc(_resources.unit.id.Value, false, -1);
        }

        private void Update()
        {
            loadingSupplyProgress -= Time.deltaTime;
            if (unit.IsServer && orders.targetClass && unit.settings.unitType == Settings.UnitType.truck && orders.targetClass.supply.supplyResource.Value > 0 && orders.nearTarget)
                GetResource();
        }

        public void GetToSupplyCenter()
        {
            loadingSupplyProgress = supplyCarryDelay;
            gameData.GetPlayer(unit.playerID.Value).AddMoney(supplyResource.Value);
            if (stockpileUnit.supplyResource.Value > 0)
                orders.SetTargetRpc(stockpileUnit.unit.id.Value, false, -1);
            else
                orders.FinishOrderRpc(false);
            supplyResource.Value = 0;
            returnToStockpile = false;
        }

        void GetResource()
        {
            stockpileUnit = orders.targetClass.supply;
            float _amount = Mathf.Clamp(supplyCarryCapacity - supplyResource.Value, 0, orders.targetClass.supply.supplyResource.Value);
            if (_amount > 0)
                loadingSupplyProgress = supplyCarryDelay;
            orders.targetClass.supply.supplyResource.Value -= _amount;
            supplyResource.Value += _amount;
            Supply _stockpile = null;
            float _f = 99999;
            foreach (Supply _u in gameData.supplyCenters[unit.playerID.Value])
            {
                float _d = Vector3.Distance(_u.transform.position, transform.position);
                if (_u.unit.IsConstructionFinished && _d < _f)
                {
                    _d = _f;
                    _stockpile = _u;
                }
            }
            if (_stockpile)
            {
                orders.SetTargetRpc(_stockpile.unit.id.Value, false, -1);
                returnToStockpile = true;
            }
        }
    }
}