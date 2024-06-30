using rts.GameLogic;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

namespace rts.Unit
{
    public class Orders : NetworkBehaviour
    {
        public List<UnitOrderStruct> unitOrderQueue { get; private set; } = new List<UnitOrderStruct>();
        public Vector3? targetVector { get; private set; }
        public Unit targetClass { get; private set; } = null;
        public Unit nearbytargetClass { get; private set; } = null;
        public int targetID { get; private set; } = -1;
        public bool isAttackingTarget { get; private set; } = false;
        public bool nearTarget { get; private set; } = false;
        Unit unit;
        NavMeshAgent agent;
        Transform t;
        GameData gameData;
        public Vector3? TargetPosition()
        {
            if (targetVector.HasValue)
                return targetVector.Value;
            else if(targetClass)
                return targetClass.transform.position;
            return null;
        } 
        public struct UnitOrderStruct
        {
            public Vector3? position;
            public int unit;
            public bool isAttackingTarget;
        }
        [Inject]
        public void Construct(GameData _gameData)
        {
            gameData = _gameData;
        }
        private void Awake()
        {
            unit = GetComponent<Unit>();
            agent = GetComponent<NavMeshAgent>();
            t = transform;
        }
        private void Start()
        {
            if (unit.settings.IsBuilding && unit.newUnitAfterSpawnPosition)
                targetVector = unit.newUnitAfterSpawnPosition.position;
            if (IsServer)
                StartCoroutine(CheckFinishedOrderLoop());
        }

        private void FixedUpdate()
        {
            if (IsServer && unit.insideClass)
            {
                t.position = unit.insideClass.transform.position;
                targetClass = unit.insideClass.orders.targetClass;
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
                targetVector = null;
                targetClass = null;
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
            targetID = _unitTarget;
            isAttackingTarget = _attackTarget;
            targetVector = null;
            if (_unitTarget < 0)
            {
                targetClass = null;
                return;
            }
            targetClass = gameData.GetUnit(targetID);
            nearbytargetClass = null;
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
            targetVector = _targetPos;
            if (agent && agent.enabled && gameObject.activeSelf)
                agent.destination = _targetPos;
            targetID = -1;
            targetClass = null;
            isAttackingTarget = _attackTarget;
        }
        public void SetNearbyEnemies(Unit _u, float _distance)
        {
            if (unit.unitWeapons.Length != 0 && _distance < unit.unitWeapons[0].maxDistance * 1.5)
                nearbytargetClass = _u;
        }
        public void SetTargetNull()
        {
            targetClass = null;
            nearTarget = false;
        }
        IEnumerator CheckFinishedOrderLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.2f);
                if (unitOrderQueue.Count == 0)
                    continue;
                if (agent && agent.enabled && targetVector.HasValue && !agent.hasPath)
                    FinishOrderRpc(false);
            }
        }
        private void OnTriggerEnter(Collider _col)
        {
            Unit _u = _col.GetComponent<Unit>();
            if (!_u)
                return;
            if (targetClass && _u == targetClass)
                nearTarget = true;
        }
    }
}
