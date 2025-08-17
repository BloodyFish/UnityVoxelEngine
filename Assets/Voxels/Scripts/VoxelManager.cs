using System;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Voxels.Scripts.Dispatcher;


public class VoxelManager
{
    public struct GreedyVertex
    {
        public Vector3 position;
        public Color32 color;
    }

    public struct GreedyMeshReturnValues : IDisposable
    {
        public JobHandle handle;

        public TopGreedyMesh topGreedyMesh;
        public BottomGreedyMesh bottomGreedyMesh;
        public RightGreedyMesh rightGreedyMesh;
        public LeftGreedyMesh leftGreedyMesh;
        public FrontGreedyMesh frontGreedyMesh;
        public BackGreedyMesh backGreedyMesh;

        public int VertexCount()
        {
            return topGreedyMesh.verticies.Length
                   + bottomGreedyMesh.verticies.Length 
                   + leftGreedyMesh.verticies.Length
                   + rightGreedyMesh.verticies.Length
                   + frontGreedyMesh.verticies.Length
                   + backGreedyMesh.verticies.Length;
        }
        public int TrianglesCount()
        {
            return topGreedyMesh.triangles.Length
                   + bottomGreedyMesh.triangles.Length 
                   + leftGreedyMesh.triangles.Length
                   + rightGreedyMesh.triangles.Length
                   + frontGreedyMesh.triangles.Length
                   + backGreedyMesh.triangles.Length;
        }

        public void Dispose()
        {
            AsyncHelper.DisposeNativeObject(topGreedyMesh.blockArray1D); // Since all jobs share the same blockArray1D, clearing one clears all!
            AsyncHelper.DisposeNativeObject(topGreedyMesh.triangles);
            AsyncHelper.DisposeNativeObject(topGreedyMesh.verticies);
            AsyncHelper.DisposeNativeObject(topGreedyMesh.blocks);
            
            AsyncHelper.DisposeNativeObject(bottomGreedyMesh.triangles);
            AsyncHelper.DisposeNativeObject(bottomGreedyMesh.verticies);
            AsyncHelper.DisposeNativeObject(bottomGreedyMesh.blocks);
            
            AsyncHelper.DisposeNativeObject(leftGreedyMesh.triangles);
            AsyncHelper.DisposeNativeObject(leftGreedyMesh.verticies);
            AsyncHelper.DisposeNativeObject(leftGreedyMesh.blocks);
            AsyncHelper.DisposeNativeObject(leftGreedyMesh.blockArray1D_left);
            
            AsyncHelper.DisposeNativeObject(rightGreedyMesh.triangles);
            AsyncHelper.DisposeNativeObject(rightGreedyMesh.verticies);
            AsyncHelper.DisposeNativeObject(rightGreedyMesh.blocks);
            AsyncHelper.DisposeNativeObject(rightGreedyMesh.blockArray1D_right);
            
            AsyncHelper.DisposeNativeObject(frontGreedyMesh.triangles);
            AsyncHelper.DisposeNativeObject(frontGreedyMesh.verticies);
            AsyncHelper.DisposeNativeObject(frontGreedyMesh.blocks);
            AsyncHelper.DisposeNativeObject(frontGreedyMesh.blockArray1D_front);
            
            AsyncHelper.DisposeNativeObject(backGreedyMesh.triangles);
            AsyncHelper.DisposeNativeObject(backGreedyMesh.verticies);
            AsyncHelper.DisposeNativeObject(backGreedyMesh.blocks);
            AsyncHelper.DisposeNativeObject(backGreedyMesh.blockArray1D_back);
        }
    }

    public struct GreedyMeshResult
    {
        private GreedyMeshReturnValues returnValues;

        private NativeArray<byte> horizontalCrossSection;
        private NativeArray<byte> verticalCrossSectionWidth;
        private NativeArray<byte> verticalCrossSectionLength;

        private NativeArray<Color32> colors;

        public GreedyMeshResult(GreedyMeshReturnValues returnValues,
            NativeArray<byte> horizontalCrossSection,
            NativeArray<byte> verticalCrossSectionWidth,
            NativeArray<byte> verticalCrossSectionLength,
            NativeArray<Color32> colors
        ) {
            this.returnValues = returnValues;
            
            this.horizontalCrossSection = horizontalCrossSection;
            this.verticalCrossSectionWidth = verticalCrossSectionWidth;
            this.verticalCrossSectionLength = verticalCrossSectionLength;
            
            this.colors = colors;
        }

        public delegate void GreedyMeshResultHandler(NativeList<GreedyVertex> vertices, NativeList<int> triangles);
        
        private void CombineTris(NativeList<int> from, NativeList<int> to, int index)
        {
            foreach (int t in from)
            {
                to.Add(index + t);
            }
        }

        public void ForceComplete() => returnValues.handle.Complete();
        
