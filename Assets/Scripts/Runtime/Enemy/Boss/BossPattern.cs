using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public abstract class BossPattern : MonoBehaviour
{
    [SerializeField, Min(0)] private float selectionWeight = 1f;
    public float SelectionWeight => isActiveAndEnabled ? selectionWeight : 0;
    public UnityEvent Started = new UnityEvent();
    public UnityEvent Finished = new UnityEvent();
    public abstract IEnumerator Execute(Enemy boss, EnemyChase movement);
    public virtual void Cancel()
    {
    }
}
