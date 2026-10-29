using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

public class ShopController : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerStats player;
    [SerializeField] private PlayerWeaponController inventory;
    [SerializeField] private RewardData settings;
    [SerializeField] private GamePause pause;
    [SerializeField] private LevelUpController levelUp;
    [SerializeField] private GameObject shopPrefab;
    [SerializeField, Min(0)] private float interactionDistance = 3f;
    [Header("Arrival")]
    [SerializeField, Min(0.1f)] private float dropHeight = 10f;
    [SerializeField, Min(0.1f)] private float fallAcceleration = 9.81f;
    [SerializeField, Min(1f)] private float minimumDropDistance = 5f;
    [SerializeField, Min(1f)] private float maximumDropDistance = 20f;
    [SerializeField, Range(0f, 180f)] private float dropSectorAngle = 90f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Header("Landing Warning")]
    [SerializeField] private Texture2D landingWarningImage;
    [SerializeField, Min(0.1f)] private float landingWarningSize = 3f;
    [SerializeField] private Material landingWarningMaterial;
    private GameObject landingWarning;
    private Material landingWarningInstance;
    private GameObject shopObject;
    private HashSet<Enemy> arrivalEnemies = new HashSet<Enemy>();
    private readonly List<Collider> landingColliders = new List<Collider>();
    private ParticleSystem waveVFX;
    private Vector3 landingPosition;
    private float fallSpeed;
    private float spawnRetryTimer;
    private bool awaitingSpawn;
    private List<RewardOffer> offers = new List<RewardOffer>();
    public IReadOnlyList<RewardOffer> Offers => offers;
    public bool IsOpen { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public bool IsFalling { get; private set; }
    public bool IsAvailable => shopObject != null && !IsFalling;
    public Vector3 Position => shopObject != null ? shopObject.transform.position : transform.position;
    public bool CanInteract => IsAvailable && player != null && !player.IsDead && !GamePause.IsPaused &&
        (levelUp == null || !levelUp.HasPending) && Vector3.Distance(player.transform.position, shopObject.transform.position) <= interactionDistance;
    public event Action Changed;
    public event Action Landed;
    public UnityEvent Appeared = new UnityEvent();
    public UnityEvent Opened = new UnityEvent();
    public UnityEvent Closed = new UnityEvent();

    public void Present(IEnumerable<Enemy> remainingEnemies)
    {
        Remove();
        if (shopPrefab == null || player == null)
        {
            Debug.LogError("ShopController: assign Player and Shop Prefab.", this);
            return;
        }
        arrivalEnemies = remainingEnemies != null ? new HashSet<Enemy>(remainingEnemies) : new HashSet<Enemy>();
        offers = RewardCatalog.Draw(RewardCatalog.CreateCandidates(inventory, settings, true));
        awaitingSpawn = true;
        TrySpawn();
    }

    private void TrySpawn()
    {
        spawnRetryTimer = 1f;
        if (!TryLandingPosition(out landingPosition))
            return;
        Vector3 groundPosition = landingPosition;
        awaitingSpawn = false;
        shopObject = Instantiate(shopPrefab, landingPosition + Vector3.up * Mathf.Max(0.1f, dropHeight), Quaternion.identity);
        waveVFX = shopObject.GetComponentInChildren<ParticleSystem>();
        waveVFX?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        float bottom = shopObject.transform.position.y;
        foreach (Collider collider in shopObject.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger)
                continue;
            bottom = Mathf.Min(bottom, collider.bounds.min.y);
            landingColliders.Add(collider);
            collider.enabled = false;
        }
        // Keep centered prefab pivots above the floor as well as bottom-aligned pivots.
        float pivotOffset = shopObject.transform.position.y - bottom;
        landingPosition.y += pivotOffset;
        shopObject.transform.position += Vector3.up * pivotOffset;
        fallSpeed = 0f;
        IsFalling = true;
        ShowLandingWarning(groundPosition);
        Appeared.Invoke();
        Changed?.Invoke();
    }

    private void ShowLandingWarning(Vector3 groundPosition)
    {
        ClearLandingWarning();
        if (landingWarningImage == null)
            return;
        if (landingWarningMaterial == null)
        {
            Debug.LogWarning("ShopController: assign Landing Warning Material to display the landing decal.", this);
            return;
        }
        landingWarningInstance = new Material(landingWarningMaterial);
        landingWarningInstance.SetTexture("Base_Map", landingWarningImage);
        landingWarning = new GameObject("Shop Landing Warning");
        landingWarning.SetActive(false);
        landingWarning.transform.SetPositionAndRotation(groundPosition + Vector3.up, Quaternion.Euler(90f, 0f, 0f));
        DecalProjector projector = landingWarning.AddComponent<DecalProjector>();
        projector.material = landingWarningInstance;
        float size = Mathf.Max(0.1f, landingWarningSize);
        projector.size = new Vector3(size, size, 2f);
        projector.pivot = new Vector3(0f, 0f, 1f);
        landingWarning.SetActive(true);
    }

    private void ClearLandingWarning()
    {
        if (landingWarning != null)
        {
            landingWarning.SetActive(false);
            Destroy(landingWarning);
        }
        landingWarning = null;
        if (landingWarningInstance != null)
            Destroy(landingWarningInstance);
        landingWarningInstance = null;
    }

    private bool TryLandingPosition(out Vector3 position)
    {
        position = default;
        if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit playerGround, 30f, NavMesh.AllAreas))
            return false;
        float minDistance = Mathf.Max(1f, minimumDropDistance);
        float maxDistance = Mathf.Max(minDistance, maximumDropDistance);
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            return false;
        float halfAngle = Mathf.Clamp(dropSectorAngle, 0f, 180f) * 0.5f;
        var path = new NavMeshPath();
        for (int i = 0; i < 32; i++)
        {
            float angle = UnityEngine.Random.Range(-halfAngle, halfAngle);
            float distance = Mathf.Sqrt(UnityEngine.Random.Range(minDistance * minDistance, maxDistance * maxDistance));
            Vector3 candidate = player.transform.position + Quaternion.AngleAxis(angle, Vector3.up) * forward * distance;
            candidate.y = playerGround.position.y;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                continue;
            Vector3 offset = hit.position - player.transform.position;
            offset.y = 0f;
            if (offset.magnitude < minDistance || offset.magnitude > maxDistance)
                continue;
            // NavMesh sampling can move a candidate outside the original sector.
            if (Vector3.Angle(forward, offset) > halfAngle + 0.001f)
                continue;
            if (!NavMesh.CalculatePath(playerGround.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            if (!Physics.Raycast(hit.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 1f, groundMask, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f)
                continue;
            if (ground.collider.GetComponentInParent<Enemy>() != null || ground.collider.GetComponentInParent<PlayerStats>() != null)
                continue;
            if (Physics.CheckCapsule(ground.point + Vector3.up * 0.9f, ground.point + Vector3.up * (Mathf.Max(0.1f, dropHeight) + 2f), 0.75f, groundMask, QueryTriggerInteraction.Ignore))
                continue;
            position = ground.point;
            return true;
        }
        return false;
    }

    private void Update()
    {
        if (IsOpen)
        {
            if ((levelUp != null && levelUp.HasPending) || (pause != null && (pause.Reasons & PauseReason.Menu) != 0))
                return;
            if (InputReader.Instance != null && !InputReader.Instance.PausePressed && InputReader.Instance.TryReadRewardInput(out int choice, out int movement, out bool confirm))
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
        if (player == null || player.IsDead || GamePause.IsPaused || Time.deltaTime <= 0f)
            return;
        if (awaitingSpawn)
        {
            spawnRetryTimer -= Time.deltaTime;
            if (spawnRetryTimer <= 0f)
                TrySpawn();
        }
        if (IsFalling && shopObject != null)
            TickFall(Time.deltaTime);
        if (CanInteract && InputReader.Instance != null && InputReader.Instance.InteractPressed)
            TryInteract();
    }

    private void TickFall(float dt)
    {
        if (!IsFalling || shopObject == null || GamePause.IsPaused || dt <= 0f)
            return;
        float acceleration = Mathf.Max(0.1f, fallAcceleration);
        float distance = fallSpeed * dt + 0.5f * acceleration * dt * dt;
        fallSpeed += acceleration * dt;
        shopObject.transform.position = Vector3.MoveTowards(shopObject.transform.position, landingPosition, distance);
        if (shopObject.transform.position != landingPosition)
            return;
        IsFalling = false;
        ClearLandingWarning();
        foreach (Collider collider in landingColliders)
            if (collider != null)
                collider.enabled = true;
        landingColliders.Clear();
        // Initialize at impact so the stopped VFX cannot discard the captured enemies during the fall.
        foreach (WaveHitDetector detector in shopObject.GetComponentsInChildren<WaveHitDetector>(true))
            detector.Initialize(arrivalEnemies);
        arrivalEnemies.Clear();
        waveVFX?.Play();
        Landed?.Invoke();
        Changed?.Invoke();
    }

    public void TryInteract()
    {
        if (!CanInteract)
            return;
        IsOpen = true;
        SelectedIndex = offers.Count > 0 ? 0 : -1;
        pause.Set(PauseReason.Shop, true);
        Opened.Invoke();
        Changed?.Invoke();
    }

    public void Buy(int index)
    {
        if (!IsOpen || (levelUp != null && levelUp.HasPending) || index < 0 || index >= offers.Count)
            return;
        if (offers[index].TryApply(player, inventory, true))
            Changed?.Invoke();
    }

    public void SelectChoice(int index)
    {
        if (!IsOpen || (levelUp != null && levelUp.HasPending) || index < 0 || index >= Mathf.Min(3, offers.Count))
            return;
        SelectedIndex = index;
        Changed?.Invoke();
    }

    public void MoveSelection(int direction)
    {
        if (!IsOpen || offers.Count == 0)
            return;
        SelectChoice(Mathf.Clamp(SelectedIndex + direction, 0, Mathf.Min(3, offers.Count) - 1));
    }

    public void ConfirmSelection()
    {
        if (pause != null && (pause.Reasons & PauseReason.Menu) != 0)
            return;
        Buy(SelectedIndex);
    }

    public void Exit()
    {
        if (!IsOpen || (levelUp != null && levelUp.HasPending))
            return;
        Remove();
    }

    public void Close()
    {
        IsOpen = false;
        SelectedIndex = -1;
        if (pause != null)
            pause.Set(PauseReason.Shop, false);
        Closed.Invoke();
        Changed?.Invoke();
    }

    public void NextStage()
    {
        if (!IsOpen || (levelUp != null && levelUp.HasPending))
            return;
        Close();
        GameFlowController.Instance?.NextStage();
    }

    private void OnDisable()
    {
        ClearLandingWarning();
        if (pause != null)
            pause.Set(PauseReason.Shop, false);
    }

    public void Remove()
    {
        ClearLandingWarning();
        awaitingSpawn = false;
        IsFalling = false;
        fallSpeed = 0f;
        arrivalEnemies.Clear();
        landingColliders.Clear();
        waveVFX = null;
        Close();
        if (shopObject != null)
            Destroy(shopObject);
        shopObject = null;
        offers.Clear();
    }
}
