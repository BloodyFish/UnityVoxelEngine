using System.Collections.Generic;
using UnityEngine;


public class VoxelManager
{
    private static List<Vector3> vertices = new List<Vector3>();
    private static List<int> triangles = new List<int>();
    private static List<Color> colors = new List<Color>();
    
    public static void GreedyMesh(Chunk chunk)
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();
        // We want to take a 2D cross-section of our voxels; a 2D representation of each "layer" on each axis
        // Then, we want to use our greedy meshing algorthim on that cross-section

        // Horizontal cross-section:
        // For every y: we start at the x and z of the first actual block
        // If the block in front of our current block is of the same type, we increase the vertex's length by one.
        // We keep doing this until we reach another block. When this happens, we look horizontally to see if we can extend our quad in the x direction
        // When we are all done, we have a quad! Then we either repeat this for the next block group in the z direction, or we move in the x direction and repeat.

        /* 0 0 0 1 1 1 0 0
         * 0 0 1 1 1 0 0 1
         * 1 1 1 0 0 0 1 1
         * 1 1 1 1 1 0 0 0
         * 0 0 0 0 1 1 1 0
         * 1 1 1 0 0 0 1 1 
         * 0 0 1 1 1 0 0 0 */


        // TOP GREEDY MESH
        TopGreedyMesh(chunk);

        // BOTTOM GREEDY MESH
        BottomGreedyMesh(chunk);

        // RIGHT GREEDY MESH
        RightGreedyMesh(chunk);

        //LEFT GREEDY MESH
        LeftGreedyMesh(chunk);

        // FRONT GREEDY MESH
        FrontGreedyMesh(chunk);

        // BACK GREEDY MESH
        BackGreedyMesh(chunk);

