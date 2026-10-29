using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[Serializable]
public class EnemyDropEntry
{
    public Pickup Prefab;
    [Range(0f, 100f)] public float Chance = 100f;

    public bool ShouldDrop(float roll)
    {
        return Prefab != null && Chance > 0f && (Chance >= 100f || roll < Chance / 100f);
    }
}

public class Enemy : MonoBehaviour, IDamageable
{
    public static readonly HashSet<Enemy> Active = new HashSet<Enemy>();
    [SerializeField, Min(1)] private float maxHealth = 30f;
    [SerializeField] private bool isBoss;
    [SerializeField] private List<EnemyDropEntry> drops = new List<EnemyDropEntry>();
    [SerializeField, Min(0f)] private float knockbackImmunityDuration = 0.4f;
    [Header("Feedbacks")]
    [SerializeField] private MMF_Player hitFeedback;
    [SerializeField] private MMF_Player deathFeedback;
    private static readonly HashSet<Enemy> dying = new HashSet<Enemy>();
    private float nextKnockbackTime;

    public bool TryKnockback(Vector3 direction, float speed, float duration)
    {
        if (IsDead || GamePause.IsPaused || Time.time < nextKnockbackTime)
            return false;
        EnemyChase chase = GetComponent<EnemyChase>();
        if (chase == null || !chase.TryKnockback(direction, speed, duration))
            return false;
        nextKnockbackTime = Time.time + Mathf.Max(duration, knockbackImmunityDuration);
        return true;
    }
    public UnityEvent Damaged = new UnityEvent();
    public UnityEvent Died = new UnityEvent();
    public UnityEvent<float> HealthChanged = new UnityEvent<float>();
    public event Action<Enemy> Killed;
    public PlayerStats Target { get; private set; }
    public bool IsBoss => isBoss;
    public bool IsDead { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public float SpeedMultiplier
    {
        get
        {
            float value = 1f;
            foreach (float slow in slows.Values)
                value = Mathf.Min(value, slow);
            return value;
        }
    }

    private readonly Dictionary<UnityEngine.Object, float> slows = new Dictionary<UnityEngine.Object, float>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Active.Clear();
        dying.Clear();
    }

    protected virtual void Awake()
    {
        Health = maxHealth;
    }

    protected virtual void OnEnable()
    {
        if (!IsDead)
            Active.Add(this);
    }

    protected virtual void OnDisable()
    {
        Active.Remove(this);
        dying.Remove(this);
    }

    public void Initialize(PlayerStats player, bool boss = false)
    {
        Target = player;
        isBoss = boss;
    }

    public void SetSlow(UnityEngine.Object source, float multiplier)
    {
        if (source != null)
            slows[source] = Mathf.Clamp01(multiplier);
    }

    public void RemoveSlow(UnityEngine.Object source)
    {
        if (source != null)
            slows.Remove(source);
    }

    public virtual void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage) || GamePause.IsPaused)
            return;
        Health = Mathf.Max(0, Health - damage);
        HealthChanged.Invoke(Health);
        if (hitFeedback != null)
        {
            hitFeedback.StopFeedbacks();
            hitFeedback.PlayFeedbacks(transform.position);
        }
        Damaged.Invoke();
        if (Health <= 0)
            Die();
    }

    public void Die()
    {
        if (IsDead)
            return;
        IsDead = true;
        Active.Remove(this);
        dying.Add(this);
        StopCombat();
        if (hitFeedback != null)
            hitFeedback.StopFeedbacks();
        DropItems();
        Killed?.Invoke(this);
        Died.Invoke();
        if (deathFeedback != null && deathFeedback.isActiveAndEnabled && gameObject.activeInHierarchy)
            StartCoroutine(PlayDeathFeedbackAndDestroy());
        else
            Destroy(gameObject);
    }

    private IEnumerator PlayDeathFeedbackAndDestroy()
    {
        yield return deathFeedback.PlayFeedbacksCoroutine(transform.position);
        Destroy(gameObject);
    }

    private void StopCombat()
    {
        // Keep renderers, animators and feedback components alive for the death sequence.
        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is EnemyChase || behaviour is ExplodingDrone || behaviour is ShootingDrone ||
                behaviour is BossController || behaviour is BossPattern)
            {
                behaviour.StopAllCoroutines();
                behaviour.enabled = false;
            }
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (NavMeshAgent agent in GetComponentsInChildren<NavMeshAgent>(true))
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;
            agent.enabled = false;
        }
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private void DropItems()
    {
        if (drops == null)
            return;
        foreach (EnemyDropEntry drop in drops)
        {
            if (drop == null || !drop.ShouldDrop(UnityEngine.Random.value))
                continue;
            Pickup pickup = Instantiate(drop.Prefab, transform.position + Vector3.up * 0.3f + UnityEngine.Random.insideUnitSphere * 0.2f, Quaternion.identity);
            pickup.Initialize(Target);
        }
    }

    private void StopAgent()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    public void DespawnWithoutReward()
    {
        IsDead = true;
        Active.Remove(this);
        StopAgent();
        Destroy(gameObject);
    }

    public static void ClearWithoutRewards()
    {
        var remaining = new HashSet<Enemy>(Active);
        remaining.UnionWith(dying);
        foreach (Enemy enemy in remaining)
            if (enemy != null)
                enemy.DespawnWithoutReward();
    }
}
