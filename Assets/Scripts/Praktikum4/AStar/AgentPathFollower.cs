using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PATH FOLLOWING: menggerakkan agent menyusuri path hasil A*.
///
/// Agent tidak tahu apa pun soal obstacle. Semua kecerdasan menghindari dinding sudah
/// ada di path (pathfinding). Tugas script ini hanya: ambil waypoint berikutnya, jalan ke sana,
/// ulangi sampai waypoint terakhir (movement).
///
/// Path baru dari AStarPathfinder (Goal pindah, obstacle berubah) langsung menggantikan path lama.
/// </summary>
public class AgentPathFollower : MonoBehaviour
{
    public enum FollowState { Idle, Following, Arrived, NoPath }

    [Header("Referensi")]
    [SerializeField] private AStarPathfinder pathfinder;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float turnSpeed = 10f;
    [Tooltip("Jarak dianggap sudah sampai di sebuah waypoint.")]
    [SerializeField] private float waypointTolerance = 0.05f;
    [Tooltip("Mulai berjalan otomatis begitu path pertama tersedia. Kalau mati, tekan Space.")]
    [SerializeField] private bool autoStart = true;

    [Header("Gizmos")]
    [SerializeField] private Color remainingPathColor = Color.yellow;

    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int waypointIndex;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool started;

    public FollowState State { get; private set; } = FollowState.Idle;

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        started = autoStart;
    }

    private void OnEnable()
    {
        if (pathfinder != null)
            pathfinder.PathUpdated += OnPathUpdated;
    }

    private void OnDisable()
    {
        if (pathfinder != null)
            pathfinder.PathUpdated -= OnPathUpdated;
    }

    private void Start()
    {
        if (pathfinder != null && pathfinder.LastResult != null)
            OnPathUpdated(pathfinder.LastResult);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            RestartFromSpawn();

        if (!started || State != FollowState.Following)
            return;

        Vector3 target = waypoints[waypointIndex];
        Vector3 toTarget = target - transform.position;

        if (toTarget.magnitude <= waypointTolerance)
        {
            waypointIndex++;
            if (waypointIndex >= waypoints.Count)
            {
                State = FollowState.Arrived;
                Debug.Log("[Follower] Sampai di Goal.");
            }
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(flatDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
    }

    private void OnPathUpdated(PathResult result)
    {
        waypoints.Clear();
        waypointIndex = 0;

        if (result == null || !result.found)
        {
            State = FollowState.NoPath;
            return;
        }

        // Tinggi waypoint disamakan dengan tinggi agent; A* hanya mengurus bidang XZ.
        foreach (GridNode node in result.path)
            waypoints.Add(new Vector3(node.worldPosition.x, transform.position.y, node.worldPosition.z));

        // Waypoint pertama = cell tempat agent berdiri. Tetap dikunjungi (paling jauh setengah cell)
        // supaya agent kembali ke tengah cell dulu dan tidak memotong sudut obstacle.
        State = waypoints.Count > 0 ? FollowState.Following : FollowState.Arrived;
    }

    private void RestartFromSpawn()
    {
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        started = true;
        if (pathfinder != null)
            pathfinder.Search();
    }

    private void OnDrawGizmos()
    {
        if (State != FollowState.Following || waypointIndex >= waypoints.Count)
            return;

        Gizmos.color = remainingPathColor;
        Gizmos.DrawLine(transform.position, waypoints[waypointIndex]);
        for (int i = waypointIndex + 1; i < waypoints.Count; i++)
            Gizmos.DrawLine(waypoints[i - 1], waypoints[i]);
        Gizmos.DrawWireSphere(waypoints[waypointIndex], 0.15f);
    }
}
