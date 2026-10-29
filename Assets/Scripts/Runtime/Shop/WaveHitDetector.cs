using System.Collections.Generic;
using UnityEngine;

public class WaveHitDetector : MonoBehaviour
{
    [SerializeField] private ParticleSystem waveVFX;
    private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[1];
    [SerializeField] private Transform center;
    [SerializeField] private SphereCollider hitCollider;

    [SerializeField] private LayerMask hitLayers;
    private readonly HashSet<Enemy> remainingEnemies = new HashSet<Enemy>();

    private void Awake()
    {
        if (hitCollider != null)
            hitCollider.enabled = false;
    }

    public void Initialize(IEnumerable<Enemy> enemies)
    {
        remainingEnemies.Clear();
        if (enemies != null)
            foreach (Enemy enemy in enemies)
                if (enemy != null && !enemy.IsDead)
                    remainingEnemies.Add(enemy);
        if (hitCollider != null)
            hitCollider.enabled = false;
    }

    private void Update()
    {
        if (hitCollider == null)
            return;
        if (waveVFX == null)
        {
            hitCollider.enabled = false;
            remainingEnemies.Clear();
            return;
        }
        int count = waveVFX.GetParticles(particles);

        hitCollider.enabled = count > 0 && remainingEnemies.Count > 0;
        if (count == 0)
        {
            if (!waveVFX.IsAlive())
                remainingEnemies.Clear();
            return;
        }

        float currentSize = particles[0].GetCurrentSize(waveVFX);
        hitCollider.radius = currentSize * 0.5f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitCollider == null || !hitCollider.enabled)
            return;
        if ((hitLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            Enemy enemy = other.GetComponentInParent<Enemy>();
            // Only remove enemies captured before the next wave started.
            if (enemy != null && remainingEnemies.Remove(enemy) && !enemy.IsDead)
            {
                enemy.Die();
            }
        }
    }

    private void OnDisable()
    {
        if (hitCollider != null)
            hitCollider.enabled = false;
        remainingEnemies.Clear();
    }
}
