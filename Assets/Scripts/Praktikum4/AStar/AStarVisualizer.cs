using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// PATH VISUALIZER: menampilkan hasil A* sebagai tile mesh sungguhan di atas lantai.
///
/// Berbeda dengan Gizmos, tile ini adalah objek biasa, sehingga:
/// - selalu tampil di Game view tanpa tombol Gizmos,
/// - ikut tampil di hasil build,
/// - tidak terkena bug depth gizmo di Game view.
///
/// Warna tile (prioritas tertinggi di atas):
///   path > start/goal > open set > closed set > obstacle > walkable.
///
/// Tampilan diperbarui hanya saat AStarPathfinder mengumumkan hasil baru (PathUpdated),
/// bukan setiap frame. Hanya aktif saat Play; di Edit mode pakai Gizmos.
/// </summary>
public class AStarVisualizer : MonoBehaviour
{
    private enum TileState { Walkable, Obstacle, Closed, Open, Start, Goal, Path }

    [Header("Referensi")]
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private GridManager grid;
    [Tooltip("Material unlit dasar. Warna tiap state dibuat dari salinan material ini. " +
             "Kosong = dibuat otomatis dari shader URP Unlit.")]
    [SerializeField] private Material baseMaterial;

    [Header("Tile")]
    [Tooltip("Skala tile terhadap ukuran cell. <1 menyisakan celah sebagai garis grid.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float tileScale = 0.92f;
    [SerializeField] private float tileHeight = 0.02f;

    [Header("Warna")]
    [SerializeField] private Color walkableColor = new Color(0.93f, 0.93f, 0.88f);
    [SerializeField] private Color obstacleColor = new Color(0.75f, 0.3f, 0.3f);
    [SerializeField] private Color closedColor = new Color(0.93f, 0.78f, 0.55f);
    [SerializeField] private Color openColor = new Color(0.55f, 0.75f, 0.95f);
    [SerializeField] private Color pathColor = new Color(0.3f, 0.85f, 0.45f);
    [SerializeField] private Color startColor = new Color(0.3f, 0.5f, 1f);
    [SerializeField] private Color goalColor = new Color(1f, 0.35f, 0.6f);

    [Header("Garis Path")]
    [SerializeField] private bool drawPathLine = true;
    [SerializeField] private float lineWidth = 0.12f;
    [SerializeField] private Color lineColor = new Color(0.05f, 0.45f, 0.15f);

    [Header("Label Cost")]
    [Tooltip("Tampilkan g, h, dan f di tiap node Open/Closed. Tekan C saat Play untuk menyalakan/mematikan.")]
    [SerializeField] private bool showCosts = false;
    [SerializeField] private Color costTextColor = new Color(0.1f, 0.1f, 0.1f);

    private Transform tileRoot;
    private MeshRenderer[,] tiles;
    private TextMesh[,] labels;
    private Material[] stateMaterials;
    private LineRenderer pathLine;
    private PathResult shownResult;

    private void Awake()
    {
        if (pathfinder == null)
            pathfinder = GetComponent<AStarPathfinder>();
        if (grid == null)
            grid = GetComponent<GridManager>();

        CreateMaterials();
        CreatePathLine();
    }

    private void OnEnable()
    {
        if (pathfinder != null)
            pathfinder.PathUpdated += Show;
    }

    private void OnDisable()
    {
        if (pathfinder != null)
            pathfinder.PathUpdated -= Show;
    }

    private void Start()
    {
        if (pathfinder != null && pathfinder.LastResult != null)
            Show(pathfinder.LastResult);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
        {
            showCosts = !showCosts;
            if (shownResult != null)
                Show(shownResult);
        }
    }

    private void OnDestroy()
    {
        if (pathLine != null)
            Destroy(pathLine.sharedMaterial);
        if (stateMaterials == null)
            return;
        foreach (Material mat in stateMaterials)
            Destroy(mat);
    }

    /// <summary>Mewarnai ulang seluruh tile sesuai hasil pencarian.</summary>
    public void Show(PathResult result)
    {
        if (grid == null || result == null)
            return;

        shownResult = result;
        EnsureTiles();

        var states = new TileState[grid.SizeX, grid.SizeY];
        foreach (GridNode node in grid.AllNodes())
            states[node.gridX, node.gridY] = node.walkable ? TileState.Walkable : TileState.Obstacle;

        foreach (GridNode node in result.closedSet)
            states[node.gridX, node.gridY] = TileState.Closed;
        foreach (GridNode node in result.openSet)
            states[node.gridX, node.gridY] = TileState.Open;

        if (result.found)
        {
            foreach (GridNode node in result.path)
                states[node.gridX, node.gridY] = TileState.Path;
        }

        states[result.startNode.gridX, result.startNode.gridY] = TileState.Start;
        states[result.goalNode.gridX, result.goalNode.gridY] = TileState.Goal;

        foreach (GridNode node in grid.AllNodes())
        {
            TileState state = states[node.gridX, node.gridY];
            MeshRenderer tile = tiles[node.gridX, node.gridY];
            tile.transform.position = node.worldPosition + Vector3.up * tileHeight;
            tile.sharedMaterial = stateMaterials[(int)state];

            UpdateLabel(node, state);
        }

        UpdatePathLine(result);
    }

    private void EnsureTiles()
    {
        if (tiles != null && tiles.GetLength(0) == grid.SizeX && tiles.GetLength(1) == grid.SizeY)
            return;

        if (tileRoot != null)
            Destroy(tileRoot.gameObject);

        tileRoot = new GameObject("AStar Tiles").transform;
        tileRoot.SetParent(transform, false);
        tiles = new MeshRenderer[grid.SizeX, grid.SizeY];
        labels = new TextMesh[grid.SizeX, grid.SizeY];

        float size = grid.NodeDiameter * tileScale;
        foreach (GridNode node in grid.AllNodes())
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Tile {node.gridX},{node.gridY}";
            // Tile tidak boleh ikut kena raycast klik atau deteksi obstacle.
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(tileRoot, false);
            quad.transform.SetPositionAndRotation(node.worldPosition + Vector3.up * tileHeight, Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(size, size, 1f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            tiles[node.gridX, node.gridY] = renderer;
        }
    }

    private void UpdateLabel(GridNode node, TileState state)
    {
        bool visited = state == TileState.Open || state == TileState.Closed || state == TileState.Path
                       || state == TileState.Start || state == TileState.Goal;
        bool hasCost = visited && node.gCost != int.MaxValue;

        TextMesh label = labels[node.gridX, node.gridY];
        if (!showCosts || !hasCost)
        {
            if (label != null)
                label.gameObject.SetActive(false);
            return;
        }

        if (label == null)
        {
            label = CreateLabel(tiles[node.gridX, node.gridY].transform);
            labels[node.gridX, node.gridY] = label;
        }

        label.gameObject.SetActive(true);
        label.text = $"g{node.gCost} h{node.hCost}\nf{node.FCost}";
    }

    private TextMesh CreateLabel(Transform tile)
    {
        var go = new GameObject("Cost");
        // Anak dari tile (yang sudah diputar menghadap atas), sedikit di depan permukaannya.
        go.transform.SetParent(tile, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(1f / tile.localScale.x, 1f / tile.localScale.y, 1f) * grid.NodeDiameter;

        var text = go.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 48;
        text.characterSize = 0.03f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = costTextColor;
        go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        return text;
    }

    private void UpdatePathLine(PathResult result)
    {
        if (pathLine == null)
            return;

        if (!drawPathLine || !result.found)
        {
            pathLine.positionCount = 0;
            return;
        }

        pathLine.positionCount = result.path.Count;
        for (int i = 0; i < result.path.Count; i++)
            pathLine.SetPosition(i, result.path[i].worldPosition + Vector3.up * (tileHeight + 0.05f));
    }

    private void CreateMaterials()
    {
        Material source = baseMaterial;
        if (source == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            source = new Material(shader);
        }

        Color[] colors =
        {
            walkableColor, obstacleColor, closedColor, openColor, startColor, goalColor, pathColor
        };

        stateMaterials = new Material[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            stateMaterials[i] = CreateColoredMaterial(source, colors[i], ((TileState)i).ToString());
    }

    private static Material CreateColoredMaterial(Material source, Color color, string label)
    {
        var mat = new Material(source) { name = $"AStarTile_{label}", color = color };
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        return mat;
    }

    private void CreatePathLine()
    {
        var go = new GameObject("AStar Path Line");
        go.transform.SetParent(transform, false);

        pathLine = go.AddComponent<LineRenderer>();
        pathLine.useWorldSpace = true;
        pathLine.startWidth = lineWidth;
        pathLine.endWidth = lineWidth;
        pathLine.positionCount = 0;
        pathLine.numCornerVertices = 2;
        pathLine.shadowCastingMode = ShadowCastingMode.Off;
        pathLine.receiveShadows = false;
        pathLine.sharedMaterial = CreateColoredMaterial(
            baseMaterial != null ? baseMaterial : stateMaterials[0], lineColor, "PathLine");
    }
}
