
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using Voxels.Scripts.Dispatcher;
using Voxels.Scripts.Utils;


public class Chunk
{
    //public static List<Chunk> chunks = new List<Chunk>();
    public static ConcurrentDictionary<Vector3Int, Chunk> chunks = new ConcurrentDictionary<Vector3Int, Chunk>();
    public static HashSet<Chunk> dirtyChunks = new ();

    public const byte CHUNK_WIDTH = 64;
    public const byte CHUNK_LENGTH = 64;
    public const byte CHUNK_HEIGHT = 64;

    public enum Direction { RIGHT = 0, LEFT = 1, FORWARD = 2, BACK = 3, UP = 4, DOWN = 5}
    public static readonly Direction chunkDirection;

    public GameObject chunkObj;
    public Vector3Int chunkPos;

    public NativeArray<byte> blockArray1D = new NativeArray<byte>(CHUNK_WIDTH * CHUNK_LENGTH * CHUNK_HEIGHT, Allocator.Persistent);
    // x + (CHUNK_WIDTH * z) + (CHUNK_WIDTH * CHUNK_LENGTH * y)

    public VoxelManager voxelManager = new VoxelManager();
    public bool isGenerated = false;

    private bool isDirty = false;
    private bool isMeshing = true;
    private bool containsVoxel = false;

    Vector3Int[] directions =
    {
        new Vector3Int(1, 0, 0), // Vector3Int RIGHT
        new Vector3Int(-1, 0, 0), // Vector3Int LEFT
        new Vector3Int(0, 1, 0), // Vector3Int UP
        new Vector3Int(0, -1, 0), // Vector3Int DOWN
        new Vector3Int(0, 0, 1), // Vector3Int FORWARD
        new Vector3Int(0, 0, -1), //Vector3Int BACK
    };


    public void MarkDirty()
    {
        isDirty = true;
        dirtyChunks.Add(this);
    }

    public bool IsDirty => isDirty;
    public bool ContainsVoxel => containsVoxel;

    public void Remesh()
    {

        if (!IsDirty || isMeshing || !ContainsVoxel) return;
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

    public Chunk(Vector3 chunkPos)
    {
        float blockSize = Generation.BLOCK_SIZE;
        // Remember, our voxels are smaller than 1 unit. Using "chunkPos.x * CHUNK_WIDTH" would give us spacing as if each block was 1 unit. Multiply to  get correct world space
        this.chunkPos = new Vector3Int((int)(chunkPos.x * (CHUNK_WIDTH * blockSize)), (int)(chunkPos.y * (CHUNK_HEIGHT * blockSize)), (int)(chunkPos.z * (CHUNK_LENGTH * blockSize)));

        this.chunkObj = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer));
        this.chunkObj.GetComponent<Renderer>().material = Generation.instance.terrainMat;
        this.chunkObj.transform.position = this.chunkPos;

        chunks.TryAdd(this.chunkPos, this);

        // Accepting an action in the queued task allows us to manually notify the AsyncHelpder when this thread should
        // be considered done. QueueTask tries to ensure only a certain number of tasks are running at a given time, if
        // a task spawns another task or other async action this task will complete before the work is done and another
        // task will be run from the queue, accepting the completion action will delay the queue from starting another
        // task until we want it to.