        public void Then(GreedyMeshResultHandler handler)
        {
            // results have already been disposed, do nothing.
            if (!horizontalCrossSection.IsCreated) return;
            
            GreedyMeshResult self = this;
            AsyncHelper.RunOnMainThreadWhenComplete(returnValues.handle, () =>
            {
                AsyncHelper.DisposeNativeObject(self.returnValues.topGreedyMesh.blocks);
                AsyncHelper.DisposeNativeObject(self.returnValues.topGreedyMesh.blockArray1D_above);

                AsyncHelper.DisposeNativeObject(self.returnValues.bottomGreedyMesh.blocks);
                AsyncHelper.DisposeNativeObject(self.returnValues.bottomGreedyMesh.blockArray1D_below);

                AsyncHelper.DisposeNativeObject(self.returnValues.leftGreedyMesh.blocks);
                AsyncHelper.DisposeNativeObject(self.returnValues.leftGreedyMesh.blockArray1D_left);
                
                AsyncHelper.DisposeNativeObject(self.returnValues.rightGreedyMesh.blocks);
                AsyncHelper.DisposeNativeObject(self.returnValues.rightGreedyMesh.blockArray1D_right);
                
                AsyncHelper.DisposeNativeObject(self.returnValues.frontGreedyMesh.blocks);
                AsyncHelper.DisposeNativeObject(self.returnValues.frontGreedyMesh.blockArray1D_front);
                
                AsyncHelper.DisposeNativeObject(self.returnValues.backGreedyMesh.blocks);
                AsyncHelper.DisposeNativeObject(self.returnValues.backGreedyMesh.blockArray1D_back);
                
                AsyncHelper.DisposeNativeObject(self.horizontalCrossSection);
                AsyncHelper.DisposeNativeObject(self.verticalCrossSectionWidth);
                AsyncHelper.DisposeNativeObject(self.verticalCrossSectionLength);
                
                AsyncHelper.DisposeNativeObject(self.colors);
                
                int vertexCount = self.returnValues.VertexCount();
                int trianglesCount = self.returnValues.TrianglesCount();
                
                NativeList<GreedyVertex> vertices = AsyncHelper.CreatePersistentNativeList<GreedyVertex>(vertexCount);
                NativeList<int> triangles = AsyncHelper.CreatePersistentNativeList<int>(trianglesCount);
                
                // When we add triangles, the number we add is (index + 0), (index + 1).... but in our job, we start with a vertices list that is empty
                // So even though we add an offset inside the job as well, the offset is correct in terms with a starting vertex list of 0. It grows correcly inside the job
                // BUT: inside the job our vertex list's length grows like: Length: 0, Length: 4, Length: 8... (for each quad), and we add that index to our tri index (hence the index + 0, index + 1, etc)
                // SO: indide each job our triangles match up with the vertcies added starting at 0, starting at 4, starting at 8...
                // HOWEVER: while this is correct, for each greedy meshing algrithim (top, bottom, left, right), our verticies start back at 0 since we pass an empty vertcies list whgen we call our job
                // THIS MEANS: that each triangle has the exast same integer relating to vertext index
                // When we combine our lists, we have to add ANOTHER offset to account for the fact that inside the jobs our list started at 0
                
                // TRIANGLES WORK LIKE:
                // 0, 1, 2, 2, 3, 0
                // Then as we add more verts (4) we add 4 to the index: 4, 5, 6, 7, 7, 4
                // And then when we add more... 8, 9, 10, 11, 11, 8
                // So these numbers should keep increasing! However, as mentioned before, when we are inside a job, we pass a new list every greedy meshing algorthim, so we start at 0 each algorithm
                // we go from 0 to a high number in one algrothmm (like top), but when the next algorithm starts (like right), the vertices list is passed as a new, empty list, and we start at 0 to a high number. 
                // This is why we, when we combine, add another offset

                // That was a vert long explanation lol
                
                self.CombineTris(self.returnValues.topGreedyMesh.triangles, triangles, vertices.Length);
                vertices.AddRange(self.returnValues.topGreedyMesh.verticies.AsArray());
                AsyncHelper.DisposeNativeObject(self.returnValues.topGreedyMesh.triangles);
                AsyncHelper.DisposeNativeObject(self.returnValues.topGreedyMesh.verticies);
                
                self.CombineTris(self.returnValues.bottomGreedyMesh.triangles, triangles, vertices.Length);
                vertices.AddRange(self.returnValues.bottomGreedyMesh.verticies.AsArray());
                AsyncHelper.DisposeNativeObject(self.returnValues.bottomGreedyMesh.triangles);
                AsyncHelper.DisposeNativeObject(self.returnValues.bottomGreedyMesh.verticies);
                
                self.CombineTris(self.returnValues.leftGreedyMesh.triangles, triangles, vertices.Length);
                vertices.AddRange(self.returnValues.leftGreedyMesh.verticies.AsArray());
                AsyncHelper.DisposeNativeObject(self.returnValues.leftGreedyMesh.triangles);
                AsyncHelper.DisposeNativeObject(self.returnValues.leftGreedyMesh.verticies);
                
                self.CombineTris(self.returnValues.rightGreedyMesh.triangles, triangles, vertices.Length);
                vertices.AddRange(self.returnValues.rightGreedyMesh.verticies.AsArray());
                AsyncHelper.DisposeNativeObject(self.returnValues.rightGreedyMesh.triangles);
                AsyncHelper.DisposeNativeObject(self.returnValues.rightGreedyMesh.verticies);
                
                self.CombineTris(self.returnValues.frontGreedyMesh.triangles, triangles, vertices.Length);
                vertices.AddRange(self.returnValues.frontGreedyMesh.verticies.AsArray());
                AsyncHelper.DisposeNativeObject(self.returnValues.frontGreedyMesh.triangles);
                AsyncHelper.DisposeNativeObject(self.returnValues.frontGreedyMesh.verticies);
                
                self.CombineTris(self.returnValues.backGreedyMesh.triangles, triangles, vertices.Length);
                vertices.AddRange(self.returnValues.backGreedyMesh.verticies.AsArray());
                AsyncHelper.DisposeNativeObject(self.returnValues.topGreedyMesh.triangles);
                AsyncHelper.DisposeNativeObject(self.returnValues.topGreedyMesh.verticies);
                
                handler(vertices, triangles);

                AsyncHelper.DisposeNativeObject(vertices);
                AsyncHelper.DisposeNativeObject(triangles);
                
                AsyncHelper.DisposeNativeObject(self.returnValues.topGreedyMesh.blockArray1D);
            });
        }


