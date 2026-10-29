using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CursorManager : MonoBehaviour
{
    private void Update()
    {
        ApplyCursorState();
    }

    private void LateUpdate()
    {
        ApplyCursorState();
    }

    private void ApplyCursorState()
    {
        bool gameplay = GameFlowController.Instance != null && GameFlowController.Instance.AllowsPlayerControl;
        bool menuOpen = GamePause.Instance != null && (GamePause.Instance.Reasons & PauseReason.Menu) != 0;
        CursorLockMode desiredState = Application.isFocused && gameplay && !menuOpen ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != desiredState)
        {
            InputReader.Instance?.SuppressLookInput();
            Cursor.lockState = desiredState;
        }
        Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
    }

    private void OnDisable()
    {
        InputReader.Instance?.SuppressLookInput();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
