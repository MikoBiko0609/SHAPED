using UnityEngine;

public class EnemyRanged : EnemyBaseNav
{
    [Header("Bands (m)")]
    public float retreatBelow = 8f;
    public float shootMin = 8f;
    public float shootMax = 14f;
    public float advanceMax = 20f;

    [Header("Animator States (exact names)")]
    public string throwState = "Throw";               // make clip non-looping
    [Range(0f, 1f)] public float releaseAtNormalized = 0.75f; // frame 90/120

    [Header("Timing")]
    public float cooldownAfterThrow = 1.5f;
    public float maxThrowDuration = 2.0f;             // fail-safe to exit throw

    [Header("Projectile")]
    public GameObject projectilePrefab;               // your Cube_Projectile prefab
    public Transform projectileHolder;                // hand/holder transform (where it spawns)
    public GameObject holderVisual;                   // the unpacked cube you always show in the hand
    public Transform muzzleFallback;                  // optional
    public float projectileSpeed = 12f;
    public float upwardBias = 0f;                     // keep 0 to stay flat

    [Header("Homing (optional)")]
    public bool addHoming = true;
    public float homingTurnRate = 120f;
    public float homingDuration = 1.0f;

    [Header("Projectile Damage")]
    public int projectileDamage = 4;   // large enemy deals 4 to player

    // Internal
    float fireTimer = 0f;
    bool inThrow = false;
    bool releasedThisCycle = false;
    int activeStateHash = -1;
    int cycleAtStart = -1;
    float throwClock = 0f;

    void OnValidate()
    {
        if (projectileSpeed < 0f) projectileSpeed = 0f;
        if (maxThrowDuration < 0.2f) maxThrowDuration = 0.2f;
    }

    // ===== Movement intent per band =====
    protected override bool TryGetGoal(out Vector3 goalWorld)
    {
        float d = Vector3.Distance(transform.position, target.position);

        if (d < retreatBelow)
        {
            Vector3 away = transform.position - target.position; away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -target.forward;
            away.Normalize();
            goalWorld = target.position + away * Mathf.Max(0.5f, shootMin);
            goalWorld.y = transform.position.y;
            return true;
        }

        if (d >= shootMin && d <= shootMax)
        {
            goalWorld = default;
            return false; // stand & throw
        }

        if (d > shootMax && d < advanceMax)
        {
            goalWorld = target != null ? target.position : transform.position;
            return true;
        }

        goalWorld = default;
        return false;
    }

    // ===== Tick after nav =====
    protected override void AfterMove()
    {
        fireTimer -= Time.deltaTime;
        FaceTargetFlat();

        if (inThrow)
        {
            DriveThrowState();
            return;
        }

        if (CanStartThrow())
            BeginThrow();
    }

    // Range-only start condition + cooldown. No LOS gating anymore.
    bool CanStartThrow()
    {
        if (fireTimer > 0f) return false;
        if (target == null || animator == null) return false;

        // Don’t re-enter while already in the state
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(throwState)) return false;
        if (animator.IsInTransition(0)) return false;

        float d = Vector3.Distance(transform.position, target.position);
        return (d >= shootMin && d <= shootMax);
    }

    void BeginThrow()
    {
        inThrow = true;
        releasedThisCycle = false;
        activeStateHash = -1;
        cycleAtStart = -1;
        throwClock = 0f;

        // Ensure the hand visual is visible until we actually release
        if (holderVisual != null) holderVisual.SetActive(true);

        animator.CrossFadeInFixedTime(throwState, 0.05f);
    }

    void DriveThrowState()
    {
        throwClock += Time.deltaTime;

        var st = animator.GetCurrentAnimatorStateInfo(0);
        bool inOurState = st.IsName(throwState);

        if (activeStateHash < 0)
        {
            if (!inOurState)
            {
                if (throwClock > 0.5f) { EndThrow(true); } // safety
                return;
            }
            activeStateHash = st.fullPathHash;
            cycleAtStart = Mathf.FloorToInt(st.normalizedTime);
        }

        if (!inOurState || st.fullPathHash != activeStateHash)
        {
            EndThrow(false);
            return;
        }

        int cycleNow = Mathf.FloorToInt(st.normalizedTime);
        float nt = Mathf.Repeat(st.normalizedTime, 1f);

        if (!releasedThisCycle && cycleNow == cycleAtStart && nt >= releaseAtNormalized)
        {
            ReleaseProjectile();
            releasedThisCycle = true;
        }

        if (cycleNow > cycleAtStart || nt >= 0.999f || throwClock >= maxThrowDuration)
        {
            EndThrow(false);
        }
    }

    void ReleaseProjectile()
    {
        // Hide the hand prop the exact frame we “throw”
        if (holderVisual != null) holderVisual.SetActive(false);

        // Spawn the real projectile only now (no pre-spawn)
        Transform spawnXf = projectileHolder != null ? projectileHolder :
                            muzzleFallback   != null ? muzzleFallback   : transform;

        Vector3 fwd = spawnXf.forward;
        fwd.y = 0f;                          // keep flat
        if (upwardBias > 0f) fwd += Vector3.up * Mathf.Clamp(upwardBias, 0f, 0.25f);
        fwd.Normalize();

        Vector3 pos = spawnXf.position;
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        var go = Instantiate(projectilePrefab, pos, rot);
        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = fwd * projectileSpeed;
        }

        var cube = go.GetComponent<CubeProjectile>();
        if (cube != null)
        {
            cube.damage = projectileDamage;
            cube.homingTurnRate = addHoming ? homingTurnRate : 0f;
            cube.homingDuration = addHoming ? homingDuration : 0f;
            cube.targetTag = "Player";
            cube.Launch(fwd * projectileSpeed, addHoming ? target : null);
        }

    }

    void EndThrow(bool interrupted)
    {
        inThrow = false;
        releasedThisCycle = false;
        activeStateHash = -1;
        cycleAtStart = -1;
        throwClock = 0f;

        // 1.5 s lockout
        fireTimer = cooldownAfterThrow;

        // Force exit -> Idle so Animator can’t immediately re-trigger
        if (!string.IsNullOrEmpty(idleState))
            animator.CrossFadeInFixedTime(idleState, 0.05f);

        // Restore the hand visual so it looks “reloaded” for the next throw
        if (holderVisual != null) holderVisual.SetActive(true);
    }

    void FaceTargetFlat()
    {
        if (target == null) return;
        Vector3 to = target.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
        float step = 480f * Time.deltaTime;
        if (lookPivot != null)
            lookPivot.rotation = Quaternion.RotateTowards(lookPivot.rotation, look, step);
        else
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, step);
    }
}
