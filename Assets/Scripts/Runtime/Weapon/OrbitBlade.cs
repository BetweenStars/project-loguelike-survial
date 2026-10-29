using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class OrbitBlade : MonoBehaviour
{
    private OrbitWeapon owner;
    private Rigidbody body;
    private Collider hitbox;
    private float damage;
    private float interval;
    private readonly Dictionary<Enemy, float> nextHits = new Dictionary<Enemy, float>();
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        hitbox = GetComponent<Collider>();
        hitbox.isTrigger = true;
    }

    public void Initialize(OrbitWeapon source, float value, float cooldown)
    {
        owner = source;
        damage = value;
        interval = cooldown;
    }

    public void MoveTo(Vector3 position, bool active)
    {
        hitbox.enabled = active;
        body.MovePosition(position);
    }

    private void OnTriggerEnter(Collider other)
    {
        Hit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        Hit(other);
    }

    private void Hit(Collider other)
    {
        if (owner == null || !owner.CanDealDamage)
            return;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.IsDead || (nextHits.TryGetValue(enemy, out float next) && Time.time < next))
            return;
        nextHits[enemy] = Time.time + interval;
        enemy.TakeDamage(damage);
        owner.ApplyKnockback(enemy);
    }
}
