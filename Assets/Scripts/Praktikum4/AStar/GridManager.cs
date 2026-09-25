using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Membangun grid di atas lantai dan mengubahnya menjadi GRAPH untuk A*.
/// - Setiap cell = GridNode (node).
/// - Cell walkable atau tidak ditentukan dengan Physics.CheckBox terhadap layer obstacle.
/// - GetNeighbours() mendefinisikan EDGE: node mana saja yang terhubung dengan sebuah node.
///
/// Grid berpusat di posisi GameObject ini, terbentang di bidang XZ.
/// [ExecuteAlways] supaya grid dan obstacle bisa dilihat langsung di Scene view tanpa Play.
/// </summary>
[ExecuteAlways]
public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Luas area grid dalam satuan dunia (X = lebar, Y = panjang pada sumbu Z).")]
    [SerializeField] private Vector2 gridWorldSize = new Vector2(20f, 14f);
    [Tooltip("Setengah ukuran satu cell. 0.5 = cell berukuran 1 x 1.")]
    [SerializeField] private float nodeRadius = 0.5f;

    [Header("Deteksi Obstacle")]
    [Tooltip("Layer yang dianggap obstacle. Lantai TIDAK boleh ikut layer ini.")]
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("Tinggi kotak pengecekan dari permukaan grid. Obstacle yang lebih pendek dari ini tetap terdeteksi.")]
    [SerializeField] private float obstacleCheckHeight = 1f;
    [Tooltip("Skala kotak pengecekan terhadap ukuran cell. <1 supaya obstacle yang hanya menyentuh " +
             "tepi cell tetangga tidak ikut menutup cell tersebut.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float checkShrink = 0.9f;

    [Header("Gizmos")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private Color walkableColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color obstacleColor = new Color(0.85f, 0.15f, 0.15f, 0.6f);

    private GridNode[,] grid;
    private int sizeX;
    private int sizeY;

    public int SizeX => sizeX;
    public int SizeY => sizeY;
    public float NodeDiameter => nodeRadius * 2f;
    public int MaxSize => sizeX * sizeY;

    private void OnEnable()
    {
        CreateGrid();
    }

    private void OnValidate()
    {
        nodeRadius = Mathf.Max(0.05f, nodeRadius);
        gridWorldSize.x = Mathf.Max(NodeDiameter, gridWorldSize.x);
        gridWorldSize.y = Mathf.Max(NodeDiameter, gridWorldSize.y);
    }

    /// <summary>
    /// Membuat grid (atau memindai ulang obstacle kalau ukurannya sama).
    /// Return true kalau ada cell yang berubah status walkable-nya atau grid dibuat baru,
    /// sehingga pathfinder tahu path lama sudah tidak valid.
    /// </summary>
    public bool CreateGrid()
    {
        int newSizeX = Mathf.Max(1, Mathf.RoundToInt(gridWorldSize.x / NodeDiameter));
        int newSizeY = Mathf.Max(1, Mathf.RoundToInt(gridWorldSize.y / NodeDiameter));

        bool rebuilt = grid == null || newSizeX != sizeX || newSizeY != sizeY;
        if (rebuilt)
        {
            sizeX = newSizeX;
            sizeY = newSizeY;
            grid = new GridNode[sizeX, sizeY];
        }

        // Di Edit mode, posisi collider belum tentu tersinkron ke physics scene.
        if (!Application.isPlaying)
            Physics.SyncTransforms();

        bool changed = rebuilt;
        Vector3 bottomLeft = BottomLeft;
        Vector3 halfExtents = new Vector3(nodeRadius * checkShrink, obstacleCheckHeight * 0.5f, nodeRadius * checkShrink);

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                Vector3 worldPoint = bottomLeft
                                     + Vector3.right * (x * NodeDiameter + nodeRadius)
                                     + Vector3.forward * (y * NodeDiameter + nodeRadius);

                Vector3 checkCenter = worldPoint + Vector3.up * (obstacleCheckHeight * 0.5f);
                bool walkable = !Physics.CheckBox(checkCenter, halfExtents, Quaternion.identity,
                    obstacleMask, QueryTriggerInteraction.Ignore);

                if (rebuilt)
                {
                    grid[x, y] = new GridNode(walkable, worldPoint, x, y);
                }
                else
                {
                    GridNode node = grid[x, y];
                    if (node.walkable != walkable || node.worldPosition != worldPoint)
                        changed = true;
                    node.walkable = walkable;
                    node.worldPosition = worldPoint;
                }
            }
        }

        return changed;
    }

    private Vector3 BottomLeft => transform.position
                                  - Vector3.right * (sizeX * NodeDiameter * 0.5f)
                                  - Vector3.forward * (sizeY * NodeDiameter * 0.5f);

    public bool IsInside(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - BottomLeft;
        return local.x >= 0f && local.z >= 0f
               && local.x < sizeX * NodeDiameter
               && local.z < sizeY * NodeDiameter;
    }

    /// <summary>Mengubah posisi dunia menjadi node. Posisi di luar grid dijepit ke cell terdekat.</summary>
    public GridNode NodeFromWorldPoint(Vector3 worldPosition)
    {
        if (grid == null)
            CreateGrid();

        Vector3 local = worldPosition - BottomLeft;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / NodeDiameter), 0, sizeX - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(local.z / NodeDiameter), 0, sizeY - 1);
        return grid[x, y];
    }

    public IEnumerable<GridNode> AllNodes()
    {
        if (grid == null)
            CreateGrid();

        foreach (GridNode node in grid)
            yield return node;
    }

    /// <summary>
    /// EDGE dari sebuah node.
    /// 4 arah  : atas, bawah, kiri, kanan.
    /// 8 arah  : ditambah diagonal. Diagonal hanya diizinkan kalau kedua cell lurus di sampingnya
    ///           walkable, supaya agent tidak "memotong sudut" obstacle.
    /// </summary>
    public List<GridNode> GetNeighbours(GridNode node, bool allowDiagonal)
    {
        var neighbours = new List<GridNode>(allowDiagonal ? 8 : 4);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                bool isDiagonal = dx != 0 && dy != 0;
                if (isDiagonal && !allowDiagonal)
                    continue;

                int checkX = node.gridX + dx;
                int checkY = node.gridY + dy;
                if (!InBounds(checkX, checkY))
                    continue;

                if (isDiagonal)
                {
                    bool sideA = grid[node.gridX + dx, node.gridY].walkable;
                    bool sideB = grid[node.gridX, node.gridY + dy].walkable;
                    if (!sideA || !sideB)
                        continue;
                }

                neighbours.Add(grid[checkX, checkY]);
            }
        }

        return neighbours;
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < sizeX && y < sizeY;

    private void OnDrawGizmos()
    {
        if (!drawGrid)
            return;

        if (grid == null)
            CreateGrid();

        Vector3 cellSize = new Vector3(NodeDiameter * 0.95f, 0.02f, NodeDiameter * 0.95f);
        foreach (GridNode node in grid)
        {
            if (node.walkable)
            {
                Gizmos.color = walkableColor;
                Gizmos.DrawWireCube(node.worldPosition, cellSize);
            }
            else
            {
                Gizmos.color = obstacleColor;
                Gizmos.DrawCube(node.worldPosition + Vector3.up * 0.01f, cellSize);
            }
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, new Vector3(sizeX * NodeDiameter, 0.05f, sizeY * NodeDiameter));
    }
}
