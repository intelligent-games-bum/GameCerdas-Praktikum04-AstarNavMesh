using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC pengejar berbasis NavMeshAgent.
///
/// Pembagian tugas:
/// - NavMeshSurface  : menyediakan peta area yang bisa dilalui (hasil Bake).
/// - NavMeshAgent    : mencari jalur di peta itu (pathfinding) DAN mengikutinya (path following + movement).
/// - Script ini      : hanya memutuskan KAPAN tujuan perlu diperbarui (repathing) dan kapan berhenti.
///
/// Repathing dibatasi: SetDestination hanya dipanggil tiap repathInterval detik, dan hanya kalau
/// target sudah bergeser lebih dari repathMoveThreshold. Menghitung ulang path setiap frame
/// membuang CPU (terutama dengan banyak agent) padahal hasilnya hampir sama.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshChaser : MonoBehaviour
{
    public enum ChaseState { Idle, Chasing, Reached, Unreachable }

    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Berhenti")]
    [Tooltip("NPC berhenti saat sisa jarak jalur <= nilai ini. Disalin ke NavMeshAgent.stoppingDistance.")]
    [SerializeField] private float stoppingDistance = 2f;
    [Tooltip("Saat berhenti, NPC tetap memutar badan menghadap target.")]
    [SerializeField] private bool faceTargetWhenStopped = true;
    [SerializeField] private float turnSpeed = 8f;

    [Header("Repathing")]
    [Tooltip("Jeda minimum antar-perhitungan path (detik).")]
    [SerializeField] private float repathInterval = 0.25f;
    [Tooltip("Target harus bergeser minimal sejauh ini dari tujuan terakhir sebelum path dihitung ulang.")]
    [SerializeField] private float repathMoveThreshold = 0.5f;
    [Tooltip("Jarak pencarian titik NavMesh terdekat dari posisi target.")]
    [SerializeField] private float sampleRadius = 2f;
    [SerializeField] private bool logRepath = false;

    private NavMeshAgent agent;
    private Vector3 lastDestination;
    private bool hasDestination;
    private float repathTimer;

    public ChaseState State { get; private set; } = ChaseState.Idle;
    public int RepathCount { get; private set; }
    public Transform Target => target;
    public float StoppingDistance => stoppingDistance;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        agent.stoppingDistance = stoppingDistance;
        RequestPath();
    }

    private void OnValidate()
    {
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        repathInterval = Mathf.Max(0f, repathInterval);
        if (agent != null)
            agent.stoppingDistance = stoppingDistance;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        hasDestination = false;
        RequestPath();
    }

    private void Update()
    {
        if (target == null || !agent.isOnNavMesh)
        {
            State = ChaseState.Idle;
            return;
        }

        repathTimer += Time.deltaTime;
        if (repathTimer >= repathInterval)
        {
            repathTimer = 0f;
            if (!hasDestination || (target.position - lastDestination).sqrMagnitude > repathMoveThreshold * repathMoveThreshold)
                RequestPath();
        }

        UpdateState();

        if (State == ChaseState.Reached && faceTargetWhenStopped)
            FaceTarget();
    }

    private void RequestPath()
    {
        if (target == null || agent == null || !agent.isOnNavMesh)
            return;

        // Target bisa sedikit di luar NavMesh (melompat, di tepi obstacle). Cari titik valid terdekat.
        Vector3 destination = target.position;
        if (NavMesh.SamplePosition(target.position, out NavMeshHit hit, sampleRadius, agent.areaMask))
            destination = hit.position;

        agent.SetDestination(destination);
        lastDestination = target.position;
        hasDestination = true;
        RepathCount++;

        if (logRepath)
            Debug.Log($"[NavMeshChaser] Repath #{RepathCount} ke {destination}");
    }

    private void UpdateState()
    {
        if (agent.pathPending)
            return;

        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            State = ChaseState.Unreachable;
            return;
        }

        // PathPartial: target tidak bisa dicapai, agent berjalan ke titik terdekat yang bisa dicapai.
        bool closeEnough = agent.remainingDistance <= agent.stoppingDistance;
        if (agent.pathStatus == NavMeshPathStatus.PathPartial)
            State = closeEnough ? ChaseState.Unreachable : ChaseState.Chasing;
        else
            State = closeEnough ? ChaseState.Reached : ChaseState.Chasing;
    }

    private void FaceTarget()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }
}
