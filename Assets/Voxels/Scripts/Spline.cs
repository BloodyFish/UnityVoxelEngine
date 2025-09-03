using System.Collections.Concurrent;

using UnityEngine;

public class Spline
{
    public ConcurrentDictionary<int, float> splineValues = new ConcurrentDictionary<int, float>();
    private int splineLength;
    private const float STEP = 0.1f;

    public static float highestPoint;
    public static float lowestPoint;

    public Spline(AnimationCurve curve)
    {
        CreateSplineFromCurve(curve);
    }



    public float EvaluateAtPoint(float x, float scale, out float slope)
    {
        int newX = Mathf.Clamp((int)(x / STEP), 0, splineLength); // We take our x and make it go from 0 to 10 / STEP

        int x1 = newX;
        int x2 = x1 + 1;
        float y1 = splineValues[x1] * scale;
        float y2 = splineValues[x2] * scale;

        slope = (y2 - y1) / (x2 - x1);
        return slope * (newX - x1) + y1;
    }

    public void CreateSplineFromCurve(AnimationCurve spline)
    {
        Debug.Log("Creating Spline");
        splineLength = (int)(spline.keys[spline.keys.Length - 1].time / STEP); // 10 / STEP (0.1) = 100

        highestPoint = spline.Evaluate(0);
        lowestPoint = spline.Evaluate(0);

        for(int t = 0; t <= splineLength; t++)
        {
            float value = spline.Evaluate(t * STEP); 
            splineValues.TryAdd(t, value); // The values will be added as an integer going from 0 to 10 / STEP

            if (value > highestPoint) { highestPoint = value; }
            if(value < lowestPoint) { lowestPoint = value; }
        }
    }
}
