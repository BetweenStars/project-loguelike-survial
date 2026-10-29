using UnityEngine;
using MoreMountains.Feedbacks;
[DefaultExecutionOrder(100)]
public class MainWeapon : Weapon
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField]private MMF_Player fireFeedback;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform gunRoot;
    [SerializeField] private PlayerAim aim;
    [SerializeField, Min(0.01f)] private float fireInterval = 0.2f;
    [Header("Damage: Level 1 to 7")]
    [SerializeField] private float[] damageByLevel = { 10, 14, 18, 23, 28, 34, 42 };
    private float nextAttackTime;
    private void Awake()
    {
        if (aim == null)
            aim = GetComponentInParent<PlayerAim>();
    }

    private void Update()
    {
        if (InputReader.Instance != null && InputReader.Instance.IsAttackHeld)
            Attack();
    }

    private void LateUpdate()
    {
        if (CanAttack && aim != null)
            AimGun(aim.GetAimPoint());
    }

    private void AimGun(Vector3 aimPoint)
    {
        if (gunRoot == null)
            return;
        Vector3 direction = aimPoint - gunRoot.position;
        if (direction.sqrMagnitude > 0.0001f)
            gunRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    public override void Attack()
    {
        if (!CanAttack || Time.time < nextAttackTime || projectilePrefab == null)
            return;
        if (!projectilePrefab.TryGetComponent(out Projectile projectile))
            return;
        Vector3 aimPoint = aim != null ? aim.GetAimPoint() : Vector3.zero;
        if (aim != null)
            AimGun(aimPoint);
        Transform spawn = firePoint != null ? firePoint : transform;
        Vector3 direction = aim != null ? aimPoint - spawn.position : spawn.forward;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : spawn.forward;
        Projectile shot = Instantiate(projectile, spawn.position, Quaternion.LookRotation(direction));
        shot.Launch(direction, LevelValue(damageByLevel, 10), DamageTeam.Player, DamageOwner, false, KnockbackSpeed, KnockbackDuration);
        nextAttackTime = Time.time + Mathf.Max(0.01f, fireInterval);
        Fired.Invoke();

        fireFeedback?.PlayFeedbacks();
    }
}
