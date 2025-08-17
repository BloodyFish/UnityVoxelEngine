using System.Collections;
using UnityEngine;
using Voxels.Scripts.Utils;

public class Generation : MonoBehaviour
{
    public static Generation instance;
    [SerializeField] string inputSeed;
    [HideInInspector] public long seed;
    [HideInInspector] public static System.Random random;

    public const float BLOCK_SIZE = 0.25f;


    public BlockList blockList;
    public static Color32[] colorList;

    [SerializeField] AnimationCurve continentalnessToHeight;
    [HideInInspector] public Spline continentalnessToHeight_spline;

    public Material terrainMat;
    public Block mainBlock, underwaterBlock, stoneBlock, dirtBlock;

    static Noise contentalness_1, contentalness_2, contentalness_3;

    //PLAYER STUFF
    private Transform player;
    private Chunk currentChunk;
    private int renderDistance = 4;

    private void Awake()
    {
        instance = this;
        continentalnessToHeight_spline = new Spline(continentalnessToHeight);

        colorList = new Color32[blockList.blocks.Count];
        int i = 0;  
        foreach(Block block in blockList.blocks)
        {
            colorList[i] = block.vertexColor;
            i++;
        }

        System.Random rand = new System.Random();

        player = GameObject.FindGameObjectWithTag("Player").transform;
    }
    private void Start()
    {
        GenerateSeed();

        contentalness_1 = new Noise(16, 16, 0.075f, Noise.NoiseType.PERLIN);
        contentalness_2 = new Noise(16, 16, 0.1f, Noise.NoiseType.SIMPLEX);
        contentalness_3 = new Noise(16, 16, 1f, Noise.NoiseType.SIMPLEX);

        GenerateChunk();
    }

    private void Update()
    {
        if(player.position.x > currentChunk.chunkPos.x / 2) 
        {
            Vector3Int right = currentChunk.chunkPos + new Vector3Int((int)(Chunk.CHUNK_WIDTH * BLOCK_SIZE), 0, 0);
            if(!Chunk.chunks.ContainsKey(right)) new Chunk(right);
            StartCoroutine(InititalizeChunks(right));

        }
        if (player.position.x < currentChunk.chunkPos.x / 2)
        {
            Vector3Int left = currentChunk.chunkPos - new Vector3Int((int)(Chunk.CHUNK_WIDTH * BLOCK_SIZE), 0, 0);
            if (!Chunk.chunks.ContainsKey(left)) new Chunk(left);
            StartCoroutine(InititalizeChunks(left));

        }
        if (player.position.y > currentChunk.chunkPos.y / 2)
        {
            Vector3Int up = currentChunk.chunkPos + new Vector3Int(0, (int)(Chunk.CHUNK_HEIGHT * BLOCK_SIZE), 0);
            if (!Chunk.chunks.ContainsKey(up)) new Chunk(up);
            StartCoroutine(InititalizeChunks(up));

        }
        if (player.position.y < currentChunk.chunkPos.y / 2)
        {
            Vector3Int down = currentChunk.chunkPos - new Vector3Int(0, (int)(Chunk.CHUNK_HEIGHT * BLOCK_SIZE), 0);
            if (!Chunk.chunks.ContainsKey(down)) new Chunk(down);
            StartCoroutine(InititalizeChunks(down));

        }
        if (player.position.z > currentChunk.chunkPos.z / 2)
        {
            Vector3Int forward = currentChunk.chunkPos + new Vector3Int(0, 0, (int)(Chunk.CHUNK_LENGTH * BLOCK_SIZE));
            if (!Chunk.chunks.ContainsKey(forward)) new Chunk(forward);
            StartCoroutine(InititalizeChunks(forward));

        }
        if (player.position.z < currentChunk.chunkPos.z / 2)
        {
            Vector3Int back = currentChunk.chunkPos - new Vector3Int(0, 0, (int)(Chunk.CHUNK_LENGTH * BLOCK_SIZE));
            if (!Chunk.chunks.ContainsKey(back)) new Chunk(back);
            StartCoroutine(InititalizeChunks(back));

        }

        if (Chunk.chunks.ContainsKey(GetChunkPosRelativeToPlayer()))
        {
            currentChunk = Chunk.chunks[GetChunkPosRelativeToPlayer()];
        }
        else { new Chunk(GetChunkPosRelativeToPlayer()); }



        if (Chunk.dirtyChunks.Count == 0) return;

        foreach (var chunk in Chunk.dirtyChunks)
        {
            chunk.Remesh();
        }
        Chunk.dirtyChunks.Clear();
    }


