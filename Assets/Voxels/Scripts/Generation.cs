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
    public int renderDistance = 4;
    private Stack chunksToGenerate = new Stack();

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

        contentalness_1 = new Noise(Chunk.CHUNK_WIDTH * BLOCK_SIZE, Chunk.CHUNK_LENGTH * BLOCK_SIZE, 0.075f, Noise.NoiseType.PERLIN);
        contentalness_2 = new Noise(Chunk.CHUNK_WIDTH * BLOCK_SIZE, Chunk.CHUNK_LENGTH * BLOCK_SIZE, 0.1f, Noise.NoiseType.SIMPLEX);
        contentalness_3 = new Noise(Chunk.CHUNK_WIDTH * BLOCK_SIZE, Chunk.CHUNK_LENGTH * BLOCK_SIZE, 1f, Noise.NoiseType.SIMPLEX);

        GenerateChunk();
    }

    private void Update()
    {

        if (Chunk.chunks.ContainsKey(GetChunkPosRelativeToPlayer()))
        {
            //if(currentChunk != null && currentChunk.chunkObj.GetComponent<MeshCollider>() != null) { currentChunk.chunkObj.GetComponent<MeshCollider>().enabled = false; }

            currentChunk = Chunk.chunks[GetChunkPosRelativeToPlayer()];
            if(currentChunk.ContainsVoxel && currentChunk.isGenerated && !currentChunk.chunkObj.GetComponent<MeshCollider>().enabled) 
            { 
                currentChunk.chunkObj.GetComponent<MeshCollider>().enabled = true;
            }

            
        }
        else { new Chunk(GetChunkPosRelativeToPlayer()); }
        StartCoroutine(InititalizeChunks(currentChunk.chunkPos));

        while(chunksToGenerate.Count > 0)
        {
            Vector3Int chunkPos = (Vector3Int)chunksToGenerate.Pop();
            new Chunk(chunkPos);
        }

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

        /*Vector3Int basePos = Vector3Int.zero;

        for(int y = (renderDistance / 2); y > -(renderDistance / 2); y--)
        {
            for(int x = renderDistance; x > -renderDistance; x--)
            {
                for(int  z = renderDistance; z > -renderDistance; z--)
                {
                    basePos.x = (int)(x * (Chunk.CHUNK_WIDTH * BLOCK_SIZE));
                    basePos.z = (int)(z * (Chunk.CHUNK_LENGTH * BLOCK_SIZE));
                    basePos.y = (int)(y * (Chunk.CHUNK_HEIGHT * BLOCK_SIZE));

                    Vector3Int new_chunkPos = chunkPos + basePos;
                    if (!Chunk.chunks.ContainsKey(new_chunkPos)) { chunksToGenerate.Push(new_chunkPos); }

                    yield return null;
                }
            }
        }*/

        // FLOOD-FILL APROACH
        Queue chunkArea = new Queue();
        chunkArea.Enqueue(chunkPos);


        while(chunkArea.Count > 0)
        {
            Vector3Int new_chunkPos = (Vector3Int)chunkArea.Dequeue();
            if (!Chunk.chunks.ContainsKey(new_chunkPos)) { chunksToGenerate.Push(new_chunkPos); }

            if(Mathf.Abs(new_chunkPos.x - chunkPos.x) / Chunk.CHUNK_WIDTH_WORLD < renderDistance)
            {
                Vector3Int right = new_chunkPos + new Vector3Int(Chunk.CHUNK_WIDTH_WORLD, 0, 0);
                if (!Chunk.chunks.ContainsKey(right)) { chunkArea.Enqueue(right); }

                Vector3Int left = new_chunkPos - new Vector3Int(Chunk.CHUNK_WIDTH_WORLD, 0, 0);
                if (!Chunk.chunks.ContainsKey(left)) { chunkArea.Enqueue(left); }
            }

            if (Mathf.Abs(new_chunkPos.z - chunkPos.z) / Chunk.CHUNK_LENGTH_WORLD < renderDistance)
            {
                Vector3Int forward = new_chunkPos + new Vector3Int(0, 0, Chunk.CHUNK_LENGTH_WORLD);
                if (!Chunk.chunks.ContainsKey(forward)) { chunkArea.Enqueue(forward); }

                Vector3Int back = new_chunkPos - new Vector3Int(0, 0, Chunk.CHUNK_LENGTH_WORLD);
                if (!Chunk.chunks.ContainsKey(back)) { chunkArea.Enqueue(back); }
            }

            if (Mathf.Abs(new_chunkPos.y - chunkPos.y) / Chunk.CHUNK_HEIGHT_WORLD < (renderDistance / 2))
            {
                Vector3Int up = new_chunkPos + new Vector3Int(0, Chunk.CHUNK_HEIGHT_WORLD, 0);
                if (!Chunk.chunks.ContainsKey(up)) { chunkArea.Enqueue(up); }

                Vector3Int down = new_chunkPos - new Vector3Int(0, Chunk.CHUNK_HEIGHT_WORLD, 0);
                if (!Chunk.chunks.ContainsKey(down)) { chunkArea.Enqueue(down); }
            }


            yield return null;
        }

        // Got a Null-Reference Exception when running the following code. I don't feel like looking over and trying to fix it as of now...
        // Out of sight out of mind, I guess...

        /*Debug.Log("Gathering Metrics.");

        yield return new WaitForSeconds(30);
        Performance.PerformanceMetric chunkGenMetric = Performance.GetMetric(Performance.ChunkGeneration);
        Performance.PerformanceMetric greedyMeshMetric = Performance.GetMetric(Performance.ChunkGreedyMeshing);
        Performance.PerformanceMetric generateMeshMetric = Performance.GetMetric(Performance.ChunkGenerateMesh);
        
        PrintMetric("Chunk Generation", chunkGenMetric);
        PrintMetric("Greedy Meshing", greedyMeshMetric);
        PrintMetric("Generate Mesh", generateMeshMetric);*/
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

    private void OnDrawGizmos()
    {
        Vector3 size = new Vector3(Chunk.CHUNK_WIDTH, Chunk.CHUNK_HEIGHT, Chunk.CHUNK_LENGTH) * BLOCK_SIZE;
        if (currentChunk != null)
        {
            Vector3 center = currentChunk.chunkPos + (size / 2);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, size);
        }
        foreach(var stackObj in chunksToGenerate)
        {
            Vector3 center = (Vector3Int)stackObj + (size / 2);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(center, size);

        }
    }

}
