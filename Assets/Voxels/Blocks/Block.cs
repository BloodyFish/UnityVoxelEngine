using UnityEngine;

[CreateAssetMenu(menuName = "VoxelStuff/Block")]
public class Block : ScriptableObject
{
    [HideInInspector] public byte block_ID;
    public Color vertexColor;
}
