using UnityEngine;

public class SlashWeapon : Weapon
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private PlayerMovement movement;
    [SerializeField, Min(0)] private float damage = 18f;
    [Header("Fire Interval: Level 1 to 7")]
    [SerializeField] private float[] intervalByLevel = { 2, 1.8f, 1.6f, 1.4f, 1.2f, 1, 0.75f };
    private float nextShot;
    private void Awake()
    {
        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();
    }

    private void Update()
    {
        Attack();
    }

    public override void Attack()
    {
        if (!CanAttack || projectilePrefab == null || Time.time < nextShot)
            return;
        Vector3 direction = firePoint != null ? firePoint.forward : transform.forward;
        Transform origin = firePoint != null ? firePoint : transform;
        Projectile shot = Instantiate(projectilePrefab, origin.position, Quaternion.LookRotation(direction));
        shot.Launch(direction, damage, DamageTeam.Player, DamageOwner, true, KnockbackSpeed, KnockbackDuration);
        nextShot = Time.time + Mathf.Max(0.05f, LevelValue(intervalByLevel, 2));
        Fired.Invoke();
    }
}