        public void Cleanup()
        {
            returnValues.Dispose();
            
            AsyncHelper.DisposeNativeObject(horizontalCrossSection);
            AsyncHelper.DisposeNativeObject(verticalCrossSectionWidth);
            AsyncHelper.DisposeNativeObject(verticalCrossSectionLength);
            
            AsyncHelper.DisposeNativeObject(colors);
        }
    }

    public GreedyMeshResult GreedyMesh(Chunk chunk)
    {

        // We want to take a 2D cross-section of our voxels; a 2D representation of each "layer" on each axis
        // Then, we want to use our greedy meshing algorthim on that cross-section

        // Horizontal cross-section:
        // For every y: we start at the x and z of the first actual block
        // If the block in front of our current block is of the same type, we increase the vertex's length by one.
        // We keep doing this until we reach another block. When this happens, we look horizontally to see if we can extend our quad in the x direction
        // When we are all done, we have a quad! Then we either repeat this for the next block group in the z direction, or we move in the x direction and repeat.


        //Create an array of the visible blocks:
        NativeArray<byte> horizontal_crossSection = AsyncHelper.CreatePersistentNativeArray<byte>(Chunk.CHUNK_WIDTH * Chunk.CHUNK_LENGTH); // NOTE THE USE OF PERSISTENT!!! This is for jobs that last multiple frames!
        NativeArray<byte> vertical_CrossSection_width = AsyncHelper.CreatePersistentNativeArray<byte>(Chunk.CHUNK_WIDTH * Chunk.CHUNK_HEIGHT);
        NativeArray<byte> vertical_CrossSection_length = AsyncHelper.CreatePersistentNativeArray<byte>(Chunk.CHUNK_LENGTH * Chunk.CHUNK_HEIGHT);

        NativeArray<Color32> colorList = AsyncHelper.CreatePersistentNativeArray<Color32>(Generation.instance.blockList.blocks.Count);
        int i = 0;
        foreach (Block block in Generation.instance.blockList.blocks)
        {
            colorList[i] = block.vertexColor;
            i++;
        }

        GreedyMeshReturnValues greedyMeshReturnValues = GreedyMeshJob(chunk, colorList, horizontal_crossSection, vertical_CrossSection_width, vertical_CrossSection_length);

        return new GreedyMeshResult(
            greedyMeshReturnValues,
            horizontal_crossSection,
            vertical_CrossSection_width,
            vertical_CrossSection_length,
            colorList
        );
    }

    public void GenerateMesh(GameObject obj, NativeList<GreedyVertex> vertices, NativeList<int> triangles)
    {
        Mesh mesh = new Mesh();

        // Setup vertex layout
        mesh.SetVertexBufferParams(vertices.Length,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4)
        );

        // Upload vertex data
        mesh.SetVertexBufferData(vertices.AsArray(), 0, 0, vertices.Length);

        // Setup index buffer (assuming UInt32)
        mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
        mesh.SetIndexBufferData(triangles.AsArray(), 0, 0, triangles.Length);

