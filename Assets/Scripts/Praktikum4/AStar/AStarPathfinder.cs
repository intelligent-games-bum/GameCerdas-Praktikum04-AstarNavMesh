using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hasil satu kali pencarian A*. Disimpan utuh supaya Open Set, Closed Set, dan path
/// bisa divisualisasikan setelah pencarian selesai.
/// </summary>
public class PathResult
{
    public bool found;
    public string message;
    public GridNode startNode;
    public GridNode goalNode;
    public List<GridNode> path = new List<GridNode>();
    /// <summary>Node yang sudah ditemukan tapi belum sempat dievaluasi saat pencarian berhenti.</summary>
    public List<GridNode> openSet = new List<GridNode>();
    /// <summary>Node yang sudah dievaluasi (tidak akan dicek lagi).</summary>
    public HashSet<GridNode> closedSet = new HashSet<GridNode>();
    public int iterations;
    public int totalCost;
}

/// <summary>
/// Implementasi A* manual di atas GridManager.
///
/// f(n) = g(n) + h(n)
///   g = biaya nyata dari Start ke n.
///   h = estimasi biaya dari n ke Goal (Manhattan untuk 4 arah, Octile untuk 8 arah).
///   f = total estimasi. Node dengan f terkecil di Open Set selalu dievaluasi lebih dulu.
///
/// Biaya langkah dikali 10 (lurus = 10, diagonal = 14 ≈ 10·√2) supaya tetap integer.
///
/// Pencarian ulang (repath) hanya terjadi saat ada yang BERUBAH: cell Goal pindah,
/// obstacle berubah, atau (di Edit mode) cell Start pindah. Tidak setiap frame.
/// </summary>
[ExecuteAlways]
public class AStarPathfinder : MonoBehaviour
{
    private const int StraightCost = 10;
    private const int DiagonalCost = 14;

    [Header("Referensi")]
    [SerializeField] private GridManager grid;
    [Tooltip("Titik awal pencarian. Biasanya diisi agent itu sendiri.")]
    [SerializeField] private Transform start;
    [SerializeField] private Transform goal;

    [Header("Pencarian")]
    [Tooltip("Challenge: izinkan gerak diagonal (8 arah). Heuristic otomatis berganti dari Manhattan ke Octile.")]
    [SerializeField] private bool allowDiagonal = false;

    [Header("Repath Otomatis")]
    [SerializeField] private bool autoSearch = true;
    [Tooltip("Jeda pemindaian ulang obstacle saat Play (detik). Obstacle baru/berpindah terdeteksi dalam jeda ini.")]
    [SerializeField] private float gridRefreshInterval = 0.25f;

    [Header("Challenge: Click Destination")]
    [Tooltip("Klik kiri pada lantai saat Play untuk memindahkan Goal.")]
    [SerializeField] private bool clickToSetGoal = true;
    [Tooltip("Layer lantai yang bisa diklik. Obstacle sebaiknya tidak ikut.")]
    [SerializeField] private LayerMask clickMask = 1;

    [Header("Visualisasi")]
    [SerializeField] private bool showOpenSet = true;
    [SerializeField] private bool showClosedSet = true;
    [SerializeField] private bool showPath = true;
    [Tooltip("Tampilkan angka g / h / f di tiap node yang tersentuh pencarian (Scene view).")]
    [SerializeField] private bool showCosts = false;
    [SerializeField] private bool showHud = true;
    [SerializeField] private Color openColor = new Color(0.2f, 0.65f, 1f, 0.55f);
    [SerializeField] private Color closedColor = new Color(1f, 0.6f, 0.1f, 0.45f);
    [SerializeField] private Color pathColor = new Color(0.1f, 0.9f, 0.3f, 0.9f);
    [SerializeField] private Color startColor = new Color(0.2f, 0.4f, 1f, 1f);
    [SerializeField] private Color goalColor = new Color(1f, 0.2f, 0.6f, 1f);

    /// <summary>Dipanggil setiap kali pencarian baru selesai (berhasil maupun gagal).</summary>
    public event Action<PathResult> PathUpdated;

    public PathResult LastResult { get; private set; }
    public Transform Goal => goal;

    private Vector2Int lastStartCoord = new Vector2Int(-1, -1);
    private Vector2Int lastGoalCoord = new Vector2Int(-1, -1);
    private bool lastAllowDiagonal;
    private float refreshTimer;

    private void OnEnable()
    {
        if (grid == null)
            grid = GetComponent<GridManager>();
        LastResult = null;
    }

