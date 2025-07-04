using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "VoxelStuff/BlockList")]
public class BlockList : ScriptableObject
{
    public List<Block> blocks;

    private void OnEnable()
    {
        foreach (var block in blocks)
        {
            block.block_ID = (byte)blocks.IndexOf(block);
        }
    }
}