        // Setup one submesh covering all indices
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length));

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        obj.GetComponent<MeshFilter>().mesh = mesh;
        //obj.GetComponent<MeshCollider>().sharedMesh = mesh;
    }


    public GreedyMeshReturnValues GreedyMeshJob(Chunk chunk, NativeArray<Color32> colorList, params NativeArray<byte>[] crossSections)
    {
        NativeList<JobHandle> jobHandles = new NativeList<JobHandle>(Allocator.Persistent);
        NativeArray<byte> blockArray1D = AsyncHelper.CreatePersistentNativeArray(chunk.blockArray1D);

        NativeArray<byte> horizontal_crossSection = crossSections[0];
        NativeArray<byte> vertical_crossSection_width = crossSections[1];
        NativeArray<byte> vertical_crossSection_length = crossSections[2];

        Chunk[] adjacentChunks = chunk.GetAdjacentChunks();

        bool chunkAbove = adjacentChunks[(int)Chunk.Direction.UP] != null;
        NativeArray<byte> blockArray1D_above = chunkAbove
            ? AsyncHelper.CreatePersistentNativeArray(adjacentChunks[(int)Chunk.Direction.UP].blockArray1D)
            : AsyncHelper.CreatePersistentNativeArray<byte>(0);

        TopGreedyMesh job_Top = new TopGreedyMesh()
        {
            chunkWidth = Chunk.CHUNK_WIDTH,
            chunkLength = Chunk.CHUNK_LENGTH,
            chunkHeight = Chunk.CHUNK_HEIGHT,
            blockSize = Generation.BLOCK_SIZE,

            blockArray1D = blockArray1D,
            blocks = AsyncHelper.CreatePersistentNativeArray<byte>(horizontal_crossSection.Length),

            verticies = AsyncHelper.CreatePersistentNativeList<GreedyVertex>(),
            triangles = AsyncHelper.CreatePersistentNativeList<int>(),

            colors = colorList,
            
            chunkAbove = chunkAbove,
            blockArray1D_above = blockArray1D_above
        };

        bool chunkBelow = adjacentChunks[(int)Chunk.Direction.DOWN] != null;
        NativeArray<byte> blockArray1D_below = chunkBelow
            ? AsyncHelper.CreatePersistentNativeArray(adjacentChunks[(int)Chunk.Direction.DOWN].blockArray1D)
            : AsyncHelper.CreatePersistentNativeArray<byte>(0);

        BottomGreedyMesh job_Bottom = new BottomGreedyMesh()
        {
            chunkWidth = Chunk.CHUNK_WIDTH,
            chunkLength = Chunk.CHUNK_LENGTH,
            chunkHeight = Chunk.CHUNK_HEIGHT,
            blockSize = Generation.BLOCK_SIZE,

            blockArray1D = blockArray1D,
            blocks = AsyncHelper.CreatePersistentNativeArray(horizontal_crossSection),

            verticies = AsyncHelper.CreatePersistentNativeList<GreedyVertex>(),
            triangles = AsyncHelper.CreatePersistentNativeList<int>(),

            colors = colorList,

            chunkBelow = chunkBelow,
            blockArray1D_below = blockArray1D_below
        };

        bool chunkToRight = adjacentChunks[(int)Chunk.Direction.RIGHT] != null;
        NativeArray<byte> blockArray1D_right = chunkToRight
            ? AsyncHelper.CreatePersistentNativeArray(adjacentChunks[(int)Chunk.Direction.RIGHT].blockArray1D)
            : AsyncHelper.CreatePersistentNativeArray<byte>(0);

        RightGreedyMesh job_Right = new RightGreedyMesh()
        {
            chunkWidth = Chunk.CHUNK_WIDTH,
            chunkLength = Chunk.CHUNK_LENGTH,
            chunkHeight = Chunk.CHUNK_HEIGHT,
            blockSize = Generation.BLOCK_SIZE,

            blockArray1D = blockArray1D,
            blocks = AsyncHelper.CreatePersistentNativeArray<byte>(vertical_crossSection_width.Length),

            verticies = AsyncHelper.CreatePersistentNativeList<GreedyVertex>(),
            triangles = AsyncHelper.CreatePersistentNativeList<int>(),

            colors = colorList,

            chunkToRight = chunkToRight,
            blockArray1D_right = blockArray1D_right
        };

        bool chunkToLeft = adjacentChunks[(int)Chunk.Direction.LEFT] != null;
        NativeArray<byte> blockArray1D_left = chunkToLeft
            ? AsyncHelper.CreatePersistentNativeArray(adjacentChunks[(int)Chunk.Direction.LEFT].blockArray1D)
            : AsyncHelper.CreatePersistentNativeArray<byte>(0);

        LeftGreedyMesh job_Left = new LeftGreedyMesh()
        {
            chunkWidth = Chunk.CHUNK_WIDTH,
            chunkLength = Chunk.CHUNK_LENGTH,
            chunkHeight = Chunk.CHUNK_HEIGHT,
            blockSize = Generation.BLOCK_SIZE,

            blockArray1D = blockArray1D,
            blocks = AsyncHelper.CreatePersistentNativeArray<byte>(vertical_crossSection_width.Length),

            verticies = AsyncHelper.CreatePersistentNativeList<GreedyVertex>(),
            triangles = AsyncHelper.CreatePersistentNativeList<int>(),

            colors = colorList,

            chunkToLeft = chunkToLeft,
            blockArray1D_left = blockArray1D_left
        };

        bool chunkToFront = adjacentChunks[(int)Chunk.Direction.FORWARD] != null;
        NativeArray<byte> blockArray1D_front = chunkToFront
            ? AsyncHelper.CreatePersistentNativeArray(adjacentChunks[(int)Chunk.Direction.FORWARD].blockArray1D)
            : AsyncHelper.CreatePersistentNativeArray<byte>(0);

        FrontGreedyMesh job_Front = new FrontGreedyMesh()
        {
            chunkWidth = Chunk.CHUNK_WIDTH,
            chunkLength = Chunk.CHUNK_LENGTH,
            chunkHeight = Chunk.CHUNK_HEIGHT,
            blockSize = Generation.BLOCK_SIZE,

            blockArray1D = blockArray1D,
            blocks = AsyncHelper.CreatePersistentNativeArray<byte>(vertical_crossSection_length.Length),

            verticies = AsyncHelper.CreatePersistentNativeList<GreedyVertex>(),
            triangles = AsyncHelper.CreatePersistentNativeList<int>(),

            colors = colorList,

            chunkToFront = chunkToFront,
            blockArray1D_front = blockArray1D_front
        };

        bool chunkToBack = adjacentChunks[(int)Chunk.Direction.BACK] != null;
        NativeArray<byte> blockArray1D_back = chunkToBack
            ? AsyncHelper.CreatePersistentNativeArray(adjacentChunks[(int)Chunk.Direction.BACK].blockArray1D)
            : AsyncHelper.CreatePersistentNativeArray<byte>(0);

        BackGreedyMesh job_Back = new BackGreedyMesh()
        {
            chunkWidth = Chunk.CHUNK_WIDTH,
            chunkLength = Chunk.CHUNK_LENGTH,
            chunkHeight = Chunk.CHUNK_HEIGHT,
            blockSize = Generation.BLOCK_SIZE,

            blockArray1D = blockArray1D,
            blocks = AsyncHelper.CreatePersistentNativeArray<byte>(vertical_crossSection_length.Length),

            verticies = AsyncHelper.CreatePersistentNativeList<GreedyVertex>(),
            triangles = AsyncHelper.CreatePersistentNativeList<int>(),

            colors = colorList,

            chunkToBack = chunkToBack,
            blockArray1D_back = blockArray1D_back
        };

        JobHandle topHandle = job_Top.Schedule();
        JobHandle bottomHandle = job_Bottom.Schedule();
        JobHandle rightHandle = job_Right.Schedule();
        JobHandle leftHandle = job_Left.Schedule();
        JobHandle frontHandle = job_Front.Schedule();
        JobHandle backHandle = job_Back.Schedule();
        
        jobHandles.Add(topHandle);
        jobHandles.Add(bottomHandle);
        jobHandles.Add(rightHandle);
        jobHandles.Add(leftHandle);
        jobHandles.Add(frontHandle);
        jobHandles.Add(backHandle);
        
        JobHandle combinedHandle = JobHandle.CombineDependencies(jobHandles.AsArray());
        
        jobHandles.Dispose();
        
        GreedyMeshReturnValues greedyMeshReturnValues = new GreedyMeshReturnValues()
        {
            handle = combinedHandle,
            topGreedyMesh = job_Top,
            bottomGreedyMesh = job_Bottom,
            rightGreedyMesh = job_Right,
            leftGreedyMesh = job_Left,
            frontGreedyMesh = job_Front,
            backGreedyMesh = job_Back
        };

        return greedyMeshReturnValues;
    }

    [BurstCompile]
    public struct TopGreedyMesh : IJob
    {
        [ReadOnly]
        public int chunkWidth, chunkLength, chunkHeight;

        [ReadOnly]
        public float blockSize;

        [ReadOnly]
        public NativeArray<byte> blockArray1D;

        public NativeArray<byte> blocks;

        public NativeList<GreedyVertex> verticies;

        [WriteOnly]
        public NativeList<int> triangles;

        [ReadOnly]
        public NativeArray<Color32> colors;

        [ReadOnly]
        public bool chunkAbove;

        [ReadOnly]
        public NativeArray<byte> blockArray1D_above;

        public void Execute()
        {
            for (int y = chunkHeight - 1; y >= 0; y--)
            {
                if (y == chunkHeight - 1 && !chunkAbove) { continue; }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int z = 0; z < chunkLength; z++)
                    {
                        if (y < chunkHeight - 1)
                        {
                            if (blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * (y + 1))] > 0)
                            {
                                blocks[x + (chunkWidth * z)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * z)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];
                            }
                        }
                        else if (y == chunkHeight - 1)
                        {
                            if (blockArray1D_above[x + (chunkWidth * z) + (chunkWidth * chunkLength * 0)] > 0)
                            {
                                blocks[x + (chunkLength * z)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * z)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];
                            }
                        }
                    }
                }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int z = 0; z < chunkLength; z++)
                    {
                        byte blockID = blocks[x + (chunkWidth * z)];
                        if (blockID == 0) { continue; }

                        int length = 0;
                        while (z + length < chunkLength && blocks[x + (chunkWidth * (z + length))] == blockID)
                        {
                            length++;
                        }

                        int width = 1; //We already checked the otherc column!
                        bool canExtend = true;
                        while (canExtend && x + width < chunkWidth)
                        {
                            for (int l = 0; l < length; l++)
                            {
                                if (blocks[(x + width) + (chunkWidth * (z + l))] != blockID)
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
                                blocks[(x + i) + (chunkWidth * (z + j))] = 0;
                            }
                        }

                        // GENERATE QUAD
                        int index = verticies.Length;

                        float size = blockSize; // 0.25

                        float xCenter = (x + width / 2f) * size;
                        float zCenter = (z + length / 2f) * size;
                        float yPos = (y * size); //This is the bottom corner y. So we'll have to move it up

                        // half sizes for the quad
                        float halfWidth = (width * size) / 2f;
                        float halfLength = (length * size) / 2f;

                        Color32 color = colors[blockID - 1];
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yPos + size, zCenter - halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yPos + size, zCenter + halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yPos + size, zCenter + halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yPos + size, zCenter - halfLength), color = color });

                        triangles.Add(index + 0);
                        triangles.Add(index + 1);
                        triangles.Add(index + 2);
                        triangles.Add(index + 2);
                        triangles.Add(index + 3);
                        triangles.Add(index + 0);
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct BottomGreedyMesh : IJob
    {
        [ReadOnly]
        public int chunkWidth, chunkLength, chunkHeight;

        [ReadOnly]
        public float blockSize;

        [ReadOnly]
        public NativeArray<byte> blockArray1D;
        public NativeArray<byte> blocks;

        public NativeList<GreedyVertex> verticies;

        [WriteOnly]
        public NativeList<int> triangles;

        [ReadOnly]
        public NativeArray<Color32> colors;

        [ReadOnly]
        public bool chunkBelow;

        [ReadOnly]
        public NativeArray<byte> blockArray1D_below;

        public void Execute()
        {
            for (int y = 1; y < chunkHeight - 1; y++) // We start at one becuase we don't actually need to render the bottom of the world. Who will see it?
            {
                if (y == 0 && !chunkBelow) { continue; }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int z = 0; z < chunkLength; z++)
                    {
                        if (y > 0)
                        {
                            if (blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * (y - 1))] > 0)
                            {
                                blocks[x + (chunkWidth * z)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * z)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];
                            }
                        }
                        else if (y == 0)
                        {
                            blocks[x + (chunkWidth * z)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];

                            if (blockArray1D_below[x + (chunkWidth * z) + (chunkWidth * chunkLength * (chunkHeight - 1))] > 0)
                            {
                                blocks[x + (chunkLength * z)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * z)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];
                            }
                        }
                    }
                }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int z = 0; z < chunkLength; z++)
                    {
                        byte blockID = blocks[x + (chunkWidth * z)];
                        if (blockID == 0) { continue; }

                        int length = 0;
                        while (z + length < chunkLength && blocks[x + (chunkWidth * (z + length))] == blockID)
                        {
                            length++;
                        }

                        int width = 1; //We already checked the otherc column!
                        bool canExtend = true;
                        while (canExtend && x + width < chunkWidth)
                        {
                            for (int l = 0; l < length; l++)
                            {
                                if (blocks[(x + width) + (chunkWidth * (z + l))] != blockID)
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
                                blocks[(x + i) + (chunkWidth * (z + j))] = 0;
                            }
                        }

                        // GENERATE QUAD
                        int index = verticies.Length;

                        float size = blockSize; // 0.25

                        float xCenter = (x + width / 2f) * size;
                        float zCenter = (z + length / 2f) * size;
                        float yPos = (y * size); //This is the bottom corner y. So we dont need to meove it

                        // half sizes for the quad
                        float halfWidth = (width * size) / 2f;
                        float halfLength = (length * size) / 2f;

                        Color32 color = colors[blockID - 1];

                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yPos, zCenter + halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yPos, zCenter - halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yPos, zCenter - halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yPos, zCenter + halfLength), color = color });

                        triangles.Add(index + 0);
                        triangles.Add(index + 1);
                        triangles.Add(index + 2);
                        triangles.Add(index + 2);
                        triangles.Add(index + 3);
                        triangles.Add(index + 0);

                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct RightGreedyMesh : IJob
    {
        [ReadOnly]
        public int chunkWidth, chunkLength, chunkHeight;

        [ReadOnly]
        public float blockSize;

        [ReadOnly]
        public NativeArray<byte> blockArray1D;
        public NativeArray<byte> blocks;

        public NativeList<GreedyVertex> verticies;

        [WriteOnly]
        public NativeList<int> triangles;

        [ReadOnly]
        public NativeArray<Color32> colors;

        [ReadOnly]
        public bool chunkToRight;

        [ReadOnly]
        public NativeArray<byte> blockArray1D_right;

        public void Execute()
        {
            for (int x = chunkWidth - 1; x >= 0; x--)
            {
                if (x == chunkWidth - 1 && !chunkToRight) { continue; }

                for (int z = 0; z < chunkLength; z++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        if (x < chunkWidth - 1)
                        {
                            if (blockArray1D[(x + 1) + (chunkWidth * z) + (chunkWidth * chunkLength * y)] > 0)
                            {
                                blocks[z + (chunkLength * y)] = 0;
                            }
                            else
                            {
                                blocks[z + (chunkLength * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];
                            }
                        }
                        else if (x == chunkWidth - 1)
                        {
                            if (blockArray1D_right[0 + (chunkWidth * z) + (chunkWidth * chunkLength * y)] > 0)
                            {
                                blocks[z + (chunkLength * y)] = 0;
                            }
                            else
                            {
                                blocks[z + (chunkLength * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];
                            }
                        }
                    }
                }

                for (int z = 0; z < chunkLength; z++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        byte blockID = blocks[z + (chunkLength * y)];
                        if (blockID == 0) { continue; }

                        int height = 0;
                        while (y + height < chunkHeight && blocks[z + (chunkLength * (y + height))] == blockID)
                        {
                            height++;
                        }

                        int length = 1; //We already checked the other column!
                        bool canExtend = true;
                        while (canExtend && z + length < chunkLength)
                        {
                            for (int h = 0; h < height; h++)
                            {
                                if (blocks[(z + length) + (chunkLength * (y + h))] != blockID)
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
                                blocks[(z + i) + (chunkLength * (y + j))] = 0;
                            }
                        }
                        // GENERATE QUAD
                        int index = verticies.Length;

                        float size = blockSize; // 0.25

                        float xPos = (x * size);
                        float yCenter = (y + height / 2f) * size;
                        float zCenter = (z + length / 2f) * size;

                        float halfHeight = (height * size) / 2f;
                        float halfLength = (length * size) / 2f;

                        Color32 color = colors[blockID - 1];

                        verticies.Add(new GreedyVertex { position = new Vector3(xPos + size, yCenter - halfHeight, zCenter - halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xPos + size, yCenter + halfHeight, zCenter - halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xPos + size, yCenter + halfHeight, zCenter + halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xPos + size, yCenter - halfHeight, zCenter + halfLength), color = color });

                        triangles.Add(index + 0);
                        triangles.Add(index + 1);
                        triangles.Add(index + 2);
                        triangles.Add(index + 2);
                        triangles.Add(index + 3);
                        triangles.Add(index + 0);

                    }
                }
            }

        }
    }

    [BurstCompile]
    public struct LeftGreedyMesh : IJob
    {
        [ReadOnly]
        public int chunkWidth, chunkLength, chunkHeight;

        [ReadOnly]
        public float blockSize;

        [ReadOnly]
        public NativeArray<byte> blockArray1D;
        public NativeArray<byte> blocks;

        public NativeList<GreedyVertex> verticies;

        [WriteOnly]
        public NativeList<int> triangles;

        [ReadOnly]
        public NativeArray<Color32> colors;

        [ReadOnly]
        public bool chunkToLeft;

        [ReadOnly]
        public NativeArray<byte> blockArray1D_left;

        public void Execute()
        {
            for (int x = 0; x < chunkWidth; x++)
            {
                if (x == 0 && !chunkToLeft) { continue; }

                for (int z = 0; z < chunkLength; z++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        if (x > 0)
                        {
                            if (blockArray1D[(x - 1) + (chunkWidth * z) + (chunkWidth * chunkLength * y)] > 0)

                            {
                                blocks[z + (chunkLength * y)] = 0;
                            }
                            else
                            {
                                blocks[z + (chunkLength * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];
                            }
                        }
                        else if (x == 0)
                        {
                            if (blockArray1D_left[(chunkWidth - 1) + (chunkWidth * z) + (chunkWidth * chunkLength * y)] > 0)
                            {
                                blocks[z + (chunkLength * y)] = 0;
                            }
                            else
                            {
                                blocks[z + (chunkLength * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];

                            }
                        }
                    }
                }

                for (int z = 0; z < chunkLength; z++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        byte blockID = blocks[z + (chunkLength * y)];
                        if (blockID == 0) { continue; }

                        int height = 0;
                        while (y + height < chunkHeight && blocks[z + (chunkLength * (y + height))] == blockID)
                        {
                            height++;
                        }

                        int length = 1; //We already checked the otherc column!
                        bool canExtend = true;
                        while (canExtend && z + length < chunkLength)
                        {
                            for (int h = 0; h < height; h++)
                            {
                                if (blocks[(z + length) + (chunkLength * (y + h))] != blockID)
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
                                blocks[(z + i) + (chunkLength * (y + j))] = 0;
                            }
                        }
                        // GENERATE QUAD
                        int index = verticies.Length;

                        float size = blockSize; // 0.25

                        float xPos = (x * size);
                        float yCenter = (y + height / 2f) * size;
                        float zCenter = (z + length / 2f) * size;

                        float halfHeight = (height * size) / 2f;
                        float halfLength = (length * size) / 2f;

                        Color32 color = colors[blockID - 1];

                        verticies.Add(new GreedyVertex { position = new Vector3(xPos, yCenter - halfHeight, zCenter + halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xPos, yCenter + halfHeight, zCenter + halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xPos, yCenter + halfHeight, zCenter - halfLength), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xPos, yCenter - halfHeight, zCenter - halfLength), color = color });

                        triangles.Add(index + 0);
                        triangles.Add(index + 1);
                        triangles.Add(index + 2);
                        triangles.Add(index + 2);
                        triangles.Add(index + 3);
                        triangles.Add(index + 0);
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct FrontGreedyMesh : IJob
    {
        [ReadOnly]
        public int chunkWidth, chunkLength, chunkHeight;

        [ReadOnly]
        public float blockSize;

        [ReadOnly]
        public NativeArray<byte> blockArray1D;
        public NativeArray<byte> blocks;

        public NativeList<GreedyVertex> verticies;

        [WriteOnly]
        public NativeList<int> triangles;

        [ReadOnly]
        public NativeArray<Color32> colors;

        [ReadOnly]
        public bool chunkToFront;

        [ReadOnly]
        public NativeArray<byte> blockArray1D_front;

        public void Execute()
        {
            for (int z = chunkLength - 1; z >= 0; z--)
            {
                if (z == chunkLength - 1 && !chunkToFront) { continue; }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        if (z < chunkLength - 1)
                        {
                            if (blockArray1D[x + (chunkWidth * (z + 1)) + (chunkWidth * chunkLength * y)] > 0)

                            {
                                blocks[x + (chunkWidth * y)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];

                            }
                        }
                        else if (z == chunkLength - 1)
                        {
                            if (blockArray1D_front[x + (chunkWidth * 0) + (chunkWidth * chunkLength * y)] > 0)

                            {
                                blocks[x + (chunkWidth * y)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];

                            }
                        }
                    }
                }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        byte blockID = blocks[x + (chunkWidth * y)];
                        if (blockID == 0) { continue; }

                        int height = 0;
                        while (y + height < chunkHeight && blocks[x + (chunkWidth * (y + height))] == blockID)
                        {
                            height++;
                        }

                        int width = 1; //We already checked the otherc column!
                        bool canExtend = true;
                        while (canExtend && x + width < chunkWidth)
                        {
                            for (int h = 0; h < height; h++)
                            {
                                if (blocks[(x + width) + (chunkWidth * (y + h))] != blockID)
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
                                blocks[(x + i) + (chunkWidth * (y + j))] = 0;
                            }
                        }

                        // GENERATE QUAD
                        int index = verticies.Length;

                        float size = blockSize; // 0.25

                        float xCenter = (x + width / 2f) * size;
                        float zPos = (z * size);
                        float yCenter = (y + height / 2f) * size;

                        // half sizes for the quad
                        float halfWidth = (width * size) / 2f;
                        float halfHeight = (height * size) / 2f;

                        Color32 color = colors[blockID - 1];

                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yCenter - halfHeight, zPos + size), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yCenter + halfHeight, zPos + size), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yCenter + halfHeight, zPos + size), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yCenter - halfHeight, zPos + size), color = color });

                        triangles.Add(index + 0);
                        triangles.Add(index + 1);
                        triangles.Add(index + 2);
                        triangles.Add(index + 2);
                        triangles.Add(index + 3);
                        triangles.Add(index + 0);
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct BackGreedyMesh : IJob
    {
        [ReadOnly]
        public int chunkWidth, chunkLength, chunkHeight;

        [ReadOnly]
        public float blockSize;

        [ReadOnly]
        public NativeArray<byte> blockArray1D;
        public NativeArray<byte> blocks;

        public NativeList<GreedyVertex> verticies;

        [WriteOnly]
        public NativeList<int> triangles;

        [ReadOnly]
        public NativeArray<Color32> colors;

        [ReadOnly]
        public bool chunkToBack;

        [ReadOnly]
        public NativeArray<byte> blockArray1D_back;

        public void Execute()
        {
            for (int z = 0; z < chunkLength; z++)
            {
                if (z == 0 && !chunkToBack) { continue; }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        if (z > 0)
                        {
                            if (blockArray1D[x + (chunkWidth * (z - 1)) + (chunkWidth * chunkLength * y)] > 0)

                            {
                                blocks[x + (chunkWidth * y)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];

                            }
                        }
                        else if (z == 0)
                        {
                            if (blockArray1D_back[x + (chunkWidth * (chunkLength - 1)) + (chunkWidth * chunkLength * y)] > 0)
                            {
                                blocks[x + (chunkWidth * y)] = 0;
                            }
                            else
                            {
                                blocks[x + (chunkWidth * y)] = blockArray1D[x + (chunkWidth * z) + (chunkWidth * chunkLength * y)];

                            }
                        }
                    }
                }

                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int y = 0; y < chunkHeight; y++)
                    {
                        byte blockID = blocks[x + (chunkWidth * y)];
                        if (blockID == 0) { continue; }

                        int height = 0;
                        while (y + height < chunkHeight && blocks[x + (chunkWidth * (y + height))] == blockID)
                        {
                            height++;
                        }

                        int width = 1; //We already checked the otherc column!
                        bool canExtend = true;
                        while (canExtend && x + width < chunkWidth)
                        {
                            for (int h = 0; h < height; h++)
                            {
                                if (blocks[(x + width) + (chunkWidth * (y + h))] != blockID)
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
                                blocks[(x + i) + (chunkWidth * (y + j))] = 0;
                            }
                        }

                        // GENERATE QUAD
                        int index = verticies.Length;

                        float size = blockSize; // 0.25

                        float xCenter = (x + width / 2f) * size;
                        float zPos = (z * size); // This is the actual z corner of the front. We dont need to readjust!
                        float yCenter = (y + height / 2f) * size;

                        // half sizes for the quad
                        float halfWidth = (width * size) / 2f;
                        float halfHeight = (height * size) / 2f;

                        Color32 color = colors[blockID - 1];

                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yCenter - halfHeight, zPos), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter - halfWidth, yCenter + halfHeight, zPos), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yCenter + halfHeight, zPos), color = color });
                        verticies.Add(new GreedyVertex { position = new Vector3(xCenter + halfWidth, yCenter - halfHeight, zPos), color = color });

                        triangles.Add(index + 0);
                        triangles.Add(index + 1);
                        triangles.Add(index + 2);
                        triangles.Add(index + 2);
                        triangles.Add(index + 3);
                        triangles.Add(index + 0);
                    }
                }
            }
        }
    }
}