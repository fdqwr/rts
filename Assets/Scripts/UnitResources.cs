using UnityEngine;
using Unity.Netcode;
[RequireComponent(typeof(Unit))]

public class UnitResources : NetworkBehaviour
{
    [SerializeField] float resourceStart;
    [SerializeField] float resourceCarryCapacity;
    [SerializeField] float resourceCarryDelay;
    [SerializeField] bool isResourceStockpile;
    [SerializeField] GameObject[] resVisual;
    public NetworkVariable<float> currentResource { get; private set; } = new NetworkVariable<float>(0);
    public bool returnToStockpile { get; private set; }
    UnitResources stockpileUnit = null;
    public float currResDelay { get; private set; }
    Unit unit;
    public float ResourceCarryCapacity => resourceCarryCapacity;
    private void Awake()
    {
        unit = GetComponent<Unit>();
        if (resourceStart > 0)
            currentResource.OnValueChanged += OnResourceChange;
    }
    void Start()
    {
        if(IsServer)
            currentResource.Value = resourceStart;
    }
    void OnResourceChange(float _prev, float _curr)
    {
        int _amount = Mathf.RoundToInt((currentResource.Value / resourceStart)*resVisual.Length);
        for (int i = 0; i < resVisual.Length; i++)
            resVisual[i].SetActive(i < _amount);
    }
    public override void OnNetworkSpawn()
    {
        if (resourceStart > 0)
            GameManager.i.AddToResources(this);
        if (isResourceStockpile)
            GameManager.i.AddStockpile(unit.playerID.Value, this);
        if (!IsServer || resourceCarryCapacity <= 0) return;
        UnitResources _resources = null;
        float _closestDistance = 100;
        foreach (UnitResources _r in GameManager.i.resources)
        {
            float _distance = Vector3.Distance(transform.position, _r.transform.position);
            if (_distance < _closestDistance)
            {
                _closestDistance = _distance;
                _resources = _r;
            }
        }
        if (_resources != null && unit.IsServer)
            unit.SetTargetRpc(_resources.unit.id.Value, false);
    }
    private void Update()
    {
        currResDelay -= Time.deltaTime;
        if (resourceCarryCapacity > 0 && unit && unit.IsServer && unit.unitTargetClass && unit.unitResources && unit.unitTargetClass.unitResources.currentResource.Value > 0 && unit.nearTarget)
            GetResource();
    }
    public void GetToStockpile()
    {
        currResDelay = resourceCarryDelay;
        GameManager.i.GetPlayer(unit.playerID.Value).AddMoney(currentResource.Value);
        if (stockpileUnit.currentResource.Value > 0) unit.SetTargetRpc(stockpileUnit.unit.id.Value, false);
        else unit.SetTargetPosRpc(transform.position);
        currentResource.Value = 0;
        returnToStockpile = false;
    }
    void GetResource()
    {
        stockpileUnit = unit.unitTargetClass.unitResources;
        float _amount = Mathf.Clamp(resourceCarryCapacity - currentResource.Value, 0, unit.unitTargetClass.unitResources.currentResource.Value);
        if (_amount > 0)
            currResDelay = resourceCarryDelay;
        unit.unitTargetClass.unitResources.currentResource.Value -= _amount;
        currentResource.Value += _amount;
        UnitResources _stockpile = null;
        float _f = 99999;
        foreach (UnitResources _u in GameManager.i.resourceStockpile[unit.playerID.Value])
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
            unit.SetTargetRpc(_stockpile.unit.id.Value, false);
            returnToStockpile = true;
        }
    }
}
