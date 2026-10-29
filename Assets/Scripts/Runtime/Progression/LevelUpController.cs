using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LevelUpController : MonoBehaviour
{
    [SerializeField] private PlayerStats player;
    [SerializeField] private PlayerWeaponController inventory;
    [SerializeField] private RewardData settings;
    [SerializeField] private GamePause pause;
    private List<RewardOffer> offers = new List<RewardOffer>();
    private int pending;
    private bool suppressed;
    public IReadOnlyList<RewardOffer> Offers => offers;
    public bool IsChoosing => offers.Count > 0;
    public int SelectedIndex { get; private set; } = -1;
    public event Action Changed;
    public UnityEvent Opened = new UnityEvent();
    public UnityEvent Closed = new UnityEvent();
    private void OnEnable()
    {
        if (player != null)
            player.LevelChanged += Queue;
    }

    private void OnDisable()
    {
        if (player != null)
            player.LevelChanged -= Queue;
        if (pause != null)
            pause.Set(PauseReason.LevelUp, false);
    }

    private void Queue(int level)
    {
        if (!suppressed)
            pending++;
    }

    private void Update()
    {
        if (IsChoosing)
        {
            if (pause != null && (pause.Reasons & PauseReason.Menu) != 0)
                return;
            if (InputReader.Instance != null && InputReader.Instance.TryReadRewardInput(out int choice, out int movement, out bool confirm))
            {
                if (choice >= 0)
                    SelectChoice(choice);
                else if (movement != 0)
                    MoveSelection(movement);
                if (confirm)
                    ConfirmSelection();
            }
            return;
        }
        if (suppressed || pending == 0 || IsChoosing || player == null || player.IsDead)
            return;
        if (GameFlowController.Instance != null && !GameFlowController.Instance.AllowsRewards)
            return;
        offers = RewardCatalog.Draw(RewardCatalog.CreateCandidates(inventory, settings, false));
        pending--;
        if (offers.Count == 0)
            return;
        SelectedIndex = 0;
        pause.Set(PauseReason.LevelUp, true);
        Opened.Invoke();
        Changed?.Invoke();
    }

    public void Choose(int index)
    {
        if (index < 0 || index >= offers.Count || !offers[index].TryApply(player, inventory, false))
            return;
        offers.Clear();
        SelectedIndex = -1;
        if (pending == 0)
            pause.Set(PauseReason.LevelUp, false);
        Closed.Invoke();
        Changed?.Invoke();
    }

    public bool HasPending => pending > 0 || IsChoosing;
    public void SelectChoice(int index)
    {
        if (!IsChoosing || index < 0 || index >= Mathf.Min(3, offers.Count))
            return;
        SelectedIndex = index;
        Changed?.Invoke();
    }

    public void MoveSelection(int direction)
    {
        if (!IsChoosing)
            return;
        SelectChoice(Mathf.Clamp(SelectedIndex + direction, 0, Mathf.Min(3, offers.Count) - 1));
    }

    public void ConfirmSelection()
    {
        if (pause != null && (pause.Reasons & PauseReason.Menu) != 0)
            return;
        Choose(SelectedIndex);
    }

    public void SuppressAndClear()
    {
        suppressed = true;
        pending = 0;
        offers.Clear();
        SelectedIndex = -1;
        if (pause != null)
            pause.Set(PauseReason.LevelUp, false);
        Closed.Invoke();
        Changed?.Invoke();
    }
}
