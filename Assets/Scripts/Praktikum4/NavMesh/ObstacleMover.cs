using UnityEngine;

/// <summary>
/// Menggerakkan obstacle bolak-balik antara dua titik, dengan jeda di tiap ujung.
/// Dipakai untuk eksperimen NavMeshObstacle:
/// - Carve ON  : lubang di NavMesh ikut berpindah, agent mencari jalur lain.
///   Dengan "Carve Only Stationary", lubang baru diperbarui saat obstacle diam (di jeda ujung).
/// - Carve OFF : NavMesh tidak berubah, agent hanya menghindar lokal dan bisa terdorong/tertahan.
/// </summary>
public class ObstacleMover : MonoBehaviour
{
    [Tooltip("Offset titik A dan B relatif terhadap posisi awal.")]
    [SerializeField] private Vector3 offsetA = new Vector3(0f, 0f, -3f);
    [SerializeField] private Vector3 offsetB = new Vector3(0f, 0f, 3f);
    [SerializeField] private float speed = 2f;
    [Tooltip("Lama diam di tiap ujung (detik).")]
    [SerializeField] private float pauseDuration = 1.5f;

    private Vector3 origin;
    private bool towardB = true;
    private float pauseTimer;

    private void Awake()
    {
        origin = transform.position;
    }

    private void Update()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        Vector3 target = origin + (towardB ? offsetB : offsetA);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            towardB = !towardB;
            pauseTimer = pauseDuration;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 basePos = Application.isPlaying ? origin : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(basePos + offsetA, basePos + offsetB);
        Gizmos.DrawWireCube(basePos + offsetA, transform.lossyScale);
        Gizmos.DrawWireCube(basePos + offsetB, transform.lossyScale);
    }
}
