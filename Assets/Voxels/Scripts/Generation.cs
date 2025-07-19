using System.Collections;
using UnityEngine;

public class Generation : MonoBehaviour
{
    public static Generation instance;
    [SerializeField] string inputSeed;
    [HideInInspector] public long seed;
    

    public static readonly float BLOCK_SIZE = 0.25f;
    public BlockList blockList;
    public Spline contenentalnessToHeight;
    public Material terrainMat;
    public Block mainBlock, underwaterBlock, stoneBlock, dirtBlock;

    static Noise contentalness_1, contentalness_2, contentalness_3;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        GenerateSeed();

        contentalness_1 = new Noise(16, 16, 0.075f, Noise.NoiseType.PERLIN);
        contentalness_2 = new Noise(16, 16, 0.1f, Noise.NoiseType.SIMPLEX);
        contentalness_3 = new Noise(16, 16, 1f, Noise.NoiseType.SIMPLEX);

        StartCoroutine("InititalizeChunks");
    }

    private IEnumerator InititalizeChunks()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                // Create our "main chunk"
                Chunk chunk = new Chunk(new Vector3(x, 0, z));

                //chunk.chunkObj = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                chunk.chunkObj = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer));
                chunk.chunkObj.GetComponent<Renderer>().material = Generation.instance.terrainMat;
                chunk.chunkObj.transform.position = chunk.chunkPos;

                VoxelManager chunkVoxelManager = new VoxelManager();
                chunkVoxelManager.GreedyMesh(chunk);
                foreach (Chunk adj_chunk in chunk.GetAdjacentChunks())
                {
                    if (adj_chunk != null)
                    {
                        chunkVoxelManager.GreedyMesh(adj_chunk);
                    }
                }

                yield return null;
            }
        }
    }

    public void GenerateSeed()
    {
        if(string.IsNullOrWhiteSpace(inputSeed))
        {
            System.Random random = new System.Random();
            seed = random.Next();
            inputSeed = seed.ToString();
        } 
        else 
        {   
            int intSeed;
            if(int.TryParse(inputSeed, out intSeed))
            {
                seed = intSeed;
            }
            else
            {
                seed = inputSeed.GetHashCode();
            }
        }
    }

    public static float GetContenentalness(float xCoord, float zCoord)
    {
        float low = contentalness_1.GetNoise(xCoord, zCoord, 10); // [0, 10]
        float mid = contentalness_2.GetNoise(xCoord, zCoord, 8); // less impact
        float high = contentalness_3.GetNoise(xCoord,zCoord, 2); // subtle variation

        float combined = (low * 0.75f) + (mid * 0.5f) + (high * 0.15f);


        return combined;
    }

}
