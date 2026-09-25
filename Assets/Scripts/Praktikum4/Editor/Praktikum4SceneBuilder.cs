using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Menyusun scene Praktikum 4 secara otomatis lewat menu "Praktikum 4".
/// Scene A: grid A* dengan obstacle, Goal, dan agent.
/// Scene B: arena NavMesh dengan NPC pengejar, target pemain, dan eksperimen NavMeshObstacle.
/// </summary>
public static class Praktikum4SceneBuilder
{
    private const string SceneFolder = "Assets/Scenes/Praktikum4";
    private const string MaterialFolder = SceneFolder + "/Materials";
    private const string AStarScenePath = SceneFolder + "/P4A_AStarGrid.unity";
    private const string NavMeshScenePath = SceneFolder + "/P4B_NavMeshChase.unity";

    // Grid Scene A: 20 x 14 cell, 1 cell = 1 unit, berpusat di (0, 0, 0).
    private const int GridCellsX = 20;
    private const int GridCellsY = 14;
    private const float WallHeightA = 1.5f;

    // ------------------------------------------------------------------ Scene A

    [MenuItem("Praktikum 4/Build Scene A - A* Grid")]
    private static void BuildAStarScene()
    {
        int obstacleLayer = RequireObstacleLayer();
        if (obstacleLayer < 0 || !PrepareNewScene(AStarScenePath))
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Material groundMat = GetMaterial("P4_Ground", new Color(0.78f, 0.8f, 0.76f));
        Material wallMat = GetMaterial("P4_Wall", new Color(0.35f, 0.33f, 0.4f));
        Material experimentMat = GetMaterial("P4_WallExperiment", new Color(0.75f, 0.35f, 0.2f));
        Material goalMat = GetMaterial("P4_Goal", new Color(1f, 0.2f, 0.55f));
        Material agentMat = GetMaterial("P4_Agent", new Color(0.2f, 0.45f, 1f));

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(GridCellsX / 10f, 1f, GridCellsY / 10f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // Obstacle utama. Koordinat dalam index cell (x 0..19, y 0..13), inklusif.
        var obstacles = new GameObject("Obstacles").transform;
        // Mangkuk (cup) di sekitar Start, terbuka ke kiri: Seek lurus ke Goal akan terjebak di sini.
        CellWall(obstacles, "Cup_Top", 3, 9, 5, 9, obstacleLayer, wallMat);
        CellWall(obstacles, "Cup_Bottom", 3, 3, 5, 3, obstacleLayer, wallMat);
        CellWall(obstacles, "Cup_Back", 5, 4, 5, 8, obstacleLayer, wallMat);
        // Dua dinding panjang berselang-seling memaksa jalur berkelok.
        CellWall(obstacles, "Wall_1", 7, 0, 7, 10, obstacleLayer, wallMat);
        CellWall(obstacles, "Wall_2", 12, 3, 12, 13, obstacleLayer, wallMat);
        CellWall(obstacles, "Block_1", 9, 2, 10, 3, obstacleLayer, wallMat);
        CellWall(obstacles, "Block_2", 9, 7, 10, 8, obstacleLayer, wallMat);
        CellWall(obstacles, "Block_3", 15, 9, 16, 10, obstacleLayer, wallMat);
        CellWall(obstacles, "Block_4", 15, 1, 16, 2, obstacleLayer, wallMat);

        // Eksperimen: aktifkan di Hierarchy.
        var extraWall = new GameObject("Experiment_ExtraWall (aktifkan)").transform;
        CellWall(extraWall, "ExtraWall", 7, 11, 7, 12, obstacleLayer, experimentMat);
        extraWall.gameObject.SetActive(false);

        var isolation = new GameObject("Experiment_GoalIsolation (aktifkan)").transform;
        CellWall(isolation, "Ring_Top", 15, 8, 19, 8, obstacleLayer, experimentMat);
        CellWall(isolation, "Ring_Bottom", 15, 4, 19, 4, obstacleLayer, experimentMat);
        CellWall(isolation, "Ring_Left", 15, 5, 15, 7, obstacleLayer, experimentMat);
        CellWall(isolation, "Ring_Right", 19, 5, 19, 7, obstacleLayer, experimentMat);
        isolation.gameObject.SetActive(false);

        GameObject goal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goal.name = "Goal";
        Object.DestroyImmediate(goal.GetComponent<Collider>());
        goal.transform.position = CellCenter(17, 6) + Vector3.up * 0.05f;
        goal.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
        goal.GetComponent<Renderer>().sharedMaterial = goalMat;

        GameObject agent = CreateCapsuleBody("Agent", CellCenter(3, 6) + Vector3.up * 0.5f,
            new Vector3(0.6f, 0.5f, 0.6f), agentMat);

        var gridObject = new GameObject("AStarGrid");
        var gridManager = gridObject.AddComponent<GridManager>();
        Set(gridManager, "gridWorldSize", new Vector2(GridCellsX, GridCellsY));
        Set(gridManager, "nodeRadius", 0.5f);
        SetMask(gridManager, "obstacleMask", 1 << obstacleLayer);
        Set(gridManager, "obstacleCheckHeight", 1f);

        var pathfinder = gridObject.AddComponent<AStarPathfinder>();
        Set(pathfinder, "grid", gridManager);
        Set(pathfinder, "start", agent.transform);
        Set(pathfinder, "goal", goal.transform);
        SetMask(pathfinder, "clickMask", 1 << 0);

        var visualizer = gridObject.AddComponent<AStarVisualizer>();
        Set(visualizer, "pathfinder", pathfinder);
        Set(visualizer, "grid", gridManager);
        Set(visualizer, "baseMaterial", GetMaterial("P4_TileUnlit", Color.white, unlit: true));

        var follower = agent.AddComponent<AgentPathFollower>();
        Set(follower, "pathfinder", pathfinder);

        Camera cam = FindMainCamera();
        cam.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.Euler(90f, 0f, 0f));
        cam.orthographic = true;
        cam.orthographicSize = 7.8f;

        // Kamera melihat lurus ke bawah. Cahaya miring membuat bayangan dinding jatuh ke cell tetangga
        // dan terlihat seperti obstacle kedua, jadi lampu diarahkan tegak lurus ke lantai.
        Light sun = Object.FindAnyObjectByType<Light>();
        if (sun != null)
            sun.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        EditorSceneManager.SaveScene(scene, AStarScenePath);
        AddToBuildSettings(AStarScenePath);
        Selection.activeGameObject = gridObject;
        Debug.Log($"[Praktikum 4] Scene A tersimpan di {AStarScenePath}. Klik 'AStarGrid' untuk melihat grid di Scene view.");
    }

