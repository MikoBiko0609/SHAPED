using UnityEngine;

public class EnemyMelee : EnemyBaseNav
{
    [Header("Bands (m)")]
    public float attackRange = 2f;  
    public float advanceMax  = 20f;

    [Header("Animator States (exact names)")]
    public string meleeState = "Melee";   
    [Range(0f, 1f)] public float hitAtNormalized = 0.35f;

    [Header("Timing")]
    public float meleeCooldown    = 1.2f; 
    public float maxMeleeDuration = 2.0f;

    [Header("Hit Volume")]
    public Transform meleeOrigin;      
    public float meleeRadius = 0.8f;

    [Header("Effects")]
    public float knockbackForce = 6f;   
    public float verticalKnock = 0.0f; 

    [Header("Damage")]
    public int meleeDamage = 2; 
    
    [Header("Debug")]
    public bool logDebug = false;

    float cooldownTimer = 0f;
    bool  inMelee = false;
    bool  hitThisCycle = false;
    int   activeStateHash = -1;
    int   cycleAtStart = -1;
    float meleeClock = 0f;

    // player refs
    Transform           playerRoot;
    CharacterController playerCC;
    PlayerController    playerController;

    protected override void Awake()
    {
        base.Awake();

        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) target = go.transform;
        }

        if (target != null)
        {
            playerRoot = target.GetComponent<CharacterController>()
                ? target
                : target.GetComponentInParent<CharacterController>()
                    ? target.GetComponentInParent<CharacterController>().transform
                    : target.root;

            playerCC = playerRoot ? playerRoot.GetComponent<CharacterController>() : null;
            playerController = playerRoot ? playerRoot.GetComponent<PlayerController>() : null;
        }
    }

    // ===== movement intent per band  =====
    protected override bool TryGetGoal(out Vector3 goalWorld)
    {
        goalWorld = default;
        if (playerRoot == null) return false;

        float d = Vector3.Distance(transform.position, playerRoot.position);

        if (inMelee)                               // hard lock movement during attack
            return false;

        if (d <= attackRange)                      // hold & attack
            return false;

        if (d > attackRange && d <= advanceMax)    // chase
        {
            goalWorld = playerRoot.position;
            return true;
        }

        return false;
    }

    // ===== tick after nav =====
    protected override void AfterMove()
    {
        cooldownTimer -= Time.deltaTime;
        FaceTargetFlat();

        // --- drive locomotion animation when NOT attacking ---
        if (!inMelee && animator != null && !animator.IsInTransition(0))
        {
            float d = (playerRoot != null)
                ? Vector3.Distance(transform.position, playerRoot.position)
                : Mathf.Infinity;

            if (d > attackRange && d <= advanceMax)
            {
                // should be walking while we are chasing
                if (!string.IsNullOrEmpty(walkState) && !animator.GetCurrentAnimatorStateInfo(0).IsName(walkState))
                    animator.CrossFadeInFixedTime(walkState, 0.05f);
            }
            else if (d > advanceMax)
            {
                // idle when out of range
                if (!string.IsNullOrEmpty(idleState) && !animator.GetCurrentAnimatorStateInfo(0).IsName(idleState))
                    animator.CrossFadeInFixedTime(idleState, 0.05f);
            }
        }

        // --- attack state machine (ranged-style) ---
        if (inMelee)
        {
            DriveMeleeState();
            return;
        }

        if (CanStartMelee())
            BeginMelee();
    }

    // range-only start condition + cooldown. No LOS/transition gating beyond that.
    bool CanStartMelee()
    {
        if (cooldownTimer > 0f) return false;
        if (playerRoot == null || animator == null) return false;

        // don’t re-enter while already in the state / during transition
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(meleeState)) return false;
        if (animator.IsInTransition(0)) return false;

        float d = Vector3.Distance(transform.position, playerRoot.position);
        return d <= attackRange;
    }

    void BeginMelee()
    {
        inMelee = true;
        hitThisCycle = false;
        activeStateHash = -1;
        cycleAtStart = -1;
        meleeClock = 0f;

        animator.CrossFadeInFixedTime(meleeState, 0.05f);
    }

    void DriveMeleeState()
    {
        meleeClock += Time.deltaTime;

        var st = animator.GetCurrentAnimatorStateInfo(0);
        bool inOurState = st.IsName(meleeState);

        if (activeStateHash < 0)
        {
            if (!inOurState)
            {
                if (meleeClock > 0.5f) { EndMelee(); } 
                return;
            }
            activeStateHash = st.fullPathHash;
            cycleAtStart    = Mathf.FloorToInt(st.normalizedTime);
        }

        if (!inOurState || st.fullPathHash != activeStateHash)
        {
            EndMelee();
            return;
        }

        int   cycleNow = Mathf.FloorToInt(st.normalizedTime);
        float nt       = Mathf.Repeat(st.normalizedTime, 1f);

        if (!hitThisCycle && cycleNow == cycleAtStart && nt >= hitAtNormalized)
        {
            ApplyMeleeHit();
            hitThisCycle = true;
        }

        if (cycleNow > cycleAtStart || nt >= 0.999f || meleeClock >= maxMeleeDuration)
        {
            EndMelee();
        }
    }

    void ApplyMeleeHit()
    {
        if (playerRoot == null || playerCC == null) return;

        Vector3 origin = meleeOrigin != null ? meleeOrigin.position
                                             : transform.position + transform.forward * (attackRange * 0.5f);

        if (!CapsuleIntersectsSphere(playerCC, origin, meleeRadius)) { if (logDebug) Debug.Log($"{name}: Melee MISS", this); return; }

        // knockback
        Vector3 dir = playerRoot.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize(); dir += Vector3.up * Mathf.Clamp(verticalKnock, 0f, 0.5f);

        if (playerController != null) playerController.AddImpulse(dir * knockbackForce);
        else if (playerRoot.TryGetComponent<Rigidbody>(out var rb)) rb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);

        // damage
        var h = playerRoot.GetComponent<Health>();
        if (h != null) h.TakeDamage(Mathf.Max(1, meleeDamage), origin, -dir, this);

        if (logDebug) Debug.Log($"{name}: Melee HIT", this);
    }


    void EndMelee()
    {
        inMelee = false;
        hitThisCycle = false;
        activeStateHash = -1;
        cycleAtStart = -1;
        meleeClock = 0f;

        cooldownTimer = meleeCooldown;

        // return to idle immediately; AfterMove will switch to walk if we’re chasing
        if (!string.IsNullOrEmpty(idleState) && animator != null)
            animator.CrossFadeInFixedTime(idleState, 0.05f);
    }

    // ==== helpers ====

    static bool CapsuleIntersectsSphere(CharacterController cc, Vector3 sphereCenter, float sphereRadius)
    {
        if (cc == null) return false;

        Vector3 c = cc.transform.TransformPoint(cc.center);
        float hemi = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
        Vector3 bottom = c - Vector3.up * hemi;
        Vector3 top    = c + Vector3.up * hemi;

        Vector3 axis = top - bottom;
        float len2 = axis.sqrMagnitude;
        float t = 0f;
        if (len2 > 1e-6f)
            t = Mathf.Clamp01(Vector3.Dot(sphereCenter - bottom, axis) / len2);

        Vector3 closest = bottom + axis * t;
        float dist = Vector3.Distance(sphereCenter, closest);

        return dist <= (sphereRadius + cc.radius);
    }

    void FaceTargetFlat()
    {
        if (playerRoot == null) return;
        Vector3 to = playerRoot.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
        float step = 480f * Time.deltaTime;
        if (lookPivot != null)
            lookPivot.rotation = Quaternion.RotateTowards(lookPivot.rotation, look, step);
        else
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, step);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 o = meleeOrigin != null ? meleeOrigin.position
                                        : transform.position + transform.forward * (attackRange * 0.5f);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawSphere(o, meleeRadius);
    }
}
