using DG.Tweening;
using rts.GameLogic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

namespace rts.Unit {
    public class Health : NetworkBehaviour, IDamageable
    {
        public NetworkVariable<float> health { get; private set; } = new NetworkVariable<float>(100);
        public bool isDestroyed { get; private set; }
        Unit unit;
        Rigidbody rb;
        Aircraft aircraft;
        Collider col;
        GameData gameData;
        [Inject]
        public void Construct(GameData _gameData)
        {
            gameData = _gameData;
        }
        void Start()
        {
            unit = GetComponent<Unit>();
            rb = GetComponent<Rigidbody>();
            aircraft = GetComponent<Aircraft>();
            col = GetComponent<Collider>();
        }
        public void HealthSetup()
        {
            health.Value = unit.settings.maxHealth;
            health.OnValueChanged += OnHealthChanged;

        }
        void OnHealthChanged(float _prev, float _current)
        {
            float _ratio = _current / unit.settings.maxHealth;
            unit.unitUI.SetHealthUI(_ratio);
        }

        public void GetDamage(float _d, bool _explosive)
        {
            if (IsServer && !isDestroyed && !unit.IsInvulnerable && (!unit.insideClass || unit.insideClass.carrier.damageReduction < 100))
                GetDamageRpc(_d, _explosive);
        }

        [Rpc(SendTo.Everyone)]
        public void GetDamageRpc(float _d, bool _explosive)
        {
            if (unit.insideClass)
                _d *= (100 - unit.insideClass.carrier.damageReduction);
            unit.unitUI.SActive(true, 3);
            if (!IsServer)
                return;
            health.Value -= _d;
            if (health.Value <= 0)
                DieRpc(_explosive);
        }

        [Rpc(SendTo.Everyone)]
        public void DieRpc(bool _explosive)
        {
            if (isDestroyed)
                return;
            if (IsServer && unit.carrier)
                unit.carrier.OnDiyng();
            if (IsServer)
                gameData.GetPlayer(unit.playerID.Value).RemoveUnit(unit.id.Value);
            isDestroyed = true;
            if (unit.animator && unit.settings.animDeath)
            {
                unit.animator.SetFloat("X", 0);
                unit.animator.SetFloat("Z", 0);
                unit.animator.SetTrigger("Die");
            }
            else if (unit.settings.IsBuilding)
                unit.visualObject.SetActive(false);
            else foreach (MeshRenderer _unitRenderer in GetComponentsInChildren<MeshRenderer>())
                    foreach (Material _mat in _unitRenderer.materials)
                        _mat.color = Color.black;
            for (int i = 0; i < unit.unitWeapons.Length; i++)
                if (unit.unitWeapons[i].unitAim)
                    unit.unitWeapons[i].unitAim.enabled = false;
            if (aircraft)
                aircraft.enabled = false;
            if (GetComponent<Builder>())
                GetComponent<Builder>().enabled = false;
            if (GetComponent<Carrier>())
                GetComponent<Carrier>().enabled = false;
            if (GetComponent<Supply>())
                GetComponent<Supply>().enabled = false;
            if (GetComponent<NavMeshObstacle>())
                GetComponent<NavMeshObstacle>().enabled = false;
            if (GetComponent<NetworkTransform>())
                GetComponent<NetworkTransform>().enabled = false;
            if (GetComponent<NavMeshAgent>())
                GetComponent<NavMeshAgent>().enabled = false;
            if (rb)
                rb.isKinematic = true;
            if (aircraft && rb)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = (transform.position - aircraft.prevPosition) / Time.deltaTime;
                col.isTrigger = false;
            }
            else if (col)
                col.enabled = false;
            if (unit.onDeathEffect && _explosive)
            {
                unit.visualObject.SetActive(false);
                unit.onDeathEffect.SetActive(true);
            }
            unit.unitUI.gameObject.SetActive(false);
            unit.OnRemovingUnit();
            transform.DOMove(transform.position + Vector3.down*30, 1).SetDelay(10f).SetEase(Ease.Linear).SetSpeedBased();
            Invoke("Despawn", 25);
        }

        void Despawn()
        {
            NetworkObject.Despawn();
        }
    }
}
