using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Blaster : MonoBehaviour
{
    [Header("Stats")] public float projectileSpeed = 10f;
    public float cooldownPeriod = 0.25f;

    [Header("Prefabs")] public GameObject projectilePrefab;

    [Header("Spawn Point (Muzzle)")] public Transform projectileSpawnPoint;

    [Header("Audio")] public float pitchRange = 0.3f;

    bool coolingDown = false;
    AudioSource audioSource;

    void Awake() { audioSource = GetComponent<AudioSource>(); }

    public void Blast()
    {
        if (coolingDown || projectilePrefab == null || projectileSpawnPoint == null) return;
        coolingDown = true;

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.pitch = Random.Range(1f - pitchRange, 1f + pitchRange);
            audioSource.PlayOneShot(audioSource.clip);
        }

        GameObject projGO = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);

        var proj = projGO.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Shoot(projectileSpawnPoint.forward * projectileSpeed);
        }
        else if (projGO.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false; rb.useGravity = false;
            rb.linearDamping = 0f; rb.angularDamping = 0.05f;
            rb.linearVelocity = projectileSpawnPoint.forward * projectileSpeed;
        }

        StartCoroutine(CooldownRoutine());
    }

    IEnumerator CooldownRoutine()
    {
        yield return new WaitForSeconds(cooldownPeriod);
        coolingDown = false;
    }
}
