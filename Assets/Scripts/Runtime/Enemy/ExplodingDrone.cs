using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Enemy), typeof(EnemyChase))]
public class ExplodingDrone : MonoBehaviour
{
    [SerializeField, Min(0)] private float triggerDistance = 2f;
    [SerializeField, Min(0)] private float explosionRadius = 3f;
    [SerializeField, Min(0)] private float fuseTime = 0.6f;
    [SerializeField, Min(0)] private float damage = 20f;
    public UnityEvent Armed = new UnityEvent();
    public UnityEvent Exploded = new UnityEvent();
    private Enemy enemy;
    private EnemyChase movement;
    private float timer = -1f;
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
        float distance = Vector3.Distance(transform.position, enemy.Target.transform.position);
        if (timer < 0)
        {
            if (distance > triggerDistance)
                return;
            timer = fuseTime;
            movement.Stop(true);
            Armed.Invoke();
        }
        timer -= Time.deltaTime;
        if (timer > 0)
            return;
        if (distance <= explosionRadius)
            enemy.Target.TakeDamage(damage);
        Exploded.Invoke();
        enemy.DespawnWithoutReward();
    }
}