        GenerateMesh(chunk.chunkObj);
    }

    private static void TopGreedyMesh(Chunk chunk)
    {
        for (int y = Chunk.CHUNK_HEIGHT; y >= 0; y--)
        {
            //Create an array of the visible blocks:
            byte[] blocks = new byte[Chunk.CHUNK_WIDTH * Chunk.CHUNK_LENGTH];
            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
                {
                    if (y < Chunk.CHUNK_HEIGHT - 1)
                    {
                        if (chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y + 1, z)] > 0)
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * z)] = 0;
                        }
                        else
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * z)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];
                        }
                    }
                    else if (y == Chunk.CHUNK_HEIGHT - 1)
                    {
                        blocks[x + (Chunk.CHUNK_WIDTH * z)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];
                    }
                }
            }


            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
                {
                    byte blockID = blocks[x + (Chunk.CHUNK_WIDTH * z)];
                    if (blockID == 0) { continue; }

                    int length = 0;
                    while (z + length < Chunk.CHUNK_LENGTH && blocks[x + (Chunk.CHUNK_WIDTH * (z + length))] == blockID)
                    {
                        length++;
                    }

                    int width = 1; //We already checked the otherc column!
                    bool canExtend = true;
                    while (canExtend && x + width < Chunk.CHUNK_WIDTH)
                    {
                        for (int l = 0; l < length; l++)
                        {
                            if (blocks[(x + width) + (Chunk.CHUNK_WIDTH * (z + l))] != blockID)
                            {
                                canExtend = false;
                                break;
                            }
                        }

                        if (canExtend)
                        {
                            width++;
                        }
                    }

                    for (int i = 0; i < width; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            blocks[(x + i) + (Chunk.CHUNK_WIDTH * (z + j))] = 0;
                        }
                    }

                    // GENERATE QUAD
                    int index = vertices.Count;

                    float size = Generation.BLOCK_SIZE; // 0.25

                    float xCenter = (x + width / 2f) * size;
                    float zCenter = (z + length / 2f) * size;
                    float yPos = (y * size); //This is the bottom corner y. So we'll have to move it up

                    // half sizes for the quad
                    float halfWidth = (width * size) / 2f;
                    float halfLength = (length * size) / 2f;

                    vertices.Add(new Vector3(xCenter - halfWidth, yPos + size, zCenter - halfLength));
                    vertices.Add(new Vector3(xCenter - halfWidth, yPos + size, zCenter + halfLength));
                    vertices.Add(new Vector3(xCenter + halfWidth, yPos + size, zCenter + halfLength));
                    vertices.Add(new Vector3(xCenter + halfWidth, yPos + size, zCenter - halfLength));

                    GenerateTris(index);
                    GenerateColors(Generation.instance.blockList.blocks[blockID - 1].vertexColor);
                }
            }
        }
    }
    private static void BottomGreedyMesh(Chunk chunk)
    {
        for (int y = 1; y < Chunk.CHUNK_HEIGHT - 1; y++) // We start at one becuase we don't actually need to render the bottom of the world. Who will see it?
        {
            //Create an array of the visible blocks:
            byte[] blocks = new byte[Chunk.CHUNK_WIDTH * Chunk.CHUNK_LENGTH];
            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
                {
                    if (y > 0)
                    {
                        if(chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y - 1, z)] > 0)
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * z)] = 0;
                        }
                        else
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * z)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];
                        }
                    }
                    else if (y == 0)
                    {
                        blocks[x + (Chunk.CHUNK_WIDTH * z)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];
                    }
                }
            }

            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
                {
                    byte blockID = blocks[x + (Chunk.CHUNK_WIDTH * z)];
                    if (blockID == 0) { continue; }

                    int length = 0;
                    while (z + length < Chunk.CHUNK_LENGTH && blocks[x + (Chunk.CHUNK_WIDTH * (z + length))] == blockID)
                    {
                        length++;
                    }

                    int width = 1; //We already checked the otherc column!
                    bool canExtend = true;
                    while (canExtend && x + width < Chunk.CHUNK_WIDTH)
                    {
                        for (int l = 0; l < length; l++)
                        {
                            if (blocks[(x + width) + (Chunk.CHUNK_WIDTH * (z + l))] != blockID)
                            {
                                canExtend = false;
                                break;
                            }
                        }

                        if (canExtend)
                        {
                            width++;
                        }
                    }

                    for (int i = 0; i < width; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            blocks[(x + i) + (Chunk.CHUNK_WIDTH * (z + j))] = 0;
                        }
                    }

                    // GENERATE QUAD
                    int index = vertices.Count;

                    float size = Generation.BLOCK_SIZE; // 0.25

                    float xCenter = (x + width / 2f) * size;
                    float zCenter = (z + length / 2f) * size;
                    float yPos = (y * size); //This is the bottom corner y. So we dont need to meove it

                    // half sizes for the quad
                    float halfWidth = (width * size) / 2f;
                    float halfLength = (length * size) / 2f;

                    vertices.Add(new Vector3(xCenter - halfWidth, yPos, zCenter + halfLength));
                    vertices.Add(new Vector3(xCenter - halfWidth, yPos, zCenter - halfLength));
                    vertices.Add(new Vector3(xCenter + halfWidth, yPos, zCenter - halfLength));
                    vertices.Add(new Vector3(xCenter + halfWidth, yPos, zCenter + halfLength));

                    GenerateTris(index);
                    GenerateColors(Generation.instance.blockList.blocks[blockID - 1].vertexColor);
                }
            }
        }
    }
    private static void RightGreedyMesh(Chunk chunk)
    {
        for (int x = Chunk.CHUNK_WIDTH - 1; x >= 0; x--)
        {
            if (x == Chunk.CHUNK_WIDTH - 1 && chunk.GetAdjacentChunks()[(int)Chunk.Direction.RIGHT] == null) { continue; }

            //Create an array of the visible blocks:
            byte[] blocks = new byte[Chunk.CHUNK_LENGTH * Chunk.CHUNK_HEIGHT];
            for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    if (x < Chunk.CHUNK_WIDTH - 1)
                    {
                        if(chunk.blockArray1D[Chunk.CalculateBlockIndex(x + 1, y, z)] > 0)
                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = 0;
                        }
                        else
                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];
                        }
                    }
                    else if (x == Chunk.CHUNK_WIDTH - 1)
                    {
                        if (chunk.GetAdjacentChunks()[(int)Chunk.Direction.RIGHT]?.blockArray1D[Chunk.CalculateBlockIndex(0, y, z)] > 0)
                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = 0;
                        }
                        else
                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];

                        }
                    }
                }
            }

            for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    byte blockID = blocks[z + (Chunk.CHUNK_LENGTH * y)];
                    if (blockID == 0) { continue; }

                    int height = 0;
                    while (y + height < Chunk.CHUNK_HEIGHT && blocks[z + (Chunk.CHUNK_LENGTH * (y + height))] == blockID)
                    {
                        height++;
                    }

                    int length = 1; //We already checked the otherc column!
                    bool canExtend = true;
                    while (canExtend && z + length < Chunk.CHUNK_LENGTH)
                    {
                        for (int h = 0; h < height; h++)
                        {
                            if (blocks[(z + length) + (Chunk.CHUNK_LENGTH * (y + h))] != blockID)
                            {
                                canExtend = false;
                                break;
                            }
                        }

                        if (canExtend)
                        {
                            length++;
                        }
                    }

                    for (int i = 0; i < length; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            blocks[(z + i) + (Chunk.CHUNK_LENGTH * (y + j))] = 0;
                        }
                    }
                    // GENERATE QUAD
                    int index = vertices.Count;

                    float size = Generation.BLOCK_SIZE; // 0.25

                    float xPos = (x * size);
                    float yCenter = (y + height / 2f) * size;
                    float zCenter = (z + length / 2f) * size;

                    float halfHeight = (height * size) / 2f;
                    float halfLength = (length * size) / 2f;

                    vertices.Add(new Vector3(xPos + size, yCenter - halfHeight, zCenter - halfLength));
                    vertices.Add(new Vector3(xPos + size, yCenter + halfHeight, zCenter - halfLength));
                    vertices.Add(new Vector3(xPos + size, yCenter + halfHeight, zCenter + halfLength));
                    vertices.Add(new Vector3(xPos + size, yCenter - halfHeight, zCenter + halfLength));

                    GenerateTris(index);
                    GenerateColors(Generation.instance.blockList.blocks[blockID - 1].vertexColor);
                }
            }
        }
    }
    private static void LeftGreedyMesh(Chunk chunk)
    {
        for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
        {
            if (x == 0 && chunk.GetAdjacentChunks()[(int)Chunk.Direction.LEFT] == null) { continue; }

            //Create an array of the visible blocks:
            byte[] blocks = new byte[Chunk.CHUNK_LENGTH * Chunk.CHUNK_HEIGHT];
            for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    if (x > 0)
                    {
                        if (chunk.blockArray1D[Chunk.CalculateBlockIndex(x - 1, y, z)] > 0)

                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = 0;
                        }
                        else
                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];
                        }
                    }
                    else if (x == 0)
                    {
                        if (chunk.GetAdjacentChunks()[(int)Chunk.Direction.LEFT]?.blockArray1D[Chunk.CalculateBlockIndex(Chunk.CHUNK_WIDTH - 1, y, z)] > 0)
                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = 0;
                        }
                        else
                        {
                            blocks[z + (Chunk.CHUNK_LENGTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];

                        }
                    }
                }
            }

            for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    byte blockID = blocks[z + (Chunk.CHUNK_LENGTH * y)];
                    if (blockID == 0) { continue; }

                    int height = 0;
                    while (y + height < Chunk.CHUNK_HEIGHT && blocks[z + (Chunk.CHUNK_LENGTH * (y + height))] == blockID)
                    {
                        height++;
                    }

                    int length = 1; //We already checked the otherc column!
                    bool canExtend = true;
                    while (canExtend && z + length < Chunk.CHUNK_LENGTH)
                    {
                        for (int h = 0; h < height; h++)
                        {
                            if (blocks[(z + length) + (Chunk.CHUNK_LENGTH * (y + h))] != blockID)
                            {
                                canExtend = false;
                                break;
                            }
                        }

                        if (canExtend)
                        {
                            length++;
                        }
                    }

                    for (int i = 0; i < length; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            blocks[(z + i) + (Chunk.CHUNK_LENGTH * (y + j))] = 0;
                        }
                    }
                    // GENERATE QUAD
                    int index = vertices.Count;

                    float size = Generation.BLOCK_SIZE; // 0.25

                    float xPos = (x * size); 
                    float yCenter = (y + height / 2f) * size;
                    float zCenter = (z + length / 2f) * size;

                    float halfHeight = (height * size) / 2f;
                    float halfLength = (length * size) / 2f;

                    vertices.Add(new Vector3(xPos, yCenter - halfHeight, zCenter + halfLength));
                    vertices.Add(new Vector3(xPos, yCenter + halfHeight, zCenter + halfLength));
                    vertices.Add(new Vector3(xPos, yCenter + halfHeight, zCenter - halfLength));
                    vertices.Add(new Vector3(xPos, yCenter - halfHeight, zCenter - halfLength));

                    GenerateTris(index);
                    GenerateColors(Generation.instance.blockList.blocks[blockID - 1].vertexColor);
                }
            }
        }
    }
    private static void FrontGreedyMesh(Chunk chunk)
    {
        for (int z = 0; z < Chunk.CHUNK_LENGTH; z++)
        {
            if (z == 0 && chunk.GetAdjacentChunks()[(int)Chunk.Direction.BACK] == null) { continue; }

            //Create an array of the visible blocks:
            byte[] blocks = new byte[Chunk.CHUNK_WIDTH * Chunk.CHUNK_HEIGHT];
            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    if (z > 0)
                    {
                        if (chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z - 1)] > 0)

                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = 0;
                        }
                        else
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];

                        }
                    }
                    else if (z == 0)
                    {
                        if (chunk.GetAdjacentChunks()[(int)Chunk.Direction.BACK]?.blockArray1D[Chunk.CalculateBlockIndex(x, y, Chunk.CHUNK_LENGTH - 1)] > 0)
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = 0;
                        }
                        else
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];

                        }
                    }
                }
            }

            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    byte blockID = blocks[x + (Chunk.CHUNK_WIDTH * y)];
                    if (blockID == 0) { continue; }

                    int height = 0;
                    while (y + height < Chunk.CHUNK_HEIGHT && blocks[x + (Chunk.CHUNK_WIDTH * (y + height))] == blockID)
                    {
                        height++;
                    }

                    int width = 1; //We already checked the otherc column!
                    bool canExtend = true;
                    while (canExtend && x + width < Chunk.CHUNK_WIDTH)
                    {
                        for (int h = 0; h < height; h++)
                        {
                            if (blocks[(x + width) + (Chunk.CHUNK_WIDTH * (y + h))] != blockID)
                            {
                                canExtend = false;
                                break;
                            }
                        }

                        if (canExtend)
                        {
                            width++;
                        }
                    }

                    for (int i = 0; i < width; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            blocks[(x + i) + (Chunk.CHUNK_WIDTH * (y + j))] = 0;
                        }
                    }

                    // GENERATE QUAD
                    int index = vertices.Count;

                    float size = Generation.BLOCK_SIZE; // 0.25

                    float xCenter = (x + width / 2f) * size;
                    float zPos = (z * size); // This is the actual z corner of the front. We dont need to readjust!
                    float yCenter = (y + height / 2f) * size;

                    // half sizes for the quad
                    float halfWidth = (width * size) / 2f;
                    float halfHeight = (height * size) / 2f;

                    vertices.Add(new Vector3(xCenter - halfWidth, yCenter - halfHeight, zPos));
                    vertices.Add(new Vector3(xCenter - halfWidth, yCenter + halfHeight, zPos));
                    vertices.Add(new Vector3(xCenter + halfWidth, yCenter + halfHeight, zPos));
                    vertices.Add(new Vector3(xCenter + halfWidth, yCenter - halfHeight, zPos));

                    GenerateTris(index);
                    GenerateColors(Generation.instance.blockList.blocks[blockID - 1].vertexColor);
                }
            }
        }
    }
    private static void BackGreedyMesh(Chunk chunk)
    {
        for (int z = Chunk.CHUNK_LENGTH - 1; z >= 0; z--)
        {
            if (z == Chunk.CHUNK_LENGTH - 1 && chunk.GetAdjacentChunks()[(int)Chunk.Direction.FORWARD] == null) { continue; }

            //Create an array of the visible blocks:
            byte[] blocks = new byte[Chunk.CHUNK_WIDTH * Chunk.CHUNK_HEIGHT];
            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    if (z < Chunk.CHUNK_LENGTH - 1)
                    {
                        if (chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z + 1)] > 0)

                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = 0;
                        }
                        else
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];

                        }
                    }
                    else if (z == Chunk.CHUNK_LENGTH - 1)
                    {
                        if (chunk.GetAdjacentChunks()[(int)Chunk.Direction.FORWARD]?.blockArray1D[Chunk.CalculateBlockIndex(x, y, 0)] > 0)

                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = 0;
                        }
                        else
                        {
                            blocks[x + (Chunk.CHUNK_WIDTH * y)] = chunk.blockArray1D[Chunk.CalculateBlockIndex(x, y, z)];

                        }
                    }
                }
            }

            for (int x = 0; x < Chunk.CHUNK_WIDTH; x++)
            {
                for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
                {
                    byte blockID = blocks[x + (Chunk.CHUNK_WIDTH * y)];
                    if (blockID == 0) { continue; }

                    int height = 0;
                    while (y + height < Chunk.CHUNK_HEIGHT && blocks[x + (Chunk.CHUNK_WIDTH * (y + height))] == blockID)
                    {
                        height++;
                    }

                    int width = 1; //We already checked the otherc column!
                    bool canExtend = true;
                    while (canExtend && x + width < Chunk.CHUNK_WIDTH)
                    {
                        for (int h = 0; h < height; h++)
                        {
                            if (blocks[(x + width) + (Chunk.CHUNK_WIDTH * (y + h))] != blockID)
                            {
                                canExtend = false;
                                break;
                            }
                        }

                        if (canExtend)
                        {
                            width++;
                        }
                    }

                    for (int i = 0; i < width; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            blocks[(x + i) + (Chunk.CHUNK_WIDTH * (y + j))] = 0;
                        }
                    }

                    // GENERATE QUAD
                    int index = vertices.Count;

                    float size = Generation.BLOCK_SIZE; // 0.25

                    float xCenter = (x + width / 2f) * size;
                    float zPos = (z * size);
                    float yCenter = (y + height / 2f) * size;

                    // half sizes for the quad
                    float halfWidth = (width * size) / 2f;
                    float halfHeight = (height * size) / 2f;

                    vertices.Add(new Vector3(xCenter + halfWidth, yCenter - halfHeight, zPos + size));
                    vertices.Add(new Vector3(xCenter + halfWidth, yCenter + halfHeight, zPos + size));
                    vertices.Add(new Vector3(xCenter - halfWidth, yCenter + halfHeight, zPos + size));
                    vertices.Add(new Vector3(xCenter - halfWidth, yCenter - halfHeight, zPos + size));

                    GenerateTris(index);
                    GenerateColors(Generation.instance.blockList.blocks[blockID - 1].vertexColor);
                }
            }
        }
    }


    private static void GenerateMesh(GameObject obj)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();


        obj.GetComponent<MeshFilter>().mesh = mesh;
        obj.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static void GenerateTris(int index)
    {
        triangles.Add(index + 0);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
        triangles.Add(index + 2);
        triangles.Add(index + 3);
        triangles.Add(index + 0);
    }


    private static void GenerateColors(Color32 color)
    {
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
    }
}
