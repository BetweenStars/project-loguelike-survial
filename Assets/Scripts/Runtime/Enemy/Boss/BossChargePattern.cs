using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class BossChargePattern : BossPattern
{
    [SerializeField, Min(0)] private float windup = 0.7f;
    [SerializeField, Min(0.01f)] private float duration = 0.65f;
    [SerializeField, Min(0)] private float speed = 18f;
    [SerializeField, Min(0)] private float damage = 30f;
    [SerializeField, Min(0)] private float hitRadius = 1.8f;
    public UnityEvent Telegraph = new UnityEvent();
    public override IEnumerator Execute(Enemy boss, EnemyChase movement)
    {
        movement.Stop(true);
        Telegraph.Invoke();
        if (boss.Target == null)
            yield break;
        Vector3 direction = boss.Target.transform.position - transform.position;
        direction.y = 0;
        direction.Normalize();
        yield return new WaitForSeconds(windup);
        NavMeshAgent agent = movement.Agent;
        bool hitPlayer = false;
        for (float elapsed = 0; elapsed < duration && !boss.IsDead; elapsed += Time.deltaTime)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                yield break;
            Vector3 from = agent.nextPosition;
            Vector3 destination = from + direction * speed * Time.deltaTime;
            bool blocked = NavMesh.Raycast(from, destination, out NavMeshHit hit, agent.areaMask);
            if (blocked)
                destination = hit.position;
            agent.Move(destination - from);
            if (!hitPlayer && boss.Target != null && DistanceToSegment(boss.Target.transform.position, from, destination) <= hitRadius)
            {
                boss.Target.TakeDamage(damage);
                hitPlayer = true;
            }
            if (blocked)
                yield break;
            yield return null;
        }
    }

    private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0;
        return Vector3.Distance(point, a + ab * t);
    }
}
