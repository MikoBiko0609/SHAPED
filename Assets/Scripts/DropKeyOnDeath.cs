using UnityEngine;

[DisallowMultipleComponent]
public class DropKeyOnDeath : MonoBehaviour
{
    [Header("Key Drop")]
    public GameObject keyPrefab;
    public bool isLargeEnemy = false;

    private Health hp;
    private bool hooked = false;

    void Awake()
    {
        hp = GetComponent<Health>();
        if (hp == null)
            Debug.LogWarning($"{name}: DropKeyOnDeath needs a Health component!");

        // Register key prefab once for the exit puzzle system
        if (keyPrefab != null)
            KeyFloating.SetSharedPrefab(keyPrefab);
    }

    void OnEnable()
    {
        if (hp != null && !hooked)
        {
            hp.onDied.AddListener(OnDied);
            hooked = true;
        }
    }

    void OnDisable()
    {
        if (hp != null && hooked)
        {
            hp.onDied.RemoveListener(OnDied);
            hooked = false;
        }
    }

    void OnDied()
    {
        SpawnKey();
        if (isLargeEnemy)
            KeyFloating.NotifyBossDrop();
    }

    void SpawnKey()
    {
        if (keyPrefab == null) return;

        // Your hardcoded vertical drop offset:
        Vector3 pos = transform.position + Vector3.up * -1f;
        var go = Instantiate(keyPrefab, pos, Quaternion.identity);

        var kf = go.GetComponent<KeyFloating>();
        if (kf != null)
        {
            kf.SetModeDropped();

            // Large enemies: use per-run unique sequence (no repeats, random order)
            if (isLargeEnemy)
            {
                KeyColor uniqueRunColor = KeyFloating.NextLargeDropColor();
                kf.SetColor(uniqueRunColor);
            }
            else
            {
                // Small enemies (if they ever drop): fully random
                kf.SetColor(KeyFloating.RandomColor());
            }
        }
    }
}