    private static Vector3 CellCenter(int x, int y)
    {
        return new Vector3(-GridCellsX * 0.5f + x + 0.5f, 0f, -GridCellsY * 0.5f + y + 0.5f);
    }

    /// <summary>Dinding yang tepat menutupi cell (x0,y0) sampai (x1,y1), supaya pas dengan grid.</summary>
    private static void CellWall(Transform parent, string name, int x0, int y0, int x1, int y1, int layer, Material mat)
    {
        Vector3 a = CellCenter(x0, y0);
        Vector3 b = CellCenter(x1, y1);
        Vector3 center = (a + b) * 0.5f + Vector3.up * (WallHeightA * 0.5f);
        Vector3 size = new Vector3(Mathf.Abs(x1 - x0) + 1, WallHeightA, Mathf.Abs(y1 - y0) + 1);
        CreateBox(parent, name, center, size, layer, mat);
    }

    // ------------------------------------------------------------------ Scene B

    [MenuItem("Praktikum 4/Build Scene B - NavMesh")]
    private static void BuildNavMeshScene()
    {
        int obstacleLayer = RequireObstacleLayer();
        if (obstacleLayer < 0 || !PrepareNewScene(NavMeshScenePath))
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Material groundMat = GetMaterial("P4_Ground", new Color(0.78f, 0.8f, 0.76f));
        Material wallMat = GetMaterial("P4_Wall", new Color(0.35f, 0.33f, 0.4f));
        Material dynamicMat = GetMaterial("P4_NavMeshObstacle", new Color(0.1f, 0.75f, 0.85f));
        Material playerMat = GetMaterial("P4_Agent", new Color(0.2f, 0.45f, 1f));
        Material npcMat = GetMaterial("P4_NPC", new Color(0.9f, 0.25f, 0.2f));
        Material lineMat = GetMaterial("P4_PathLine", new Color(1f, 0.85f, 0.1f), unlit: true);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        var environment = new GameObject("Obstacles").transform;
        // Dinding tengah panjang: jalur lurus NPC -> Player tertutup, harus memutar lewat utara/selatan.
        CreateBox(environment, "CenterWall", new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 24f), obstacleLayer, wallMat);
        CreateBox(environment, "Block_1", new Vector3(-8f, 1f, 6f), new Vector3(3f, 2f, 3f), obstacleLayer, wallMat);
        CreateBox(environment, "Block_2", new Vector3(-8f, 1f, -6f), new Vector3(3f, 2f, 3f), obstacleLayer, wallMat);
        CreateBox(environment, "Block_3", new Vector3(8f, 1f, 7f), new Vector3(4f, 2f, 2f), obstacleLayer, wallMat);
        CreateBox(environment, "Block_4", new Vector3(8f, 1f, -7f), new Vector3(2f, 2f, 4f), obstacleLayer, wallMat);

