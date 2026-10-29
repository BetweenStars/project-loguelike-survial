using System.Collections;
using UnityEngine;

public class BossMissilePattern : BossPattern
{
    [SerializeField] private Projectile missilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(1)] private int missileCount = 4;
    [SerializeField, Min(0.01f)] private float interval = 0.25f;
    [SerializeField, Min(0)] private float damage = 18f;
    public override IEnumerator Execute(Enemy boss, EnemyChase movement)
    {
        movement.Stop(true);
        for (int i = 0; i < missileCount && !boss.IsDead && boss.Target != null; i++)
        {
            if (missilePrefab != null)
            {
                Transform muzzle = firePoint != null ? firePoint : transform;
                Vector3 direction = (boss.Target.transform.position + Vector3.up - muzzle.position).normalized;
                Projectile shot = Instantiate(missilePrefab, muzzle.position, Quaternion.LookRotation(direction));
                shot.Launch(direction, damage, DamageTeam.Enemy, boss.transform);
            }
            yield return new WaitForSeconds(interval);
        }
    }
}
