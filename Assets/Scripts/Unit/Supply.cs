using UnityEngine;
using Unity.Netcode;
using static Codice.Client.BaseCommands.QueryParser;
namespace rts.Unit
{
    public class Supply : NetworkBehaviour
    {
        [field: SerializeField] public float resourceStart { get; private set; }
        [field: SerializeField] public float resourceCarryCapacity { get; private set; }

        [SerializeField] float resourceCarryDelay;
        [field: SerializeField] public bool isResourceStockpile { get; private set; }
        [SerializeField] GameObject[] resVisual;
        public NetworkVariable<float> currentResource { get; private set; } = new NetworkVariable<float>(0);
        public bool returnToStockpile { get; private set; }
        Supply stockpileUnit = null;
        public float currResDelay { get; private set; }
        Unit unit;
        Orders orders;
        private const int maximumResourceDistance = 100;
        private void Awake()
        {
            unit = GetComponent<Unit>();
            orders = GetComponent<Orders>();
            if (resourceStart > 0)
                currentResource.OnValueChanged += OnResourceChange;
        }
        void Start()
        {
            if (IsServer)
                currentResource.Value = resourceStart;
        }
        void OnResourceChange(float _prev, float _curr)
        {
            int _amount = Mathf.RoundToInt((currentResource.Value / resourceStart) * resVisual.Length);
            for (int i = 0; i < resVisual.Length; i++)
                resVisual[i].SetActive(i < _amount);
        }
        public override void OnNetworkSpawn()
        {
            if (resourceStart > 0)
                GameData.i.AddToSupply(this);
            if (isResourceStockpile)
                GameData.i.AddStockpile(unit.playerID.Value, this);
            if (!IsServer || resourceCarryCapacity <= 0)
                return;
            Supply _resources = null;
            float _closestDistance = maximumResourceDistance;
            foreach (Supply _r in GameData.i.supplies)
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
            currResDelay -= Time.deltaTime;
            if (resourceCarryCapacity > 0 && unit.IsServer && orders.unitTargetClass 
                && unit.unitResources && orders.unitTargetClass.unitResources.currentResource.Value > 0 && orders.nearTarget)
                GetResource();
        }
        public void GetToStockpile()
        {
            currResDelay = resourceCarryDelay;
            GameData.i.GetPlayer(unit.playerID.Value).AddMoney(currentResource.Value);
            if (stockpileUnit.currentResource.Value > 0)
                orders.SetTargetRpc(stockpileUnit.unit.id.Value, false, -1);
            else
                orders.FinishOrderRpc(false);
            currentResource.Value = 0;
            returnToStockpile = false;
        }
        void GetResource()
        {
            stockpileUnit = orders.unitTargetClass.unitResources;
            float _amount = Mathf.Clamp(resourceCarryCapacity - currentResource.Value, 0, orders.unitTargetClass.unitResources.currentResource.Value);
            if (_amount > 0)
                currResDelay = resourceCarryDelay;
            orders.unitTargetClass.unitResources.currentResource.Value -= _amount;
            currentResource.Value += _amount;
            Supply _stockpile = null;
            float _f = 99999;
            foreach (Supply _u in GameData.i.supplyStockpile[unit.playerID.Value])
            {
                float _d = Vector3.Distance(_u.transform.position, transform.position);
                if (_u.unit.IsBuild && _d < _f)
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