        // NOTE: calling complete() is necessary for the task to offically end
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
                // Only greedy mesh chunks with at least one voxel
                if (!ContainsVoxel)
                {
                    //Debug.Log("chunk does not contain any voxels");
                    isMeshing = false;
                    complete();
                    return;
                }

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
        SetBlockArray(); // The amount of times this function is called is affected by the RandomizeBlocks() function call
        Debug.Log("Finished SetBlockArray()");
        //if (ContainsVoxel) { RandomizeBlocks(); }
        Debug.Log("Finished RandomizeBlocks()");
        isGenerated = true;
    }
    public void SetBlockArray()
    {
        float blockSize = Generation.BLOCK_SIZE;

        for(int i = 0; i < CHUNK_WIDTH * CHUNK_LENGTH; i++)
        {
            // i % 16 gives you the x coordinate, which cycles from 0 --> 15.
            // i / 16 gives you the z coordinate, which increases every 16 steps.

            // Position in 3D array
            int x = i % CHUNK_WIDTH;
            int z = i / CHUNK_WIDTH;

            // World position
            float xCoord = (x * blockSize) + chunkPos.x;
            float zCoord = (z * blockSize) + chunkPos.z;

            float contentalness = Generation.GetContenentalness(xCoord, zCoord);

            float slope = new float();
            int yVal = (int)Mathf.Ceil(Generation.instance.continentalnessToHeight_spline.EvaluateAtPoint(contentalness, 100 / blockSize, out slope));
            //Debug.Log(slope);

            for (int y = 0; y < CHUNK_HEIGHT; y++)
            {
                // Our chunk position assumes are blocks are 1 by one (by default our blocks are 1/4th the size of a normal block
                // So our 64 by 64 chunk of 1/4 sized voxels is equal to a 16 by 16 sized chunks of 1 by 1 voxel. 
                // We divide by blockSize (0.25) to find the actual y position of the voxel
                float yCoord = y + (chunkPos.y / blockSize);

                if (yCoord > yVal) { break; }

                int blockIndex = CalculateBlockIndex(x, y, z);
                if (slope > 3f)
                {
                    blockArray1D[blockIndex] = (byte)(Generation.instance.stoneBlock.block_ID + 1);

                }
                else
                {

                    if (yCoord <= 20 / blockSize)
                    {
                        blockArray1D[blockIndex] = (byte)(Generation.instance.underwaterBlock.block_ID + 1);

                    }
                    else if (yCoord == yVal)
                    {
                        blockArray1D[blockIndex] = (byte)(Generation.instance.mainBlock.block_ID + 1);

                    }
                    else
                    {
                        blockArray1D[blockIndex] = (byte)(Generation.instance.dirtBlock.block_ID + 1);

                    }
                }

                containsVoxel = true;                
            }
        }
    }


    public void RandomizeBlocks()
    {
        byte[] originalBlocks = new byte[blockArray1D.Length];
        blockArray1D.CopyTo(originalBlocks);
        

        Vector3Int[] possibleDirections = new Vector3Int[6];
        int possibleDirections_count;


        for (int i = 0; i < CHUNK_WIDTH * CHUNK_LENGTH * CHUNK_HEIGHT; i++)
        {
            possibleDirections_count = 0;

            // Position in 3D array
            int x = i % CHUNK_WIDTH;
            int z = (i / CHUNK_WIDTH) % CHUNK_LENGTH;
            int y = i / (CHUNK_WIDTH * CHUNK_LENGTH);

            int blockIndex = CalculateBlockIndex(x, y, z);
            if (originalBlocks[blockIndex] == 0) { continue; }


            if (x < CHUNK_WIDTH - 1)
            {
                int right = CalculateBlockIndex(x + 1, y, z);
                if (originalBlocks[right] > 0 && originalBlocks[right] != originalBlocks[blockIndex]) 
                { 
                    possibleDirections[possibleDirections_count] = directions[0]; // RIGHT
                    possibleDirections_count++;
                }
            }
            if (x > 0)
            {
                int left = CalculateBlockIndex(x - 1, y, z);
                if (originalBlocks[left] > 0 && originalBlocks[left] != originalBlocks[blockIndex]) 
                { 
                    possibleDirections[possibleDirections_count] = directions[1]; // LEFT
                    possibleDirections_count++;
                }
            }
            if (y < CHUNK_HEIGHT - 1)
            {
                int up = CalculateBlockIndex(x, y + 1, z);
                if (originalBlocks[up] > 0 && originalBlocks[up] != originalBlocks[blockIndex]) 
                { 
                    possibleDirections[possibleDirections_count] = directions[2]; // UP
                    possibleDirections_count++;
                }
            }
            if (y > 0)
            {
                int down = CalculateBlockIndex(x, y - 1, z);
                if (originalBlocks[down] > 0 && originalBlocks[down] != originalBlocks[blockIndex]) 
                { 
                    possibleDirections[possibleDirections_count] = directions[3]; // DOWN
                    possibleDirections_count++;
                }
            }

            if (z < CHUNK_LENGTH - 1)
            {
                int forward = CalculateBlockIndex(x, y, z + 1);
                if (originalBlocks[forward] > 0 && originalBlocks[forward] != originalBlocks[blockIndex]) 
                { 
                    possibleDirections[possibleDirections_count] = directions[4]; // FORWARD
                    possibleDirections_count++;
                }
            }
            if (z > 0)
            {
                int back = CalculateBlockIndex(x, y, z - 1);
                if (originalBlocks[back] > 0 && originalBlocks[back] != originalBlocks[blockIndex]) 
                { 
                    possibleDirections[possibleDirections_count] = directions[5]; // BACK
                    possibleDirections_count++;
                }
            }

            // Pick random direction
            if (possibleDirections_count > 0)
            {
                Vector3Int chosenDirection = possibleDirections[Generation.random.Next(possibleDirections_count)];
                Vector3Int chosenBlockPos = new Vector3Int(x, y, z) + chosenDirection;
                blockArray1D[blockIndex] = originalBlocks[CalculateBlockIndex(chosenBlockPos.x, chosenBlockPos.y, chosenBlockPos.z)];
            }
            
        }
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
        Chunk[] adjacentChunks = new Chunk[6];

        adjacentChunks[(int)Direction.RIGHT] = GetChunkFromCoords((int)(chunkPos.x + CHUNK_WIDTH * blockSize), chunkPos.y, chunkPos.z);
        adjacentChunks[(int)Direction.LEFT] = GetChunkFromCoords((int)(chunkPos.x - CHUNK_WIDTH * blockSize), chunkPos.y, chunkPos.z);
        adjacentChunks[(int)Direction.FORWARD] = GetChunkFromCoords(chunkPos.x, chunkPos.y, (int)(chunkPos.z + CHUNK_LENGTH * blockSize));
        adjacentChunks[(int)Direction.BACK] = GetChunkFromCoords(chunkPos.x, chunkPos.y, (int)(chunkPos.z - CHUNK_LENGTH * blockSize));
        adjacentChunks[(int)Direction.UP] = GetChunkFromCoords(chunkPos.x, (int)(chunkPos.y + CHUNK_HEIGHT * blockSize), chunkPos.z);
        adjacentChunks[(int)Direction.DOWN] = GetChunkFromCoords(chunkPos.x, (int)(chunkPos.y - CHUNK_HEIGHT * blockSize), chunkPos.z);

        return adjacentChunks;
    }
}
