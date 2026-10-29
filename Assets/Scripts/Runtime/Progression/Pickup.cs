using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum PickupKind
{
    Experience = 0, Money = 1, Health = 2
}
public class Pickup : MonoBehaviour
{
    public static readonly HashSet<Pickup> Active = new HashSet<Pickup>();
    [SerializeField] private PickupKind kind;
    [SerializeField, Min(1)] private int amount = 5;
    [SerializeField, Min(0)] private float attractionRadius = 3f;
    [SerializeField, Min(0.1f)] private float collectDistance = 0.6f;
    [SerializeField, Min(0)] private float attractionSpeed = 10f;
    public UnityEvent Collected = new UnityEvent();
    private PlayerStats target;
    private bool collected;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Active.Clear();
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    public void Initialize(PlayerStats player)
    {
        target = player;
    }

    private void Start()
    {
        if (target == null && GameFlowController.Instance != null)
            target = GameFlowController.Instance.Player;
    }

    private void Update()
    {
        if (collected || target == null || target.IsDead || Time.deltaTime <= 0)
            return;
        Vector3 destination = target.transform.position + Vector3.up * 0.5f;
        float distance = Vector3.Distance(transform.position, destination);
        if (distance <= collectDistance)
            Collect(target);
        else if (distance <= attractionRadius)
            transform.position = Vector3.MoveTowards(transform.position, destination, attractionSpeed * Time.deltaTime);
    }

    public void Collect(PlayerStats player)
    {
        if (collected || player == null || player.IsDead)
            return;
        collected = true;
        Active.Remove(this);
        switch (kind)
        {
            case PickupKind.Experience:
                player.AddExperience(amount);
                break;
            case PickupKind.Money:
                player.AddMoney(amount);
                break;
            case PickupKind.Health:
                player.Heal(amount);
                break;
        }
        Collected.Invoke();
        Destroy(gameObject);
    }

}
