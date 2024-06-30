using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
namespace rts.Unit
{
    public class Attack : NetworkBehaviour
    {
        Unit unit;
        Orders orders;
        Aircraft unitAir;
        Transform t;
        NavMeshAgent agent;

        private void Start()
        {
            unit = GetComponent<Unit>();
            orders = GetComponent<Orders>();
            agent = GetComponent<NavMeshAgent>();
            unitAir = GetComponent<Aircraft>();
            t = transform;
        }

        void Update()
        {
            Unit _target = orders.targetClass;
            if (!_target)
                _target = orders.nearbytargetClass;
            SetAimTargets(_target);
            if (!_target || _target.healthClass.isDestroyed || !IsServer)
                return;
            Vector3 _targetClosestPoint = _target.col.ClosestPoint(transform.position);
            float _distance = Vector3.Distance(_targetClosestPoint, t.position);
            if (unit.unitWeapons.Length == 0 || _distance >= unit.maxRange
               || !orders.isAttackingTarget && !orders.nearbytargetClass || _target.IsInvulnerable
               || unit.insideClass && !unit.insideClass.carrier.shootFromInside)
                return;
            if (agent && agent.enabled && orders.targetClass)
                agent.stoppingDistance = unit.unitWeapons[0].maxDistance;
            for (int _i = 0; _i < unit.unitWeapons.Length; _i++)
            {
                if (!IsServer || (!orders.targetClass && !orders.nearbytargetClass) || !unit.IsWeaponReady(_i, _distance) ||
                    (unitAir && (unitAir.aircraftState != Aircraft.AircraftState.flying || unitAir.altitude != 1)))
                    continue;
                AttackRpc(_i);
            }
        }

        [Rpc(SendTo.Everyone)]
        public void AttackRpc(int _slot)
        {
            Unit.UnitWeaponStatsStruct weaponStats = unit.unitWeapons[_slot];
            unit.unitWeapons[_slot].RemoveAmmo();
            Unit _u = orders.targetClass;
            if (!_u)
                _u = orders.nearbytargetClass;
            if (weaponStats.shootParticles)
                weaponStats.shootParticles.Play();
            if (unit.animator)
                unit.animator.SetTrigger("Shoot");
            if (weaponStats.projectile)
            {
                int _spawnIndex = 0;
                if (weaponStats.projectileSpawn.Length > 1)
                    _spawnIndex = _slot;
                Rigidbody _rb = Instantiate(weaponStats.projectile, weaponStats.projectileSpawn[_spawnIndex].position, weaponStats.projectileSpawn[weaponStats.currentAmmo].rotation);
                if (!weaponStats.isProjectileHoming)
                    _rb.linearVelocity = weaponStats.projectileSpeed * weaponStats.projectileSpawn[weaponStats.currentAmmo].forward;
                if (weaponStats.isProjectileHoming)
                    _rb.GetComponent<Projectile>().Setup(_u.transform, _u, weaponStats.damage, unit.unitWeapons[_slot].projectileSpeed);
                else _rb.GetComponent<Projectile>().Setup(null, null, unit.unitWeapons[_slot].damage, unit.unitWeapons[_slot].projectileSpeed);
            }
            else if (IsServer) _u.healthClass.GetDamage(weaponStats.damage, false);
        }
        void SetAimTargets(Unit _u)
        {
            for (int i = 0; i < unit.unitWeapons.Length; i++)
            {
                Turret _uA = unit.unitWeapons[i].unitAim;
                if (!_uA || unit.unitWeapons[i].unitAimParent)
                    continue;
                if (!_u || _u.healthClass.isDestroyed)
                    _uA.SetTarget(null);
                else
                {
                    if (_uA.ballisticTrajectory)
                        _uA.SetTarget(_u.col.ClosestPoint(t.position));
                    else
                        _uA.SetTarget(_u.targetTransform.position);
                }
            }
        }
    }
}
