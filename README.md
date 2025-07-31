# UnityVoxelEngine
This is my first voxel implementation in Unity. It is still a work in progress. 
<img width="864" height="542" alt="Image" src="https://github.com/user-attachments/assets/8a1b4361-768d-4e04-86d1-e75ff227bcd2" />
![Image](https://github.com/user-attachments/assets/411e4ef0-1591-454a-ae65-a3f2546e4f3a)

## 👏 Special Thanks
Threading made possible because of [Logyrac](https://github.com/Logyrac)

### 💻 Project Uses:
  * 📈 **Kickstart any voxel project for Unity**
  * 📖 **Learn how voxel-based games work**
  * 📝 **Contribute *your* voxel knowledge**

## 🤔 Setting it up
There a couple of things you need to set up before a voxel world is created:
 1. Create an empty Game Object. You can call it anything, but something like *GenerationManager* can help with organization
 2. Add the `Generation` script to the empty object
 3. Set up the `Block List`, `Contentalness To Height` spline, `Terrain Material`, specify whether or not you want to `Use Greedy Meshing` (it is recommended), and then add the `Main Block`,  `Underwater Block`, `Stone Block `, and `Dirt Block
    * The terrain material, `TerrainMat` is in the `Shaders` folder
 4. **(Optional)** Input whatever you want for your seed in the `Input Seed` field! This can be a int or a string. A float will be converted to a string.
    * If left blank, a random seed will be generated

## 📋 Setting up a Block List
In the `Blocks` folder, right click, *Create > VoxelStuff > BlockList* you can name it whatever you like (Recomended: *BlockList*)\
Now you can add block types to the `Blocks` field in the block list!

### 📋 Creating different Block types
In the `Blocks` folder, right click, *Create > VoxelStuff > Block* you can name it whatever you like (Recomended: *[BlockName]*)\
As of right now, there is only one field: `Vertex Color`. This is the color the voxel will apear in the world
  * Create a `GrassBlock`, `SandBlock`, `StoneBlock`, and `DirtBlock`. Make sure to place these in the coresponding fields in the Generation inspector

## 📈 Setting up a Contenentalness to Height spline
  * **NOTE:** as of version 1.0.3, the `Splines` folder was removed, and you do not need to create a spline scriptable object
<br />

In your *GenerationManager* (or the object in which you placed the `Generation` script), you can manipulate the `Contentalness To Height` spline to represent how terrain height will respond to "contentalness" (the perlin noise values)
  * The spline have x-values going from 0-10, and y-values going from 0-100
  * Imagine the x-value of 0 as the bottom of the ocean
  * y = 20 is coastline

Below is a good example for a Contentalness to Height spline:\
<img width="317" height="584" alt="Image" src="https://github.com/user-attachments/assets/b0e81106-6bb7-412e-b97f-dab4c46b005d" />

