using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Visualisasi path NavMeshAgent.
/// - LineRenderer : garis path (agent.path.corners), terlihat di Game view dan hasil build.
/// - Gizmos       : titik corner, tujuan, dan lingkaran stopping distance (Scene view).
/// - HUD          : status path, jumlah corner, sisa jarak, jumlah repath.
///
/// NavMesh path hanya berisi CORNER (titik belok), bukan cell per cell seperti A* grid.
/// Di antara dua corner, agent berjalan lurus.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshPathDebugger : MonoBehaviour
{
    [Header("Line Renderer")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float lineWidth = 0.15f;
    [SerializeField] private float lineHeightOffset = 0.1f;
    [SerializeField] private Color lineColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Header("Gizmos")]
    [SerializeField] private Color cornerColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private Color stoppingColor = new Color(0.2f, 1f, 0.4f, 1f);

    [Header("HUD")]
    [SerializeField] private bool showHud = true;
    [Tooltip("Urutan kotak HUD, supaya beberapa NPC tidak saling menimpa.")]
    [SerializeField] private int hudSlot = 0;

    private NavMeshAgent agent;
    private NavMeshChaser chaser;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        chaser = GetComponent<NavMeshChaser>();

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null)
            SetupLineRenderer();
    }

    private void SetupLineRenderer()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.positionCount = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        if (lineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = lineColor;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", lineColor);
                lineRenderer.material = mat;
            }
        }
    }

    private void LateUpdate()
    {
        if (lineRenderer == null)
            return;

        if (!agent.hasPath)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        Vector3[] corners = agent.path.corners;
        lineRenderer.positionCount = corners.Length;
        for (int i = 0; i < corners.Length; i++)
            lineRenderer.SetPosition(i, corners[i] + Vector3.up * lineHeightOffset);
    }

    private void OnDrawGizmos()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            return;

        float stopDistance = Application.isPlaying ? agent.stoppingDistance : GetConfiguredStoppingDistance();

#if UNITY_EDITOR
        UnityEditor.Handles.color = stoppingColor;
        Transform stopCenter = GetTarget();
        if (stopCenter != null)
            UnityEditor.Handles.DrawWireDisc(stopCenter.position, Vector3.up, stopDistance);
#endif

        if (!Application.isPlaying || !agent.hasPath)
            return;

        Vector3[] corners = agent.path.corners;
        Gizmos.color = cornerColor;
        for (int i = 0; i < corners.Length; i++)
        {
            Gizmos.DrawSphere(corners[i] + Vector3.up * lineHeightOffset, 0.18f);
            if (i > 0)
                Gizmos.DrawLine(corners[i - 1] + Vector3.up * lineHeightOffset, corners[i] + Vector3.up * lineHeightOffset);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(agent.destination + Vector3.up * 0.5f, Vector3.one);
    }

    private Transform GetTarget()
    {
        if (chaser == null)
            chaser = GetComponent<NavMeshChaser>();
        return chaser != null ? chaser.Target : null;
    }

    private float GetConfiguredStoppingDistance()
    {
        if (chaser == null)
            chaser = GetComponent<NavMeshChaser>();
        return chaser != null ? chaser.StoppingDistance : agent.stoppingDistance;
    }

    private void OnGUI()
    {
        if (!showHud || agent == null)
            return;

        string state = chaser != null ? chaser.State.ToString() : "-";
        string repath = chaser != null ? chaser.RepathCount.ToString() : "-";
        int corners = agent.hasPath ? agent.path.corners.Length : 0;
        string remaining = agent.pathPending ? "menghitung..." : agent.remainingDistance.ToString("0.00");

        GUI.Box(new Rect(10, 10 + hudSlot * 100, 330, 92),
            $"{name}\n" +
            $"State {state}  |  Path {agent.pathStatus}\n" +
            $"Corner {corners}  |  Sisa {remaining}  |  Stop {agent.stoppingDistance:0.0}\n" +
            $"Repath {repath}  |  Speed {agent.velocity.magnitude:0.0}");
    }
}
