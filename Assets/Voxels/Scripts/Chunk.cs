
using System.Collections.Concurrent;
using System.Collections.Generic;

using UnityEngine;
using Voxels.Scripts.Dispatcher;
using Voxels.Scripts.Utils;


public class Chunk
{
    //public static List<Chunk> chunks = new List<Chunk>();
    public static ConcurrentDictionary<Vector3Int, Chunk> chunks = new ConcurrentDictionary<Vector3Int, Chunk>();
    public static HashSet<Chunk> dirtyChunks = new ();

    public static readonly short CHUNK_WIDTH = (short)(64);
    public static readonly short CHUNK_LENGTH = (short)(64);
    public static readonly short CHUNK_HEIGHT = (short)(2000);

    public enum Direction { RIGHT = 0, LEFT = 1, FORWARD = 2, BACK = 3}
    public static readonly Direction chunkDirection;

    public GameObject chunkObj;
    public Vector3Int chunkPos;

    //public byte[,,] blockArray; // these bytes should reference different blocks. If they are 0 it is air. A byte goes to 0-255
    public byte[] blockArray1D;
    // Setting x: just add x
    // Setting y: add CHUNK_WIDTH + y 
    // Setting z: add CHUNK_WIDTH + CHUNK_HEIGHT + z
    // Adding it together: x + (CHUNK_WIDTH * z) + (CHUNK_WIDTH * CHUNK_LENGTH * y)

    public VoxelManager voxelManager = new VoxelManager();
    public bool isGenerated = false;

    private bool isDirty = false;
    private bool isMeshing = true;

    public void MarkDirty()
    {
        isDirty = true;
        dirtyChunks.Add(this);
    }

    public bool IsDirty => isDirty;

    public void Remesh()
    {
        if (!IsDirty || isMeshing) return;
        isMeshing = true; // Prevent trying to remesh while already meshing
        isDirty = false;
        var greedyEntry = Performance.Begin(Performance.ChunkGreedyMeshing);
        VoxelManager.GreedyMeshResult result = voxelManager.GreedyMesh(this);
        result.Then((vertices, triangles) => {
            greedyEntry.End();
            var entry = Performance.Begin(Performance.ChunkGenerateMesh);
            voxelManager.GenerateMesh(chunkObj, vertices, triangles);
            entry.End();
            isMeshing = false;
        });
    }