    private void Update()
    {
        if (grid == null || start == null || goal == null)
            return;

        if (Application.isPlaying && clickToSetGoal)
            HandleClickToSetGoal();

        if (!autoSearch)
            return;

        // Edit mode: Update hanya terpanggil saat scene berubah, jadi langsung pindai.
        bool dueForRefresh = !Application.isPlaying;
        if (Application.isPlaying)
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= gridRefreshInterval)
            {
                refreshTimer = 0f;
                dueForRefresh = true;
            }
        }

        bool gridChanged = dueForRefresh && grid.CreateGrid();
        Vector2Int startCoord = grid.NodeFromWorldPoint(start.position).Coord;
        Vector2Int goalCoord = grid.NodeFromWorldPoint(goal.position).Coord;

        bool goalMoved = goalCoord != lastGoalCoord;
        // Saat Play, start = agent yang sedang berjalan. Pindah cell bukan alasan untuk mencari ulang,
        // karena path lama masih benar. Di Edit mode, start digeser manual sehingga perlu dicari ulang.
        bool startMoved = !Application.isPlaying && startCoord != lastStartCoord;
        bool settingsChanged = allowDiagonal != lastAllowDiagonal;

        if (LastResult == null || gridChanged || goalMoved || startMoved || settingsChanged)
            Search();
    }

    /// <summary>Menjalankan A* dari posisi start ke goal saat ini dan mengumumkan hasilnya.</summary>
    [ContextMenu("Find Path Now")]
    public void Search()
    {
        if (grid == null || start == null || goal == null)
            return;

        LastResult = FindPath(start.position, goal.position);
        lastStartCoord = LastResult.startNode.Coord;
        lastGoalCoord = LastResult.goalNode.Coord;
        lastAllowDiagonal = allowDiagonal;

        if (Application.isPlaying)
        {
            if (LastResult.found)
                Debug.Log($"[A*] Path ditemukan: {LastResult.path.Count} node, cost {LastResult.totalCost}, " +
                          $"{LastResult.iterations} iterasi, closed {LastResult.closedSet.Count}, open {LastResult.openSet.Count}.");
            else
                Debug.LogWarning($"[A*] {LastResult.message}");
        }

        PathUpdated?.Invoke(LastResult);
    }

    public PathResult FindPath(Vector3 fromPosition, Vector3 toPosition)
    {
        var result = new PathResult
        {
            startNode = grid.NodeFromWorldPoint(fromPosition),
            goalNode = grid.NodeFromWorldPoint(toPosition)
        };

        GridNode startNode = result.startNode;
        GridNode goalNode = result.goalNode;

        if (!startNode.walkable)
        {
            result.message = "Start berada di dalam obstacle.";
            return result;
        }
        if (!goalNode.walkable)
        {
            result.message = "Goal berada di dalam obstacle.";
            return result;
        }

        foreach (GridNode node in grid.AllNodes())
            node.ResetSearchData();

        // OPEN SET  : node yang sudah ditemukan, menunggu dievaluasi (kandidat).
        // CLOSED SET: node yang sudah dievaluasi; jalur terbaik ke node ini sudah pasti.
        // HashSet pendamping openSet hanya untuk cek keanggotaan O(1).
        var openSet = new List<GridNode>();
        var openLookup = new HashSet<GridNode>();
        var closedSet = new HashSet<GridNode>();

        startNode.gCost = 0;
        startNode.hCost = Heuristic(startNode, goalNode);
        openSet.Add(startNode);
        openLookup.Add(startNode);

        while (openSet.Count > 0)
        {
            // 1. Ambil node dengan fCost terkecil. Kalau seri, pilih hCost terkecil (lebih dekat ke Goal).
            int bestIndex = 0;
            for (int i = 1; i < openSet.Count; i++)
            {
                GridNode candidate = openSet[i];
                GridNode best = openSet[bestIndex];
                if (candidate.FCost < best.FCost ||
                    (candidate.FCost == best.FCost && candidate.hCost < best.hCost))
                    bestIndex = i;
            }

            GridNode current = openSet[bestIndex];
            openSet.RemoveAt(bestIndex);
            openLookup.Remove(current);
            closedSet.Add(current);
            result.iterations++;

            // 2. Goal diambil dari Open Set = jalur terpendek pasti sudah ditemukan.
            if (current == goalNode)
            {
                result.found = true;
                result.totalCost = goalNode.gCost;
                result.path = RetracePath(startNode, goalNode);
                result.message = "Path ditemukan.";
                break;
            }

            // 3. Periksa setiap tetangga (edge).
            foreach (GridNode neighbour in grid.GetNeighbours(current, allowDiagonal))
            {
                if (!neighbour.walkable || closedSet.Contains(neighbour))
                    continue;

                int newGCost = current.gCost + StepCost(current, neighbour);
                bool inOpenSet = openLookup.Contains(neighbour);

                // Jalur baru lebih murah, atau node ini baru pertama kali ditemukan.
                if (!inOpenSet || newGCost < neighbour.gCost)
                {
                    neighbour.gCost = newGCost;
                    neighbour.hCost = Heuristic(neighbour, goalNode);
                    neighbour.parent = current;

                    if (!inOpenSet)
                    {
                        openSet.Add(neighbour);
                        openLookup.Add(neighbour);
                    }
                }
            }
        }

        if (!result.found)
            result.message = "Path tidak ditemukan: Open Set habis sebelum Goal tercapai (Goal terisolasi).";

        result.openSet = openSet;
        result.closedSet = closedSet;
        return result;
    }

    /// <summary>
    /// Menyusun path dengan mengikuti parent dari Goal mundur ke Start, lalu dibalik.
    /// Tanpa parent, A* hanya tahu Goal bisa dicapai, tapi tidak tahu lewat mana.
    /// </summary>
    private static List<GridNode> RetracePath(GridNode startNode, GridNode goalNode)
    {
        var path = new List<GridNode>();
        GridNode current = goalNode;
        while (current != null)
        {
            path.Add(current);
            if (current == startNode)
                break;
            current = current.parent;
        }
        path.Reverse();
        return path;
    }

    private int StepCost(GridNode a, GridNode b)
    {
        bool diagonal = a.gridX != b.gridX && a.gridY != b.gridY;
        return diagonal ? DiagonalCost : StraightCost;
    }

    /// <summary>
    /// 4 arah : Manhattan = |dx| + |dy|. Tepat sama dengan jumlah langkah minimum
    ///          kalau tidak ada obstacle, jadi tidak pernah melebihi biaya sebenarnya (admissible).
    /// 8 arah : Octile. Manhattan akan melebih-lebihkan karena diagonal adalah jalan pintas.
    /// </summary>
    private int Heuristic(GridNode a, GridNode b)
    {
        int dx = Mathf.Abs(a.gridX - b.gridX);
        int dy = Mathf.Abs(a.gridY - b.gridY);

        if (!allowDiagonal)
            return StraightCost * (dx + dy);

        int diag = Mathf.Min(dx, dy);
        int straight = Mathf.Max(dx, dy) - diag;
        return DiagonalCost * diag + StraightCost * straight;
    }

    private void HandleClickToSetGoal()
    {
        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;
        if (mouse == null || cam == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, clickMask, QueryTriggerInteraction.Ignore))
            return;
        if (!grid.IsInside(hit.point))
            return;

        Vector3 cell = grid.NodeFromWorldPoint(hit.point).worldPosition;
        goal.position = new Vector3(cell.x, goal.position.y, cell.z);
    }

    private void OnDrawGizmos()
    {
        // Edit mode: scene baru dibuka belum memicu Update, jadi cari sekali agar hasil langsung terlihat.
        if (LastResult == null && !Application.isPlaying && grid != null)
        {
            grid.CreateGrid();
            Search();
        }

        if (grid == null || LastResult == null)
            return;

        float size = grid.NodeDiameter * 0.8f;
        Vector3 flat = new Vector3(size, 0.04f, size);

        if (showClosedSet)
        {
            Gizmos.color = closedColor;
            foreach (GridNode node in LastResult.closedSet)
                Gizmos.DrawCube(node.worldPosition + Vector3.up * 0.03f, flat);
        }

        if (showOpenSet)
        {
            Gizmos.color = openColor;
            foreach (GridNode node in LastResult.openSet)
                Gizmos.DrawCube(node.worldPosition + Vector3.up * 0.03f, flat);
        }

        if (showPath && LastResult.found)
        {
            Gizmos.color = pathColor;
            List<GridNode> path = LastResult.path;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 p = path[i].worldPosition + Vector3.up * 0.08f;
                Gizmos.DrawCube(p, flat * 0.5f);
                if (i > 0)
                    Gizmos.DrawLine(path[i - 1].worldPosition + Vector3.up * 0.08f, p);
            }
        }

        Gizmos.color = startColor;
        Gizmos.DrawWireCube(LastResult.startNode.worldPosition, new Vector3(size, 0.6f, size));
        Gizmos.color = goalColor;
        Gizmos.DrawWireCube(LastResult.goalNode.worldPosition, new Vector3(size, 0.6f, size));

