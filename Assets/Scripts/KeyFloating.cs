using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class KeyFloating : MonoBehaviour
{
    [Header("Visuals")]
    public KeyColor keyColor = KeyColor.Pink;
    public List<MeshRenderer> colorRenderers = new();
    public string colorProperty = "_Color"; 

    [Header("Float/Spin")]
    public float bobAmplitude = 0.15f;
    public float bobSpeed = 2f;
    public float spinDegPerSec = 90f;

    [Header("Visual Offset")]
    public float heightOffset = 0f;        

    [Header("Lifetime")]
    public float autoDespawnSeconds = 0f;

    [Header("Interaction")]
    public bool isExitGridKey = false;    
    public float pickupRadius = 1.3f;
    public string playerTag = "Player";

    float t0, baseY;
    MaterialPropertyBlock mpb;


    static readonly List<KeyFloating> activeDrops = new();
    public static void RegisterDrop(KeyFloating kf)
    {
        if (kf != null && !activeDrops.Contains(kf)) activeDrops.Add(kf);
    }
    static void ClearDroppedKeys()
    {
        for (int i = 0; i < activeDrops.Count; i++)
            if (activeDrops[i] != null) Object.Destroy(activeDrops[i].gameObject);
        activeDrops.Clear();
    }

    void Awake()
    {
        t0 = Time.time;
        baseY = transform.position.y;
        mpb = mpb ?? new MaterialPropertyBlock();
        ApplyColor();
    }

    void Start()
    {
        if (autoDespawnSeconds > 0f) Destroy(gameObject, autoDespawnSeconds);
    }

    void Update()
    {
        // float & spin
        float t = Time.time - t0;
        var p = transform.position;
        p.y = baseY + heightOffset + Mathf.Sin(t * bobSpeed) * bobAmplitude;
        transform.position = p;
        transform.Rotate(Vector3.up, spinDegPerSec * Time.deltaTime, Space.World);

        // Grid keys auto-pickup on proximity
        if (isExitGridKey && PlayerWithinRadius(out _))
        {
            bool ok = ValidatePick(keyColor);
            Destroy(gameObject);

            if (!ok)
            {
                // Full soft reset so player can re-run the encounter
                ResetRunWorld();
            }
            else if (picksSoFar >= requiredPicks)
            {
                // All 4 correct -> open door
                var door = GameObject.Find("DoorRoot");
                if (door != null && door.TryGetComponent<DoorController>(out var dc))
                    dc.OpenDoor();
            }
        }
    }

    bool PlayerWithinRadius(out Transform player)
    {
        player = null;
        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null) return false;
        player = go.transform;
        return (player.position - transform.position).sqrMagnitude <= pickupRadius * pickupRadius;
    }

    public void SetColor(KeyColor c) { keyColor = c; ApplyColor(); }

    void ApplyColor()
    {
        Color col = ColorFor(keyColor);
        for (int i = 0; i < colorRenderers.Count; i++)
        {
            var r = colorRenderers[i]; if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(colorProperty, col);
            r.SetPropertyBlock(mpb);
        }
    }

    public static KeyColor RandomColor()
    {
        int n = System.Enum.GetValues(typeof(KeyColor)).Length;
        return (KeyColor)Random.Range(0, n);
    }

    static Color ColorFor(KeyColor c) => c switch
    {
        KeyColor.Red    => new Color(0.95f, 0.15f, 0.15f),
        KeyColor.Blue   => new Color(0.20f, 0.45f, 1.00f),
        KeyColor.Green  => new Color(0.20f, 0.85f, 0.30f),
        KeyColor.Purple => new Color(0.60f, 0.20f, 0.85f),
        KeyColor.Pink   => new Color(1.00f, 0.35f, 0.70f),
        KeyColor.Orange => new Color(1.00f, 0.55f, 0.10f),
        KeyColor.Black  => new Color(0.05f, 0.05f, 0.05f),
        KeyColor.White  => Color.white,
        _ => Color.white
    };

    public void SetModeDropped() { isExitGridKey = false; }
    public void SetModeExitGrid() { isExitGridKey = true; heightOffset += 1f; }

    static GameObject sharedKeyPrefab;
    public static void SetSharedPrefab(GameObject keyPrefab) => sharedKeyPrefab = keyPrefab;

    static int bossDrops = 0;
    public static int requiredBossDrops = 4;

    static readonly KeyColor[] allEight =
        { KeyColor.Red, KeyColor.Blue, KeyColor.Green, KeyColor.Purple, KeyColor.Pink, KeyColor.Orange, KeyColor.Black, KeyColor.White };

    static KeyColor[] runSolution = new KeyColor[4];
    static HashSet<KeyColor> correctSet = new();
    static int picksSoFar = 0;
    static int requiredPicks = 4;

    static List<KeyFloating> activeGrid = new();
    static Transform exitRoot;

    public static void ResetBossDrops()
    {
        bossDrops = 0;
        ResetRunLargeDropColors(); // new 4-color code for the next attempt
    }

    public static void NotifyBossDrop()
    {
        bossDrops++;
        if (bossDrops >= requiredBossDrops)
        {
            bossDrops = requiredBossDrops;
            SpawnExitGrid();
        }
    }

    static void SpawnExitGrid()
    {
        if (sharedKeyPrefab == null) { Debug.LogWarning("KeyFloating: shared key prefab not set."); return; }

        if (exitRoot == null)
        {
            var rootGO = GameObject.Find("ExitGridRoot");
            if (rootGO == null) { Debug.LogWarning("Place an empty 'ExitGridRoot' to position the key grid."); return; }
            exitRoot = rootGO.transform;
        }

        DespawnExitGrid();

        correctSet.Clear();
        picksSoFar = 0;
        requiredPicks = 4;
        for (int i = 0; i < 4; i++) correctSet.Add(runSolution[i]);

        Vector3 basePos = exitRoot.position;
        Quaternion rot = exitRoot.rotation;
        const float spacingX = 2.2f, spacingZ = 2.2f;

        for (int i = 0; i < allEight.Length; i++)
        {
            int row = i / 4, col = i % 4;
            Vector3 offset = new((col - 1.5f) * spacingX, 0f, (row - 0.5f) * spacingZ);

            var go = Object.Instantiate(sharedKeyPrefab, basePos + offset, rot);
            var kf = go.GetComponent<KeyFloating>();
            if (kf != null)
            {
                kf.SetModeExitGrid();
                kf.SetColor(allEight[i]);
                activeGrid.Add(kf);
            }
        }

        Debug.Log("[KeyPuzzle] Grid spawned.");
    }

    static void DespawnExitGrid()
    {
        for (int i = 0; i < activeGrid.Count; i++)
            if (activeGrid[i] != null) Object.Destroy(activeGrid[i].gameObject);
        activeGrid.Clear();
    }

    static bool ValidatePick(KeyColor picked)
    {
        if (!correctSet.Contains(picked)) { Debug.Log("[KeyPuzzle] WRONG key: " + picked); return false; }
        if (correctSet.Remove(picked)) { picksSoFar++; Debug.Log($"[KeyPuzzle] Correct: {picked}. {picksSoFar}/{requiredPicks}"); }
        return true;
    }

    static Queue<KeyColor> runLargeDropQueue;
    static bool runColorsReady = false;

    static void EnsureRunLargeDropColors()
    {
        if (runColorsReady && runLargeDropQueue != null && runLargeDropQueue.Count > 0) return;

        var bag = new List<KeyColor>(allEight);
        for (int i = bag.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }

        runLargeDropQueue = new Queue<KeyColor>(4);
        for (int i = 0; i < 4; i++) { runLargeDropQueue.Enqueue(bag[i]); runSolution[i] = bag[i]; }
        runColorsReady = true;
    }

    static void ResetRunLargeDropColors()
    {
        runColorsReady = false;
        runLargeDropQueue = null;
        EnsureRunLargeDropColors();
    }

    public static KeyColor NextLargeDropColor()
    {
        EnsureRunLargeDropColors();
        if (runLargeDropQueue.Count == 0) ResetRunLargeDropColors();
        return runLargeDropQueue.Dequeue();
    }

    public static void ResetRunWorld()
    {
        // 1) remove puzzle grid
        DespawnExitGrid();

        // 2) remove previously dropped keys
        ClearDroppedKeys();

        // 3) clean up enemies & projectiles
        // kill all enemies
        foreach (var e in FindObjectsByType<EnemyBaseNav>(FindObjectsSortMode.None))
            if (e != null) Destroy(e.gameObject);

        // kill all projectiles
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            if (p != null) Destroy(p.gameObject);

        // kill all cube projectiles
        foreach (var c in FindObjectsByType<CubeProjectile>(FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);

        // 4) reset all spawners
        foreach (var sp in FindObjectsByType<EncounterSpawner>(FindObjectsSortMode.None))
            if (sp != null) sp.ResetAndRespawn();


        // 5) reset counters and choose a fresh code
        ResetBossDrops();
    }
}
