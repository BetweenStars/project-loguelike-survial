using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Enemy))]
public class EnemyChase : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0)] private float moveSpeed = 3f;
    [SerializeField, Min(0)] private float stoppingDistance = 1f;
    private Enemy enemy;
    private NavMeshAgent agent;
    private bool externallyStopped;
    private Vector3 knockbackVelocity;
    private float knockbackRemaining;
    public bool IsKnockedBack => knockbackRemaining > 0f;

    public bool TryKnockback(Vector3 direction, float speed, float duration)
    {
        direction.y = 0f;
        if (!isActiveAndEnabled || agent == null || !agent.enabled || !agent.isOnNavMesh ||
            speed <= 0f || duration <= 0f || float.IsNaN(speed) || float.IsInfinity(speed) ||
            float.IsNaN(duration) || float.IsInfinity(duration) || direction.sqrMagnitude < 0.0001f ||
            float.IsNaN(direction.sqrMagnitude) || float.IsInfinity(direction.sqrMagnitude))
            return false;
        knockbackVelocity = direction.normalized * speed;
        knockbackRemaining = duration;
        return true;
    }

    private void OnDisable()
    {
        knockbackRemaining = 0f;
        knockbackVelocity = Vector3.zero;
    }
    public NavMeshAgent Agent => agent;
    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        agent = GetComponent<NavMeshAgent>();
        if (enemy == null || agent == null)
        {
            Debug.LogError("EnemyChase requires Enemy and NavMeshAgent on the same object.", this);
            enabled = false;
        }
    }

    public void Stop(bool stop)
    {
        externallyStopped = stop;
    }

    private void Update()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
            return;
        Transform destination = enemy.Target != null ? enemy.Target.transform : target;
        if (!enemy.IsDead && !GamePause.IsPaused && Time.deltaTime > 0f &&
            (GameFlowController.Instance == null || GameFlowController.Instance.IsCombat) && IsKnockedBack)
        {
            agent.isStopped = true;
            float dt = Mathf.Min(Time.deltaTime, knockbackRemaining);
            Vector3 displacement = knockbackVelocity * (dt - dt * dt / (2f * knockbackRemaining));
            Vector3 next = transform.position + displacement;
            if (agent.Raycast(next, out NavMeshHit boundary))
                next = boundary.position;
            agent.Move(next - transform.position);
            knockbackVelocity *= 1f - dt / knockbackRemaining;
            knockbackRemaining = Mathf.Max(0f, knockbackRemaining - dt);
            return;
        }
        bool stop = externallyStopped || enemy.IsDead || GamePause.IsPaused || destination == null ||
            (GameFlowController.Instance != null && !GameFlowController.Instance.IsCombat);
        agent.isStopped = stop;
        if (stop)
            return;
        agent.speed = moveSpeed * enemy.SpeedMultiplier;
        agent.stoppingDistance = stoppingDistance;
        // Ground drones follow the nearest walkable position while the player grapples above them.
        if (NavMesh.SamplePosition(destination.position, out NavMeshHit hit, 30f, agent.areaMask))
            agent.SetDestination(hit.position);
    }
}