    public Chunk(Vector2 chunkPos)
    {
        float blockSize = Generation.BLOCK_SIZE;
        // Remember, our voxels are smaller than 1 unit. Using "chunkPos.x * CHUNK_WIDTH" would give us spacing as if each block was 1 unit. Multiply to  get correct world space
        this.chunkPos = new Vector3Int((int)(chunkPos.x * (CHUNK_WIDTH * blockSize)), 0, (int)(chunkPos.y * (CHUNK_LENGTH * blockSize)));

        this.chunkObj = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer));
        this.chunkObj.GetComponent<Renderer>().material = Generation.instance.terrainMat;
        this.chunkObj.transform.position = this.chunkPos;

        blockArray1D = new byte[CHUNK_WIDTH * CHUNK_HEIGHT * CHUNK_LENGTH];

        chunks.TryAdd(this.chunkPos, this);
        
        // Accepting an action in the queued task allows us to manually notify the AsyncHelpder when this thread should
        // be considered done. QueueTask tries to ensure only a certain number of tasks are running at a given time, if
        // a task spawns another task or other async action this task will complete before the work is done and another
        // task will be run from the queue, accepting the completion action will delay the que from starting another
        // task until we want it to.
        AsyncHelper.QueueTask(complete =>
        {
            var generationEntry = Performance.Begin(Performance.ChunkGeneration);
            GenerateChunk();
            generationEntry.End();

            // Unity does not expose a synchronization context, as such ContinueWith using current context makes no
            // guarantees about actually running on the main thread. To ensure the generation happens on main thread
            // make sure there's an object with MainThreadDispatcher component in the scene and submit work to it as so
            AsyncHelper.RunOnMainThread(() =>
            {
                var greedyEntry = Performance.Begin(Performance.ChunkGreedyMeshing);
                VoxelManager.GreedyMeshResult result = voxelManager.GreedyMesh(this);
                
                // Due to the job spawning previously happening in threads, it was possible for a chunk to be greedy
                // meshed twice with the same VoxelManager and simultaneously editing the native arrays, which would
                // often result in a Unity crash when the voxel array was modified while copying. The greedy mesher now
                // returns a result struct containing it's own non-shared native arrays for vertices and triangles, and
                // GenerateMesh now expects the vertices and triangles to be passed in.
                
                // The greedy meshing runs as a job, this Then() method uses the AsyncHelper to run code on the main
                // thread when the job handles are complete, without forcefully blocking until completion. Jobs should
                // not be schedules from anywhere other than the main thread, and waiting on/completing job handles
                // from non-main threads can cause issues. So the AsyncHelper checks the completeness each frame until
                // the job is complete, then calls the handler. It also automatically cleans up the native resources
                // once complete.
                result.Then((vertices, triangles) => {
                    greedyEntry.End();
                    var entry = Performance.Begin(Performance.ChunkGenerateMesh);
                    voxelManager.GenerateMesh(chunkObj, vertices, triangles);
                    entry.End();
                    isMeshing = false;
                    complete();
                });
                
                foreach (Chunk adj_chunk in GetAdjacentChunks())
                {
                    if (adj_chunk != null && adj_chunk.isGenerated)
                    {
                        adj_chunk.MarkDirty();
                    }
                }
            });
        });
    }


    public void GenerateChunk()
    {
        SetBlockArray(this);
    }
    public void SetBlockArray(Chunk chunk)
    {
        float blockSize = Generation.BLOCK_SIZE;

        for(int i = 0; i < CHUNK_WIDTH * CHUNK_LENGTH; i++)
        {
            // i % 16 gives you the x coordinate, which cycles from 0 --> 15.
            // i / 16 gives you the z coordinate, which increases every 16 steps.

            int x = i % CHUNK_WIDTH;
            int z = i / CHUNK_LENGTH;

            float xCoord = (x * blockSize) + chunkPos.x;
            float zCoord = (z * blockSize) + chunkPos.z;

            float contentalness = Generation.GetContenentalness(xCoord, zCoord);
            short yVal = (short)Mathf.Ceil(Generation.instance.continentalnessToHeight_spline.EvaluateAtPoint(contentalness, 100 / blockSize));
            float slope = Generation.instance.continentalnessToHeight_spline.GetInstantaneousSlopeAtPoint(contentalness);


            for (short y = 0; y < yVal; y++)
            {

                if (slope > 1f)
                {
                    blockArray1D[CalculateBlockIndex(x, y, z)] = (byte)(Generation.instance.stoneBlock.block_ID + 1);
                }
                else
                {

                    if (y <= 20 / blockSize)
                    {
                        blockArray1D[CalculateBlockIndex(x, y, z)] = (byte)(Generation.instance.underwaterBlock.block_ID + 1);

                    }
                    else if (y == yVal - 1)
                    {
                        blockArray1D[CalculateBlockIndex(x, y, z)] = (byte)(Generation.instance.mainBlock.block_ID + 1);

                    }
                    else
                    {
                        blockArray1D[CalculateBlockIndex(x, y, z)] = (byte)(Generation.instance.dirtBlock.block_ID + 1);

                    }
                }

            }
        }

        isGenerated = true;
    }

    public static int CalculateBlockIndex(int x, int y, int z)
    {
        int index = x + (CHUNK_WIDTH * z) + (CHUNK_WIDTH * CHUNK_LENGTH * y);
        return index;
    } 

    public static Chunk GetChunkFromCoords(int x, int y, int z)
    {
        chunks.TryGetValue(new Vector3Int(x, y, z), out Chunk chunk);
        return chunk;
    }

    public Chunk[] GetAdjacentChunks()
    {
        // TO-DO: Make this work with blockSize
        float blockSize = Generation.BLOCK_SIZE;
        Chunk[] adjacentChunks = new Chunk[4];

        adjacentChunks[(int)Direction.RIGHT] = GetChunkFromCoords((int)(chunkPos.x + CHUNK_WIDTH * blockSize), chunkPos.y, chunkPos.z);
        adjacentChunks[(int)Direction.LEFT] = GetChunkFromCoords((int)(chunkPos.x - CHUNK_WIDTH * blockSize), chunkPos.y, chunkPos.z);
        adjacentChunks[(int)Direction.FORWARD] = GetChunkFromCoords(chunkPos.x, chunkPos.y, (int)(chunkPos.z + CHUNK_LENGTH * blockSize));
        adjacentChunks[(int)Direction.BACK] = GetChunkFromCoords(chunkPos.x, chunkPos.y, (int)(chunkPos.z - CHUNK_LENGTH * blockSize));

        return adjacentChunks;
    }
}