    private IEnumerator InititalizeChunks(Vector3Int chunkPos)
    {
        Performance.Reset();

        Vector3Int basePos = Vector3Int.zero;

        for(int i = renderDistance * renderDistance * renderDistance; i > -(renderDistance * renderDistance * renderDistance); i--)
        {
            // i % 16 gives you the x coordinate, which cycles from 0 --> 15.
            // i / 16 gives you the y coordinate, which increases every 16 steps and % subChunks resets to 0 when it completes a cycle.
            if (i != 0)
            {
                int x = i % renderDistance;
                int y = (i / renderDistance) % renderDistance;
                int z = i / (renderDistance * renderDistance);

                basePos.x = (int)(x * (Chunk.CHUNK_WIDTH * BLOCK_SIZE));
                basePos.z = (int)(z * (Chunk.CHUNK_LENGTH * BLOCK_SIZE));
                basePos.y = (int)(y * (Chunk.CHUNK_HEIGHT * BLOCK_SIZE));
            }
            
            Vector3Int new_chunkPos = chunkPos + basePos;
            if (!Chunk.chunks.ContainsKey(new_chunkPos)){ new Chunk(new_chunkPos); }
            
            if(i != 0)
            {
                if (i % 2 == 0) // Generates 2 chunks per frame, i.e. we don't "yeild" every single time a chunk is loaded, we yeild when 2 chunks are loaded
                {
                    yield return null;
                }
            }
        }

        Debug.Log("Gathering Metrics.");

        yield return new WaitForSeconds(30);
        Performance.PerformanceMetric chunkGenMetric = Performance.GetMetric(Performance.ChunkGeneration);
        Performance.PerformanceMetric greedyMeshMetric = Performance.GetMetric(Performance.ChunkGreedyMeshing);
        Performance.PerformanceMetric generateMeshMetric = Performance.GetMetric(Performance.ChunkGenerateMesh);
        
        PrintMetric("Chunk Generation", chunkGenMetric);
        PrintMetric("Greedy Meshing", greedyMeshMetric);
        PrintMetric("Generate Mesh", generateMeshMetric);
    }

    private void GenerateChunk()
    {
        currentChunk = new Chunk(GetChunkPosRelativeToPlayer());
    }

    private Vector3Int GetChunkPosRelativeToPlayer()
    {
        Vector3Int chunkPos = new Vector3Int();
        chunkPos.x = (int)((Chunk.CHUNK_WIDTH * BLOCK_SIZE) * Mathf.Floor(player.position.x / (Chunk.CHUNK_WIDTH * BLOCK_SIZE)));
        chunkPos.y = (int)((Chunk.CHUNK_HEIGHT * BLOCK_SIZE) * Mathf.Floor(player.position.y / (Chunk.CHUNK_HEIGHT * BLOCK_SIZE)));
        chunkPos.z = (int)((Chunk.CHUNK_LENGTH * BLOCK_SIZE) * Mathf.Floor(player.position.z / (Chunk.CHUNK_LENGTH * BLOCK_SIZE)));

        return chunkPos;
    }

    private void PrintMetric(string label, Performance.PerformanceMetric metric)
    {
        Debug.Log($"{label} = total={metric.Total:F}ms, min={metric.Min:F}ms, max={metric.Max:F}ms, mean={metric.Mean:F}ms");
    }


    public void GenerateSeed()
    {
        if(string.IsNullOrWhiteSpace(inputSeed))
        {
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
