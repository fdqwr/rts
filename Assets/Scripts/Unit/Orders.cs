using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace rts.Unit
{
    public class Orders : NetworkBehaviour
    {
        public List<UnitOrderStruct> unitOrderQueue { get; private set; } = new List<UnitOrderStruct>();
        public Vector3? targetPosition { get; private set; }
        public Unit unitTargetClass { get; private set; } = null;
        public Unit nearbyUnitTargetClass { get; private set; } = null;
        public int unitTargetID { get; private set; } = -1;
        public bool isAttackingTarget { get; private set; } = false;
        public bool nearTarget { get; private set; } = false;
        Unit unit;
        NavMeshAgent agent;
        Transform t;
        public struct UnitOrderStruct
        {
            public Vector3? position;
            public int unit;
            public bool isAttackingTarget;
        }
        private void Awake()
        {
            unit = GetComponent<Unit>();
            agent = GetComponent<NavMeshAgent>();
            t = transform;
        }
        private void Start()
        {
            if (unit.unitSettings.unitType == Settings.UnitType.building && unit.newUnitAfterSpawnPosition)
                targetPosition = unit.newUnitAfterSpawnPosition.position;
            if (IsServer)
                StartCoroutine(CheckFinishedOrderLoop());
        }
        private void FixedUpdate()
        {
            if (IsServer && unit.insideUnitClass)
            {
                t.position = unit.insideUnitClass.transform.position;
                unitTargetClass = unit.insideUnitClass.orders.unitTargetClass;
            }
        }
        [Rpc(SendTo.Everyone)]
        public void SetTargetRpc(int _unitTarget, bool _attackTarget, int _queue)
        {
            SetTarget(_unitTarget, _attackTarget, _queue);
        }
        [Rpc(SendTo.Everyone)]
        public void SetTargetPosRpc(Vector3 _targetPos, bool _attackTarget, int _queue)
        {
            SetTargetPos(_targetPos, _attackTarget, _queue);
        }
        [Rpc(SendTo.Everyone)]
        public void FinishOrderRpc(bool _allOrders)
        {
            if (_allOrders)
                unitOrderQueue = new List<UnitOrderStruct>();
            if (unitOrderQueue.Count > 0)
                unitOrderQueue.RemoveAt(0);
            if (unitOrderQueue.Count > 0)
            {
                if (unitOrderQueue[0].unit > -1)
                    SetTarget(unitOrderQueue[0].unit, unitOrderQueue[0].isAttackingTarget, 0);
                else
                    SetTargetPos(unitOrderQueue[0].position.Value, unitOrderQueue[0].isAttackingTarget, 0);
            }
            else
            {
                isAttackingTarget = false;
                targetPosition = null;
                unitTargetClass = null;
            }
        }
        public void SetTarget(int _unitTarget, bool _attackTarget, int _queue)
        {
            if (_queue == -1)
                unitOrderQueue = new List<UnitOrderStruct> { new UnitOrderStruct { position = null, unit = _unitTarget, isAttackingTarget = _attackTarget } };
            if (_queue == 1)
                unitOrderQueue.Add(new UnitOrderStruct { position = null, unit = _unitTarget, isAttackingTarget = _attackTarget });
            if (_queue != 0 && unitOrderQueue.Count != 1)
                return;
            nearTarget = false;
            unitTargetID = _unitTarget;
            isAttackingTarget = _attackTarget;
            targetPosition = null;
            if (_unitTarget < 0)
            {
                unitTargetClass = null;
                return;
            }
            unitTargetClass = GameData.i.GetUnit(unitTargetID);
            nearbyUnitTargetClass = null;
        }
        public void SetTargetPos(Vector3 _targetPos, bool _attackTarget, int _queue)
        {
            if (_queue == -1)
                unitOrderQueue = new List<UnitOrderStruct> { new UnitOrderStruct { position = _targetPos, unit = -1, isAttackingTarget = _attackTarget } };
            if (_queue == 1)
                unitOrderQueue.Add(new UnitOrderStruct { position = _targetPos, unit = -1, isAttackingTarget = _attackTarget });
            if (_queue != 0 && unitOrderQueue.Count != 1)
                return;
            for (int i = 0; i < unit.unitWeapons.Length; i++)
            {
                Turret _uA = unit.unitWeapons[i].unitAim;
                if (_uA && !unit.unitWeapons[i].unitAimParent)
                    _uA.SetTarget(null);
            }
            nearTarget = false;
            targetPosition = _targetPos;
            if (agent && agent.enabled && gameObject.activeSelf)
                agent.destination = _targetPos;
            unitTargetID = -1;
            unitTargetClass = null;
            isAttackingTarget = _attackTarget;
        }
        public void SetNearbyEnemies(Unit _u, float _distance)
        {
            if (unit.unitWeapons.Length != 0 && _distance < unit.unitWeapons[0].maxDistance * 1.5)
                nearbyUnitTargetClass = _u;
        }
        public void SetTargetNull()
        {
            unitTargetClass = null;
            nearTarget = false;
        }
        IEnumerator CheckFinishedOrderLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.2f);
                if (unitOrderQueue.Count == 0)
                    continue;
                if (agent && agent.enabled && targetPosition.HasValue && !agent.hasPath)
                    FinishOrderRpc(false);
            }
        }
        private void OnTriggerEnter(Collider _col)
        {
            Unit _u = _col.GetComponent<Unit>();
            if (!_u)
                return;
            if (unitTargetClass && _u == unitTargetClass)
                nearTarget = true;
        }
    }
}
