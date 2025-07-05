# UnityVoxelEngine
This is my first voxel implementation in Unity. It is still a work in progress. 
![alt text](https://www.reddit.com/media?url=https%3A%2F%2Fpreview.redd.it%2Fgreedy-meshing-update-it-works-unity-v0-iygy5hi37kaf1.png%3Fwidth%3D640%26crop%3Dsmart%26auto%3Dwebp%26s%3Df99d40a471c0629d508bf21c86a306be5aa2f473)

## Setting it up
There a couple of things you need to set up before a voxel world is created
 1. Create an empty Game Object. You can call it anything, but something like *GenerationManager* can help with organization
 2. Add the Generation script to the empty object
 3. Add a `Block List`, `Contentalness To Height` spline, `Terrain Material`, specify whether or not you want to `Use Greedy Meshing` (it is recommended), and then add the `Main Block`,  `Underwater Block`, `Stone Block `, and `Dirt Block
    * The terrain material, `TerrainMat` is in the `Shaders` folder

## Setting up a Block List
In the `Blocks` folder, right click, *Create > VoxelStuff > BlockList* you can name it whatever you like (Recomended: *BlockList*)\
Now you can add block types to the `Blocks` field in the block list!

### Creating different Block types
In the `Blocks` folder, right click, *Create > VoxelStuff > Block* you can name it whatever you like (Recomended: *[BlockName]*)\
As of right now, there is only one field: `Vertex Color`. This is the color the voxel will apear in the world
  * Create a `GrassBlock`, `SandBlock`, `StoneBlock`, and `DirtBlock`. Make sure to place these in the coresponding fields in the Generation inspector

## Setting up a Contenentalness to Height spline
In the `Splines` folder, right click, *Create > VoxelStuff > Spline* you can name it whatever you like (Recomended: *ContenentalnessToHeight*)\
In the Spline field, you can manipulate the spline to represent how terrain height will respond to "contentalness" (the perlin noise values)
  * The spline have x-values going from 0-10, and y-values going from 0-100
  * Imagine the x-value of 0 as the bottom of the ocean
  * y = 20 is coastline

