using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

public enum GamePhase
{
    Intro = 0, Wave = 1, Intermission = 2, Ending = 3, Clear = 4, Dead = 5
}

[DefaultExecutionOrder(-50)]
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }
    [SerializeField] private PlayerStats player;
    [SerializeField] private PlayerWeaponController inventory;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private WaveData[] stages;
    [SerializeField] private ShopController shop;
    [SerializeField] private LevelUpController levelUp;
    [SerializeField] private GamePause pause;
    [Header("Timeline")]
    [SerializeField] private PlayableDirector opening;
    [SerializeField] private PlayableDirector ending;
    [SerializeField] private bool advanceWithoutTimeline = true;
    [SerializeField] private string titleScene = "TitleScene";
    public UnityEvent<int> StageStarted = new UnityEvent<int>();
    public UnityEvent<GamePhase> PhaseChangedEvent = new UnityEvent<GamePhase>();
    public UnityEvent Cleared = new UnityEvent();
    public event Action<GamePhase> PhaseChanged;
    public event Action<GamePhase> PhaseEnded;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Intro;
    public int CurrentStage { get; private set; }
    public PlayerStats Player => player;
    public ShopController Shop => shop;
    public bool IsCombat => CurrentPhase == GamePhase.Wave || CurrentPhase == GamePhase.Intermission;
    public bool AllowsPlayerControl => IsCombat;
    public bool AllowsRewards => IsCombat;
    public bool CanPause => CurrentPhase != GamePhase.Dead && CurrentPhase != GamePhase.Clear;
    private bool transitioning;
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (player == null || inventory == null || waveManager == null || pause == null || shop == null || levelUp == null || stages == null || stages.Length == 0)
        {
            Debug.LogError("GameFlowController: assign Player, Inventory, WaveManager, Pause, Shop, LevelUp and Stages.", this);
            enabled = false;
            return;
        }
        pause.Clear();
        player.ResetStats();
        inventory.ResetLoadout();
        player.Death += OnPlayerDeath;
        waveManager.BossDefeated += OnBossDefeated;
        shop.Landed += OnShopLanded;
        if (opening != null)
            opening.stopped += OnOpeningStopped;
        if (ending != null)
            ending.stopped += OnEndingStopped;
        CurrentStage = 0;
        SetPhase(GamePhase.Intro);
        PlayTimeline(opening, CompleteOpening);
    }

    private void PlayTimeline(PlayableDirector director, Action missing)
    {
        movement?.ResetMotion();
        if (director != null && director.playableAsset != null)
        {
            director.extrapolationMode = DirectorWrapMode.None;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.Play();
        }
        else if (advanceWithoutTimeline)
            missing();
        else
            Debug.LogWarning("Timeline is missing. Assign a Timeline or enable Advance Without Timeline.", this);
    }

    public void CompleteOpening()
    {
        if (CurrentPhase == GamePhase.Intro)
            StartStage();
    }

    private void OnOpeningStopped(PlayableDirector director)
    {
        CompleteOpening();
    }

    private void OnEndingStopped(PlayableDirector director)
    {
        CompleteEnding();
    }

    private void StartStage(bool removeShop = true)
    {
        movement?.ResetMotion();
        if (removeShop)
            shop.Remove();
        SetPhase(GamePhase.Wave);
        waveManager.Begin(stages[CurrentStage]);
        StageStarted.Invoke(CurrentStage + 1);
    }

    public void SetPhase(GamePhase phase)
    {
        if (CurrentPhase != phase)
            PhaseEnded?.Invoke(CurrentPhase);
        CurrentPhase = phase;
        PhaseChanged?.Invoke(phase);
        PhaseChangedEvent.Invoke(phase);
    }

    private void OnBossDefeated(Vector3 position)
    {
        if (transitioning || CurrentPhase != GamePhase.Wave || player.IsDead)
            return;
        transitioning = true;
        bool final = CurrentStage >= stages.Length - 1;
        if (final)
            levelUp.SuppressAndClear();
        if (final)
            SetPhase(GamePhase.Ending);
        movement?.ResetMotion();
        //Enemy.ClearWithoutRewards();
        Projectile.ClearEnemyProjectiles();
        if (final)
        {
            shop.Remove();
            pause.Clear();
            PlayTimeline(ending, CompleteEnding);
        }
        else
        {
            var remainingEnemies = new HashSet<Enemy>(Enemy.Active);
            SetPhase(GamePhase.Intermission);
            shop.Present(remainingEnemies);
        }
        transitioning = false;
    }

    public void NextStage()
    {
        if (!IsCombat || !shop.IsAvailable || levelUp.HasPending || player.IsDead || transitioning)
            return;
        // The next wave already started when the shop landed; this only finishes shopping.
        shop.Remove();
    }

    private void OnShopLanded()
    {
        if (CurrentPhase != GamePhase.Intermission || player.IsDead || CurrentStage >= stages.Length - 1)
            return;
        CurrentStage++;
        StartStage(false);
    }

    public void CompleteEnding()
    {
        if (CurrentPhase != GamePhase.Ending)
            return;
        SetPhase(GamePhase.Clear);
        Cleared.Invoke();
    }

    private void OnPlayerDeath()
    {
        SetPhase(GamePhase.Dead);
        levelUp.SuppressAndClear();
        waveManager.Stop();
        shop.Remove();
        movement?.ResetMotion();
        pause.Clear();
        SceneNavigation.Load(titleScene);
    }

    public void ReturnToTitle()
    {
        SceneNavigation.Load(titleScene);
    }

    private void OnDestroy()
    {
        if (player != null)
            player.Death -= OnPlayerDeath;
        if (waveManager != null)
            waveManager.BossDefeated -= OnBossDefeated;
        if (shop != null)
            shop.Landed -= OnShopLanded;
        if (opening != null)
            opening.stopped -= OnOpeningStopped;
        if (ending != null)
            ending.stopped -= OnEndingStopped;
        if (Instance == this)
            Instance = null;
    }
}
