using System;
using System.Collections.Generic;
using UnityEngine;


public class Chunk : IComparable<Chunk> 
{
    //public static List<Chunk> chunks = new List<Chunk>();
    public static Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();

    public static readonly short CHUNK_WIDTH = (short)(16 / Generation.BLOCK_SIZE);
    public static readonly short CHUNK_LENGTH = (short)(16 / Generation.BLOCK_SIZE);
    public static readonly short CHUNK_HEIGHT = (short)(384 / Generation.BLOCK_SIZE);

    public enum Direction { RIGHT = 0, LEFT = 1, FORWARD = 2, BACK = 3}
    public static Direction chunkDirection;

    public GameObject chunkObj;
    public Vector3Int chunkPos;

    public byte[,,] blockArray; // these bytes should reference different blocks. If they are 0 it is air. A byte goes to 0-255

    public Chunk(Vector3 chunkPos)
    {
        float blockSize = Generation.BLOCK_SIZE;

        // We multiply by blockSize to cancel out. 16 / 0.25 is 64. We still want the chunks to be 16 units away from each other
        // So we multiply: (16 / 0.25) * 0.25 = 16

        this.chunkPos = new Vector3Int((int)(chunkPos.x * CHUNK_WIDTH * blockSize), 0, (int)(chunkPos.z * CHUNK_LENGTH * blockSize));
        blockArray = new byte[CHUNK_WIDTH, CHUNK_HEIGHT, CHUNK_LENGTH];
        
        chunks.Add(this.chunkPos, this);


        SetBlockArray();

    }

    public void SetBlockArray()
    {
        for (short x = 0; x < CHUNK_WIDTH; x++)
        {
            for (short z = 0; z < CHUNK_LENGTH; z++)
            {
                // Since we multiplied everything by blockSize before, we need to divide by blockSize to get the right noise position (since everything is still on the same size grid (CHUNK_WIDTH x CHUNK_LENGTH), its just the size of the blocks thats different)
                // Therefore, we need to get rid of the multiplication we did, or else the noise will have weird spaces
                float xCoord = x + chunkPos.x / Generation.BLOCK_SIZE;
                float zCoord = z + chunkPos.z / Generation.BLOCK_SIZE;

                float contentalness = Generation.GetContenentalness(xCoord, zCoord);
                short yVal = (short)Mathf.Ceil(Generation.instance.contenentalnessToHeight.EvaluateAtPoint(contentalness, 100 / Generation.BLOCK_SIZE));
                float slope = Generation.instance.contenentalnessToHeight.GetInstantaneousSlopeAtPoint(contentalness);

                for (short y = 0; y < CHUNK_HEIGHT; y++)
                {
                    
                    if(y <= yVal)
                    {
                        if (slope > 0.5f)
                        {
                            blockArray[x, y, z] = (byte)(Generation.instance.stoneBlock.block_ID + 1);
                        }
                        else
                        {
                            if (y <= 20 / Generation.BLOCK_SIZE)
                            {
                                blockArray[x, y, z] = (byte)(Generation.instance.underwaterBlock.block_ID + 1);
                            }
                            else if (y == yVal)
                            {
                                blockArray[x, y, z] = (byte)(Generation.instance.mainBlock.block_ID + 1);
                            }
                            else
                            {
                                blockArray[x, y, z] = (byte)(Generation.instance.dirtBlock.block_ID + 1);
                            }
                        }
                    }
                }
            }
        }
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

        adjacentChunks[(int)Direction.RIGHT] = GetChunkFromCoords((int)(chunkPos.x + (CHUNK_WIDTH * blockSize)), chunkPos.y, chunkPos.z);
        adjacentChunks[(int)Direction.LEFT] = GetChunkFromCoords((int)(chunkPos.x - (CHUNK_WIDTH * blockSize)), chunkPos.y, chunkPos.z);
        adjacentChunks[(int)Direction.FORWARD] = GetChunkFromCoords(chunkPos.x, chunkPos.y, (int)(chunkPos.z + (CHUNK_LENGTH * blockSize)));
        adjacentChunks[(int)Direction.BACK] = GetChunkFromCoords(chunkPos.x, chunkPos.y, (int)(chunkPos.z - (CHUNK_LENGTH * blockSize)));

        return adjacentChunks;
    }

    public int CompareTo(Chunk other)
    {

        // x values hold more prevalence
        int xComparison = this.chunkPos.x.CompareTo(other.chunkPos.x);
        int zComparison = this.chunkPos.z.CompareTo(other.chunkPos.z);

        if (xComparison != 0)
        {
            return xComparison;
        }
        else // if the x values are the same we compare the z values
        {
            return zComparison;
        }

    }
}
