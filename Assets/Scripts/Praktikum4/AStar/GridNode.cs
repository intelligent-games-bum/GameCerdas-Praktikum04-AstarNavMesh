using UnityEngine;

/// <summary>
/// Satu cell pada grid = satu NODE pada graph.
/// Hubungan ke cell tetangga (atas/bawah/kiri/kanan, opsional diagonal) = EDGE.
///
/// Node menyimpan dua jenis data:
/// 1. Data grid (tetap)    : posisi, index, walkable.
/// 2. Data pencarian (A*)  : gCost, hCost, parent. Direset setiap kali A* dijalankan.
///
/// Sengaja dibuat class biasa (bukan MonoBehaviour) karena node hanyalah data,
/// bukan objek yang hidup di scene.
/// </summary>
public class GridNode
{
    // ---------- Data grid ----------
    public readonly int gridX;
    public readonly int gridY;
    public Vector3 worldPosition;

    /// <summary>false = cell ini tertutup obstacle, tidak boleh dilewati.</summary>
    public bool walkable;

    // ---------- Data pencarian A* ----------
    /// <summary>Biaya NYATA dari Start ke node ini, lewat jalur terbaik yang sudah ditemukan.</summary>
    public int gCost;

    /// <summary>ESTIMASI biaya dari node ini ke Goal (heuristic). Tidak pernah melebihi biaya sebenarnya.</summary>
    public int hCost;

    /// <summary>
    /// Node sebelumnya pada jalur terbaik menuju node ini.
    /// Setelah Goal ditemukan, jalur disusun ulang dengan mengikuti parent dari Goal mundur ke Start.
    /// </summary>
    public GridNode parent;

    /// <summary>Total estimasi biaya jalur Start -> node ini -> Goal. A* selalu mengembangkan fCost terkecil.</summary>
    public int FCost => gCost + hCost;

    public Vector2Int Coord => new Vector2Int(gridX, gridY);

    public GridNode(bool walkable, Vector3 worldPosition, int gridX, int gridY)
    {
        this.walkable = walkable;
        this.worldPosition = worldPosition;
        this.gridX = gridX;
        this.gridY = gridY;
        ResetSearchData();
    }

    public void ResetSearchData()
    {
        gCost = int.MaxValue;
        hCost = 0;
        parent = null;
    }
}
