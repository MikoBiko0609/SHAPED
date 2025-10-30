using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class CubeProjectile : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 4;                        // large enemy default
    public string targetTag = "Player";

    [Header("Lifetime")]
    public float lifeTime = 10f;

    [Header("Homing")]
    public float homingTurnRate = 120f;
    public float homingDuration = 1.0f;
    public Transform target;

    Rigidbody rb;
    float homingTimer;
    bool homing = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        if (lifeTime > 0f) Destroy(gameObject, lifeTime);
    }

    public void Launch(Vector3 velocity, Transform targetToHome = null)
    {
        rb.linearVelocity = velocity;
        target = targetToHome;
        homingTimer = homingDuration;
        homing = (target != null && homingTurnRate > 0f && homingDuration > 0f);

        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
    }

    void FixedUpdate()
    {
        if (!homing || target == null) return;

        homingTimer -= Time.fixedDeltaTime;
        if (homingTimer <= 0f) { homing = false; return; }

        Vector3 v = rb.linearVelocity; if (v.sqrMagnitude < 0.01f) return;
        Vector3 to = (target.position + Vector3.up * 0.9f) - transform.position; if (to.sqrMagnitude < 0.01f) return;

        float maxRad = Mathf.Deg2Rad * homingTurnRate * Time.fixedDeltaTime;
        Vector3 dir = Vector3.RotateTowards(v.normalized, to.normalized, maxRad, 0f);

        rb.linearVelocity = dir * v.magnitude;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    void OnCollisionEnter(Collision col)
    {
        var h = col.collider.GetComponentInParent<Health>();
        if (h != null && !h.IsDead)
        {
            bool ok = string.IsNullOrEmpty(targetTag) ||
                      col.collider.CompareTag(targetTag) ||
                      h.gameObject.CompareTag(targetTag);

            if (ok) h.TakeDamage(damage, transform.position,
                                 col.contacts.Length > 0 ? col.contacts[0].normal : -transform.forward, this);
        }
        Destroy(gameObject);
    }
}
