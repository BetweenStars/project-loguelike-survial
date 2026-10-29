using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Enemy), typeof(EnemyChase))]
public class BossController : MonoBehaviour
{
    [SerializeField] private BossPattern[] patterns;
    [SerializeField, Min(0)] private float restTime = 1.5f;
    private Enemy boss;
    private EnemyChase movement;
    private BossPattern current;
    private void Awake()
    {
        boss = GetComponent<Enemy>();
        movement = GetComponent<EnemyChase>();
    }

    private IEnumerator Start()
    {
        if (patterns == null || patterns.Length == 0)
            patterns = GetComponents<BossPattern>();
        while (!boss.IsDead)
        {
            yield return new WaitForSeconds(restTime);
            if (boss.Target == null || boss.Target.IsDead || (GameFlowController.Instance != null && !GameFlowController.Instance.IsCombat))
                continue;
            float total = 0;
            foreach (BossPattern pattern in patterns)
                if (pattern != null)
                    total += pattern.SelectionWeight;
            if (total <= 0)
                continue;
            float roll = Random.value * total;
            current = null;
            foreach (BossPattern pattern in patterns)
            {
                if (pattern == null || pattern.SelectionWeight <= 0)
                    continue;
                roll -= pattern.SelectionWeight;
                if (roll <= 0)
                {
                    current = pattern;
                    break;
                }
            }
            if (current == null)
                continue;
            current.Started.Invoke();
            yield return current.Execute(boss, movement);
            current.Finished.Invoke();
            current = null;
            movement.Stop(false);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (current != null)
            current.Cancel();
    }
}
