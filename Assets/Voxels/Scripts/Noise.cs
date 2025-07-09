using System;
using Unity.Mathematics;
using UnityEngine;

public class Noise
{
    public float width, length;
    public float scale;

    public enum NoiseType { PERLIN, SIMPLEX};
    public static NoiseType noiseType;
    public NoiseType chosenNoiseType;

    private float2 offset;

    public Noise(float width, float length, float scale, NoiseType noiseType)
    {
        this.width = width;
        this.length = length;
        this.scale = scale;

        this.chosenNoiseType = noiseType;

        System.Random rand = new System.Random(Generation.instance.seed.GetHashCode()); // Convert the long seed into a unique int
        float randNumX = rand.Next(-10000, 10000); // First number in sequence
        float randNumY = rand.Next(-10000, 10000); // Second number in sequence
        offset = new float2(randNumX, randNumY);

    }

    public float GetNoise(float x, float y, int expansion)
    {
        float raw = 0;
        float2 value = new float2(x / width, y / length) * scale + offset;
        switch (chosenNoiseType)
        {
            case NoiseType.PERLIN:
                raw = noise.cnoise(value); // Returns [-1, 1];
                break;
            case NoiseType.SIMPLEX:
                raw = noise.snoise(value);
                break;
        }

        float modified = (raw + 1) / 2; // Returns [0, 1]  

        return modified * expansion; // Now we go from [0, exansion]
    }


}
