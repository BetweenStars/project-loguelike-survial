using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerStats))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float gravity = -20f;
    [SerializeField, Min(0f)] private float jumpHeight = 2f;
    [SerializeField, Min(0)] private float inertiaDecay = 8f;
    [Header("Rotation")]
    [Tooltip("Player child pivot used as the Cinemachine Camera Tracking Target, not the Main Camera.")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField, Min(0)] private float lookSensitivity = 0.1f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    private PlayerStats playerStats;
    private CharacterController controller;
    private GrappleController grapple;
    private float verticalVelocity;
    private float cameraPitch;
    private Vector3 inertia;
    public Vector3 LastMoveDirection { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        controller = GetComponent<CharacterController>();
        grapple = GetComponent<GrappleController>();
        LastMoveDirection = transform.forward;
        cameraPitch = cameraTarget != null ? Mathf.DeltaAngle(0, cameraTarget.localEulerAngles.x) : 0;
    }

    public void AddInertia(Vector3 velocity)
    {
        inertia = velocity;
    }

    public void ResetMotion()
    {
        verticalVelocity = 0;
        inertia = Vector3.zero;
        grapple?.CancelForTransition();
    }

    private void Update()
    {
        if (GamePause.IsPaused || playerStats.IsDead || (GameFlowController.Instance != null && !GameFlowController.Instance.AllowsPlayerControl))
            return;
        InputReader input = InputReader.Instance;
        if (input == null)
            return;
        Rotate(input.Look);
        float dt = Time.deltaTime;
        Vector3 velocity = Vector3.zero;
        bool pulling = grapple != null && grapple.Tick(dt, input.HookPressed, input.IsHookHeld, out velocity);
        if (pulling)
        {
            verticalVelocity = 0;
            inertia = Vector3.zero;
        }
        else
        {
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1);
            velocity = (transform.right * move.x + transform.forward * move.y) * playerStats.MoveSpeed;
            if (controller.isGrounded && verticalVelocity < 0)
                verticalVelocity = -2;
            if (controller.isGrounded && input.JumpPressed && jumpHeight > 0f && gravity < 0f)
            {
                verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                inertia.y = 0f;
            }
            verticalVelocity += gravity * dt;
            velocity += Vector3.up * verticalVelocity + inertia;
            inertia = Vector3.MoveTowards(inertia, Vector3.zero, inertiaDecay * dt);
        }
        Vector3 before = transform.position;
        CollisionFlags flags = controller.Move(velocity * dt);
        Vector3 delta = transform.position - before;
        delta.y = 0;
        if (delta.sqrMagnitude > 0.000001f)
            LastMoveDirection = delta.normalized;
        if ((flags & CollisionFlags.Above) != 0)
        {
            verticalVelocity = Mathf.Min(0, verticalVelocity);
            inertia.y = Mathf.Min(0, inertia.y);
            if (pulling)
                grapple.Blocked();
        }
        if ((flags & CollisionFlags.Sides) != 0)
        {
            inertia.x = 0;
            inertia.z = 0;
            if (pulling)
                grapple.Blocked();
        }
        if ((flags & CollisionFlags.Below) != 0 && inertia.y < 0)
            inertia.y = 0;
    }

    private void Rotate(Vector2 look)
    {
        transform.Rotate(0, look.x * lookSensitivity, 0);
        cameraPitch = Mathf.Clamp(cameraPitch - look.y * lookSensitivity, minPitch, maxPitch);
        if (cameraTarget != null)
            cameraTarget.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
    }
}
