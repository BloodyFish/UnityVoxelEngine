using UnityEngine;

public class Generation : MonoBehaviour
{
    public static Generation instance;
    
    public static float BLOCK_SIZE = 0.25f;
    public BlockList blockList;
    public Spline contenentalnessToHeight;
    public bool useGreedyMeshing = false;
    public Material terrainMat;
    public Block mainBlock, underwaterBlock, stoneBlock, dirtBlock;

    static Noise contentalness_1, contentalness_2, contentalness_3;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        contentalness_1 = new Noise(Chunk.CHUNK_WIDTH, Chunk.CHUNK_LENGTH, 0.075f, Noise.NoiseType.PERLIN);
        contentalness_2 = new Noise(Chunk.CHUNK_WIDTH, Chunk.CHUNK_LENGTH, 0.1f, Noise.NoiseType.SIMPLEX);
        contentalness_3 = new Noise(Chunk.CHUNK_WIDTH, Chunk.CHUNK_LENGTH, 1f, Noise.NoiseType.SIMPLEX);

        Chunk.InititalizeChunks();

    }


    public static float GetContenentalness(float xCoord, float zCoord)
    {
        float low = contentalness_1.GetNoise(xCoord, zCoord, 10); // [0, 10]
        float mid = contentalness_2.GetNoise(xCoord, zCoord, 7); // less impact
        float high = contentalness_3.GetNoise(xCoord,zCoord, 2); // subtle variation

        float combined = (low * 0.75f) + (mid * 0.5f) + (high * 0.15f);


        return combined;
    }

}
