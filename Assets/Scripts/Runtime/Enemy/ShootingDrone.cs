using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Enemy), typeof(EnemyChase))]
public class ShootingDrone : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0)] private float shootingRange = 12f;
    [SerializeField, Min(0.05f)] private float fireInterval = 1.5f;
    [SerializeField, Min(0)] private float damage = 10f;
    public UnityEvent Fired = new UnityEvent();
    private Enemy enemy;
    private EnemyChase movement;
    private float nextShot;
    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        movement = GetComponent<EnemyChase>();
    }

    private void Update()
    {
        if (enemy.IsDead || enemy.Target == null || enemy.Target.IsDead || Time.deltaTime <= 0)
            return;
        if (GameFlowController.Instance != null && !GameFlowController.Instance.IsCombat)
            return;
        bool inRange = Vector3.Distance(transform.position, enemy.Target.transform.position) <= shootingRange;
        movement.Stop(inRange);
        if (!inRange || Time.time < nextShot || projectilePrefab == null)
            return;
        Transform origin = firePoint != null ? firePoint : transform;
        Vector3 direction = (enemy.Target.transform.position + Vector3.up - origin.position).normalized;
        Projectile shot = Instantiate(projectilePrefab, origin.position, Quaternion.LookRotation(direction));
        shot.Launch(direction, damage, DamageTeam.Enemy, transform);
        nextShot = Time.time + fireInterval;
        Fired.Invoke();
    }
}
