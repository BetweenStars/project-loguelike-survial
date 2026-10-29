using System;
using UnityEngine;
using UnityEngine.Events;

public abstract class Weapon : MonoBehaviour
{
    public const int MaxLevel = 7;
    [SerializeField] protected WeaponData weaponData;
    [Header("Knockback")]
    [SerializeField] protected bool canKnockback;
    [SerializeField, Min(0f)] private float knockbackSpeed = 8f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.15f;
    public float KnockbackSpeed => canKnockback ? knockbackSpeed : 0f;
    public float KnockbackDuration => knockbackDuration;
    public void ApplyKnockback(Enemy enemy)
    {
        if (canKnockback && enemy != null)
            enemy.TryKnockback(enemy.transform.position - DamageOwner.position, knockbackSpeed, knockbackDuration);
    }
    [Header("Runtime Level (0 = not owned)")]
    [SerializeField, Range(0, MaxLevel)] private int level;
    public int Level => level;
    public bool IsOwned => level > 0;
    public WeaponData Data => weaponData;
    public string DisplayName => weaponData != null && !string.IsNullOrEmpty(weaponData.DisplayName) ? weaponData.DisplayName : name;
    public event Action Changed;
    public UnityEvent Acquired = new UnityEvent();
    public UnityEvent Upgraded = new UnityEvent();
    public UnityEvent Fired = new UnityEvent();
    protected bool CanAttack => IsOwned && isActiveAndEnabled && !GamePause.IsPaused &&
        (GameFlowController.Instance == null || GameFlowController.Instance.IsCombat);
    protected Transform DamageOwner
    {
        get
        {
            PlayerStats player = GetComponentInParent<PlayerStats>();
            return player != null ? player.transform : transform;
        }
    }

    public abstract void Attack();
    public void ResetOwnership()
    {
        level = 0;
        gameObject.SetActive(false);
        Changed?.Invoke();
    }

    public void Acquire()
    {
        if (IsOwned)
            return;
        level = 1;
        gameObject.SetActive(true);
        ApplyLevel();
        Changed?.Invoke();
        Acquired.Invoke();
    }

    public virtual bool CanUpgrade()
    {
        return IsOwned && level < MaxLevel;
    }

    public virtual void Upgrade()
    {
        if (!CanUpgrade())
            return;
        level++;
        ApplyLevel();
        Changed?.Invoke();
        Upgraded.Invoke();
    }

    protected virtual void ApplyLevel()
    {
    }

    protected float LevelValue(float[] values, float fallback)
    {
        return values == null || values.Length == 0 ? fallback : values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
    }
}
