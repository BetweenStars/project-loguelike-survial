using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum DamageTeam
{
    Player, Enemy
}

public class Projectile : MonoBehaviour
{
    public static readonly HashSet<Projectile> Active = new HashSet<Projectile>();
    [SerializeField, Min(0)] private float speed = 20f;
    [SerializeField, Min(0)] private float lifetime = 3f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.08f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private DamageTeam team;
    [SerializeField, Min(0)] private float damage = 10f;
    [SerializeField] private bool piercing;
    public UnityEvent Hit = new UnityEvent();
    public DamageTeam Team => team;
    private readonly HashSet<Component> hitTargets = new HashSet<Component>();
    private Vector3 moveDirection;
    private Transform owner;
    private float age;
    private bool finished;
    private float knockbackSpeed;
    private float knockbackDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Active.Clear();
    }

    private void Awake()
    {
        moveDirection = transform.forward;
    }

    private void OnEnable()
    {
        Active.Add(this);
        age = 0;
        finished = false;
        hitTargets.Clear();
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    public void Initialize(Vector3 direction)
    {
        moveDirection = direction.normalized;
    }

    public void Launch(Vector3 direction, float value, DamageTeam sourceTeam, Transform source, bool passThrough = false, float pushSpeed = 0f, float pushDuration = 0.15f)
    {
        Initialize(direction);
        damage = value;
        team = sourceTeam;
        owner = source;
        piercing = passThrough;
        knockbackSpeed = pushSpeed;
        knockbackDuration = pushDuration;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void Update()
    {
        if (finished || Time.deltaTime <= 0)
            return;
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Remove();
            return;
        }
        Vector3 start = transform.position;
        float distance = Mathf.Max(0, speed) * Time.deltaTime;
        // Check initial overlaps as well as the swept path to avoid tunnelling near the muzzle.
        foreach (Collider collider in Physics.OverlapSphere(start, hitRadius, collisionMask, QueryTriggerInteraction.Collide))
        {
            TryHit(collider);
            if (finished)
                return;
        }
        RaycastHit[] hits = Physics.SphereCastAll(start, hitRadius, moveDirection, distance, collisionMask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            TryHit(hit.collider);
            if (finished)
            {
                transform.position = start + moveDirection * hit.distance;
                return;
            }
        }
        transform.position += moveDirection * distance;
    }

    private void TryHit(Collider collider)
    {
        if (collider == null || collider.transform.IsChildOf(transform) || (owner != null && collider.transform.IsChildOf(owner)))
            return;
        if (collider.GetComponentInParent<Projectile>() != null || collider.GetComponentInParent<Pickup>() != null)
            return;
        Enemy enemy = collider.GetComponentInParent<Enemy>();
        PlayerStats player = collider.GetComponentInParent<PlayerStats>();
        Component target = team == DamageTeam.Player ? (Component)enemy : player;
        if (target != null)
        {
            if (!hitTargets.Add(target))
                return;
            ((IDamageable)target).TakeDamage(damage);
            if (team == DamageTeam.Player && enemy != null && knockbackSpeed > 0f)
                enemy.TryKnockback(moveDirection, knockbackSpeed, knockbackDuration);
            Hit.Invoke();
            if (!piercing)
                Remove();
            return;
        }
        if (enemy != null || player != null || collider.isTrigger)
            return;
        Hit.Invoke();
        Remove();
    }

    private void Remove()
    {
        finished = true;
        Active.Remove(this);
        Destroy(gameObject);
    }

    public static void ClearEnemyProjectiles()
    {
        foreach (Projectile projectile in new List<Projectile>(Active))
            if (projectile != null && projectile.team == DamageTeam.Enemy)
                projectile.Remove();
    }
}
