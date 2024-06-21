using UnityEngine;
using UnityEngine.AI;
using static Codice.Client.BaseCommands.QueryParser;
namespace rts.Unit
{
    public class Movement : MonoBehaviour
    {
        Unit unit;
        Supply unitResources;
        NavMeshAgent agent;
        Transform t;
        Orders orders;
        void Start()
        {
            t = transform;
            unit = GetComponent<Unit>();
            agent = GetComponent<NavMeshAgent>();
            unitResources = GetComponent<Supply>();
            orders = GetComponent<Orders>();
        }

        // Update is called once per frame
        void Update()
        {
            Unit _u = orders.unitTargetClass;
            if (!_u)
                _u = orders.nearbyUnitTargetClass;
            if (!_u)
                return;
            Vector3 _targetClosestPoint = _u.col.ClosestPoint(transform.position);
            if (agent && agent.enabled && orders.unitTargetID != -1 && !orders.nearbyUnitTargetClass)
            {
                if (_u.unitAir)
                    _targetClosestPoint.y = t.position.y;
                agent.destination = _targetClosestPoint;
                if (unitResources && unitResources.currResDelay > 0)
                {
                    agent.destination = t.position;
                    return;
                }
            }
            if (unitResources && unitResources.returnToStockpile && orders.nearTarget)
                unitResources.GetToStockpile();
            float _distance = Vector3.Distance(_targetClosestPoint, t.position);
            if (orders.unitTargetClass && unit.unitSettings.unitType == Settings.UnitType.builder
                && orders.unitTargetClass.currentBuildPoints.Value < orders.unitTargetClass.unitSettings.buildPointsNeeded
                && orders.nearTarget)
            {
                orders.unitTargetClass.currentBuildPoints.Value
                    = Mathf.Clamp(orders.unitTargetClass.currentBuildPoints.Value + Time.deltaTime, 0, orders.unitTargetClass.unitSettings.buildPointsNeeded);
                if (orders.unitTargetClass.currentBuildPoints.Value == orders.unitTargetClass.unitSettings.buildPointsNeeded)
                    orders.FinishOrderRpc(false);
            }
            else
            {
                if (!orders.isAttackingTarget && orders.unitTargetClass && unit.unitSettings.occupyPSlots > 0 && unit.insideUnitID.Value == -1 && orders.unitTargetClass.unitCarrier)
                {
                    if (orders.unitTargetClass.unitCarrier.FreeUnitSlots() == 0)
                    {
                        orders.FinishOrderRpc(false);
                        return;
                    }
                    if (orders.unitTargetClass.unitAir)
                    {
                        Vector3 _uPos = _targetClosestPoint;
                        _uPos.y = transform.position.y;
                        if (Vector3.Distance(_uPos, t.position) < 7)
                            orders.unitTargetClass.orders.SetTargetRpc(unit.id.Value, false, -1);
                    }
                    else if (orders.nearTarget)
                        orders.unitTargetClass.unitCarrier.GetInsideUnit(unit);
                }
            }
        }
    }
}