        // Eksperimen NavMeshObstacle. Tidak ikut di-bake (NavMeshModifier ignore) agar efek Carve terlihat.
        var experiments = new GameObject("NavMeshObstacle Experiments").transform;

        GameObject moving = CreateBox(experiments, "MovingObstacle (Carve ON)", new Vector3(0f, 1f, 16f),
            new Vector3(2f, 2f, 4f), 0, dynamicMat);
        AddCarvingObstacle(moving, carve: true);
        var mover = moving.AddComponent<ObstacleMover>();
        Set(mover, "offsetA", new Vector3(0f, 0f, -2f));
        Set(mover, "offsetB", new Vector3(0f, 0f, 2f));

        // Menutup celah selatan. Carve ON: NPC lewat utara. Carve OFF: NPC mencoba menembus dan tertahan.
        GameObject gate = CreateBox(experiments, "GateObstacle (toggle Carve)", new Vector3(0f, 1f, -16f),
            new Vector3(3f, 2f, 6f), 0, dynamicMat);
        AddCarvingObstacle(gate, carve: true);

        GameObject player = CreateCapsuleBody("Player (Target)", new Vector3(15f, 1f, 0f), Vector3.one, playerMat);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;
        player.AddComponent<PlayerTargetMovement>();
        IgnoreFromBake(player);

        GameObject npc = CreateChaser("NPC_Chaser", new Vector3(-15f, 1f, 0f), player.transform, npcMat, lineMat, 0);
        GameObject npc2 = CreateChaser("NPC_Chaser_2 (challenge: aktifkan)", new Vector3(-15f, 1f, 10f),
            player.transform, npcMat, lineMat, 1);
        npc2.SetActive(false);

        var surfaceObject = new GameObject("NavMeshSurface");
        var surface = surfaceObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        Camera cam = FindMainCamera();
        cam.transform.SetPositionAndRotation(new Vector3(0f, 34f, -26f), Quaternion.Euler(52f, 0f, 0f));

        EditorSceneManager.SaveScene(scene, NavMeshScenePath);
        AddToBuildSettings(NavMeshScenePath);
        Selection.activeGameObject = npc;

