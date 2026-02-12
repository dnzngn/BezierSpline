using UnityEngine;

/// <summary>
/// Pure Bezier curve math utilities using Bernstein polynomials.
/// No Vector3.Lerp — all calculations use the explicit Bezier formula:
/// B(t) = Sum(i=0..n) C(n,i) * (1-t)^(n-i) * t^i * P_i
/// </summary>
public static class BezierMath
{
    // --- Combinatorics ---

    private static long Factorial(int n)
    {
        long result = 1;
        for (int i = 2; i <= n; i++)
            result *= i;
        return result;
    }

    /// <summary> Binomial coefficient C(n,k) = n! / (k! * (n-k)!) </summary>
    private static long BinomialCoefficient(int n, int k)
    {
        return Factorial(n) / (Factorial(k) * Factorial(n - k));
    }

    /// <summary> Bernstein basis polynomial: B(i,n,t) = C(n,i) * t^i * (1-t)^(n-i) </summary>
    private static float BernsteinBasis(int i, int n, float t)
    {
        return BinomialCoefficient(n, i) * Mathf.Pow(t, i) * Mathf.Pow(1f - t, n - i);
    }

    // --- Core Bezier Evaluation ---

    /// <summary>
    /// Evaluates the Bezier curve at parameter t (0..1).
    /// Supports any degree (N control points = degree N-1).
    /// </summary>
    public static Vector3 EvaluateCurve(Vector3[] controlPoints, float t)
    {
        int n = controlPoints.Length - 1;
        Vector3 point = Vector3.zero;

        for (int i = 0; i <= n; i++)
            point += BernsteinBasis(i, n, t) * controlPoints[i];

        return point;
    }

    /// <summary>
    /// Evaluates the tangent (first derivative) at parameter t.
    /// B'(t) = n * Sum(i=0..n-1) Bernstein(i,n-1,t) * (P[i+1] - P[i])
    /// </summary>
    public static Vector3 EvaluateTangent(Vector3[] controlPoints, float t)
    {
        int n = controlPoints.Length - 1;
        if (n < 1) return Vector3.forward;

        Vector3 tangent = Vector3.zero;
        for (int i = 0; i <= n - 1; i++)
        {
            Vector3 diff = controlPoints[i + 1] - controlPoints[i];
            tangent += BernsteinBasis(i, n - 1, t) * diff;
        }

        return tangent * n;
    }

    /// <summary>
    /// Returns the normal vector perpendicular to the tangent on the XZ plane.
    /// Used to offset lanes left/right from the center curve.
    /// </summary>
    public static Vector3 GetNormal(Vector3[] controlPoints, float t)
    {
        Vector3 tangent = EvaluateTangent(controlPoints, t).normalized;
        Vector3 normal = Vector3.Cross(tangent, Vector3.up).normalized;

        // Fallback if tangent is parallel to up
        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.Cross(tangent, Vector3.forward).normalized;

        return normal;
    }

    // --- Arc-Length Parameterization ---
    // Bezier t parameter is NOT uniformly distributed along the curve length.
    // We build a lookup table to map real distances to t values.

    /// <summary> Builds cumulative arc-length table by sampling the curve. </summary>
    public static float[] BuildArcLengthTable(Vector3[] controlPoints, int sampleCount = 1000)
    {
        float[] table = new float[sampleCount + 1];
        table[0] = 0f;
        Vector3 prev = EvaluateCurve(controlPoints, 0f);

        for (int i = 1; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 curr = EvaluateCurve(controlPoints, t);
            table[i] = table[i - 1] + Vector3.Distance(prev, curr);
            prev = curr;
        }

        return table;
    }

    /// <summary> Returns total curve length from the arc-length table. </summary>
    public static float GetTotalLength(float[] arcLengthTable)
    {
        return arcLengthTable[arcLengthTable.Length - 1];
    }

    /// <summary> Converts a distance along the curve to the corresponding t parameter using binary search. </summary>
    public static float DistanceToT(float[] arcLengthTable, float distance)
    {
        int sampleCount = arcLengthTable.Length - 1;
        float totalLength = GetTotalLength(arcLengthTable);

        if (distance <= 0f) return 0f;
        if (distance >= totalLength) return 1f;

        // Binary search
        int low = 0, high = sampleCount;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (arcLengthTable[mid] < distance)
                low = mid + 1;
            else
                high = mid;
        }

        // Linear interpolation between samples for precision
        if (low > 0)
        {
            float before = arcLengthTable[low - 1];
            float after = arcLengthTable[low];
            float frac = (distance - before) / (after - before);
            float tBefore = (float)(low - 1) / sampleCount;
            float tAfter = (float)low / sampleCount;
            return tBefore + frac * (tAfter - tBefore);
        }

        return (float)low / sampleCount;
    }

    /// <summary> Generates evenly spaced points along the curve by distance interval. </summary>
    public static Vector3[] GetPointsByDistance(Vector3[] controlPoints, float distance)
    {
        float[] arcTable = BuildArcLengthTable(controlPoints);
        float totalLength = GetTotalLength(arcTable);
        int count = Mathf.FloorToInt(totalLength / distance) + 1;

        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++)
            points[i] = EvaluateCurve(controlPoints, DistanceToT(arcTable, i * distance));

        return points;
    }

    /// <summary> Generates a fixed number of evenly spaced points along the curve. </summary>
    public static Vector3[] GetPointsByCount(Vector3[] controlPoints, int count)
    {
        if (count < 2) count = 2;

        float[] arcTable = BuildArcLengthTable(controlPoints);
        float spacing = GetTotalLength(arcTable) / (count - 1);

        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++)
            points[i] = EvaluateCurve(controlPoints, DistanceToT(arcTable, i * spacing));

        return points;
    }

    // --- Lane Offset ---

    /// <summary> Returns a point offset from the center curve by 'offset' units along the normal. </summary>
    public static Vector3 GetOffsetPoint(Vector3[] controlPoints, float t, float offset)
    {
        return EvaluateCurve(controlPoints, t) + GetNormal(controlPoints, t) * offset;
    }

    /// <summary> Approximates offset control points by shifting each along its normal. </summary>
    public static Vector3[] GetOffsetControlPoints(Vector3[] controlPoints, float offset)
    {
        Vector3[] result = new Vector3[controlPoints.Length];
        for (int i = 0; i < controlPoints.Length; i++)
        {
            float t = (float)i / (controlPoints.Length - 1);
            result[i] = controlPoints[i] + GetNormal(controlPoints, t) * offset;
        }
        return result;
    }
}