using UnityEngine;

[CreateAssetMenu(menuName = "VoxelStuff/Block")]
public class Block : ScriptableObject
{
    public byte block_ID;
    public Color vertexColor;
}
