using UnityEngine;

public class Projectile : MonoBehaviour
{
    Rigidbody rb;
    [SerializeField] GameObject projecile;
    [SerializeField] GameObject explosion;
    [SerializeField] GameObject explosionDecal;
    [SerializeField] float radius;
    Transform target;
    Unit unit;
    float damage;
    float speed;
    float time;
    bool exploded;
    Vector3 explodePosition;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (unit && unit.destroyed)
            target = null;
        if (exploded)
            transform.position = explodePosition;
        else if(rb.isKinematic)
        {
            if (target)
            { 
                transform.LookAt(target);
                transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.fixedDeltaTime);
                if (Vector3.Distance(transform.position, target.position) < 0.5f)
                    Explode(null);
            }
            else
                transform.position += transform.forward * speed * Time.fixedDeltaTime;
        }
    }
    void Update()
    {
        time += Time.deltaTime;
        if (time > 10 && !exploded)
            Explode(null);
        if (!rb.isKinematic && !unit)
            transform.forward = rb.linearVelocity;

    }
    private void OnTriggerEnter(Collider _c)
    {
        if ((target && _c.transform != target) || _c.GetComponent<Projectile>())
            return;
        Explode(_c);
    }
    public void Setup(Transform _target, Unit _unit, float _damage, float _speed)
    {
        target = _target;
        unit = _unit;
        damage = _damage;
        speed = _speed;
    }
    void Explode(Collider _c)
    {
        exploded = true;
        explodePosition = transform.position;
        if (explosionDecal)
            explosionDecal.SetActive(_c && _c.gameObject.layer == 0);
        Collider[] _colList = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider _col in _colList)
        {
            IDamageable _damagaeble = _col.GetComponent<IDamageable>();
            if (_damagaeble != null)
            {
                float _d = Vector3.Distance(transform.position, _col.ClosestPoint(transform.position)) / radius;
                _d = Mathf.Clamp(_d, 0, 1);
                _damagaeble.GetDamage(damage * (1 - _d / 2),true);
            }
        }
        Destroy(gameObject, 10);
        projecile.SetActive(false);
        explosion.SetActive(true);
        rb.isKinematic = true;
        if (!target)
            transform.forward = -Vector3.up;
    }
}
