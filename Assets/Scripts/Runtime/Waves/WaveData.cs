using System;
using UnityEngine;

[Serializable]
public class EnemySpawnEntry
{
    public Enemy Prefab;
    [Min(0)] public float Weight = 1f;
}
[CreateAssetMenu(fileName = "WaveData", menuName = "Survival/Wave Data")]
public class WaveData : ScriptableObject
{
    public string DisplayName = "Stage";
    public EnemySpawnEntry[] Enemies;
    [Min(0.01f)] public float SpawnInterval = 1f;
    [Min(1)] public int SpawnCount = 1;
    [Min(1)] public int MaxAliveEnemies = 100;
    [Min(0)] public float BossTime = 60f;
    public Enemy BossPrefab;
    public Enemy PickEnemy(float normalizedRoll)
    {
        if (Enemies == null)
            return null;
        float total = 0;
        foreach (EnemySpawnEntry entry in Enemies)
            if (entry != null && entry.Prefab != null)
                total += Mathf.Max(0, entry.Weight);
        if (total <= 0)
            return null;
        float roll = Mathf.Clamp(normalizedRoll, 0, 0.999999f) * total;
        foreach (EnemySpawnEntry entry in Enemies)
        {
            if (entry == null || entry.Prefab == null || entry.Weight <= 0)
                continue;
            roll -= entry.Weight;
            if (roll < 0)
                return entry.Prefab;
        }
        return null;
    }
}
