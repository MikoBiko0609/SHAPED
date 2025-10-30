using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float jumpHeight = 1.2f;

    [Header("Look")]
    public float mouseSensitivity = 1.5f;
    public bool invertY = false;
    public float minPitch = -85f, maxPitch = 85f;

    [Header("Refs")]
    public Transform cameraPivot;     // Player/CameraPivot
    public Blaster blaster;           // child under GunRoot (auto-found)

    [Header("Gravity")]
    public float gravity = -20f;
    public float groundedGravity = -2f;

    [Header("External Forces")]
    public float impulseDamping = 6f;
    Vector3 externalVel;
    public void AddImpulse(Vector3 v) { externalVel += v; }

    // —— NEW ——
    [Header("Death / Respawn")]
    public float respawnDelay = 0.75f;
    Health health;

    CharacterController cc;
    Transform cam;
    float yaw, pitch;
    Vector3 vel;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        health = GetComponent<Health>();

        if (health == null)
        {
            Debug.LogError("PlayerController requires a Health component on the Player!");
        }
        else
        {
            // Listen for death event
            health.onDied.AddListener(OnPlayerDied);
        }

        // ---- AUTO-WIRE scene refs by name (no markers) ----
        if (cameraPivot == null)
        {
            var pivot = transform.Find("CameraPivot");
            if (pivot != null) cameraPivot = pivot;
        }

        if (cameraPivot != null)
        {
            var camTr = cameraPivot.Find("Main Camera");
            cam = camTr != null ? camTr : (Camera.main != null ? Camera.main.transform : null);
        }
        else cam = Camera.main != null ? Camera.main.transform : null;

        if (blaster == null && cameraPivot != null)
        {
            var gunRoot = cameraPivot.Find("GunRoot");
            if (gunRoot != null)
                blaster = gunRoot.GetComponentInChildren<Blaster>(true);
        }
        if (blaster == null)
            blaster = GetComponentInChildren<Blaster>(true);

        if (cam != null)
        {
            var e = (cameraPivot != null ? cameraPivot.rotation : cam.rotation).eulerAngles;
            yaw = e.y;
            pitch = e.x > 180 ? e.x - 360f : e.x;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // --- Look ---
        if (cameraPivot != null)
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? 1f : -1f);

            yaw += mx;
            pitch = Mathf.Clamp(pitch + my, minPitch, maxPitch);

            cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);

            var e = transform.eulerAngles;
            e.y = yaw;
            transform.eulerAngles = e;
        }

        // --- Move ---
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);

        Quaternion yawOnly = Quaternion.Euler(0f, yaw, 0f);
        Vector3 world = yawOnly * input * moveSpeed;

        bool grounded = cc.isGrounded;
        if (grounded && vel.y < 0f) vel.y = groundedGravity;
        if (grounded && Input.GetButtonDown("Jump"))
            vel.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        vel.y += gravity * Time.deltaTime;

        externalVel = Vector3.MoveTowards(externalVel, Vector3.zero, impulseDamping * Time.deltaTime);

        cc.Move((world + externalVel + vel) * Time.deltaTime);

        // --- Fire ---
        if (Input.GetButton("Fire1") && blaster != null)
            blaster.Blast();

        // toggle cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = !locked;
        }
    }

    // ——— NEW death handler ———
    void OnPlayerDied()
    {
        StartCoroutine(RespawnRoutine());
    }

    System.Collections.IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Make sure time is normal (in case pause/slowmo later)
        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
