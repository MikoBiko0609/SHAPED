using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public abstract class EnemyBaseNav : MonoBehaviour
{
    [Header("Refs")]
    public GridNav grid;
    public Transform target;
    public Transform lookPivot;             // rotate this if present, else rotate root
    public Animator animator;               // optional (Idle/Walking)

    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float repathInterval = 0.40f;
    public float waypointReachDist = 0.55f; // larger reach kills node ping-pong
    public float cornerReachDist = 0.50f;   // when we use far corner targets
    public float maxTurnDegPerSec = 360f;   // calmer turning

    [Header("Height Lock")]
    public bool lockY = true;
    public float lockedY = 0.89f;

    [Header("Anim Names")]
    public string idleState = "Idle";
    public string walkState = "Walking";
    public float walkAnimBaseSpeed = 3.5f;

    CharacterController cc;
    readonly List<Vector3> path = new();
    int wpIndex;
    float repathTimer;
    string curAnim = "";

    // “Sticky corner” so we don’t keep flipping between two nearby nodes
    int cornerIndex = -1;
    Vector3 cornerPoint;
    float cornerRecalcTimer = 0f;

    // Smoother, non-oscillating direction low-pass (no SmoothDamp velocity ping-pong)
    Vector3 dirSmooth = Vector3.forward;

    // Cache to avoid repath spam when goal barely moves
    Vector3 lastGoalRequested = new Vector3(9999, 9999, 9999);

    // Small constants
    const float EPS = 0.0001f;

    protected virtual void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (grid == null) grid = FindFirstObjectByType<GridNav>();
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
    }

    void OnEnable()
    {
        path.Clear();
        wpIndex = 0;
        cornerIndex = -1;
        dirSmooth = transform.forward.sqrMagnitude > EPS ? transform.forward : Vector3.forward;
        PlayIdle();
    }

    void Update()
    {
        if (grid == null || target == null) { PlayIdle(); HardLockY(); return; }

        // Ask child where it wants to go; false => hold position
        if (!TryGetGoal(out Vector3 goalWorld))
        {
            PlayIdle();
            HardLockY();
            AfterMove();
            return;
        }

        // Repath on timer or when goal moved enough
        repathTimer -= Time.deltaTime;
        bool goalShifted = (goalWorld - lastGoalRequested).sqrMagnitude > 1.0f * 1.0f; // >1m change
        if (repathTimer <= 0f || goalShifted || path.Count == 0)
        {
            repathTimer = repathInterval;
            lastGoalRequested = goalWorld;

            if (grid.FindPath(transform.position, goalWorld, path))
            {
                wpIndex = 0;
                // pick a far visible corner now and keep it “sticky”
                ChooseCornerTarget();
            }
            else
            {
                path.Clear();
            }
        }

        if (path.Count == 0) { PlayIdle(); HardLockY(); return; }

        // Refresh corner target occasionally to avoid going stale while moving
        cornerRecalcTimer -= Time.deltaTime;
        if (cornerRecalcTimer <= 0f) ChooseCornerTarget();

        Vector3 aim = cornerIndex >= 0 ? cornerPoint : path[wpIndex];
        aim.y = transform.position.y;

        Vector3 desired = (aim - transform.position);
        desired.y = 0f;
        if (desired.sqrMagnitude < 0.01f) // very close, advance corner/waypoint
        {
            // Advance waypoint if we’re near the current one
            if (wpIndex < path.Count)
            {
                if (Vector3.Distance(transform.position, path[wpIndex]) < waypointReachDist)
                    wpIndex = Mathf.Min(wpIndex + 1, path.Count - 1);
            }
            ChooseCornerTarget(); // refresh sticky corner
            PlayIdle();
            HardLockY();
            return;
        }
        desired.Normalize();

        // Direction low-pass without oscillation: exponential slerp
        float lerp = 1f - Mathf.Exp(-10f * Time.deltaTime); // snappy, stable
        dirSmooth = Vector3.Slerp(dirSmooth, desired, lerp);
        if (dirSmooth.sqrMagnitude > EPS) dirSmooth.Normalize();

        // Rotate only around Y
        Quaternion tRot = Quaternion.LookRotation(new Vector3(dirSmooth.x, 0f, dirSmooth.z), Vector3.up);
        float step = maxTurnDegPerSec * Time.deltaTime;
        if (lookPivot != null) lookPivot.rotation = Quaternion.RotateTowards(lookPivot.rotation, tRot, step);
        else                   transform.rotation = Quaternion.RotateTowards(transform.rotation, tRot, step);

        // Move forward
        Vector3 delta = dirSmooth * moveSpeed * Time.deltaTime;
        cc.Move(delta);

        // Reached the corner? advance it, not the tiny grid node
        if (cornerIndex >= 0 && Vector3.Distance(transform.position, cornerPoint) < cornerReachDist)
        {
            // push corner forward to the next far visible point
            AdvanceCornerTarget();
        }
        else
        {
            // Also advance raw wp if we get very near
            if (wpIndex < path.Count && Vector3.Distance(transform.position, path[wpIndex]) < waypointReachDist)
                wpIndex = Mathf.Min(wpIndex + 1, path.Count - 1);
        }

        HardLockY();
        PlayWalk();
        AfterMove();
    }

    // ---- Sticky corner logic ----
    void ChooseCornerTarget()
    {
        cornerIndex = -1;
        cornerPoint = Vector3.zero;
        cornerRecalcTimer = 0.25f; // don’t recalc every frame

        if (path.Count <= 1) return;

        // from current pos, find FARTHEST path point we have straight line of sight to
        Vector3 from = transform.position + Vector3.up * 0.5f;
        int best = -1;
        for (int i = path.Count - 1; i >= wpIndex; i--)
        {
            Vector3 to = path[i] + Vector3.up * 0.5f;
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.05f) { best = i; break; }
            dir /= len;
            if (!Physics.Raycast(from, dir, len - 0.05f, grid.obstacleMask, QueryTriggerInteraction.Ignore))
            {
                best = i;
                break;
            }
        }

        if (best >= 0)
        {
            cornerIndex = best;
            cornerPoint = path[best];
        }
    }

    void AdvanceCornerTarget()
    {
        if (cornerIndex < 0) { ChooseCornerTarget(); return; }

        // try push one farther if still visible
        int next = Mathf.Min(cornerIndex + 1, path.Count - 1);
        if (next == cornerIndex) { ChooseCornerTarget(); return; }

        Vector3 from = transform.position + Vector3.up * 0.5f;
        Vector3 to = path[next] + Vector3.up * 0.5f;
        Vector3 dir = to - from; float len = dir.magnitude; if (len > 0.01f) dir /= len;

        if (!Physics.Raycast(from, dir, len - 0.05f, grid.obstacleMask, QueryTriggerInteraction.Ignore))
        {
            cornerIndex = next;
            cornerPoint = path[next];
        }
        else
        {
            // can’t see next; just keep current or re-choose soon
            cornerRecalcTimer = 0.15f;
        }
    }

    protected virtual void AfterMove() { }

    void HardLockY()
    {
        if (!lockY) return;
        var p = transform.position; p.y = lockedY; transform.position = p;
    }

    void CrossfadeIfNew(string name, float speed = 1f, float xfade = 0.05f)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return;
        animator.speed = speed;
        if (curAnim == name) return;
        animator.CrossFadeInFixedTime(name, xfade);
        curAnim = name;
    }
    void PlayIdle() => CrossfadeIfNew(idleState, 1f);
    void PlayWalk() => CrossfadeIfNew(walkState, Mathf.Max(0.1f, moveSpeed / Mathf.Max(0.1f, walkAnimBaseSpeed)));

    /// <summary>Child returns a goal (world position) to move toward/away. Return false to stand still.</summary>
    protected abstract bool TryGetGoal(out Vector3 goalWorld);
}
