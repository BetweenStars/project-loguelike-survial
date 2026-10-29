using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    public static InputReader Instance { get; private set; }

    private GameInputActions inputActions;
    private readonly InputAction[] rewardChoices = new InputAction[3];
    private InputAction confirmReward;
    private InputAction previousChoice;
    private InputAction nextChoice;
    private InputAction scrollChoices;
    private int rewardInputFrame = -1;
    private int suppressLookThroughFrame = -1;
    private int suppressJumpThroughFrame = -1;

    public Vector2 Move => inputActions.Player.Move.ReadValue<Vector2>();
    public Vector2 Look => !isActiveAndEnabled || !Application.isFocused || GamePause.IsPaused ||
        Cursor.lockState != CursorLockMode.Locked || Time.frameCount <= suppressLookThroughFrame
        ? Vector2.zero : inputActions.Player.Look.ReadValue<Vector2>();
    public bool IsAttackHeld => inputActions.Player.Attack.IsPressed();
    public bool IsHookHeld => inputActions.Player.Hook.IsPressed();
    public bool HookPressed => inputActions.Player.Hook.WasPressedThisFrame();
    public bool JumpPressed => isActiveAndEnabled && !GamePause.IsPaused &&
        Time.frameCount > suppressJumpThroughFrame && rewardInputFrame != Time.frameCount &&
        inputActions.Player.Jump.WasPressedThisFrame();
    public bool InteractPressed => inputActions.Player.Interact.WasPressedThisFrame();
    public bool PausePressed => inputActions.UI.Pause.WasPressedThisFrame();

    private void Awake()
    {
        Instance = this;
        inputActions = new GameInputActions();
        InputActionMap ui = inputActions.UI.Get();
        for (int i = 0; i < rewardChoices.Length; i++)
            rewardChoices[i] = ui.FindAction("RewardChoice" + (i + 1), throwIfNotFound: true);
        confirmReward = inputActions.UI.ConfirmReward;
        previousChoice = inputActions.UI.Prev;
        nextChoice = inputActions.UI.Next;
        scrollChoices = inputActions.UI.ScrollChoices;
    }

    public bool TryReadRewardInput(out int choice, out int movement, out bool confirm)
    {
        choice = -1;
        movement = 0;
        confirm = false;
        if (rewardInputFrame == Time.frameCount || !isActiveAndEnabled)
            return false;
        for (int i = 0; i < rewardChoices.Length; i++)
            if (rewardChoices[i].WasPressedThisFrame())
            {
                choice = i;
                break;
            }
        confirm = confirmReward.WasPressedThisFrame();
        float scroll = scrollChoices.ReadValue<float>();
        bool previous = previousChoice.WasPressedThisFrame() || scroll > 0f;
        bool next = nextChoice.WasPressedThisFrame() || scroll < 0f;
        movement = (next ? 1 : 0) - (previous ? 1 : 0);
        if (choice < 0 && movement == 0 && !confirm)
            return false;
        // A confirmation must not reach another popup that opens in the same frame.
        rewardInputFrame = Time.frameCount;
        return true;
    }

    public void SuppressLookInput()
    {
        // Cursor recentering can arrive in the next input update, after the state change.
        suppressLookThroughFrame = Time.frameCount + 1;
    }

    public void SuppressJumpInput()
    {
        // Space used to close a reward window must not also trigger a jump.
        suppressJumpThroughFrame = Time.frameCount + 1;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SuppressLookInput();
        SuppressJumpInput();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        SuppressLookInput();
        SuppressJumpInput();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions.Dispose();
        if (Instance == this)
            Instance = null;
    }
}
