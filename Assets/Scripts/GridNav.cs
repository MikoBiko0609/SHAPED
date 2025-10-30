using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GridNav : MonoBehaviour
{
    [Header("Bounds (XZ)")] public Vector2 size = new(40, 40);
    public float cellSize = 0.5f;

    [Header("Obstacles")]
    public LayerMask obstacleMask;       // MUST be only your wall layers (not floor/player)
    public float clearanceHeight = 2f;   // vertical clearance checked per cell

    [Header("Debug")]
    public bool drawGizmos = true;

    // ----- Internal node -----
    class Node {
        public bool walk;
        public Vector3 w;
        public int x, y;
        public int g, h;
        public Node p;
        public int f => g + h;
    }

    Node[,] nodes;
    int cols, rows;
    float halfH;

    void Awake() => Build();

    // Build occupancy from colliders in obstacleMask
    public void Build()
    {
        cols = Mathf.Max(1, Mathf.RoundToInt(size.x / cellSize));
        rows = Mathf.Max(1, Mathf.RoundToInt(size.y / cellSize));
        nodes = new Node[cols, rows];
        halfH = clearanceHeight * 0.5f;

        Vector3 origin = transform.position - new Vector3(size.x, 0f, size.y) * 0.5f;

        for (int x = 0; x < cols; x++)
        for (int y = 0; y < rows; y++)
        {
            Vector3 p = origin + new Vector3((x + 0.5f) * cellSize, 0f, (y + 0.5f) * cellSize);
            bool blocked = Physics.CheckBox(
                p + Vector3.up * halfH,
                new Vector3(cellSize * 0.45f, halfH, cellSize * 0.45f),
                Quaternion.identity,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            nodes[x, y] = new Node { walk = !blocked, w = p, x = x, y = y, g = 0, h = 0, p = null };
        }
    }

    // Public path API (Vector3 → waypoints)
    public bool FindPath(Vector3 a, Vector3 b, List<Vector3> outPts)
    {
        outPts.Clear();
        if (nodes == null) return false;

        // Snap start to closest walkable if needed
        if (!WorldToNode(a, out var s) || !s.walk)
        {
            if (!ClosestWalkable(a, out var fixedStart)) return false;
            if (!WorldToNode(fixedStart, out s)) return false;
        }

        // Snap end to closest walkable if needed
        if (!WorldToNode(b, out var e) || !e.walk)
        {
            if (!ClosestWalkable(b, out var fixedGoal)) return false;
            if (!WorldToNode(fixedGoal, out e)) return false;
        }

        // Reset costs/parents
        foreach (var n in nodes) { n.g = 0; n.h = 0; n.p = null; }

        var open = new List<Node>(128);
        var closed = new HashSet<Node>();
        open.Add(s);

        // Local iterator with corner-cut prevention
        System.Collections.Generic.IEnumerable<Node> Nbs(Node n)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = n.x + dx, ny = n.y + dy;
                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;

                var c = nodes[nx, ny];
                if (!c.walk) continue;

                // Block diagonal corner cutting
                if (dx != 0 && dy != 0)
                {
                    var a1 = nodes[n.x + dx, n.y];
                    var b1 = nodes[n.x, n.y + dy];
                    if (!a1.walk || !b1.walk) continue;
                }
                yield return c;
            }
        }

        // A* loop
        while (open.Count > 0)
        {
            Node cur = open[0];
            for (int i = 1; i < open.Count; i++)
            {
                // Favor lower f, then lower h, then closer to end in euclidean (tiny tie-break)
                var n = open[i];
                if (n.f < cur.f || (n.f == cur.f && (n.h < cur.h ||
                    (n.h == cur.h && (n.x - e.x)*(n.x - e.x) + (n.y - e.y)*(n.y - e.y) <
                                               (cur.x - e.x)*(cur.x - e.x) + (cur.y - e.y)*(cur.y - e.y)))))
                {
                    cur = n;
                }
            }

            open.Remove(cur);
            closed.Add(cur);

            if (cur == e)
            {
                // Reconstruct
                var t = e;
                outPts.Add(t.w);
                while (t != s) { t = t.p; outPts.Add(t.w); }
                outPts.Reverse();

                // Smooth the path to kill zig-zags/spins
                SmoothCollinear(outPts);
                StraightenWithRaycasts(outPts);

                return true;
            }

            foreach (var nb in Nbs(cur))
            {
                if (closed.Contains(nb)) continue;

                int step = (nb.x == cur.x || nb.y == cur.y) ? 10 : 14; // straight/diag
                int gNew = cur.g + step;
                bool inOpen = open.Contains(nb);

                if (gNew < nb.g || !inOpen)
                {
                    nb.g = gNew;

                    // Octile heuristic (good for 8-connectivity grids)
                    int dx = Mathf.Abs(nb.x - e.x), dy = Mathf.Abs(nb.y - e.y);
                    nb.h = 14 * Mathf.Min(dx, dy) + 10 * Mathf.Abs(dx - dy);

                    nb.p = cur;
                    if (!inOpen) open.Add(nb);
                }
            }
        }

        return false;
    }

    // ---- Utility: find closest walkable node near a world pos ----
    public bool ClosestWalkable(Vector3 world, out Vector3 bestW, int maxRadiusCells = 8)
    {
        bestW = default;
        if (!WorldToNode(world, out var start)) return false;

        int sx = start.x, sy = start.y;
        // ring search expanding radius
        for (int r = 0; r <= maxRadiusCells; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                int nx = sx + dx, ny = sy + dy;
                if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                var cand = nodes[nx, ny];
                if (cand.walk) { bestW = cand.w; return true; }
            }
        }
        return false;
    }

    // ---- Helpers ----
    bool WorldToNode(Vector3 w, out Node n)
    {
        Vector3 local = w - (transform.position - new Vector3(size.x, 0f, size.y) * 0.5f);
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.z / cellSize);
        if (x < 0 || y < 0 || x >= cols || y >= rows) { n = null; return false; }
        n = nodes[x, y]; return true;
    }

    // Remove middle points that are nearly collinear (reduces tiny turns)
    void SmoothCollinear(List<Vector3> pts)
    {
        if (pts.Count <= 2) return;
        for (int i = pts.Count - 3; i >= 0; i--)
        {
            Vector2 a = new Vector2(pts[i + 1].x - pts[i].x,     pts[i + 1].z - pts[i].z).normalized;
            Vector2 b = new Vector2(pts[i + 2].x - pts[i + 1].x, pts[i + 2].z - pts[i + 1].z).normalized;
            if (Vector2.Dot(a, b) > 0.999f) pts.RemoveAt(i + 1);
        }
    }

    // Try to "see" ahead and skip intermediate waypoints when there's clear LoS
    void StraightenWithRaycasts(List<Vector3> pts)
    {
        if (pts.Count <= 2) return;

        // Cast slightly above ground to avoid floor hits; use the same obstacleMask
        float castHeight = 0.5f;
        int i = 0;
        while (i < pts.Count - 2)
        {
            Vector3 from = pts[i]   + Vector3.up * castHeight;
            // find farthest j we can see directly
            int far = -1;
            for (int j = pts.Count - 1; j > i + 1; j--)
            {
                Vector3 to = pts[j] + Vector3.up * castHeight;
                Vector3 dir = (to - from);
                float d = dir.magnitude;
                if (d <= 0.001f) continue;
                dir /= d;

                if (!Physics.Raycast(from, dir, d - 0.01f, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    far = j; break;
                }
            }

            if (far > 0)
            {
                // keep pts[i], pts[far], remove everything in between
                pts.RemoveRange(i + 1, far - i - 1);
            }
            else
            {
                i++;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || nodes == null) return;

        for (int x = 0; x < nodes.GetLength(0); x++)
        for (int y = 0; y < nodes.GetLength(1); y++)
        {
            var n = nodes[x, y];
            Gizmos.color = n.walk ? new Color(0f, 1f, 0f, 0.15f) : new Color(1f, 0f, 0f, 0.35f);
            Gizmos.DrawCube(n.w + Vector3.up * 0.01f, new Vector3(cellSize * 0.95f, 0.02f, cellSize * 0.95f));
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.02f, size.y));
    }
}
