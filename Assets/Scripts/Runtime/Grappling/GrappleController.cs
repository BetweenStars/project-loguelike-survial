using System;
using UnityEngine;
using UnityEngine.Events;

public enum GrappleState
{
    Ready, Flying, Attached, Pulling, Cooldown
}

[RequireComponent(typeof(PlayerMovement), typeof(PlayerStats), typeof(PlayerAim))]
public class GrappleController : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private Transform launchPoint;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private LayerMask grappleLayers;
    [SerializeField, Min(1)] private float maxDistance = 35f;
    [SerializeField, Min(0f)] private float targetGraceTime = 0.2f;
    [SerializeField, Min(0.1f)] private float arrivalDistance = 1.2f;
    [Header("Hook Flight")]
    [SerializeField] private Transform hookVisualPrefab;
    [SerializeField] private LineRenderer rope;
    [SerializeField, Min(0.01f)] private float flightDuration = 0.25f;
    [SerializeField] private AnimationCurve flightCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));
    [SerializeField, Min(0)] private float attachmentDelay = 0.05f;
    [Header("Pull / Ground Drag / Lift")]
    [SerializeField, Min(0)] private float pullSpeed = 18f;
    [SerializeField, Min(0.01f)] private float accelerationTime = 0.2f;
    [SerializeField, Min(0)] private float groundDragTime = 0.15f;
    [SerializeField, Min(0.01f)] private float liftTime = 0.3f;
    [SerializeField] private AnimationCurve liftCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField, Range(0, 1)] private float releaseInertia = 0.25f;
    [Header("Protection / Cooldown")]
    [SerializeField] private bool invulnerableEntirePull = true;
    [SerializeField, Min(0)] private float initialProtectionTime = 0.3f;
    [SerializeField, Min(0)] private float cooldown = 2f;
    public UnityEvent Fired = new UnityEvent();
    public UnityEvent Attached = new UnityEvent();
    public UnityEvent PullStarted = new UnityEvent();
    public UnityEvent Released = new UnityEvent();
    public UnityEvent<float> CooldownChanged = new UnityEvent<float>();
    public event Action StateChanged;
    public GrappleState State { get; private set; }
    public float CooldownRemaining { get; private set; }
    public bool CanFire => TryGetFireTarget(out _);
    private Vector3 LaunchPosition => launchPoint != null ? launchPoint.position : transform.position + Vector3.up;
    private PlayerAim aim;
    private PlayerStats stats;
    private PlayerMovement movement;
    private Transform hook;
    private Collider targetCollider;
    private Vector3 flightStart;
    private Vector3 targetPoint;
    private Vector3 hookPosition;
    private Vector3 lastVelocity;
    private float phaseTime;
    private bool groundLaunch;
    private RaycastHit lastValidHit;
    private Vector3 lastValidLocalPoint;
    private float lastValidTime = float.NegativeInfinity;
    private void Awake()
    {
        aim = GetComponent<PlayerAim>();
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();
    }

    private void SetState(GrappleState value)
    {
        State = value;
        if (value != GrappleState.Ready)
            ClearRememberedTarget();
        phaseTime = 0;
        StateChanged?.Invoke();
    }

    public bool Tick(float dt, bool pressed, bool held, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        if (dt <= 0)
            return false;
        if (State == GrappleState.Cooldown)
        {
            CooldownRemaining = Mathf.Max(0, CooldownRemaining - dt);
            CooldownChanged.Invoke(CooldownRemaining);
            if (CooldownRemaining == 0)
                SetState(GrappleState.Ready);
        }
        if (State == GrappleState.Ready && pressed)
            TryFire();
        if (State == GrappleState.Ready || State == GrappleState.Cooldown)
            return false;
        if (!held || targetCollider == null || !targetCollider.enabled || !targetCollider.gameObject.activeInHierarchy)
        {
            Release(!held && State == GrappleState.Pulling);
            return false;
        }
        GrappleTarget marker = targetCollider.GetComponentInParent<GrappleTarget>();
        if (marker != null && !marker.IsAvailable)
        {
            Release(false);
            return false;
        }
        phaseTime += dt;
        if (State == GrappleState.Flying)
        {
            float t = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, flightDuration));
            Vector3 next = Vector3.Lerp(flightStart, targetPoint, Mathf.Clamp01(flightCurve.Evaluate(t)));
            if (t >= 1)
                next = targetPoint;
            if (IsBlocked(hookPosition, next, targetCollider))
            {
                Release(false);
                return false;
            }
            hookPosition = next;
            if (t >= 1)
            {
                SetState(GrappleState.Attached);
                Attached.Invoke();
            }
            return false;
        }
        if (State == GrappleState.Attached)
        {
            if (phaseTime < attachmentDelay)
                return false;
            groundLaunch = movement.IsGrounded && targetPoint.y > transform.position.y + 1;
            SetState(GrappleState.Pulling);
            PullStarted.Invoke();
        }
        Vector3 offset = targetPoint - transform.position;
        if (offset.magnitude <= arrivalDistance)
        {
            Release(false);
            return false;
        }
        Vector3 direction = offset.normalized;
        if (groundLaunch)
        {
            float lift = Mathf.Clamp01(liftCurve.Evaluate(Mathf.Clamp01((phaseTime - groundDragTime) / Mathf.Max(0.01f, liftTime))));
            direction.y *= lift;
            direction = direction.normalized;
        }
        float speed = pullSpeed * Mathf.Clamp01((phaseTime + dt) / Mathf.Max(0.01f, accelerationTime));
        velocity = direction * Mathf.Min(speed, Mathf.Max(0, offset.magnitude - arrivalDistance) / dt);
        lastVelocity = velocity;
        stats.SetGrappleInvulnerable(invulnerableEntirePull || phaseTime < initialProtectionTime);
        return true;
    }

    private void TryFire()
    {
        if (!TryGetFireTarget(out RaycastHit hit))
            return;
        targetCollider = hit.collider;
        targetPoint = hit.point;
        flightStart = LaunchPosition;
        hookPosition = flightStart;
        lastVelocity = Vector3.zero;
        if (hookVisualPrefab != null)
            hook = Instantiate(hookVisualPrefab, flightStart, Quaternion.identity);
        SetState(GrappleState.Flying);
        Fired.Invoke();
    }

    private bool TryGetFireTarget(out RaycastHit target)
    {
        target = default;
        if (!isActiveAndEnabled || State != GrappleState.Ready || aim == null || stats == null || stats.IsDead || GamePause.IsPaused ||
            (GameFlowController.Instance != null && !GameFlowController.Instance.AllowsPlayerControl))
        {
            ClearRememberedTarget();
            return false;
        }
        if (TryGetAimTarget(out target))
        {
            lastValidHit = target;
            lastValidLocalPoint = target.collider.transform.InverseTransformPoint(target.point);
            lastValidTime = Time.unscaledTime;
            return true;
        }
        Collider remembered = lastValidHit.collider;
        if (targetGraceTime <= 0f || Time.unscaledTime - lastValidTime > targetGraceTime || !IsAllowedTarget(remembered))
        {
            ClearRememberedTarget();
            return false;
        }
        Vector3 point = remembered.transform.TransformPoint(lastValidLocalPoint);
        if (Vector3.Distance(transform.position, point) > maxDistance || IsBlocked(LaunchPosition, point, remembered))
        {
            ClearRememberedTarget();
            return false;
        }
        target = lastValidHit;
        target.point = point;
        return true;
    }

    private bool IsAllowedTarget(Collider candidate)
    {
        if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy || candidate.isTrigger ||
            (obstacleMask.value & (1 << candidate.gameObject.layer)) == 0 || candidate.GetComponentInParent<Enemy>() != null)
            return false;
        GrappleTarget marker = candidate.GetComponentInParent<GrappleTarget>();
        return marker != null ? marker.IsAvailable : (grappleLayers.value & (1 << candidate.gameObject.layer)) != 0;
    }

    private void ClearRememberedTarget()
    {
        lastValidHit = default;
        lastValidTime = float.NegativeInfinity;
    }

    private bool TryGetAimTarget(out RaycastHit target)
    {
        target = default;
        Ray ray = aim.GetRay();
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
                continue;
            if (!IsAllowedTarget(hit.collider))
                return false;
            if (Vector3.Distance(transform.position, hit.point) > maxDistance)
                return false;
            if (IsBlocked(LaunchPosition, hit.point, hit.collider))
                return false;
            target = hit;
            return true;
        }
        return false;
    }

    private bool IsBlocked(Vector3 from, Vector3 to, Collider ignoredCollider)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.00001f)
            return false;
        foreach (RaycastHit hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (hook != null && hit.transform.IsChildOf(hook)))
                continue;
            if (hit.collider == ignoredCollider)
                continue;
            return true;
        }
        return false;
    }

    public void Blocked()
    {
        if (State == GrappleState.Pulling)
            Release(false);
    }

    private void Release(bool preserveMomentum)
    {
        if (preserveMomentum)
            movement.AddInertia(lastVelocity * releaseInertia);
        stats.SetGrappleInvulnerable(false);
        if (hook != null)
            Destroy(hook.gameObject);
        hook = null;
        targetCollider = null;
        CooldownRemaining = cooldown;
        SetState(GrappleState.Cooldown);
        CooldownChanged.Invoke(CooldownRemaining);
        Released.Invoke();
    }

    public void CancelForTransition()
    {
        ClearRememberedTarget();
        if (stats != null)
            stats.SetGrappleInvulnerable(false);
        if (hook != null)
            Destroy(hook.gameObject);
        hook = null;
        targetCollider = null;
        lastVelocity = Vector3.zero;
        CooldownRemaining = 0;
        SetState(GrappleState.Ready);
        if (rope != null)
            rope.enabled = false;
    }

    private void OnDisable()
    {
        CancelForTransition();
    }

    private void LateUpdate()
    {
        // Remember targets even when no HUD indicator is assigned and the fire button is not pressed.
        TryGetFireTarget(out _);
        bool active = State == GrappleState.Flying || State == GrappleState.Attached || State == GrappleState.Pulling;
        if (hook != null)
            hook.position = hookPosition;
        if (rope == null)
            return;
        rope.enabled = active;
        rope.useWorldSpace = true;
        rope.positionCount = 2;
        if (active)
        {
            rope.SetPosition(0, launchPoint != null ? launchPoint.position : transform.position + Vector3.up);
            rope.SetPosition(1, hookPosition);
        }
    }
}
