using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class SurvivalSampleBuilder
{
    public const string Root = "Assets/SurvivalSample";
    public const string GameScenePath = Root + "/Scenes/SurvivalGame.unity";
    public const string TitleScenePath = Root + "/Scenes/SurvivalTitle.unity";
    [MenuItem("Survival/Create Cube Sample")]
    public static void Create()
    {
        if (AssetDatabase.IsValidFolder(Root))
        {
            Debug.LogWarning("Cube sample already exists. Existing assets were not overwritten.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Build();
    }

    public static void Build()
    {
        if (AssetDatabase.IsValidFolder(Root))
            return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string folder in new[] { Root, Root + "/Prefabs", Root + "/Data", Root + "/Scenes", Root + "/Materials" })
            Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var gray = Material("Concrete", new Color(0.35f, 0.37f, 0.4f));
        var blue = Material("Player", new Color(0.15f, 0.6f, 1f));
        var red = Material("Enemy", new Color(0.9f, 0.25f, 0.2f));
        var gold = Material("Money", new Color(1f, 0.8f, 0.1f));
        var white = Material("Projectile", Color.white);
        var cyan = Material("Experience", Color.cyan);
        Pickup exp = PickupPrefab("Experience", cyan, new Vector3(0.25f, 0.25f, 0.25f), PickupKind.Experience, 10);
        Pickup money = PickupPrefab("Money", gold, new Vector3(0.4f, 0.12f, 0.4f), PickupKind.Money, 5);
        Projectile bullet = ProjectilePrefab("Bullet", white, 35, 3, 0.08f);
        Projectile slash = ProjectilePrefab("Slash", cyan, 15, 2, 0.5f);
        Projectile missile = ProjectilePrefab("Missile", red, 15, 5, 0.2f);
        Enemy explosive = EnemyPrefab("ExplodingDrone", red, exp, money, bullet, false, false);
        Enemy shooter = EnemyPrefab("ShootingDrone", gold, exp, money, bullet, true, false);
        Enemy boss = EnemyPrefab("RobotBoss", red, exp, money, missile, false, true);
        GameObject shopPrefab = SavePrefab(Cube("Shop", Vector3.zero, new Vector3(1.5f, 1.5f, 1.5f), gold), "Shop");
        GameObject bladeObject = Cube("OrbitBlade", Vector3.zero, Vector3.one * 0.45f, white);
        bladeObject.AddComponent<OrbitBlade>();
        OrbitBlade blade = SavePrefab(bladeObject, "OrbitBlade").GetComponent<OrbitBlade>();
        GameObject hookObject = Cube("Hook", Vector3.zero, Vector3.one * 0.15f, white);
        Object.DestroyImmediate(hookObject.GetComponent<Collider>());
        Transform hook = SavePrefab(hookObject, "Hook").transform;
        RewardData rewards = ScriptableObject.CreateInstance<RewardData>();
        AssetDatabase.CreateAsset(rewards, Root + "/Data/Rewards.asset");
        WaveData[] stages = new WaveData[3];
        for (int i = 0; i < stages.Length; i++)
        {
            WaveData data = ScriptableObject.CreateInstance<WaveData>();
            data.DisplayName = $"Stage {i + 1}";
            data.Enemies = new[] { new EnemySpawnEntry { Prefab = explosive, Weight = 90 }, new EnemySpawnEntry { Prefab = shooter, Weight = 10 } };
            data.BossPrefab = boss;
            data.BossTime = 60;
            data.MaxAliveEnemies = 100;
            AssetDatabase.CreateAsset(data, Root + $"/Data/Wave{i + 1}.asset");
            stages[i] = data;
        }
        GameObject ground = Cube("Ground", new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60), gray);
        ground.isStatic = true;
        var surface = ground.AddComponent<NavMeshSurface>();
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1;
        for (int i = 0; i < 4; i++)
        {
            Vector3 position = i < 2 ? new Vector3(i == 0 ? -30 : 30, 4, 0) : new Vector3(0, 4, i == 2 ? -30 : 30);
            Vector3 scale = i < 2 ? new Vector3(1, 8, 60) : new Vector3(60, 8, 1);
            Cube("Boundary", position, scale, gray).AddComponent<GrappleTarget>();
        }
        Cube("Building A", new Vector3(-9, 2, 7), new Vector3(5, 4, 5), gray).AddComponent<GrappleTarget>();
        Cube("Building B", new Vector3(9, 3, 8), new Vector3(5, 6, 5), gray).AddComponent<GrappleTarget>();
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        GameObject playerObject = new GameObject("Player");
        playerObject.layer = 2;
        playerObject.transform.position = Vector3.up * 0.1f;
        CharacterController character = playerObject.AddComponent<CharacterController>();
        character.height = 2;
        character.radius = 0.4f;
        character.center = Vector3.up;
        GameObject model = Cube("Model", Vector3.zero, new Vector3(0.7f, 1.8f, 0.7f), blue);
        Object.DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(playerObject.transform);
        model.transform.localPosition = Vector3.up;
        PlayerStats player = playerObject.AddComponent<PlayerStats>();
        PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
        PlayerAim aim = playerObject.AddComponent<PlayerAim>();
        GrappleController grapple = playerObject.AddComponent<GrappleController>();
        var cameraTarget = new GameObject("Camera Target").transform;
        cameraTarget.SetParent(playerObject.transform);
        cameraTarget.localPosition = Vector3.up * 1.5f;
        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = cameraTarget.position + new Vector3(0.5f, 0.5f, -5f);
        camera.gameObject.AddComponent<AudioListener>();
        camera.gameObject.AddComponent<CinemachineBrain>();
        CinemachineCamera followCamera = new GameObject("Cinemachine Camera").AddComponent<CinemachineCamera>();
        followCamera.Follow = cameraTarget;
        CinemachineThirdPersonFollow follow = followCamera.gameObject.AddComponent<CinemachineThirdPersonFollow>();
        follow.Damping = new Vector3(0.1f, 0.5f, 0.3f);
        follow.ShoulderOffset = new Vector3(0.5f, 0, 0);
        follow.VerticalArmLength = 0.5f;
        follow.CameraSide = 1f;
        follow.CameraDistance = 5f;
        follow.AvoidObstacles.Enabled = true;
        follow.AvoidObstacles.CollisionFilter = 1; // Sample terrain uses the Default layer.
        follow.AvoidObstacles.CameraRadius = 0.2f;
        Set(movement, "cameraTarget", cameraTarget);
        Set(aim, "aimCamera", camera);
        var canvas = new GameObject("Crosshair Canvas", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Image crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        crosshair.transform.SetParent(canvas.transform, false);
        crosshair.rectTransform.sizeDelta = new Vector2(6, 6);
        crosshair.raycastTarget = false;
        Set(aim, "crosshair", crosshair.rectTransform);
        Transform muzzle = new GameObject("Fire Point").transform;
        muzzle.SetParent(playerObject.transform);
        muzzle.localPosition = new Vector3(0.6f, 1.3f, 0.7f);
        LineRenderer rope = new GameObject("Wire").AddComponent<LineRenderer>();
        rope.transform.SetParent(playerObject.transform);
        rope.startWidth = rope.endWidth = 0.035f;
        rope.sharedMaterial = white;
        Set(grapple, "launchPoint", muzzle);
        Set(grapple, "hookVisualPrefab", hook);
        Set(grapple, "rope", rope);
        MainWeapon main = ChildWeapon<MainWeapon>("Main Gun", playerObject.transform);
        Set(main, "projectilePrefab", bullet.gameObject);
        Set(main, "firePoint", muzzle);
        Set(main, "aim", aim);
        SlowFieldWeapon slow = ChildWeapon<SlowFieldWeapon>("Slow Field", playerObject.transform);
        OrbitWeapon orbit = ChildWeapon<OrbitWeapon>("Orbit", playerObject.transform);
        var orbitSettings = new SerializedObject(orbit);
        orbitSettings.FindProperty("canKnockback").boolValue = true;
        orbitSettings.ApplyModifiedPropertiesWithoutUndo();
        Set(orbit, "bladePrefab", blade);
        SlashWeapon slashWeapon = ChildWeapon<SlashWeapon>("Slash", playerObject.transform);
        Set(slashWeapon, "projectilePrefab", slash);
        Set(slashWeapon, "firePoint", muzzle);
        Set(slashWeapon, "movement", movement);
        foreach (Weapon weapon in new Weapon[] { main, slow, orbit, slashWeapon })
        {
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            data.DisplayName = weapon.name;
            AssetDatabase.CreateAsset(data, Root + "/Data/" + weapon.name.Replace(" ", "") + ".asset");
            Set(weapon, "weaponData", data);
        }
        PlayerWeaponController inventory = playerObject.AddComponent<PlayerWeaponController>();
        Set(inventory, "mainWeapon", main);
        SetArray(inventory, "auxiliaryWeapons", slow, orbit, slashWeapon);
        GameObject systems = new GameObject("Game Systems");
        systems.AddComponent<InputReader>();
        systems.AddComponent<CursorManager>();
        GamePause pause = systems.AddComponent<GamePause>();
        WaveManager waves = systems.AddComponent<WaveManager>();
        LevelUpController level = systems.AddComponent<LevelUpController>();
        ShopController shop = systems.AddComponent<ShopController>();
        GameFlowController flow = systems.AddComponent<GameFlowController>();
        var points = new List<Object>();
        GameObject spawnRoot = new GameObject("Spawn Points");
        for (int i = 0; i < 12; i++)
        {
            Transform point = new GameObject("Spawn " + i).transform;
            point.SetParent(spawnRoot.transform);
            float angle = i * Mathf.PI * 2 / 12;
            point.position = new Vector3(Mathf.Sin(angle) * 23, 0, Mathf.Cos(angle) * 23);
            points.Add(point);
        }
        Set(waves, "player", player);
        SetArray(waves, "spawnPoints", points.ToArray());
        Set(level, "player", player);
        Set(level, "inventory", inventory);
        Set(level, "settings", rewards);
        Set(level, "pause", pause);
        Set(shop, "player", player);
        Set(shop, "inventory", inventory);
        Set(shop, "settings", rewards);
        Set(shop, "pause", pause);
        Set(shop, "levelUp", level);
        Set(shop, "shopPrefab", shopPrefab);
        Set(flow, "player", player);
        Set(flow, "inventory", inventory);
        Set(flow, "movement", movement);
        Set(flow, "waveManager", waves);
        SetArray(flow, "stages", stages);
        Set(flow, "shop", shop);
        Set(flow, "levelUp", level);
        Set(flow, "pause", pause);
        SetString(flow, "titleScene", "SurvivalTitle");
        var debugUI = systems.AddComponent<PrototypeDebugUI>();
        Set(debugUI, "flow", flow);
        Set(debugUI, "player", player);
        Set(debugUI, "levelUp", level);
        Set(debugUI, "shop", shop);
        Set(debugUI, "pause", pause);
        Set(debugUI, "waves", waves);
        surface.BuildNavMesh();
        if (surface.navMeshData != null)
            AssetDatabase.CreateAsset(surface.navMeshData, Root + "/Data/SampleNavMesh.asset");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), GameScenePath);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Camera").AddComponent<Camera>();
        SceneNavigation navigation = new GameObject("Title").AddComponent<SceneNavigation>();
        SetString(navigation, "gameScene", "SurvivalGame");
        SetString(navigation, "titleScene", "SurvivalTitle");
        PrototypeDebugUI titleUI = navigation.gameObject.AddComponent<PrototypeDebugUI>();
        Set(titleUI, "navigation", navigation);
        navigation.gameObject.AddComponent<CursorManager>();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TitleScenePath);
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (File.Exists(scene.path))
                scenes.Add(scene);
        AddScene(scenes, TitleScenePath);
        AddScene(scenes, GameScenePath);
        AddScene(scenes, "Assets/Scenes/TitleScene.unity");
        AddScene(scenes, "Assets/Scenes/GameScene.unity");
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Cube sample created. Open Assets/SurvivalSample/Scenes/SurvivalTitle.unity and press Play.");
    }

    private static void AddScene(List<EditorBuildSettingsScene> scenes, string path)
    {
        if (File.Exists(path) && !scenes.Exists(scene => scene.path == path))
            scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    private static Material Material(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
        return material;
    }

    private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static GameObject SavePrefab(GameObject obj, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, Root + "/Prefabs/" + name + ".prefab");
        Object.DestroyImmediate(obj);
        return prefab;
    }

    private static Pickup PickupPrefab(string name, Material material, Vector3 scale, PickupKind kind, int amount)
    {
        GameObject obj = Cube(name, Vector3.zero, scale, material);
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        ConfigurePickup(obj.AddComponent<Pickup>(), kind, amount);
        return SavePrefab(obj, name).GetComponent<Pickup>();
    }

    private static Projectile ProjectilePrefab(string name, Material material, float speed, float life, float radius)
    {
        GameObject obj = Cube(name, Vector3.zero, new Vector3(radius * 2, radius * 2, 0.4f), material);
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        Projectile shot = obj.AddComponent<Projectile>();
        SetFloat(shot, "speed", speed);
        SetFloat(shot, "lifetime", life);
        SetFloat(shot, "hitRadius", radius);
        return SavePrefab(obj, name).GetComponent<Projectile>();
    }

    private static Enemy EnemyPrefab(string name, Material material, Pickup exp, Pickup money, Projectile shot, bool shooter, bool boss)
    {
        GameObject obj = new GameObject(name);
        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.center = Vector3.up;
        collider.size = new Vector3(1, 2, 1);
        GameObject visual = Cube("Model", Vector3.zero, boss ? new Vector3(2, 3, 2) : Vector3.one, material);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(obj.transform);
        visual.transform.localPosition = Vector3.up;
        Enemy enemy = obj.AddComponent<Enemy>();
        SetFloat(enemy, "maxHealth", boss ? 300 : shooter ? 40 : 30);
        SetDrops(enemy, new EnemyDropEntry { Prefab = exp }, new EnemyDropEntry { Prefab = money });
        NavMeshAgent agent = obj.AddComponent<NavMeshAgent>();
        agent.radius = boss ? 0.8f : 0.4f;
        agent.height = 2;
        agent.angularSpeed = 360;
        obj.AddComponent<EnemyChase>();
        Transform muzzle = new GameObject("Fire Point").transform;
        muzzle.SetParent(obj.transform);
        muzzle.localPosition = new Vector3(0, 1.2f, 0.8f);
        if (boss)
        {
            collider.size = new Vector3(2, 3, 2);
            collider.center = Vector3.up * 1.5f;
            BossMissilePattern missile = obj.AddComponent<BossMissilePattern>();
            Set(missile, "missilePrefab", shot);
            Set(missile, "firePoint", muzzle);
            BossChargePattern charge = obj.AddComponent<BossChargePattern>();
            BossController controller = obj.AddComponent<BossController>();
            SetArray(controller, "patterns", missile, charge);
        }
        else if (shooter)
        {
            ShootingDrone drone = obj.AddComponent<ShootingDrone>();
            Set(drone, "projectilePrefab", shot);
            Set(drone, "firePoint", muzzle);
        }
        else
            obj.AddComponent<ExplodingDrone>();
        return SavePrefab(obj, name).GetComponent<Enemy>();
    }

    private static T ChildWeapon<T>(string name, Transform parent) where T : Weapon
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        return obj.AddComponent<T>();
    }

    public static void ConfigurePickup(Pickup pickup, PickupKind kind, int amount)
    {
        var serialized = new SerializedObject(pickup);
        serialized.FindProperty("kind").intValue = (int)kind;
        serialized.FindProperty("amount").intValue = amount;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetDrops(Enemy enemy, params EnemyDropEntry[] drops)
    {
        var serialized = new SerializedObject(enemy);
        SerializedProperty entries = serialized.FindProperty("drops");
        entries.arraySize = drops.Length;
        for (int i = 0; i < drops.Length; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Prefab").objectReferenceValue = drops[i].Prefab;
            entry.FindPropertyRelative("Chance").floatValue = drops[i].Chance;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetArray(Object target, string field, params Object[] values)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetFloat(Object target, string field, float value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetString(Object target, string field, string value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
