
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;


public class Chunk
{
    //public static List<Chunk> chunks = new List<Chunk>();
    public static ConcurrentDictionary<Vector3Int, Chunk> chunks = new ConcurrentDictionary<Vector3Int, Chunk>();

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

    public Chunk(Vector3 chunkPos)
    {
        float blockSize = Generation.BLOCK_SIZE;
        // Remember, our voxels are smaller than 1 unit. Using "chunkPos.x * CHUNK_WIDTH" would give us spacing as if each block was 1 unit. Multiply to  get correct world space
        this.chunkPos = new Vector3Int((int)(chunkPos.x * (CHUNK_WIDTH * blockSize)), 0, (int)(chunkPos.z * (CHUNK_LENGTH * blockSize)));

        this.chunkObj = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer));
        this.chunkObj.GetComponent<Renderer>().material = Generation.instance.terrainMat;
        this.chunkObj.transform.position = this.chunkPos;

        blockArray1D = new byte[CHUNK_WIDTH * CHUNK_HEIGHT * CHUNK_LENGTH];

        chunks.TryAdd(this.chunkPos, this);


        Task.Run(() => GenerateChunk()).ContinueWith(task =>
        {
            voxelManager.GenerateMesh(chunkObj);

        }, TaskScheduler.FromCurrentSynchronizationContext());


        Task.Run(() => {
            foreach (Chunk adj_chunk in GetAdjacentChunks())
            {
                if (adj_chunk != null && adj_chunk.isGenerated)
                {
                    adj_chunk.voxelManager.GreedyMesh(adj_chunk);
                    Thread.Sleep(10);
                }
            }
        }).ContinueWith(task =>
        {
            foreach(Chunk adj_chunk in GetAdjacentChunks()) 
            {
                if (adj_chunk != null && adj_chunk.isGenerated)
                {
                    adj_chunk.voxelManager.GenerateMesh(adj_chunk.chunkObj);
                }
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }


    public void GenerateChunk()
    {
        SetBlockArray(this);
        voxelManager.GreedyMesh(this);
    }
    public void SetBlockArray(Chunk chunk)
    {
        float blockSize = Generation.BLOCK_SIZE;

        for (short x = 0; x < CHUNK_WIDTH; x++)
        {
            for (short z = 0; z < CHUNK_LENGTH; z++)
            {
                float xCoord = (x * blockSize) + chunkPos.x;
                float zCoord = (z * blockSize + chunkPos.z);

                float contentalness = Generation.GetContenentalness(xCoord, zCoord);
                short yVal = (short)Mathf.Ceil(Generation.instance.contenentalnessToHeight_spline.EvaluateAtPoint(contentalness, 100 / blockSize));
                //Debug.Log(yVal);
                float slope = Generation.instance.contenentalnessToHeight_spline.GetInstantaneousSlopeAtPoint(contentalness);
                //Debug.Log(slope);

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
        Chunk chunk = null;
        chunks.TryGetValue(new Vector3Int(x, y, z), out chunk);
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
