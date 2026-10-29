using System;
using UnityEngine;
using UnityEngine.Events;

[Flags]
public enum PauseReason
{
    None = 0, Menu = 1, LevelUp = 2, Shop = 4
}

public class GamePause : MonoBehaviour
{
    public static GamePause Instance { get; private set; }
    public static bool IsPaused => Instance != null && Instance.Reasons != PauseReason.None;
    public PauseReason Reasons { get; private set; }
    public event Action Changed;
    public UnityEvent<bool> PausedChanged = new UnityEvent<bool>();

    private void Awake()
    {
        Instance = this;
        Reasons = PauseReason.None;
        Time.timeScale = 1f;
    }

    public void Set(PauseReason reason, bool paused)
    {
        PauseReason previousReasons = Reasons;
        Reasons = paused ? Reasons | reason : Reasons & ~reason;
        if (Reasons != previousReasons)
        {
            InputReader.Instance?.SuppressLookInput();
            InputReader.Instance?.SuppressJumpInput();
        }
        Time.timeScale = Reasons == PauseReason.None ? 1f : 0f;
        Changed?.Invoke();
        PausedChanged.Invoke(Reasons != PauseReason.None);
    }

    public void ToggleMenu()
    {
        Set(PauseReason.Menu, (Reasons & PauseReason.Menu) == 0);
    }

    public void Resume()
    {
        Set(PauseReason.Menu, false);
    }

    public void Clear()
    {
        Set(Reasons, false);
    }

    private void Update()
    {
        if (InputReader.Instance != null && InputReader.Instance.PausePressed &&
            (GameFlowController.Instance == null || GameFlowController.Instance.CanPause))
            HandleEscape();
    }

    public void HandleEscape()
    {
        if ((Reasons & PauseReason.Menu) != 0)
        {
            Resume();
            return;
        }
        if ((Reasons & PauseReason.Shop) != 0)
        {
            GameFlowController.Instance?.Shop?.Exit();
            return;
        }
        ToggleMenu();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f;
        }
    }
}
