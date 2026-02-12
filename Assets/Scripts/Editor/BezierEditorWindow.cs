using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor Window for Bezier Spline node placement.
/// Provides Distance Mode and Count Mode for distributing nodes along the curve.
/// Accessible via menu: Tools > Bezier Spline Editor
/// </summary>
public class BezierEditorWindow : EditorWindow
{
    private float distance = 1f;
    private float minDistance = 0.1f;
    private float maxDistance = 20f;

    private int count = 5;
    private int minCount = 2;
    private int maxCount = 100;

    private BezierSpline targetSpline;

    [MenuItem("Tools/Bezier Spline Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<BezierEditorWindow>("Curve Second Phase");
        window.minSize = new Vector2(350, 300);
    }

    private void OnGUI()
    {
        // Title
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            normal = { textColor = new Color(0.2f, 0.6f, 1f) },
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Curve Second Phase", titleStyle, GUILayout.Height(30));
        EditorGUILayout.Space(15);

        // Find or create spline
        FindTargetSpline();

        if (targetSpline == null)
        {
            EditorGUILayout.HelpBox(
                "No BezierSpline found in scene.\nCreate one first.",
                MessageType.Warning);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create Bezier Spline", GUILayout.Height(35)))
                CreateBezierSpline();
            return;
        }

        // Info box
        EditorGUILayout.HelpBox(
            $"Active Spline: {targetSpline.gameObject.name}\n" +
            $"Control Points: {targetSpline.controlPoints.Length} | " +
            $"Degree: {targetSpline.controlPoints.Length - 1}",
            MessageType.Info);
        EditorGUILayout.Space(15);

        // Styled red label for section headers
        GUIStyle redLabel = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.9f, 0.3f, 0.3f) }
        };

        // --- Distance Mode ---
        EditorGUILayout.LabelField("Distance", redLabel);
        distance = EditorGUILayout.Slider(distance, minDistance, maxDistance);

        if (GUILayout.Button("Distance Mode", GUILayout.Height(30)))
        {
            Undo.RecordObject(targetSpline, "Distance Mode");
            targetSpline.CreateNodesByDistance(distance);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(15);

        // --- Count Mode ---
        EditorGUILayout.LabelField("Count", redLabel);
        count = EditorGUILayout.IntSlider(count, minCount, maxCount);

        if (GUILayout.Button("Count Mode", GUILayout.Height(30)))
        {
            Undo.RecordObject(targetSpline, "Count Mode");
            targetSpline.CreateNodesByCount(count);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(20);

        // --- Cancel ---
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Cancel", GUILayout.Height(35)))
        {
            Undo.RecordObject(targetSpline, "Cancel");
            targetSpline.ClearNodes();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
    }

    /// <summary> Finds an existing BezierSpline in the scene (selected object or first found). </summary>
    private void FindTargetSpline()
    {
        if (targetSpline != null) return;

        if (Selection.activeGameObject != null)
        {
            targetSpline = Selection.activeGameObject.GetComponent<BezierSpline>();
            if (targetSpline != null) return;
        }

        targetSpline = FindFirstObjectByType<BezierSpline>();
    }

    /// <summary>
    /// Creates a new BezierSpline at the Scene camera's look-at point on the ground plane (Y=0).
    /// Falls back to world origin if the camera doesn't intersect the ground.
    /// </summary>
    private void CreateBezierSpline()
    {
        // Get the active Scene view camera
        Vector3 spawnOrigin = Vector3.zero;
        SceneView sceneView = SceneView.lastActiveSceneView;

        if (sceneView != null)
        {
            Camera cam = sceneView.camera;

            // Raycast from camera center onto the ground plane (Y = 0)
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float hitDistance))
            {
                spawnOrigin = ray.GetPoint(hitDistance);
                spawnOrigin.y = 0f; // Ensure exactly on ground
            }
        }

        // Create spline object
        GameObject obj = new GameObject("BezierSpline");
        targetSpline = obj.AddComponent<BezierSpline>();

        // Place control points relative to camera hit point
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        // Use camera's forward direction projected onto XZ plane if available
        if (sceneView != null)
        {
            forward = sceneView.camera.transform.forward;
            forward.y = 0f;
            forward = forward.normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        // Spread 4 default control points along the camera's forward direction
        targetSpline.controlPoints = new Vector3[]
        {
            spawnOrigin,
            spawnOrigin + forward * 5f + right * 3f,
            spawnOrigin + forward * 10f - right * 3f,
            spawnOrigin + forward * 15f
        };

        Undo.RegisterCreatedObjectUndo(obj, "Create Bezier Spline");
        Selection.activeGameObject = obj;
        SceneView.RepaintAll();
    }

    private void OnFocus() { targetSpline = null; }

    private void OnSelectionChange()
    {
        targetSpline = null;
        Repaint();
    }
}