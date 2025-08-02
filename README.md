# UnityVoxelEngine
<img width="864" height="542" alt="Image" src="https://github.com/user-attachments/assets/8a1b4361-768d-4e04-86d1-e75ff227bcd2" />

## 👏 Special Thanks
Threading made possible because of [Logyrac](https://github.com/Logyrac)

## 💻 Project Uses:
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
 5. Create an empty Game Object. You can call it anything, but something like *AsyncHelper* can help with organization
 6. Add the `AsyncHelper` script to the empty object (*Assets > Voxels > Scripts > Dispactcher*)

## 📋 Setting up a Block List
In the `Blocks` folder, right click, *Create > VoxelStuff > BlockList* you can name it whatever you like (Recomended: *BlockList*)\
Now you can add block types to the `Blocks` field in the block list!

### 📋 Creating different Block types
In the `Blocks` folder, right click, *Create > VoxelStuff > Block* you can name it whatever you like (Recomended: *[BlockName]*)\
As of right now, there is only one field: `Vertex Color`. This is the color the voxel will apear in the world
  * Create a `GrassBlock`, `SandBlock`, `StoneBlock`, and `DirtBlock`. Make sure to place these in the coresponding fields in the Generation inspector

## 📈 Setting up a Contenentalness to Height spline (basis for terrain generation)
**Fun fact:** this method of generating terrain is inspired by the *Minecraft* way of generating terrain!\
This entire project was inspired by the following talk by *Minecraft* developer Henrik Kniberg\
[![Reinventing Minecraft world generation by Henrik Kniberg](https://img.youtube.com/vi/ob3VwY4JyzE/0.jpg)](https://www.youtube.com/watch?v=ob3VwY4JyzE&list=LL&index=4&t=1384s)

In your *GenerationManager* (or the object in which you placed the `Generation` script), you can manipulate the `Contentalness To Height` spline to represent how terrain height will respond to "contentalness" (the perlin noise values)
  * The spline have x-values going from 0-10, and y-values going from 0-100
  * Imagine the x-value of 0 as the bottom of the ocean
  * y = 20 is coastline

Below is a good example for a Contentalness to Height spline:\
<img width="317" height="584" alt="Image" src="https://github.com/user-attachments/assets/b0e81106-6bb7-412e-b97f-dab4c46b005d" />\
<br />
