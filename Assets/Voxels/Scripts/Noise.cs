using Unity.Mathematics;

public class Noise
{
    public float width, length;
    public float scale;

    public enum NoiseType { PERLIN, SIMPLEX};
    public static NoiseType noiseType;
    public NoiseType chosenNoiseType;

    public Noise(float width, float length, float scale, NoiseType noiseType)
    {
        this.width = width;
        this.length = length;
        this.scale = scale;

        this.chosenNoiseType = noiseType;
    }

    public float GetNoise(float x, float y, int expansion)
    {
        float raw = 0;
        switch (chosenNoiseType)
        {
            case NoiseType.PERLIN:
                raw = noise.cnoise(new float2(x / width * scale, y / length * scale)); // Returns [-1, 1];
                break;
            case NoiseType.SIMPLEX:
                raw = noise.snoise(new float2(x / width * scale, y / length * scale));
                break;
        }

        float modified = (raw + 1) / 2; // Returns [0, 1]  

        return modified * expansion; // Now we go from [0, exansion]
    }


}