#if UNITY_EDITOR
        if (showCosts)
            DrawCostLabels();
#endif
    }

#if UNITY_EDITOR
    private GUIStyle costStyle;

    private void DrawCostLabels()
    {
        if (costStyle == null)
        {
            // GUI.skin tidak boleh diakses di luar OnGUI, jadi style dibuat dari nol.
            costStyle = new GUIStyle
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };
        }

        foreach (GridNode node in LastResult.closedSet)
            DrawCostLabel(node);
        foreach (GridNode node in LastResult.openSet)
            DrawCostLabel(node);
    }

    private void DrawCostLabel(GridNode node)
    {
        string g = node.gCost == int.MaxValue ? "-" : node.gCost.ToString();
        UnityEditor.Handles.Label(node.worldPosition + Vector3.up * 0.1f,
            $"g{g} h{node.hCost}\nf{node.gCost + node.hCost}", costStyle);
    }
#endif

    private void OnGUI()
    {
        if (!showHud || !Application.isPlaying || LastResult == null)
            return;

        string status = LastResult.found
            ? $"FOUND  |  path {LastResult.path.Count} node  |  cost {LastResult.totalCost}"
            : $"NO PATH  |  {LastResult.message}";

        GUI.Box(new Rect(10, 10, 520, 96),
            $"A* ({(allowDiagonal ? "8 arah, Octile" : "4 arah, Manhattan")})\n" +
            $"{status}\n" +
            $"Iterasi {LastResult.iterations}  |  Closed {LastResult.closedSet.Count}  |  Open {LastResult.openSet.Count}\n" +
            "Klik kiri: pindah Goal  |  Space: ulang dari awal  |  C: label cost");
    }
}
