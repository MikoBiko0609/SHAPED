using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class EncounterSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject smallEnemyPrefab;
    public GameObject largeEnemyPrefab;

    [Header("Counts per Spawn Point")]
    public int smallPerPoint = 4;
    public int largePerPoint = 1;

    [Header("Placement")]
    public float ringRadius = 1.5f;
    public float yOffset = 0.0f;
    public LayerMask groundMask = ~0;

    [Header("When to Spawn")]
    public bool spawnOnStart = false;
    public bool spawnOnPlayerEnter = true;
    public string playerTag = "Player";

    Transform[] points;
    bool spawned = false;
    Collider triggerCol;
    Transform player;

    void Awake()
    {
        // collect children as spawn points
        int childCount = transform.childCount;
        points = new Transform[childCount];
        for (int i = 0; i < childCount; i++) points[i] = transform.GetChild(i);

        triggerCol = GetComponent<Collider>();
        triggerCol.isTrigger = true;

        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Start()
    {
        var pGo = GameObject.FindGameObjectWithTag(playerTag);
        if (pGo != null) player = pGo.transform;

        if (spawnOnStart && !spawned) SpawnAll();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!spawnOnPlayerEnter || spawned) return;
        if (other.CompareTag(playerTag)) SpawnAll();
    }

    void OnTriggerStay(Collider other)
    {
        if (!spawnOnPlayerEnter || spawned) return;
        if (other.CompareTag(playerTag)) SpawnAll();
    }

    void Update()
    {
        if (!spawnOnPlayerEnter || spawned || player == null) return;
        if (triggerCol.bounds.Contains(player.position)) SpawnAll();
    }

    public void SpawnAll()
    {
        if (spawned) return;
        spawned = true;

        if (smallEnemyPrefab == null || largeEnemyPrefab == null)
        {
            Debug.LogError("EncounterSpawner: Assign both small & large enemy prefabs.", this);
            return;
        }

        foreach (var p in points)
        {
            if (p == null) continue;
            SpawnGroupAt(p.position, p.rotation);
        }
    }

    void SpawnGroupAt(Vector3 center, Quaternion rot)
    {
        // smalls around a ring
        for (int i = 0; i < smallPerPoint; i++)
        {
            float ang = (Mathf.PI * 2f) * (i / Mathf.Max(1f, (float)smallPerPoint));
            Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ringRadius;
            SpawnOne(smallEnemyPrefab, center + offset, rot);
        }

        // large near the middle
        Vector3 largePos = center + new Vector3(0.4f, 0f, -0.4f);
        for (int i = 0; i < Mathf.Max(1, largePerPoint); i++)
            SpawnOne(largeEnemyPrefab, largePos, rot);
    }

    void SpawnOne(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        Vector3 start = pos + Vector3.up * 2f;
        if (Physics.Raycast(start, Vector3.down, out var hit, 5f, groundMask, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        pos.y += yOffset;
        Instantiate(prefab, pos, rot);
    }

    // Called by the puzzle reset to make this encounter available again
    public void ResetAndRespawn()
    {
        spawned = false;
        SpawnAll();
    }
}
