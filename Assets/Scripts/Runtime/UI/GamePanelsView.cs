using UnityEngine;

public class GamePanelsView : MonoBehaviour
{
    [SerializeField] private GamePause pause;
    [SerializeField] private GameFlowController flow;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject clearPanel;
    [SerializeField] private GameObject hud;
    private void OnEnable()
    {
        if (pause != null)
            pause.Changed += Refresh;
        if (flow != null)
            flow.PhaseChanged += OnPhase;
        Refresh();
    }

    private void OnDisable()
    {
        if (pause != null)
            pause.Changed -= Refresh;
        if (flow != null)
            flow.PhaseChanged -= OnPhase;
    }

    private void OnPhase(GamePhase phase)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (pausePanel != null)
            pausePanel.SetActive(pause != null && (pause.Reasons & PauseReason.Menu) != 0);
        if (clearPanel != null)
            clearPanel.SetActive(flow != null && flow.CurrentPhase == GamePhase.Clear);
        if (hud != null)
            hud.SetActive(flow != null && flow.AllowsPlayerControl);
    }
}
