using UnityEngine;
[RequireComponent(typeof(Unit))]
public class UnitAim : MonoBehaviour
{
    Unit unit;
    [SerializeField] Transform turret;
    [SerializeField] Transform cannon;
    [SerializeField] float verticalRotSpeed = 5;
    [SerializeField] float mVertical = 15;
    [SerializeField] float pVertical = 30;
    [SerializeField] float horizontalRotSpeed = 30;
    //[SerializeField] float mHorizontal = 180;
    //[SerializeField] float pHorizontal = 180;
    [SerializeField] bool ballisticTrajectory;
    [SerializeField] bool lowArc;
    Quaternion defTurretRot;
    Quaternion defCannonRot;
    Vector3 target = new Vector3(-99999, -99999, -99999);
    public bool onTheTarget { get; private set; }
    public Transform Cannon => cannon;
    public bool BallisticTrajectory => ballisticTrajectory;
    void Awake()
    {
        unit = GetComponent<Unit>();
        if (turret)
            defTurretRot = turret.localRotation;
        if (cannon)
            defCannonRot = cannon.localRotation;
    }
    void Update()
    {
        if (target.x == -99999)
        {
            if (turret && horizontalRotSpeed > 0)
                turret.localRotation = Quaternion.RotateTowards(turret.localRotation, defTurretRot, horizontalRotSpeed * Time.deltaTime);
            if (cannon && verticalRotSpeed > 0)
                cannon.localRotation = Quaternion.RotateTowards(cannon.localRotation, defCannonRot, verticalRotSpeed * Time.deltaTime);
            onTheTarget = false;
            return;
        }
        bool _horizontalFinished = true;
        if (turret && horizontalRotSpeed > 0)
        {
            Vector3 _offset = target - turret.position;
            Quaternion _newRot = Quaternion.LookRotation(turret.forward, -_offset) * Quaternion.Euler(new Vector3(0, 0, 90));
            turret.rotation = Quaternion.RotateTowards(turret.rotation, _newRot, horizontalRotSpeed * Time.deltaTime);
            _horizontalFinished = (turret.rotation == _newRot);
        }
        if (cannon && verticalRotSpeed > 0)
        {
            Vector3 _offset = target - cannon.position;
            if (ballisticTrajectory)
            {
                float _f = CalculateAngle(lowArc);
                if (_f == -99999 || _f + 10 > pVertical)
                    _f = 0;
                float _eA = cannon.eulerAngles.z;
                if (_eA > _f)
                {
                    _eA -= Time.deltaTime * verticalRotSpeed;
                    if (_eA < _f)
                        _eA = _f;
                }
                else
                {
                    _eA += Time.deltaTime * verticalRotSpeed;
                    if (_eA > _f)
                        _eA = _f;
                }
                if (_eA > 180)
                    _eA = Mathf.Clamp(_eA, 360 - mVertical, 360);
                else _eA = Mathf.Clamp(_eA, 0, pVertical);
                cannon.eulerAngles = new Vector3(cannon.eulerAngles.x, cannon.eulerAngles.y, _eA);
                onTheTarget = (_f != 0 && _eA == _f);
            }
            else
            {
                Quaternion _newRotation = Quaternion.LookRotation(cannon.forward, -_offset) * Quaternion.Euler(new Vector3(0, 0, -90));
                Quaternion _newRotationLerp = Quaternion.RotateTowards(cannon.rotation, _newRotation, verticalRotSpeed * Time.deltaTime);
                float _zLerp = _newRotationLerp.eulerAngles.z;
                if (_zLerp > 180)
                    _zLerp = Mathf.Clamp(_zLerp, 360 - mVertical, 360);
                else _zLerp = Mathf.Clamp(_zLerp, 0, pVertical);
                cannon.eulerAngles = new Vector3(_newRotationLerp.eulerAngles.x, _newRotationLerp.eulerAngles.y, _zLerp);
                onTheTarget = (_horizontalFinished && _zLerp == _newRotation.eulerAngles.z);
            }
        }
    }
    public void SetTarget(Vector3 _t)
    {
        target = _t;
    }
    float CalculateAngle(bool _low)
    {
        float _projectileGravity = 9.81f;
        Vector3 _offset = target - cannon.position;
        float _y = _offset.y;
        _offset.y = 0;
        float _x = _offset.magnitude - 10;
        float _sSqr = unit.UnitWeapons[0].projectileSpeed * unit.UnitWeapons[0].projectileSpeed;
        float _underTheSqrRoot = (_sSqr * _sSqr) - _projectileGravity * (_projectileGravity * _x * _x + 2 * _y * _sSqr);
        if (_underTheSqrRoot >= 0)
        {
            float _root = Mathf.Sqrt(_underTheSqrRoot);
            float _lowAngle = _sSqr - _root;
            float _highAngle = _sSqr + _root;
            if (_low)
                return (Mathf.Atan2(_lowAngle, _projectileGravity * _x) * Mathf.Rad2Deg);
            else return (Mathf.Atan2(_highAngle, _projectileGravity * _x) * Mathf.Rad2Deg);

        }
        else return -99999;
    }
}
