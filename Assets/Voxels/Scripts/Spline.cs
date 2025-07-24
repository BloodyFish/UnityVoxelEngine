using System.Collections.Concurrent;

using UnityEngine;

public class Spline
{
    public ConcurrentDictionary<int, float> splineValues = new ConcurrentDictionary<int, float>();
    private int splineLength;
    private const float STEP = 0.1f;

    public Spline(AnimationCurve curve)
    {
        CreateSplineFromCurve(curve);
    }



    public float EvaluateAtPoint(float x, float scale)
    {
        //x = Mathf.Round(x * 10f) / 10f; // 4.588889 becomes 45.8 and then 4.5
        int newX = Mathf.Clamp((int)(x / STEP), 0, splineLength);
        if (splineValues.ContainsKey(newX))
        {
            return splineValues[newX] * scale;
        }

        //float roundedX = Mathf.Round(x / STEP) * STEP;
        int x1 = newX;
        int x2 = x1 + 1;
        float y1 = splineValues[x1];
        float y2 = splineValues[x2];

        float slope = (y2 - y1) / (x2 - x1);
        return (slope * (newX - x1) + y1) * scale;
    }

    public float GetInstantaneousSlopeAtPoint(float x)
    {
        // Slope = (y2 - y1) / (x2 - x1)
        float x1 = x - 1;
        float x2 = x + 1;
        float y1 = EvaluateAtPoint(x1, 1);
        float y2 = EvaluateAtPoint(x2, 1);

        float slope = (y2  - y1) / (x2 - x1);

        return slope;
    }

    public void CreateSplineFromCurve(AnimationCurve spline)
    {
        Debug.Log("Creating Spline");
        splineLength = (int)(spline.keys[spline.keys.Length - 1].time / STEP);

        for(int t = 0; t <= splineLength; t++)
        {
            splineValues.TryAdd(t, spline.Evaluate(t * STEP));
        }
    }
}
