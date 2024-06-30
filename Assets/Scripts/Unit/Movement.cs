using UnityEngine;
using UnityEngine.AI;
namespace rts.Unit
{
    public class Movement : MonoBehaviour
    {
        Unit unit;
        Supply unitResources;
        NavMeshAgent agent;
        Transform t;
        Orders orders;
        Vector3 prevModelPosition;

        void Start()
        {
            t = transform;
            unit = GetComponent<Unit>();
            agent = GetComponent<NavMeshAgent>();
            unitResources = GetComponent<Supply>();
            orders = GetComponent<Orders>();
        }

        void Update()
        {
            if (unit.healthClass && unit.healthClass.isDestroyed)
                return;
            VisualRotation();
            Move();
            prevModelPosition = unit.visualObject.transform.position;
        }
        private void Move()
        {
            Unit _target = orders.targetClass;
            if (!_target)
                _target = orders.nearbytargetClass;
            if (!_target)
                return;
            Vector3 _targetClosestPoint = _target.col.ClosestPoint(transform.position);
            if (agent && agent.enabled && orders.targetID != -1 && !orders.nearbytargetClass)
            {
                if (_target.settings.IsAircraft)
                    _targetClosestPoint.y = t.position.y;
                agent.destination = _targetClosestPoint;
                if (unitResources && unitResources.loadingSupplyProgress > 0)
                {
                    agent.destination = t.position;
                    return;
                }
            }
            if (unitResources && unitResources.returnToStockpile && orders.nearTarget)
                unitResources.GetToSupplyCenter();
            if (orders.targetClass && unit.settings.unitType == Settings.UnitType.builder && !_target.IsConstructionFinished && orders.nearTarget)
            {
                _target.currentBuildPoints.Value = Mathf.Clamp(_target.currentBuildPoints.Value + Time.deltaTime, 0, _target.settings.buildPointsNeeded);
                if (_target.currentBuildPoints.Value == _target.settings.buildPointsNeeded)
                    orders.FinishOrderRpc(false);
            }
            else
            {
                if (!orders.isAttackingTarget && orders.targetClass && unit.settings.occupyPSlots > 0 && unit.insideID.Value == -1 && _target.carrier)
                {
                    if (_target.carrier.freeUnitSlots == 0)
                    {
                        orders.FinishOrderRpc(false);
                        return;
                    }
                    if (_target.settings.IsAircraft)
                    {
                        Vector3 _uPos = _targetClosestPoint;
                        _uPos.y = transform.position.y;
                        if (Vector3.Distance(_uPos, t.position) < 7)
                            _target.orders.SetTargetRpc(unit.id.Value, false, -1);
                    }
                    else if (orders.nearTarget)
                        _target.carrier.GetInsideUnit(unit);
                }
            }
        }
        void VisualRotation()
        {
            Unit _unit = orders.targetClass;
            if (!_unit)
                _unit = orders.nearbytargetClass;
            Vector3 _velocity = unit.visualObject.transform.InverseTransformDirection(unit.visualObject.transform.position - prevModelPosition) * Time.deltaTime * 5000;
            foreach (Transform _wheel in unit.wheels)
                _wheel.Rotate(unit.wheelsAxis * _velocity.z * Time.deltaTime * 50);
            if (unit.settings.rotateVisualToTarget)
            {
                unit.animator.SetFloat("X", Mathf.Clamp(_velocity.z, -1, 1));
                unit.animator.SetFloat("Z", Mathf.Clamp(_velocity.x, -1, 1));
                if (_unit && !_unit.healthClass.isDestroyed && (orders.nearbytargetClass || orders.isAttackingTarget))
                {
                    Vector3 targetDirection = _unit.targetTransform.position - unit.visualObject.transform.position;
                    unit.visualObject.transform.rotation = Quaternion.LookRotation(targetDirection);
                    unit.visualObject.transform.eulerAngles = new Vector3(0, unit.visualObject.transform.eulerAngles.y, 0);
                }
                else
                    unit.visualObject.transform.localRotation = Quaternion.Lerp(unit.visualObject.transform.localRotation, unit.defaultVisualRotation, 0.2f);
            }
        }
    }
}