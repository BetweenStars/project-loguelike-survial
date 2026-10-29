using System.Collections.Generic;
using UnityEngine;

public class OrbitWeapon : Weapon
{
    [SerializeField] private OrbitBlade bladePrefab;
    [SerializeField, Min(0)] private float radius = 2f;
    [SerializeField] private float height = 1f;
    [SerializeField] private float degreesPerSecond = 180;
    [SerializeField, Min(0)] private float damage = 12f;
    [SerializeField, Min(0.01f)] private float hitInterval = 0.4f;
    [Header("Blade Count: Level 1 to 7")]
    [SerializeField] private int[] countByLevel = { 1, 2, 3, 4, 5, 6, 7 };
    private readonly List<OrbitBlade> blades = new List<OrbitBlade>();
    private float angle;
    private void Reset()
    {
        canKnockback = true;
    }
    private void OnEnable()
    {
        if (IsOwned)
            Rebuild();
    }

    private void OnDisable()
    {
        Clear();
    }

    protected override void ApplyLevel()
    {
        Rebuild();
    }

    public override void Attack()
    {
    }

    private void Rebuild()
    {
        Clear();
        if (bladePrefab == null || !IsOwned)
            return;
        int count = countByLevel.Length == 0 ? 1 : Mathf.Max(1, countByLevel[Mathf.Clamp(Level - 1, 0, countByLevel.Length - 1)]);
        for (int i = 0; i < count; i++)
        {
            OrbitBlade blade = Instantiate(bladePrefab, Position(i, count), Quaternion.identity);
            blade.Initialize(this, damage, hitInterval);
            blades.Add(blade);
        }
    }

    private Vector3 Position(int index, int count)
    {
        float radians = (angle + index * 360f / count) * Mathf.Deg2Rad;
        return transform.position + new Vector3(Mathf.Cos(radians) * radius, height, Mathf.Sin(radians) * radius);
    }

    private void FixedUpdate()
    {
        bool active = CanAttack;
        angle += active ? degreesPerSecond * Time.fixedDeltaTime : 0;
        for (int i = 0; i < blades.Count; i++)
            if (blades[i] != null)
                blades[i].MoveTo(Position(i, blades.Count), active);
    }

    private void Clear()
    {
        foreach (OrbitBlade blade in blades)
            if (blade != null)
            {
                blade.gameObject.SetActive(false);
                Destroy(blade.gameObject);
            }
        blades.Clear();
    }

    public bool CanDealDamage => CanAttack;
}
