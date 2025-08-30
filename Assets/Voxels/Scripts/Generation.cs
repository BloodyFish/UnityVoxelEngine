using System.Collections;
using System.Collections.Generic;
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
    public Transform player;
    private Chunk currentChunk;
    public int renderDistance = 4;
    private Queue chunksToGenerate = new Queue();
    private HashSet<Vector3Int> chunksToGenerate_hashSet = new HashSet<Vector3Int>();
    private Coroutine chunkInitCoroutine;

    Vector3Int[] chunkDirections =
    {
        new Vector3Int(Chunk.CHUNK_WIDTH_WORLD, 0, 0),
        new Vector3Int(0, 0, Chunk.CHUNK_LENGTH_WORLD),
        new Vector3Int(0, Chunk.CHUNK_HEIGHT_WORLD, 0)
    };


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
        Vector3Int playerChunkPos = GetChunkPosRelativeToPlayer();

        // "OFFLOAD" CHUNKS THAT ARE OUTSIDE THE RENDER DISTANCE
        foreach (Chunk activeChunk in Chunk.chunks.Values)
        {
            if (activeChunk.chunkObj.activeInHierarchy && !Chunk.IsInRenderDistance(activeChunk.chunkPos))
            {
                activeChunk.chunkObj.SetActive(false);
            }
        }

        // FIGURE OUT WHICH CHUNK IS THE CHUNK THE PLAYER IS CURRENTLY IN
        // ENABLE A MESH COLLIDER ON THIS CHUNK AND DISABLE THE COLLIDER ON THE PREVIOUS
        if (Chunk.chunks.ContainsKey(playerChunkPos))
        {
            if(currentChunk != null && currentChunk.chunkObj.GetComponent<MeshCollider>().enabled)
            {
                if (currentChunk != Chunk.chunks[playerChunkPos]) 
                { 
                    currentChunk.chunkObj.GetComponent<MeshCollider>().enabled = false;
                    chunksToGenerate.Clear();
                    StopCoroutine(chunkInitCoroutine);
                }
            }

            // SET CURRENT CHUNK / COLLIDER AND START INITIALZING CHUNKS
            currentChunk = Chunk.chunks[playerChunkPos];
            if(!currentChunk.chunkObj.GetComponent<MeshCollider>().enabled) 
            {
                if (currentChunk.ContainsVoxel) { currentChunk.chunkObj.GetComponent<MeshCollider>().enabled = true; }
                chunkInitCoroutine = StartCoroutine(InititalizeChunks(currentChunk.chunkPos));
            }
        }
        else { new Chunk(playerChunkPos); }

        // ACTUALLY CREATE THE CHUNKS 
        while(chunksToGenerate.Count > 0)
        {
            Vector3Int chunkPos = (Vector3Int)chunksToGenerate.Dequeue();
            if (!Chunk.chunks.ContainsKey(chunkPos)) { new Chunk(chunkPos); }
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

        // FLOOD-FILL APROACH
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Queue chunkArea = new Queue();
        chunkArea.Enqueue(chunkPos);

        while(chunkArea.Count > 0)
        {
            Vector3Int new_chunkPos = (Vector3Int)chunkArea.Dequeue();

            // Not putting !Chunk.chunks.ContainsKey(new_chunkPos) allows us to continue to traverse chunks that exist to get further positions
            // Otherwise, if we are standing on a chunk we've discovered, the floodfill won't work
            if (chunksToGenerate_hashSet.Add(new_chunkPos)) { chunksToGenerate.Enqueue(new_chunkPos); }
            else if(Chunk.chunks.TryGetValue(new_chunkPos, out var chunk) && !chunk.ContainsVoxel) { continue; } // Don't branch from existing air chunks

            foreach (Vector3Int dir in chunkDirections)
            {
                Vector3Int dir_one = new_chunkPos + dir;
                Vector3Int dir_two = new_chunkPos - dir;
                // Not using !Chunk.chunks.ContainsKey(dir_one) or !Chunk.chunks.ContainsKey(dir_two) allows us to continue to traverse chunks that exist to get further positions
                // Otherwise, if we are standing on a chunk we've discovered, the floodfill wont work

                // Enqueue if each dir is NOT in the hash set (hash set will return true when it does not contain!)
                if (Chunk.IsInRenderDistance(dir_one) && visited.Add(dir_one)) { chunkArea.Enqueue(dir_one); }
                if (Chunk.IsInRenderDistance(dir_two) && visited.Add(dir_two)) { chunkArea.Enqueue(dir_two); }

            }

            yield return new WaitForSeconds(0);
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
        Vector3 renderDist_size = size * renderDistance;
        if (currentChunk != null)
        {
            Vector3 center = currentChunk.chunkPos + (size / 2);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(center, renderDist_size.x); 
        }
        foreach(var stackObj in chunksToGenerate)
        {
            if (Vector3.Distance(player.position, (Vector3Int)stackObj) < (Chunk.CHUNK_WIDTH_WORLD * renderDistance) && !Chunk.chunks.ContainsKey((Vector3Int)stackObj))
            {
                Vector3 center = (Vector3Int)stackObj + (size / 2);

                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(center, size);
            }

        }


    }

}
