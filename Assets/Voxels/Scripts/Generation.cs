using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class Generation : MonoBehaviour
{
    public static Generation instance;
    [SerializeField] string inputSeed;
    [HideInInspector] public long seed;
    

    public static readonly float BLOCK_SIZE = 0.25f;
    public BlockList blockList;
    public static Color32[] colorList;

    [SerializeField] AnimationCurve contenentalnessToHeight;
    [HideInInspector] public Spline contenentalnessToHeight_spline;

    public Material terrainMat;
    public Block mainBlock, underwaterBlock, stoneBlock, dirtBlock;

    static Noise contentalness_1, contentalness_2, contentalness_3;

    private void Awake()
    {
        instance = this;
        contenentalnessToHeight_spline = new Spline(contenentalnessToHeight);

        colorList = new Color32[blockList.blocks.Count];
        int i = 0;  
        foreach(Block block in blockList.blocks)
        {
            colorList[i] = block.vertexColor;
            i++;
        }

    }
    private void Start()
    {
        GenerateSeed();

        contentalness_1 = new Noise(16, 16, 0.075f, Noise.NoiseType.PERLIN);
        contentalness_2 = new Noise(16, 16, 0.1f, Noise.NoiseType.SIMPLEX);
        contentalness_3 = new Noise(16, 16, 1f, Noise.NoiseType.SIMPLEX);

        StartCoroutine("InititalizeChunks");
    }

    private IEnumerator InititalizeChunks()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                Chunk chunk = new Chunk(new Vector3(x, 0, z));
                yield return null;
            }
        }
    }


    public void GenerateSeed()
    {
        if(string.IsNullOrWhiteSpace(inputSeed))
        {
            System.Random random = new System.Random();
            seed = random.Next();
            inputSeed = seed.ToString();
        } 
        else 
        {   
            int intSeed;
            if(int.TryParse(inputSeed, out intSeed))
            {
                seed = intSeed;
            }
            else
            {
                seed = inputSeed.GetHashCode();
            }
        }
    }

    public static float GetContenentalness(float xCoord, float zCoord)
    {
        float low = contentalness_1.GetNoise(xCoord, zCoord, 10); // [0, 10]
        float mid = contentalness_2.GetNoise(xCoord, zCoord, 8); // less impact
        float high = contentalness_3.GetNoise(xCoord,zCoord, 2); // subtle variation

        float combined = (low * 0.75f) + (mid * 0.5f) + (high * 0.15f);


        return combined;
    }

}
