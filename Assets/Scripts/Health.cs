using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [Header("Health")]
    public int maxHP = 3;
    public bool destroyOnDeath = true;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDied;

    public int Current { get; private set; }
    public bool IsDead => Current <= 0;

    void Awake()
    {
        Current = Mathf.Max(1, maxHP);
    }

    public bool TakeDamage(int amount, Vector3 hitPoint = default, Vector3 hitNormal = default, Object source = null)
    {
        if (IsDead || amount <= 0) return false;

        Current = Mathf.Max(0, Current - amount);
        onDamaged?.Invoke();

        if (Current == 0)
        {
            onDied?.Invoke();

            if (destroyOnDeath)
                Destroy(gameObject);
        }
        return true;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead) return;
        Current = Mathf.Clamp(Current + amount, 1, maxHP);
    }
}
