using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Runs real game components in Play Mode. No test-only behavior is added to runtime classes.
[InitializeOnLoad]
public static class SurvivalValidation
{
    private const string RunningKey = "Survival.Validation.Running";
    private static readonly List<string> results = new List<string>();
    private static int step;
    private static double deadline;
    private static double readyAt;
    private static PlayerStats player;
    private static PlayerWeaponController inventory;
    private static WaveManager waves;
    private static GameFlowController flow;
    private static GamePause pause;
    private static LevelUpController level;
    private static ShopController shop;
    private static GrappleController grapple;
    private static PlayerMovement motor;
    private static int beforeMoney;
    private static Vector3 bossPosition;
    private static WaveData[] testWaves;
    private static bool finishing;
    private static Enemy firstTarget;
    private static Enemy secondTarget;
    private static Enemy centerTarget;
    private static Projectile piercingShot;
    private static Projectile expiringShot;
    private static GameObject testWall;
    private static OrbitWeapon orbit;
    private static SlowFieldWeapon slow;
    private static Pickup proximityMoney;
    private static Pickup proximityExperience;
    private static float shopWaveElapsed;
    private static int shopEnemyCount;
    static SurvivalValidation()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }
    [MenuItem("Survival/Run Functional Checks")]
    public static void RunAll()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before running checks.");
            return;
        }
        SurvivalSampleBuilder.Build();
        EditorSceneManager.OpenScene(SurvivalSampleBuilder.GameScenePath);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetString("Survival.Validation.Start", DateTime.UtcNow.ToString("O"));
        EditorApplication.isPlaying = true;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(RunningKey, false) && !finishing && (type == LogType.Exception || type == LogType.Error))
            Finish(false, message + "\n" + stack);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
        results.Add("PASS: " + description);
    }

    private static T Find<T>() where T : Object
    {
        return Object.FindAnyObjectByType<T>();
    }

    private static void MovePlayer(Vector3 position)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position;
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    private static void DrainChoices()
    {
        if (level.IsChoosing)
            level.Choose(0);
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false) || finishing)
            return;
        if (DateTime.TryParse(SessionState.GetString("Survival.Validation.Start", ""), out DateTime start) && (DateTime.UtcNow - start).TotalSeconds > 180)
        {
            Finish(false, "Functional checks timed out.");
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;
        try
        {
            Tick();
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < readyAt)
            return;
        if (step == 0)
        {
            flow = Find<GameFlowController>();
            if (flow == null || !flow.IsCombat)
                return;
            player = Find<PlayerStats>();
            inventory = Find<PlayerWeaponController>();
            waves = Find<WaveManager>();
            pause = Find<GamePause>();
            level = Find<LevelUpController>();
            shop = Find<ShopController>();
            grapple = Find<GrappleController>();
            motor = Find<PlayerMovement>();
            motor.enabled = false;
            Check(player.Hp == player.MaxHp && player.Money == 0 && player.Level == 1, "New game resets stats");
            Check(inventory.Weapons.Count == 1 && inventory.MainWeapon.Level == 1 && inventory.AuxiliaryCount == 0, "Main weapon starts at level 1 outside auxiliary slots");
            Check(inventory.AvailableAuxiliaries.All(w => !w.gameObject.activeSelf && !w.IsOwned), "Unowned weapons are inactive");
            var candidates = RewardCatalog.CreateCandidates(inventory, null, true);
            Check(candidates.Count == 6, "Each eligible product has its own candidate");
            Check(RewardCatalog.CreateCandidates(inventory, null, false).All(o => o.Kind != RewardKind.AcquireWeapon), "Level-up excludes new weapons");
            var drawn = RewardCatalog.Draw(candidates, 3);
            Check(drawn.Count == 3 && drawn.Distinct().Count() == 3, "Draw without replacement");
            player.IncreaseSpeed(10);
            player.IncreaseSpeed(10);
            Check(Mathf.Approximately(player.MoveSpeed, 6), "Speed bonuses add against base speed");
            int hp = player.Hp;
            player.IncreaseMaxHp(20);
            Check(player.Hp == hp && player.MaxHp == 120, "Max HP upgrade does not heal");
            player.TakeDamage(10);
            player.TakeDamage(10);
            Check(player.Hp == hp - 10, "Hit protection blocks repeated damage");
            pause.Set(PauseReason.Menu, true);
            pause.Set(PauseReason.LevelUp, true);
            pause.Resume();
            Check(Time.timeScale == 0 && pause.Reasons == PauseReason.LevelUp, "Pause reasons cannot resume each other");
            pause.Clear();
            var costly = new RewardOffer(RewardKind.MaxHealth, 999, 10);
            Check(!costly.TryApply(player, inventory, true) && !costly.Purchased, "Insufficient money cannot consume offer");
            player.AddMoney(100);
            var purchased = new RewardOffer(RewardKind.MaxHealth, 15, 20);
            Check(purchased.TryApply(player, inventory, true) && !purchased.TryApply(player, inventory, true) && player.Money == 85, "Purchase charges exactly once");
            foreach (Weapon weapon in inventory.AvailableAuxiliaries)
                Check(inventory.TryAcquire(weapon), "Acquire auxiliary " + weapon.name);
            Check(inventory.AuxiliaryCount == 3 && !inventory.TryAcquire(inventory.AvailableAuxiliaries[0]), "Auxiliary cap and duplicate ownership");
            for (int i = 0; i < 10; i++)
                inventory.MainWeapon.Upgrade();
            Check(inventory.MainWeapon.Level == 7 && !inventory.MainWeapon.CanUpgrade(), "Weapon level capped at 7");
            Check(!RewardCatalog.CreateCandidates(inventory, null, true).Any(o => o.Weapon == inventory.MainWeapon || o.Kind == RewardKind.AcquireWeapon), "Maxed/owned weapons excluded from reward pool");
            var wave = ScriptableObject.CreateInstance<WaveData>();
            wave.Enemies = new[]
            {
                new EnemySpawnEntry { Prefab = waves.CurrentWave.Enemies[0].Prefab, Weight = 90 },
                new EnemySpawnEntry { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurvivalSampleBuilder.Root + "/Prefabs/ShootingDrone.prefab").GetComponent<Enemy>(), Weight = 10 }
            };
            Check(wave.PickEnemy(0.899f) == wave.Enemies[0].Prefab && wave.PickEnemy(0.9f) == wave.Enemies[1].Prefab, "90/10 spawn weight boundary");
            Object.Destroy(wave);
            player.AddExperience(150);
            step = 1;
            return;
        }
        if (step == 1)
        {
            if (!level.IsChoosing)
                return;
            Check(Time.timeScale == 0 && level.Offers.Count <= 3, "Experience opens paused reward choices");
            Check(level.SelectedIndex == 0, "Level-up defaults to the first choice");
            level.MoveSelection(-1);
            Check(level.SelectedIndex == 0, "Previous cannot wrap from the first level-up choice to the last");
            level.SelectChoice(level.Offers.Count - 1);
            level.MoveSelection(1);
            Check(level.SelectedIndex == level.Offers.Count - 1, "Next cannot wrap from the last level-up choice to the first");
            level.MoveSelection(-1);
            Check(level.SelectedIndex == Mathf.Max(0, level.Offers.Count - 2), "Previous moves one level-up choice backward");
            int selectedIndex = Mathf.Min(1, level.Offers.Count - 1);
            RewardOffer selectedOffer = level.Offers[selectedIndex];
            level.SelectChoice(selectedIndex);
            Check(level.SelectedIndex == selectedIndex && !selectedOffer.Purchased, "Selecting a level-up choice does not grant it");
            level.SelectChoice(99);
            Check(level.SelectedIndex == selectedIndex, "Invalid reward selection is ignored");
            ValidateRewardKeyInput();
            pause.Set(PauseReason.Menu, true);
            level.ConfirmSelection();
            Check(!selectedOffer.Purchased && level.IsChoosing, "Pause menu blocks reward confirmation");
            pause.Resume();
            level.ConfirmSelection();
            Check(selectedOffer.Purchased && !level.IsChoosing && level.SelectedIndex == -1, "Confirm grants the selected reward and closes its window");
            step = 2;
            return;
        }
        if (step == 2)
        {
            DrainChoices();
            if (level.HasPending)
                return;
            Check(Time.timeScale == 1, "Queued level-ups resume after final choice");
            player.SetGrappleInvulnerable(true);
            int hp = player.Hp;
            player.TakeDamage(1000);
            Check(player.Hp == hp, "Grapple protection blocks damage independently");
            inventory.ResetLoadout();
            // Accelerated copies exercise the flow without changing authored wave assets.
            testWaves = new WaveData[3];
            for (int i = 0; i < 3; i++)
            {
                testWaves[i] = Object.Instantiate(waves.CurrentWave);
                testWaves[i].BossTime = 2;
                testWaves[i].SpawnInterval = 0.1f;
                testWaves[i].MaxAliveEnemies = 2;
            }
            SurvivalSampleBuilder.SetArray(flow, "stages", testWaves);
            waves.Begin(testWaves[0]);
            step = 3;
            deadline = now + 10;
            return;
        }
        if (step == 3)
        {
            if (waves.Elapsed < 1)
                return;
            Check(Enemy.Active.Count(e => !e.IsBoss) == 2, "Spawn cap skips extra spawns");
            step = 4;
            return;
        }
        if (step == 4)
        {
            if (waves.CurrentBoss == null)
            {
                if (now > deadline)
                    throw new Exception("Boss did not spawn on NavMesh");
                return;
            }
            Check(waves.Elapsed >= 2 && Enemy.Active.Count(e => e.IsBoss) == 1, "Boss spawns once at configured time");
            Enemy knockbackTarget = waves.CurrentBoss;
            Check(!knockbackTarget.TryKnockback(Vector3.zero, 8f, 0.15f), "Invalid knockback does not consume immunity");
            Check(knockbackTarget.TryKnockback(Vector3.right, 8f, 0.15f), "First knockback is accepted on NavMesh");
            Check(!knockbackTarget.TryKnockback(Vector3.left, 8f, 0.15f), "Repeated knockback is blocked during immunity");
            float healthBeforeKnockback = knockbackTarget.Health;
            knockbackTarget.TakeDamage(1f);
            Check(Mathf.Approximately(knockbackTarget.Health, healthBeforeKnockback - 1f), "Knockback immunity does not block damage");
            step = 5;
            readyAt = now + 0.6;
            return;
        }
        if (step == 5)
        {
            Check(waves.CurrentBoss.TryKnockback(Vector3.left, 8f, 0.15f), "Knockback is accepted again after immunity expires");
            Check(Enemy.Active.Count(e => !e.IsBoss) <= 2, "No ordinary spawns during boss phase");
            bossPosition = waves.CurrentBoss.transform.position;
            beforeMoney = player.Money;
            int experienceBefore = player.Exp;
            int levelBefore = player.Level;
            int healthBefore = player.Hp;
            var pickupsBefore = new HashSet<Pickup>(Pickup.Active);
            Enemy defeatedBoss = waves.CurrentBoss;
            defeatedBoss.TakeDamage(10000);
            Check(defeatedBoss.IsDead && !Enemy.Active.Contains(defeatedBoss), "Dying enemy immediately leaves the active enemy registry");
            Check(defeatedBoss.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Death immediately disables enemy collisions");
            Check(!defeatedBoss.GetComponent<EnemyChase>().enabled && !defeatedBoss.GetComponent<BossController>().enabled,
                "Death stops movement and boss attack coroutines before feedback completes");
            Check(flow.CurrentPhase == GamePhase.Intermission && flow.CurrentStage == 0 && !waves.Running, "Boss defeat waits for shop impact before starting stage 2");
            Check(flow.IsCombat && flow.AllowsPlayerControl && !GamePause.IsPaused, "Existing enemies and player remain in combat while the shop falls");
            Check(pickupsBefore.IsSubsetOf(Pickup.Active) && Pickup.Active.Count > pickupsBefore.Count, "Boss defeat leaves existing pickups and new drops on the field");
            Check(player.Money == beforeMoney && player.Exp == experienceBefore && player.Level == levelBefore && player.Hp == healthBefore, "Boss defeat does not automatically grant money, experience or healing");
            Check(Projectile.Active.All(p => p.Team != DamageTeam.Enemy), "Boss defeat clears enemy projectiles");
            Check(shop.IsFalling && !shop.IsAvailable && shop.Offers.Count == 3, "Boss defeat creates a falling shop with three offers");
            ValidateShopArrival();
            Check(flow.IsCombat && flow.CurrentStage == 1 && waves.Running && waves.CurrentWave == testWaves[1], "Shop impact starts stage 2");
            step = 6;
            return;
        }
        if (step == 6)
        {
            DrainChoices();
            if (level.HasPending)
                return;
            MovePlayer(shop.Position + Vector3.right * 2);
            player.AddMoney(10000);
            shop.TryInteract();
            Check(shop.IsOpen, "Shop opens within interaction distance");
            Check(shop.SelectedIndex == 0, "Shop defaults to the first item");
            shop.MoveSelection(-1);
            Check(shop.SelectedIndex == 0, "Previous cannot wrap from the first shop item to the last");
            shop.SelectChoice(shop.Offers.Count - 1);
            shop.MoveSelection(1);
            Check(shop.SelectedIndex == shop.Offers.Count - 1, "Next cannot wrap from the last shop item to the first");
            shop.SelectChoice(0);
            shop.MoveSelection(1);
            Check(shop.SelectedIndex == Mathf.Min(1, shop.Offers.Count - 1), "Next moves one shop choice forward");
            Check(Time.timeScale == 0, "Shop pauses the active wave");
            shopWaveElapsed = waves.Elapsed;
            shopEnemyCount = Enemy.Active.Count;
            step = 60;
            readyAt = now + 0.3;
            return;
        }
        if (step == 60)
        {
            Check(waves.Elapsed == shopWaveElapsed && Enemy.Active.Count == shopEnemyCount, "Open shop freezes wave time and spawning across frames");
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                int moneyBefore = player.Money;
                shop.SelectChoice(i);
                Check(shop.SelectedIndex == i && player.Money == moneyBefore, "Selecting a shop item does not purchase it");
                shop.ConfirmSelection();
                Check(shop.IsOpen && shop.Offers[i].Purchased && player.Money == moneyBefore - shop.Offers[i].Price, "Confirm purchases the selected item and keeps the shop open");
                shop.ConfirmSelection();
                Check(player.Money == moneyBefore - shop.Offers[i].Price, "Confirming a sold item cannot charge again");
            }
            Check(shop.Offers.All(o => o.Purchased), "All three shop items can be bought");
            var original = shop.Offers[0];
            shop.Close();
            shop.TryInteract();
            Check(ReferenceEquals(original, shop.Offers[0]) && original.Purchased, "Reopening shop preserves sold inventory");
            pause.Set(PauseReason.Menu, true);
            pause.HandleEscape();
            Check(shop.IsOpen && pause.Reasons == PauseReason.Shop, "Escape dismisses an existing pause menu before the shop");
            pause.HandleEscape();
            Check(flow.CurrentStage == 1 && flow.IsCombat && waves.Elapsed == shopWaveElapsed && !shop.IsAvailable && !shop.IsOpen, "Escape closes and removes shop without restarting the wave");
            Check(pause.Reasons == PauseReason.None && Time.timeScale == 1, "Shop escape resumes gameplay without opening the pause menu");
            ValidateResumedLookInput();
            shop.NextStage();
            flow.NextStage();
            Check(flow.CurrentStage == 1, "Repeated next-stage requests cannot skip a wave");
            player.SetGrappleInvulnerable(true);
            step = 7;
            deadline = now + 10;
            return;
        }
        if (step == 7)
        {
            if (waves.CurrentBoss == null)
            {
                if (now > deadline)
                    throw new Exception("Stage 2 boss missing");
                return;
            }
            waves.CurrentBoss.TakeDamage(10000);
            Check(flow.CurrentStage == 1 && !waves.Running && flow.CurrentPhase == GamePhase.Intermission, "Stage 3 waits for the next shop impact");
            Check(flow.IsCombat, "Later shop arrivals also keep existing enemy movement and attacks enabled");
            step = 8;
            deadline = now + 10;
            return;
        }
        if (step == 8)
        {
            DrainChoices();
            if (level.HasPending)
                return;
            if (!shop.IsAvailable)
            {
                if (now > deadline)
                    throw new Exception("Stage 3 shop did not land");
                return;
            }
            Check(flow.CurrentStage == 2 && shop.IsAvailable, "Stage 3 continues even without using the stage 2 shop");
            player.SetGrappleInvulnerable(true);
            step = 9;
            deadline = now + 10;
            return;
        }
        if (step == 9)
        {
            if (waves.CurrentBoss == null)
            {
                if (now > deadline)
                    throw new Exception("Stage 3 boss missing");
                return;
            }
            waves.CurrentBoss.TakeDamage(10000);
            Check(flow.CurrentPhase == GamePhase.Clear && !shop.IsAvailable, "Final boss skips shop and missing Timeline advances to clear");
            Check(!level.HasPending && Time.timeScale == 1, "Final boss clears pending level-up UI and restores time");
            step = 10;
            return;
        }
        if (step == 10)
        {
            ValidateDrops();
            ValidateShopWave();
            // Exercise hook timing directly with a fixed camera and real wall collider.
            flow.SetPhase(GamePhase.Wave);
            MovePlayer(Vector3.zero);
            Camera camera = Camera.main;
            // This isolated hook check controls the output camera directly.
            CinemachineBrain brain = camera.GetComponent<CinemachineBrain>();
            if (brain != null)
                brain.enabled = false;
            camera.transform.SetParent(null);
            camera.transform.position = new Vector3(0, 1, 0);
            camera.transform.rotation = Quaternion.identity;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Validation Grapple Target";
            wall.transform.position = new Vector3(0, 1, 10);
            wall.transform.localScale = new Vector3(3, 3, 1);
            GrappleTarget grappleTarget = wall.AddComponent<GrappleTarget>();
            Physics.SyncTransforms();
            grapple.CancelForTransition();
            player.SetGrappleInvulnerable(false);
            Check(grapple.CanFire && grapple.State == GrappleState.Ready, "Availability detects a reachable target without firing");
            camera.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Check(grapple.CanFire, "Wire remembers the last valid target when aim briefly leaves it");
            typeof(GrappleController).GetField("lastValidTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grapple, Time.unscaledTime - 0.21f);
            Check(!grapple.CanFire, "Remembered wire target expires after the 0.2 second grace period");
            camera.transform.rotation = Quaternion.identity;
            Check(grapple.CanFire, "A newly aimed valid target refreshes the grace period");
            grappleTarget.SetAvailable(false);
            Check(!grapple.CanFire, "Unavailable grapple targets do not show availability");
            grappleTarget.SetAvailable(true);
            pause.Set(PauseReason.Menu, true);
            Check(!grapple.CanFire, "Paused gameplay hides wire availability");
            pause.Resume();
            wall.transform.position = new Vector3(0, 1, 100);
            Physics.SyncTransforms();
            Check(!grapple.CanFire, "Out-of-range targets do not show availability");
            wall.transform.position = new Vector3(0, 1, 10);
            Physics.SyncTransforms();
            Check(grapple.CanFire, "Target is valid again before testing buffered firing");
            camera.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            grapple.Tick(0.05f, true, true, out Vector3 velocity);
            camera.transform.rotation = Quaternion.identity;
            Check(grapple.State == GrappleState.Flying && !player.IsGrappleInvulnerable, "Hook flight starts without pull invulnerability");
            Check(!grapple.CanFire, "Active wire flight hides availability");
            for (int i = 0; i < 10 && grapple.State != GrappleState.Pulling; i++)
                grapple.Tick(0.05f, false, true, out velocity);
            Check(grapple.State == GrappleState.Pulling && player.IsGrappleInvulnerable && velocity.sqrMagnitude > 0, "Finite hook flight attaches before protected pull");
            grapple.Tick(0.05f, false, false, out velocity);
            Check(grapple.State == GrappleState.Cooldown && !player.IsGrappleInvulnerable && grapple.CooldownRemaining > 0, "Release ends invulnerability and starts cooldown");
            Check(!grapple.CanFire, "Cooldown hides wire availability");
            Vector3 inertia = (Vector3)typeof(PlayerMovement).GetField("inertia", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor);
            Check(inertia.sqrMagnitude > 0, "Release preserves limited pull inertia");
            grapple.Tick(3, false, true, out velocity);
            Check(grapple.State == GrappleState.Ready, "Holding after cooldown does not auto-fire");
            grapple.Tick(0.05f, true, true, out velocity);
            grapple.Tick(0.05f, false, false, out velocity);
            Check(grapple.State == GrappleState.Cooldown, "Cancelling hook flight consumes cooldown");
            Object.Destroy(wall);
            motor.ResetMotion();
            player.SetGrappleInvulnerable(true);
            player.TakeDamage(10000);
            Check(!player.IsDead, "Independent grapple protection survives normal hit timer expiry");
            player.SetGrappleInvulnerable(false);
            step = 20;
            readyAt = now + 0.6;
            return;
        }
        if (step == 20)
        {
            inventory.ResetLoadout();
            Projectile prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurvivalSampleBuilder.Root + "/Prefabs/Bullet.prefab").GetComponent<Projectile>();
            firstTarget = CreateTarget("Pierce Target A", new Vector3(0, 12, 43));
            secondTarget = CreateTarget("Pierce Target B", new Vector3(0, 12, 46));
            testWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testWall.transform.position = new Vector3(0, 12, 49);
            testWall.transform.localScale = new Vector3(3, 3, 1);
            piercingShot = Object.Instantiate(prefab, new Vector3(0, 12, 40), Quaternion.identity);
            piercingShot.Launch(Vector3.forward, 7, DamageTeam.Player, player.transform, true);
            expiringShot = Object.Instantiate(prefab, new Vector3(10, 12, 40), Quaternion.identity);
            SurvivalSampleBuilder.SetFloat(expiringShot, "speed", 0);
            SurvivalSampleBuilder.SetFloat(expiringShot, "lifetime", 0.1f);
            Physics.SyncTransforms();
            step = 21;
            readyAt = now + 0.7;
            return;
        }
        if (step == 21)
        {
            Check(firstTarget.Health == 23 && secondTarget.Health == 23, "Piercing projectile hits each target once along swept path");
            Check(piercingShot == null, "Piercing projectile stops at wall");
            Check(expiringShot == null, "Projectile expires at configured lifetime");
            Object.Destroy(firstTarget.gameObject);
            Object.Destroy(secondTarget.gameObject);
            Object.Destroy(testWall);
            MovePlayer(Vector3.zero);
            firstTarget = CreateTarget("Orbit Contact", new Vector3(2, 1, 0));
            centerTarget = CreateTarget("Orbit Center", new Vector3(0, 1, 0));
            orbit = inventory.AvailableAuxiliaries.OfType<OrbitWeapon>().First();
            inventory.TryAcquire(orbit);
            Physics.SyncTransforms();
            step = 22;
            readyAt = now + 2.2;
            return;
        }
        if (step == 22)
        {
            Check(firstTarget == null || firstTarget.Health < 30, "Orbit blade damages actual contact target");
            Check(centerTarget.Health == 30, "Orbit does not damage noncontact center target");
            orbit.gameObject.SetActive(false);
            if (firstTarget != null)
                Object.Destroy(firstTarget.gameObject);
            slow = inventory.AvailableAuxiliaries.OfType<SlowFieldWeapon>().First();
            inventory.TryAcquire(slow);
            step = 23;
            readyAt = now + 0.1;
            return;
        }
        if (step == 23)
        {
            Check(centerTarget.SpeedMultiplier < 1 && centerTarget.Health == 30, "Persistent field slows without damage");
            slow.gameObject.SetActive(false);
            Check(centerTarget.SpeedMultiplier == 1, "Disabling field removes slow");
            Object.Destroy(centerTarget.gameObject);
            Pickup moneyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurvivalSampleBuilder.Root + "/Prefabs/Money.prefab").GetComponent<Pickup>();
            Pickup expPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurvivalSampleBuilder.Root + "/Prefabs/Experience.prefab").GetComponent<Pickup>();
            beforeMoney = player.Money;
            proximityMoney = Object.Instantiate(moneyPrefab, player.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            SurvivalSampleBuilder.ConfigurePickup(proximityMoney, PickupKind.Money, 9);
            proximityMoney.Initialize(player);
            proximityExperience = Object.Instantiate(expPrefab, player.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            SurvivalSampleBuilder.ConfigurePickup(proximityExperience, PickupKind.Experience, 3);
            proximityExperience.Initialize(player);
            step = 24;
            readyAt = now + 0.1;
            return;
        }
        if (step == 24)
        {
            Check(proximityMoney == null && proximityExperience == null && player.Money == beforeMoney + 9, "Both drop kinds collect by proximity");
            step = 11;
            return;
        }
        if (step == 11)
        {
            player.TakeDamage(10000);
            step = 12;
            deadline = now + 10;
            return;
        }
        if (step == 12)
        {
            if (SceneManager.GetActiveScene().name != "SurvivalTitle")
            {
                if (now > deadline)
                    throw new Exception("Death did not return to title");
                return;
            }
            Check(Time.timeScale == 1, "Death returns to title with time restored");
            Find<SceneNavigation>().StartGame();
            step = 13;
            return;
        }
        if (step == 13)
        {
            flow = Find<GameFlowController>();
            if (flow == null || !flow.IsCombat)
                return;
            player = Find<PlayerStats>();
            inventory = Find<PlayerWeaponController>();
            Check(player.Money == 0 && player.Level == 1 && player.Hp == player.MaxHp && inventory.AuxiliaryCount == 0, "New game after death clears all run progress");
            Finish(true, "All functional checks passed.");
        }
    }

    private static void ValidateResumedLookInput()
    {
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(500f, -300f) });
            InputSystem.Update();
            InputReader reader = InputReader.Instance;
            var actions = (GameInputActions)typeof(InputReader).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader);
            Check(actions.Player.Look.ReadValue<Vector2>().sqrMagnitude > 0f, "Resume test supplies a nonzero mouse delta");
            Check(reader.Look == Vector2.zero, "Closing the shop discards the transition-frame mouse delta");
            int suppressedThrough = (int)typeof(InputReader).GetField("suppressLookThroughFrame", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader);
            Check(suppressedThrough == Time.frameCount + 1, "Resume also suppresses the next input update's cursor recentering delta");
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
        }
    }

    private static void ValidateRewardKeyInput()
    {
        var speedOffer = new RewardOffer(RewardKind.MoveSpeed, 15, 10);
        var healthOffer = new RewardOffer(RewardKind.MaxHealth, 15, 20);
        Check(speedOffer.Title == "이동속도 증가" && speedOffer.Description == "이동속도 +10%" && speedOffer.UpgradeText == "",
            "Speed reward separates title and actual stat increase");
        Check(healthOffer.Title == "최대체력 증가" && healthOffer.Description == "최대체력 +20",
            "Health reward separates title and actual stat increase");
        WeaponData display = ScriptableObject.CreateInstance<WeaponData>();
        display.Description = "fallback";
        display.LevelDescriptions = new[] { "new", "upgrade to 2", "" };
        Check(display.GetLevelDescription(1) == "new" && display.GetLevelDescription(2) == "upgrade to 2" &&
            display.GetLevelDescription(3) == "fallback" && display.GetLevelDescription(7) == "fallback",
            "Weapon descriptions use destination level and safely fall back for missing entries");
        Object.Destroy(display);
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2, Key.Space, Key.W));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f) });
            InputSystem.Update();
            Check(InputReader.Instance.TryReadRewardInput(out int choice, out int movement, out bool confirm) && choice == 1 && movement == -1 && confirm, "Number key, W, wheel up and Space work while gameplay is paused without double movement");
            Check(!InputReader.Instance.TryReadRewardInput(out _, out _, out _), "One key press cannot confirm two popups in the same frame");
            Check(!InputReader.Instance.JumpPressed, "Space used for a paused reward choice cannot trigger a jump");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
        }
    }

    private static void ValidateShopArrival()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo tick = typeof(ShopController).GetMethod("TickFall", flags);
        Vector3 landing = (Vector3)typeof(ShopController).GetField("landingPosition", flags).GetValue(shop);
        Vector3 start = shop.Position;
        FieldInfo warningField = typeof(ShopController).GetField("landingWarning", flags);
        FieldInfo warningImageField = typeof(ShopController).GetField("landingWarningImage", flags);
        FieldInfo warningMaterialField = typeof(ShopController).GetField("landingWarningMaterial", flags);
        object originalWarningImage = warningImageField.GetValue(shop);
        object originalWarningMaterial = warningMaterialField.GetValue(shop);
        try
        {
            warningImageField.SetValue(shop, Texture2D.whiteTexture);
            warningMaterialField.SetValue(shop, AssetDatabase.LoadAssetAtPath<Material>(SurvivalSampleBuilder.Root + "/Materials/ShopLandingWarning.mat"));
            typeof(ShopController).GetMethod("ShowLandingWarning", flags).Invoke(shop, new object[] { landing });
        }
        finally
        {
            warningImageField.SetValue(shop, originalWarningImage);
            warningMaterialField.SetValue(shop, originalWarningMaterial);
        }
        GameObject warning = (GameObject)warningField.GetValue(shop);
        Check(warning != null && warning.activeSelf && Vector3.Dot(warning.transform.forward, Vector3.down) > 0.999f, "Landing warning projects vertically down while the shop falls");
        Vector3 warningPosition = warning.transform.position;
        Vector3 offset = start - player.transform.position;
        offset.y = 0f;
        Check(Mathf.Approximately(start.y - landing.y, 10f) && offset.magnitude >= 5f && offset.magnitude <= 20f, "Shop starts 10 units above a landing position 5 to 20 units from the player");
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
        Check(Vector3.Angle(forward, offset) <= 45.001f, "Shop landing stays inside the player's forward 90-degree sector after NavMesh sampling");
        shop.TryInteract();
        Check(!shop.IsOpen && !shop.CanInteract, "Falling shop cannot be opened");
        ParticleSystem vfx = (ParticleSystem)typeof(ShopController).GetField("waveVFX", flags).GetValue(shop);
        Check(vfx == null || !vfx.IsAlive(), "Shop shockwave waits until impact");
        tick.Invoke(shop, new object[] { 0.1f });
        float firstDistance = start.y - shop.Position.y;
        float beforeSecond = shop.Position.y;
        tick.Invoke(shop, new object[] { 0.1f });
        Check(firstDistance > 0f && beforeSecond - shop.Position.y > firstDistance, "Shop accelerates over equal fall time steps");
        Check(warning.transform.position == warningPosition, "Landing warning stays fixed on the ground while the shop descends");
        Vector3 pausedPosition = shop.Position;
        pause.Set(PauseReason.Menu, true);
        tick.Invoke(shop, new object[] { 1f });
        Check(shop.Position == pausedPosition && !waves.Running, "Pause freezes shop descent without starting the wave");
        pause.Resume();
        int impacts = 0;
        Action onImpact = () => impacts++;
        shop.Landed += onImpact;
        try
        {
            tick.Invoke(shop, new object[] { 10f });
            Check(shop.Position == landing && shop.IsAvailable && !shop.IsFalling && impacts == 1, "Shop lands without overshoot and signals impact once");
            Check(warningField.GetValue(shop) == null && !warning.activeSelf, "Landing removes the warning decal immediately");
            Check(vfx == null || vfx.isPlaying, "Shop shockwave plays at impact");
            tick.Invoke(shop, new object[] { 10f });
            Check(impacts == 1 && flow.CurrentStage == 1, "Further updates cannot repeat impact or skip a stage");
        }
        finally
        {
            shop.Landed -= onImpact;
        }
    }

    [MenuItem("Survival/Run Drop Checks (Play Mode)")]
    public static void RunDropChecks()
    {
        if (!Application.isPlaying || GamePause.IsPaused)
        {
            Debug.LogWarning("Run drop checks in unpaused Play Mode.");
            return;
        }
        ValidateDrops();
        Debug.Log("Drop checks passed: probability, prefab values, healing and duplicate prevention.");
    }

    private static void ValidateDrops()
    {
        var testPlayerObject = new GameObject("Drop Check Player");
        var templates = new GameObject("Drop Check Templates");
        Enemy enemy = CreateTarget("Drop Check Enemy", Vector3.up * 100);
        var existingPickups = new HashSet<Pickup>(Pickup.Active);
        try
        {
            PlayerStats testPlayer = testPlayerObject.AddComponent<PlayerStats>();
            Pickup[] prefabs = new Pickup[3];
            for (int i = 0; i < prefabs.Length; i++)
            {
                var template = new GameObject("Pickup " + i);
                template.transform.SetParent(templates.transform);
                prefabs[i] = template.AddComponent<Pickup>();
                SurvivalSampleBuilder.ConfigurePickup(prefabs[i], (PickupKind)i, i == 0 ? 20 : i == 1 ? 7 : 10);
            }
            var entry = new EnemyDropEntry { Prefab = prefabs[0], Chance = 0 };
            Check(!entry.ShouldDrop(0), "Zero percent never drops");
            entry.Chance = 100;
            Check(entry.ShouldDrop(1), "One hundred percent always drops including maximum random roll");
            entry.Chance = 25;
            Check(entry.ShouldDrop(0.249f) && !entry.ShouldDrop(0.25f), "Drop probability uses a separate percentage per entry");
            entry.Prefab = null;
            Check(!entry.ShouldDrop(0), "Missing pickup prefab is skipped");
            enemy.Initialize(testPlayer);
            SurvivalSampleBuilder.SetDrops(enemy,
                new EnemyDropEntry { Prefab = prefabs[0] },
                new EnemyDropEntry { Prefab = prefabs[1] },
                new EnemyDropEntry { Prefab = prefabs[2] },
                new EnemyDropEntry { Prefab = prefabs[2], Chance = 0 },
                new EnemyDropEntry());
            var beforeDrop = new HashSet<Pickup>(Pickup.Active);
            testPlayer.TakeDamage(30);
            Check(testPlayer.Hp == 70, "Drop test player starts injured");
            enemy.Die();
            var dropped = Pickup.Active.Where(p => !beforeDrop.Contains(p)).ToList();
            Check(dropped.Count == 3, "Enemy drops each guaranteed entry and skips zero chance or missing entries");
            enemy.Die();
            Check(Pickup.Active.Count == beforeDrop.Count + 3, "Repeated death cannot duplicate drops");
            foreach (Pickup pickup in dropped)
            {
                pickup.Collect(testPlayer);
                pickup.Collect(testPlayer);
            }
            Check(testPlayer.Exp == 20 && testPlayer.Money == 7 && testPlayer.Hp == 80, "Pickups retain authored kinds and amounts and collect only once");
            int healthEvents = 0;
            testPlayer.HpChanged += hp => healthEvents++;
            testPlayer.Heal(int.MaxValue);
            Check(testPlayer.Hp == testPlayer.MaxHp && healthEvents == 1, "Healing clamps to maximum HP and notifies health UI");
            testPlayer.Heal(0);
            testPlayer.Heal(-10);
            Check(testPlayer.Hp == testPlayer.MaxHp && healthEvents == 1, "Invalid healing amounts have no effect");
            testPlayer.ResetStats();
            testPlayer.TakeDamage(10000);
            testPlayer.Heal(10);
            Check(testPlayer.IsDead, "Healing cannot revive a dead player");
        }
        finally
        {
            foreach (Pickup pickup in new List<Pickup>(Pickup.Active))
                if (pickup != null && !existingPickups.Contains(pickup))
                    Object.Destroy(pickup.gameObject);
            Object.Destroy(templates);
            Object.Destroy(testPlayerObject);
            if (enemy != null) Object.Destroy(enemy.gameObject);
        }
    }

    private static void ValidateShopWave()
    {
        var waveObject = new GameObject("Validation Shop Wave");
        Enemy oldEnemy = CreateTarget("Previous Wave Enemy", Vector3.up * 100);
        Enemy newEnemy = CreateTarget("Next Wave Enemy", Vector3.up * 100);
        Enemy newBoss = CreateTarget("Next Wave Boss", Vector3.up * 100);
        var existingPickups = new HashSet<Pickup>(Pickup.Active);
        try
        {
            oldEnemy.Initialize(player);
            SurvivalSampleBuilder.SetDrops(oldEnemy,
                new EnemyDropEntry { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurvivalSampleBuilder.Root + "/Prefabs/Experience.prefab").GetComponent<Pickup>() },
                new EnemyDropEntry { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurvivalSampleBuilder.Root + "/Prefabs/Money.prefab").GetComponent<Pickup>() });
            newBoss.Initialize(player, true);
            var snapshot = new HashSet<Enemy> { oldEnemy };
            ParticleSystem particles = waveObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SphereCollider hitCollider = waveObject.AddComponent<SphereCollider>();
            hitCollider.isTrigger = true;
            WaveHitDetector detector = waveObject.AddComponent<WaveHitDetector>();
            SurvivalSampleBuilder.Set(detector, "waveVFX", particles);
            SurvivalSampleBuilder.Set(detector, "hitCollider", hitCollider);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WaveHitDetector).GetField("hitLayers", flags).SetValue(detector, (LayerMask)(1 << 0));
            MethodInfo update = typeof(WaveHitDetector).GetMethod("Update", flags);
            MethodInfo hit = typeof(WaveHitDetector).GetMethod("OnTriggerEnter", flags);
            detector.Initialize(snapshot);
            snapshot.Add(newEnemy);
            Check(!hitCollider.enabled, "Shop hit collider waits for visible particles");
            particles.Emit(1);
            update.Invoke(detector, null);
            Check(hitCollider.enabled, "Visible shop wave enables hit detection");
            hit.Invoke(detector, new object[] { newEnemy.GetComponent<Collider>() });
            hit.Invoke(detector, new object[] { newBoss.GetComponent<Collider>() });
            Check(!newEnemy.IsDead && !newBoss.IsDead, "Shop wave ignores new enemies and bosses even if caller changes its snapshot");
            int pickupsBefore = Pickup.Active.Count;
            int killEvents = 0;
            oldEnemy.Killed += enemy => killEvents++;
            hit.Invoke(detector, new object[] { oldEnemy.GetComponent<Collider>() });
            Check(oldEnemy.IsDead && killEvents == 1 && Pickup.Active.Count == pickupsBefore + 2, "Shop wave kills captured enemies and drops experience and money");
            hit.Invoke(detector, new object[] { oldEnemy.GetComponent<Collider>() });
            Check(killEvents == 1 && Pickup.Active.Count == pickupsBefore + 2, "Repeated shop wave hits cannot duplicate rewards");
            detector.Initialize(new[] { newEnemy });
            update.Invoke(detector, null);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            update.Invoke(detector, null);
            Check(!hitCollider.enabled, "Finished shop wave disables collider with targets still remaining");
            hit.Invoke(detector, new object[] { newEnemy.GetComponent<Collider>() });
            Check(!newEnemy.IsDead, "Finished shop wave cannot remove enemies");
        }
        finally
        {
            foreach (Pickup pickup in new List<Pickup>(Pickup.Active))
                if (pickup != null && !existingPickups.Contains(pickup))
                    Object.Destroy(pickup.gameObject);
            Object.Destroy(waveObject);
            if (oldEnemy != null) Object.Destroy(oldEnemy.gameObject);
            Object.Destroy(newEnemy.gameObject);
            Object.Destroy(newBoss.gameObject);
        }
    }

    private static Enemy CreateTarget(string name, Vector3 position)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.position = position;
        target.transform.localScale = Vector3.one * 0.5f;
        return target.AddComponent<Enemy>();
    }

    private static void Finish(bool success, string message)
    {
        if (finishing)
            return;
        finishing = true;
        SessionState.SetBool(RunningKey, false);
        string report = string.Join("\n", results) + "\n" + (success ? "SUCCESS: " : "FAILED: ") + message;
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Documentation", "FUNCTIONAL_CHECKS.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report);
        if (success)
            Debug.Log(report);
        else
            Debug.LogError(report);
        if (Application.isBatchMode)
            EditorApplication.Exit(success ? 0 : 1);
        else
        {
            EditorApplication.isPlaying = false;
            finishing = false;
            step = 0;
            readyAt = 0;
            results.Clear();
        }
    }
}
