using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    float t0;
    float baseY;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        t0 = Time.time;
        baseY = transform.position.y;
        mpb = mpb ?? new MaterialPropertyBlock();
        ApplyColor();
    }

    void Start()
    {
        if (autoDespawnSeconds > 0f)
            Destroy(gameObject, autoDespawnSeconds);
    }

    void Update()
    {
        float t = Time.time - t0;
        Vector3 p = transform.position;
        p.y = baseY + heightOffset + Mathf.Sin(t * bobSpeed) * bobAmplitude;
        transform.position = p;
        transform.Rotate(Vector3.up, spinDegPerSec * Time.deltaTime, Space.World);

        // exit-grid: auto-pickup by proximity
        if (isExitGridKey && PlayerWithinRadius(out _))
        {
            // consume on enter
            bool ok = ValidatePick(keyColor);
            Destroy(gameObject);

            if (!ok)
            {
                DespawnExitGrid();
                ResetBossDrops();
            }
            else
            {
                if (picksSoFar >= requiredPicks)
                {
                    GameObject door = GameObject.Find("DoorRoot");
                    if (door != null)
                    {
                        var dc = door.GetComponent<DoorController>();
                        if (dc != null)
                            dc.OpenDoor();
                    }
                }
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

    // color helpers
    public void SetColor(KeyColor c)
    {
        keyColor = c;
        ApplyColor();
    }

    void ApplyColor()
    {
        Color col = ColorFor(keyColor);
        for (int i = 0; i < colorRenderers.Count; i++)
        {
            var r = colorRenderers[i];
            if (r == null) continue;
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

    public void SetModeDropped()
    {
        isExitGridKey = false;           // dropped keys are NOT pickable
        // keep current heightOffset as-is (usually 0)
    }

    public void SetModeExitGrid()
    {
        isExitGridKey = true;            // grid keys are pickable
        heightOffset += 1f;              // raise ONLY the grid keys by +1
    }

    // exit grid
    static GameObject sharedKeyPrefab;
    public static void SetSharedPrefab(GameObject keyPrefab) => sharedKeyPrefab = keyPrefab;

    // large-enemy drops needed before the grid appears
    static int bossDrops = 0;
    public static int requiredBossDrops = 4;

    // 8 colors
    static readonly KeyColor[] allEight =
        { KeyColor.Red, KeyColor.Blue, KeyColor.Green, KeyColor.Purple, KeyColor.Pink, KeyColor.Orange, KeyColor.Black, KeyColor.White };

    // the 4 correct colors for THIS run
    static KeyColor[] runSolution = new KeyColor[4];

    static HashSet<KeyColor> correctSet = new();
    static int picksSoFar = 0;
    static int requiredPicks = 4;

    static List<KeyFloating> activeGrid = new();
    static Transform exitRoot; 

    public static void ResetBossDrops()
    {
        bossDrops = 0;
        ResetRunLargeDropColors(); // fresh unique set for next run attempt
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
        if (sharedKeyPrefab == null)
        {
            Debug.LogWarning("KeyFloating: shared key prefab not set. Call SetSharedPrefab(keyPrefab) before spawning the grid.");
            return;
        }

        if (exitRoot == null)
        {
            var rootGO = GameObject.Find("ExitGridRoot");
            if (rootGO == null)
            {
                Debug.LogWarning("Place an empty named 'ExitGridRoot' at the exit to position the 2×4 key grid.");
                return;
            }
            exitRoot = rootGO.transform;
        }

        // clears previous grid
        DespawnExitGrid();

        // the correct set IS the 4 unique large-drop colors for this run
        correctSet.Clear();
        picksSoFar = 0;
        requiredPicks = 4;
        for (int i = 0; i < 4; i++) correctSet.Add(runSolution[i]);

        // builds a 2×4 grid (one of each of the 8 colors)
        Vector3 basePos = exitRoot.position;
        Quaternion rot = exitRoot.rotation;

        // spreads keys further apart
        const float spacingX = 2.2f;
        const float spacingZ = 2.2f;

        for (int i = 0; i < allEight.Length; i++)
        {
            int row = i / 4;                           
            int col = i % 4;                         
            Vector3 offset = new((col - 1.5f) * spacingX, 0f, (row - 0.5f) * spacingZ);

            var go = Object.Instantiate(sharedKeyPrefab, basePos + offset, rot);
            var kf = go.GetComponent<KeyFloating>();
            if (kf != null)
            {
                kf.SetModeExitGrid();                  // makes them +1 higher and pickable by proximity
                kf.SetColor(allEight[i]);
                activeGrid.Add(kf);
            }
        }

        Debug.Log("[KeyPuzzle] Grid spawned. Correct colors = the 4 large drops from this run.");
    }

    static void DespawnExitGrid()
    {
        for (int i = 0; i < activeGrid.Count; i++)
        {
            if (activeGrid[i] != null)
                Object.Destroy(activeGrid[i].gameObject);
        }
        activeGrid.Clear();
    }

    // returns true if the pick is correct 
    static bool ValidatePick(KeyColor picked)
    {
        if (!correctSet.Contains(picked))
        {
            Debug.Log("[KeyPuzzle] WRONG key: " + picked);
            return false;
        }

        if (correctSet.Remove(picked))
        {
            picksSoFar++;
            Debug.Log($"[KeyPuzzle] Correct: {picked}. {picksSoFar}/{requiredPicks}");
        }
        return true;
    }

    static Queue<KeyColor> runLargeDropQueue;   // 4 unique colors in random order
    static bool runColorsReady = false;

    static void EnsureRunLargeDropColors()
    {
        if (runColorsReady && runLargeDropQueue != null && runLargeDropQueue.Count > 0) return;

        var bag = new List<KeyColor>(allEight);
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        // take the first 4 unique colors for this run
        runLargeDropQueue = new Queue<KeyColor>(4);
        for (int i = 0; i < 4; i++)
        {
            runLargeDropQueue.Enqueue(bag[i]);
            runSolution[i] = bag[i]; // store the solution set for the exit puzzle
        }

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
        if (runLargeDropQueue.Count == 0)
        {
            // Safety: reinitialize if empty
            ResetRunLargeDropColors();
        }
        return runLargeDropQueue.Dequeue();
    }
}
