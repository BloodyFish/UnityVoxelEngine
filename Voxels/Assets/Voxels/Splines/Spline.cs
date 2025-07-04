using UnityEngine;

[CreateAssetMenu(menuName = "VoxelStuff/Spline")]
public class Spline : ScriptableObject
{
    public AnimationCurve spline;

    public float EvaluateAtPoint(float x, float scale)
    {
        return spline.Evaluate(x) * scale; 
    }

    public float GetInstantaneousSlopeAtPoint(float x)
    {
        // Slope = (y2 - y1) / (x2 - x1)
        float x1 = x - 0.01f;
        float x2 = x + 0.01f; ;
        float y1 = spline.Evaluate(x1);
        float y2 = spline.Evaluate(x2);

        float slope = (y2  - y1) / (x2 - x1);

        return slope;
    }
}
