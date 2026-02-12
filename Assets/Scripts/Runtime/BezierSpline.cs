using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Main Bezier Spline component. Attach to a GameObject to create and edit
/// a dual-lane Bezier curve in the Scene view.
/// [ExecuteAlways] ensures this runs in Edit Mode (not just Play Mode).
/// </summary>
[ExecuteAlways]
public class BezierSpline : MonoBehaviour
{
    [Header("Control Points")]
    [Tooltip("Bezier control points. First/last are endpoints, middle ones shape the curve.")]
    public Vector3[] controlPoints = new Vector3[]
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(5f, 0f, 5f),
        new Vector3(10f, 0f, -5f),
        new Vector3(15f, 0f, 0f)
    };

    [Header("Lane Settings")]
    [Range(1f, 20f)]
    public float laneWidth = 4f;

    [Header("Curve Drawing")]
    [Range(10, 200)]
    public int curveResolution = 50;

    [Header("Generated Objects")]
    public List<GameObject> generatedNodes = new List<GameObject>();
    public GameObject roadMeshObject;

    /// <summary> Enforces 2-10 control point limit when edited via Inspector. </summary>
    private void OnValidate()
    {
        if (controlPoints != null && controlPoints.Length > 10)
        {
            System.Array.Resize(ref controlPoints, 10);
            Debug.LogWarning("BezierSpline: Maximum 10 control points allowed.");
        }
        if (controlPoints != null && controlPoints.Length < 2)
        {
            System.Array.Resize(ref controlPoints, 2);
            Debug.LogWarning("BezierSpline: Minimum 2 control points required.");
        }
    }

    // --- Lane Point Evaluation ---

    public Vector3 GetLeftLanePoint(float t)
    {
        return BezierMath.GetOffsetPoint(controlPoints, t, laneWidth / 2f);
    }

    public Vector3 GetRightLanePoint(float t)
    {
        return BezierMath.GetOffsetPoint(controlPoints, t, -laneWidth / 2f);
    }

    // --- Node Creation ---

    /// <summary>
    /// Distance Mode: creates nodes at fixed distance intervals along both lanes.
    /// Uses center curve arc-length for aligned left/right pairs.
    /// </summary>
    public void CreateNodesByDistance(float distance)
    {
        ClearNodes();

        float[] arcTable = BezierMath.BuildArcLengthTable(controlPoints);
        float totalLength = BezierMath.GetTotalLength(arcTable);
        int count = Mathf.Max(2, Mathf.FloorToInt(totalLength / distance) + 1);

        Vector3[] leftPts = new Vector3[count];
        Vector3[] rightPts = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float t = BezierMath.DistanceToT(arcTable, i * distance);
            leftPts[i] = GetLeftLanePoint(t);
            rightPts[i] = GetRightLanePoint(t);
        }

        CreateNodeGameObjects(leftPts, "LeftNode");
        CreateNodeGameObjects(rightPts, "RightNode");
        GenerateRoadMesh(leftPts, rightPts);
    }

    /// <summary>
    /// Count Mode: creates a fixed number of evenly spaced nodes along both lanes.
    /// </summary>
    public void CreateNodesByCount(int count)
    {
        if (count < 2) count = 2;
        ClearNodes();

        float[] arcTable = BezierMath.BuildArcLengthTable(controlPoints);
        float spacing = BezierMath.GetTotalLength(arcTable) / (count - 1);

        Vector3[] leftPts = new Vector3[count];
        Vector3[] rightPts = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float t = BezierMath.DistanceToT(arcTable, i * spacing);
            leftPts[i] = GetLeftLanePoint(t);
            rightPts[i] = GetRightLanePoint(t);
        }

        CreateNodeGameObjects(leftPts, "LeftNode");
        CreateNodeGameObjects(rightPts, "RightNode");
        GenerateRoadMesh(leftPts, rightPts);
    }

    /// <summary> Creates small sphere GameObjects at given positions. </summary>
    private void CreateNodeGameObjects(Vector3[] positions, string prefix)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = $"{prefix}_{i}";
            node.transform.position = positions[i];
            node.transform.localScale = Vector3.one * 0.3f;
            node.transform.SetParent(this.transform);

            // Remove unnecessary collider from visual-only spheres
            var col = node.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            generatedNodes.Add(node);
        }
    }

    /// <summary> Clears all generated nodes and the road mesh. Uses DestroyImmediate for Edit Mode. </summary>
    public void ClearNodes()
    {
        foreach (var node in generatedNodes)
            if (node != null) DestroyImmediate(node);
        generatedNodes.Clear();

        if (roadMeshObject != null)
        {
            DestroyImmediate(roadMeshObject);
            roadMeshObject = null;
        }
    }

    // --- Mesh Generation ---

    /// <summary>
    /// Triangulates between aligned left/right lane points to create a road mesh.
    /// Each consecutive pair of left-right points forms a quad (2 triangles).
    /// Also adds MeshCollider for physics interaction.
    /// </summary>
    public void GenerateRoadMesh(Vector3[] leftPoints, Vector3[] rightPoints)
    {
        int vertCount = leftPoints.Length;
        if (vertCount < 2) return;

        if (roadMeshObject != null) DestroyImmediate(roadMeshObject);

        roadMeshObject = new GameObject("RoadMesh");
        roadMeshObject.transform.SetParent(this.transform);

        MeshFilter meshFilter = roadMeshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = roadMeshObject.AddComponent<MeshRenderer>();

        // Double-sided material (Cull Off) so mesh is visible from both sides
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.4f, 0.6f, 0.4f, 1f);
        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        meshRenderer.sharedMaterial = mat;

        Mesh mesh = new Mesh { name = "RoadMesh" };

        // Vertices: interleaved left[i], right[i] pairs
        Vector3[] verts = new Vector3[vertCount * 2];
        for (int i = 0; i < vertCount; i++)
        {
            verts[i * 2] = leftPoints[i];
            verts[i * 2 + 1] = rightPoints[i];
        }

        // Triangles: 2 per quad, counter-clockwise winding for front face
        int quadCount = vertCount - 1;
        int[] tris = new int[quadCount * 6];
        for (int i = 0; i < quadCount; i++)
        {
            int idx = i * 6;
            int bl = i * 2, br = i * 2 + 1;
            int tl = (i + 1) * 2, tr = (i + 1) * 2 + 1;

            tris[idx] = bl;     tris[idx + 1] = tl;  tris[idx + 2] = br;
            tris[idx + 3] = br; tris[idx + 4] = tl;  tris[idx + 5] = tr;
        }

        // UVs: u=0 (left) to u=1 (right), v=0 (start) to v=1 (end)
        Vector2[] uvs = new Vector2[vertCount * 2];
        for (int i = 0; i < vertCount; i++)
        {
            float v = (float)i / (vertCount - 1);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;

        // Add MeshCollider only if we have enough geometry
        if (vertCount >= 3)
        {
            MeshCollider mc = roadMeshObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }
    }

    // --- Gizmo Drawing ---

    private void OnDrawGizmos()
    {
        if (controlPoints == null || controlPoints.Length < 2) return;

        // Center curve (yellow)
        Gizmos.color = Color.yellow;
        DrawBezierGizmo(controlPoints);

        // Left lane (green)
        Gizmos.color = Color.green;
        DrawOffsetGizmo(laneWidth / 2f);

        // Right lane (blue)
        Gizmos.color = Color.blue;
        DrawOffsetGizmo(-laneWidth / 2f);

        // Control points: red spheres for endpoints, green for reference points
        for (int i = 0; i < controlPoints.Length; i++)
        {
            bool isEndpoint = (i == 0 || i == controlPoints.Length - 1);
            Gizmos.color = isEndpoint ? Color.red : Color.green;
            Gizmos.DrawSphere(controlPoints[i], isEndpoint ? 0.4f : 0.25f);

            // Control polygon lines
            if (i < controlPoints.Length - 1)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawLine(controlPoints[i], controlPoints[i + 1]);
            }
        }
    }

    private void DrawBezierGizmo(Vector3[] cp)
    {
        Vector3 prev = BezierMath.EvaluateCurve(cp, 0f);
        for (int i = 1; i <= curveResolution; i++)
        {
            float t = (float)i / curveResolution;
            Vector3 curr = BezierMath.EvaluateCurve(cp, t);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }

    private void DrawOffsetGizmo(float offset)
    {
        Vector3 prev = BezierMath.GetOffsetPoint(controlPoints, 0f, offset);
        for (int i = 1; i <= curveResolution; i++)
        {
            float t = (float)i / curveResolution;
            Vector3 curr = BezierMath.GetOffsetPoint(controlPoints, t, offset);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }
}