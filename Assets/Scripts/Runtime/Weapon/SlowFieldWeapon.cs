using System.Collections.Generic;
using UnityEngine;

public class SlowFieldWeapon : Weapon
{
    [Header("Radius: Level 1 to 7")]
    [SerializeField] private float[] radiusByLevel = { 3, 3.5f, 4, 4.5f, 5, 5.5f, 6 };
    [SerializeField, Range(0, 1)] private float speedMultiplier = 0.5f;
    [SerializeField] private Transform fieldVisual;
    private readonly HashSet<Enemy> affected = new HashSet<Enemy>();
    public float Radius => LevelValue(radiusByLevel, 3);
    private void Update()
    {
        Attack();
    }

    public override void Attack()
    {
        if (!CanAttack)
        {
            ClearSlow();
            return;
        }
        foreach (Enemy enemy in Enemy.Active)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            if ((enemy.transform.position - transform.position).sqrMagnitude <= Radius * Radius)
            {
                enemy.SetSlow(this, speedMultiplier);
                if (affected.Add(enemy))
                    ApplyKnockback(enemy);
            }
            else if (affected.Remove(enemy))
                enemy.RemoveSlow(this);
        }
        affected.RemoveWhere(enemy => enemy == null || enemy.IsDead);
    }

    protected override void ApplyLevel()
    {
        if (fieldVisual != null)
            fieldVisual.localScale = new Vector3(Radius, 0.05f, Radius);
    }

    private void OnDisable()
    {
        ClearSlow();
    }

    private void ClearSlow()
    {
        foreach (Enemy enemy in affected)
            if (enemy != null)
                enemy.RemoveSlow(this);
        affected.Clear();
    }
}
