using UnityEngine;
using System.Collections.Generic;

public static class PathUtils
{
    // Calculates the total length of the path
    public static float GetTotalLength(List<Vector3> points)
    {
        float length = 0;
        for (int i = 0; i < points.Count - 1; i++)
            length += Vector3.Distance(points[i], points[i + 1]);
        return length;
    }

    // Finds a position at a specific distance along the path
    public static Vector3 GetPointAtDistance(List<Vector3> points, float distance)
    {
        float accumulated = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            float segment = Vector3.Distance(points[i], points[i + 1]);
            if (accumulated + segment >= distance)
            {
                float t = (distance - accumulated) / segment;
                return Vector3.Lerp(points[i], points[i + 1], t);
            }
            accumulated += segment;
        }
        return points[points.Count - 1];
    }
}