using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Editor for BezierSpline. Draws interactive position handles
/// in the Scene view and adds control point management buttons in the Inspector.
/// </summary>
[CustomEditor(typeof(BezierSpline))]
public class BezierSplineEditor : Editor
{
    private BezierSpline spline;

    private void OnEnable()
    {
        spline = target as BezierSpline;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10);

        // Control point add/remove (Bonus: supports up to 10 points)
        EditorGUILayout.LabelField("Control Point Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Point (+)"))
            AddControlPoint();

        GUI.enabled = spline.controlPoints.Length > 2;
        if (GUILayout.Button("Remove Point (-)"))
            RemoveLastControlPoint();
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            $"Control Points: {spline.controlPoints.Length} | " +
            $"Degree: {spline.controlPoints.Length - 1} | Max: 10",
            MessageType.Info);

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Clear Nodes"))
        {
            Undo.RecordObject(spline, "Clear Nodes");
            spline.ClearNodes();
        }
    }

    /// <summary> Draws draggable position handles for each control point in the Scene view. </summary>
    private void OnSceneGUI()
    {
        if (spline.controlPoints == null) return;

        for (int i = 0; i < spline.controlPoints.Length; i++)
        {
            bool isEndpoint = (i == 0 || i == spline.controlPoints.Length - 1);
            Handles.color = isEndpoint ? Color.red : Color.green;

            // Label above the handle
            string label = i == 0 ? "Start" :
                          i == spline.controlPoints.Length - 1 ? "End" :
                          $"Ref {i}";
            Handles.Label(spline.controlPoints[i] + Vector3.up * 0.5f, label);

            // Draggable position handle
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(spline.controlPoints[i], Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, "Move Control Point");
                spline.controlPoints[i] = newPos;
                EditorUtility.SetDirty(spline);
            }
        }

        DrawCurveHandles();
    }

    /// <summary> Draws the center curve and both lane offset curves using Handles API. </summary>
    private void DrawCurveHandles()
    {
        if (spline.controlPoints.Length < 2) return;

        // Center (yellow), left (green), right (blue)
        Handles.color = Color.yellow;
        DrawCurve(spline.controlPoints);

        Handles.color = Color.green;
        DrawCurveOffset(spline.laneWidth / 2f);

        Handles.color = Color.blue;
        DrawCurveOffset(-spline.laneWidth / 2f);

        // Dotted control polygon
        Handles.color = new Color(1f, 1f, 1f, 0.3f);
        for (int i = 0; i < spline.controlPoints.Length - 1; i++)
            Handles.DrawDottedLine(spline.controlPoints[i], spline.controlPoints[i + 1], 4f);
    }

    private void DrawCurve(Vector3[] cp)
    {
        Vector3 prev = BezierMath.EvaluateCurve(cp, 0f);
        for (int i = 1; i <= spline.curveResolution; i++)
        {
            float t = (float)i / spline.curveResolution;
            Vector3 curr = BezierMath.EvaluateCurve(cp, t);
            Handles.DrawLine(prev, curr);
            prev = curr;
        }
    }

    private void DrawCurveOffset(float offset)
    {
        Vector3 prev = BezierMath.GetOffsetPoint(spline.controlPoints, 0f, offset);
        for (int i = 1; i <= spline.curveResolution; i++)
        {
            float t = (float)i / spline.curveResolution;
            Vector3 curr = BezierMath.GetOffsetPoint(spline.controlPoints, t, offset);
            Handles.DrawLine(prev, curr);
            prev = curr;
        }
    }

    /// <summary> Appends a new control point in the direction of the last segment. Max 10. </summary>
    private void AddControlPoint()
    {
        if (spline.controlPoints.Length >= 10)
        {
            EditorUtility.DisplayDialog("Maximum Reached",
                "Cannot add more than 10 control points.", "OK");
            return;
        }

        Undo.RecordObject(spline, "Add Control Point");

        int n = spline.controlPoints.Length;
        Vector3[] newPts = new Vector3[n + 1];
        System.Array.Copy(spline.controlPoints, newPts, n);

        // Place new point 5 units ahead in the direction of the last segment
        Vector3 dir = (n >= 2)
            ? (spline.controlPoints[n - 1] - spline.controlPoints[n - 2]).normalized
            : Vector3.forward;
        newPts[n] = spline.controlPoints[n - 1] + dir * 5f;

        spline.controlPoints = newPts;
        EditorUtility.SetDirty(spline);
    }

    /// <summary> Removes the last control point. Minimum 2 required. </summary>
    private void RemoveLastControlPoint()
    {
        if (spline.controlPoints.Length <= 2) return;

        Undo.RecordObject(spline, "Remove Control Point");

        int newCount = spline.controlPoints.Length - 1;
        Vector3[] newPts = new Vector3[newCount];
        System.Array.Copy(spline.controlPoints, newPts, newCount);
        spline.controlPoints = newPts;

        EditorUtility.SetDirty(spline);
    }
}