        BakeAndSave(surface, scene);
    }

    private static GameObject CreateChaser(string name, Vector3 position, Transform target,
        Material bodyMat, Material lineMat, int hudSlot)
    {
        GameObject npc = CreateCapsuleBody(name, position, Vector3.one, bodyMat);

        var agent = npc.AddComponent<NavMeshAgent>();
        agent.baseOffset = 1f;
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.speed = 3.5f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;

        var chaser = npc.AddComponent<NavMeshChaser>();
        Set(chaser, "target", target);
        Set(chaser, "stoppingDistance", 2f);

        var line = npc.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMat;
        line.startWidth = 0.15f;
        line.endWidth = 0.15f;
        line.positionCount = 0;

        var debugger = npc.AddComponent<NavMeshPathDebugger>();
        Set(debugger, "lineRenderer", line);
        Set(debugger, "hudSlot", hudSlot);

        IgnoreFromBake(npc);
        return npc;
    }

    private static void AddCarvingObstacle(GameObject go, bool carve)
    {
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = Vector3.one;
        obstacle.carving = carve;
        obstacle.carveOnlyStationary = true;
        IgnoreFromBake(go);
    }

    private static void IgnoreFromBake(GameObject go)
    {
        var modifier = go.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }

    /// <summary>
    /// Bake NavMesh secara sinkron lalu simpan hasilnya sebagai asset di folder bernama sama dengan scene
    /// (konvensi yang sama dengan tombol Bake di Inspector), kemudian simpan scene.
    /// </summary>
    private static void BakeAndSave(NavMeshSurface surface, Scene scene)
    {
        surface.BuildNavMesh();

        string dataFolder = NavMeshScenePath.Substring(0, NavMeshScenePath.Length - ".unity".Length);
        EnsureFolder(dataFolder);
        string assetPath = $"{dataFolder}/NavMesh-{surface.name}.asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Praktikum 4] Scene B tersimpan di {NavMeshScenePath}, NavMesh sudah di-bake ({assetPath}).");
    }

    /// <summary>Entry point command line: Unity -batchmode -executeMethod Praktikum4SceneBuilder.BuildAllScenesBatch</summary>
    public static void BuildAllScenesBatch()
    {
        BuildAStarScene();
        BuildNavMeshScene();
    }

    // ------------------------------------------------------------------ Helper umum

    private static int RequireObstacleLayer()
    {
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0)
            EditorUtility.DisplayDialog("Praktikum 4", "Layer 'Obstacle' belum ada. Tambahkan di Project Settings > Tags and Layers.", "OK");
        return layer;
    }

    private static bool PrepareNewScene(string scenePath)
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        if (!Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null &&
            !EditorUtility.DisplayDialog("Praktikum 4", $"{scenePath} sudah ada. Timpa dengan scene baru?", "Timpa", "Batal"))
            return false;

        EnsureFolder(SceneFolder);
        EnsureFolder(MaterialFolder);
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private static Material GetMaterial(string name, Color color, bool unlit = false)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size, int layer, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.layer = layer;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = mat;
        return box;
    }

    /// <summary>Kapsul tanpa collider + "hidung" kecil supaya arah hadap terlihat.</summary>
    private static GameObject CreateCapsuleBody(string name, Vector3 position, Vector3 scale, Material mat)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = name;
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.position = position;
        body.transform.localScale = scale;
        body.GetComponent<Renderer>().sharedMaterial = mat;

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Nose";
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.transform.SetParent(body.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0.45f, 0.5f);
        nose.transform.localScale = new Vector3(0.35f, 0.2f, 0.4f);
        nose.GetComponent<Renderer>().sharedMaterial = mat;

        return body;
    }

    private static Camera FindMainCamera()
    {
        Camera cam = Object.FindAnyObjectByType<Camera>();
        return cam;
    }

    private static void AddToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // Field di script Praktikum 4 bersifat private [SerializeField], jadi diisi lewat SerializedObject.
    private static void Set(Component target, string property, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogError($"[Praktikum 4] Field '{property}' tidak ditemukan di {target.GetType().Name}.");
            return;
        }

        switch (value)
        {
            case Object obj: prop.objectReferenceValue = obj; break;
            case float f: prop.floatValue = f; break;
            case int i: prop.intValue = i; break;
            case bool b: prop.boolValue = b; break;
            case Vector2 v2: prop.vector2Value = v2; break;
            case Vector3 v3: prop.vector3Value = v3; break;
            default:
                Debug.LogError($"[Praktikum 4] Tipe {value?.GetType().Name} belum didukung untuk '{property}'.");
                return;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetMask(Component target, string property, int mask)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogError($"[Praktikum 4] Field '{property}' tidak ditemukan di {target.GetType().Name}.");
            return;
        }
        prop.intValue = mask;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
