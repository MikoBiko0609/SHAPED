using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 1;
    [Tooltip("Only damage objects with this tag (leave empty to damage anything with Health).")]
    public string targetTag = "Enemy";

    [Header("Lifetime")]
    public float lifeTimeSeconds = 10f;

    Rigidbody rb;

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
        if (lifeTimeSeconds > 0f) Destroy(gameObject, lifeTimeSeconds);
    }

    public void Shoot(Vector3 vel)
    {
        rb.linearVelocity = vel;
        if (vel.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
    }

    void OnCollisionEnter(Collision c)
    {
        var h = c.collider.GetComponentInParent<Health>();
        if (h != null && !h.IsDead)
        {
            bool ok = string.IsNullOrEmpty(targetTag) ||
                      c.collider.CompareTag(targetTag) ||
                      h.gameObject.CompareTag(targetTag);

            if (ok) h.TakeDamage(damage, transform.position,
                                 c.contacts.Length > 0 ? c.contacts[0].normal : -transform.forward, this);
        }
        Destroy(gameObject);
    }
}
