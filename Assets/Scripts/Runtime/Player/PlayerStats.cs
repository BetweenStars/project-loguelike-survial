using System;
using UnityEngine;
using UnityEngine.Events;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Starting Stats")]
    [SerializeField, Min(1)] private int baseMaxHp = 100;
    [SerializeField, Min(0)] private float baseMoveSpeed = 5f;
    [Header("Progression")]
    [SerializeField, Min(1)] private int firstLevelExperience = 30;
    [SerializeField, Min(1f)] private float experienceGrowth = 1.3f;
    [Header("Protection")]
    [SerializeField, Min(0)] private float hitProtectionDuration = 0.5f;
    public UnityEvent Damaged = new UnityEvent();
    public UnityEvent Died = new UnityEvent();
    public UnityEvent StatsChanged = new UnityEvent();
    public event Action<int> LevelChanged;
    public event Action<int> MaxHpChanged;
    public event Action<int> HpChanged;
    public event Action<float> MoveSpeedChanged;
    public event Action Changed;
    public event Action Death;
    public int Level { get; private set; } = 1;
    public int Exp { get; private set; }
    public int Money { get; private set; }
    public int MaxHp { get; private set; }
    public int Hp { get; private set; }
    public float SpeedBonusPercent { get; private set; }
    public float MoveSpeed => baseMoveSpeed * (1f + SpeedBonusPercent / 100f);
    public bool IsDead => Hp <= 0;
    public bool IsGrappleInvulnerable { get; private set; }
    public bool IsInvulnerable => IsGrappleInvulnerable || Time.time < protectedUntil;
    public int ExperienceRequired => Mathf.Max(1, (int)Math.Min(100000000, firstLevelExperience * Math.Pow(Math.Max(1f, experienceGrowth), Level - 1)));
    private float protectedUntil;

    private void Awake()
    {
        ResetStats();
    }

    public void ResetStats()
    {
        Level = 1;
        Exp = 0;
        Money = 0;
        SpeedBonusPercent = 0;
        MaxHp = Mathf.Max(1, baseMaxHp);
        Hp = MaxHp;
        protectedUntil = 0;
        IsGrappleInvulnerable = false;
        Notify();
    }

    public void SetGrappleInvulnerable(bool value)
    {
        IsGrappleInvulnerable = value;
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0 || float.IsNaN(damage) || IsDead || IsInvulnerable || GamePause.IsPaused)
            return;
        Hp = Mathf.Max(0, Hp - Mathf.CeilToInt(damage));
        protectedUntil = Time.time + hitProtectionDuration;
        HpChanged?.Invoke(Hp);
        Damaged.Invoke();
        Notify();
        if (IsDead)
        {
            Death?.Invoke();
            Died.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead || Hp >= MaxHp)
            return;
        Hp += Mathf.Min(amount, MaxHp - Hp);
        HpChanged?.Invoke(Hp);
        Notify();
    }

    public void IncreaseMaxHp(int amount)
    {
        if (amount <= 0)
            return;
        MaxHp += amount;
        MaxHpChanged?.Invoke(MaxHp);
        Notify();
    }

    public void IncreaseSpeed(float percent)
    {
        if (percent <= 0 || float.IsNaN(percent) || float.IsInfinity(percent))
            return;
        SpeedBonusPercent += percent;
        MoveSpeedChanged?.Invoke(MoveSpeed);
        Notify();
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0 || IsDead)
            return;
        long remaining = (long)Exp + amount;
        while (remaining >= ExperienceRequired)
        {
            remaining -= ExperienceRequired;
            Level++;
            LevelChanged?.Invoke(Level);
        }
        Exp = (int)remaining;
        Notify();
    }

    public void AddMoney(int amount)
    {
        if (amount > 0)
        {
            Money = (int)Math.Min(int.MaxValue, (long)Money + amount);
            Notify();
        }
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || Money < amount)
            return false;
        Money -= amount;
        Notify();
        return true;
    }

    private void Notify()
    {
        Changed?.Invoke();
        StatsChanged.Invoke();
    }
}
