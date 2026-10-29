using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class WaveManager : MonoBehaviour
{
    [SerializeField] private PlayerStats player;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(0)] private float minimumSpawnDistance = 8f;
    [SerializeField, Min(0.01f)] private float navMeshSampleRadius = 2f;
    public UnityEvent BossAppeared = new UnityEvent();
    public UnityEvent WaveCompleted = new UnityEvent();
    public event Action<Vector3> BossDefeated;
    public event Action<Enemy> BossSpawned;
    public WaveData CurrentWave { get; private set; }
    public float Elapsed { get; private set; }
    public Enemy CurrentBoss { get; private set; }
    public bool Running { get; private set; }
    private bool bossSpawned;
    private float spawnTimer;
    private float retryBossAt;
    public void Begin(WaveData data)
    {
        Stop();
        CurrentWave = data;
        Elapsed = 0;
        spawnTimer = 0;
        retryBossAt = 0;
        bossSpawned = false;
        if (data == null || data.BossPrefab == null || player == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("WaveManager: assign Player, Spawn Points and WaveData with Boss Prefab.", this);
            return;
        }
        Running = true;
    }

    public void Stop()
    {
        Running = false;
        if (CurrentBoss != null)
            CurrentBoss.Killed -= OnBossKilled;
        CurrentBoss = null;
    }

    private void Update()
    {
        if (!Running || player.IsDead || Time.deltaTime <= 0)
            return;
        Elapsed += Time.deltaTime;
        if (Elapsed >= CurrentWave.BossTime)
        {
            if (!bossSpawned && Elapsed >= retryBossAt)
            {
                retryBossAt = Elapsed + 1f;
                if (TryPosition(out Vector3 position))
                {
                    bossSpawned = true;
                    CurrentBoss = Instantiate(CurrentWave.BossPrefab, position, Quaternion.identity);
                    CurrentBoss.Initialize(player, true);
                    CurrentBoss.Killed += OnBossKilled;
                    BossAppeared.Invoke();
                    BossSpawned?.Invoke(CurrentBoss);
                }
            }
            return;
        }
        spawnTimer += Time.deltaTime;
        if (spawnTimer < Mathf.Max(0.01f, CurrentWave.SpawnInterval))
            return;
        spawnTimer = 0;
        int alive = 0;
        foreach (Enemy enemy in Enemy.Active)
            if (enemy != null && !enemy.IsBoss && !enemy.IsDead)
                alive++;
        int count = Mathf.Min(CurrentWave.SpawnCount, Mathf.Max(0, CurrentWave.MaxAliveEnemies - alive));
        for (int i = 0; i < count; i++)
        {
            Enemy prefab = CurrentWave.PickEnemy(UnityEngine.Random.value);
            if (prefab == null || !TryPosition(out Vector3 position))
                continue;
            Enemy enemy = Instantiate(prefab, position, Quaternion.identity);
            enemy.Initialize(player);
        }
    }

    private bool TryPosition(out Vector3 position)
    {
        position = default;
        if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit playerGround, 30f, NavMesh.AllAreas))
            return false;
        int start = UnityEngine.Random.Range(0, spawnPoints.Length);
        var path = new NavMeshPath();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform point = spawnPoints[(start + i) % spawnPoints.Length];
            if (point == null || !NavMesh.SamplePosition(point.position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                continue;
            if (Vector3.Distance(hit.position, player.transform.position) < minimumSpawnDistance)
                continue;
            if (!NavMesh.CalculatePath(hit.position, playerGround.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            position = hit.position;
            return true;
        }
        return false;
    }

    private void OnBossKilled(Enemy boss)
    {
        if (!Running || boss != CurrentBoss)
            return;
        Vector3 position = boss.transform.position;
        Stop();
        WaveCompleted.Invoke();
        BossDefeated?.Invoke(position);
    }

    private void OnDisable()
    {
        Stop();
    }
}
