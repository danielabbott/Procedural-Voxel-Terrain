using UnityEngine;
using System.Runtime.InteropServices;

public class Noise
{

    [DllImport("FastNoiseSIMD")]
    private static extern ulong cppNewNoise(int seed);

    [DllImport("FastNoiseSIMD")]
    private static extern void generateNoiseCPP(ulong noiseObj, [MarshalAs(UnmanagedType.LPArray)] float[,] noiseData, int x, int z, int szX, int szZ, float scaleX, float scaleY, float frequency, int octaves, float lacunarity, float gain);

    private ulong cppObj;

    public Noise(int seed)
    {
        cppObj = cppNewNoise(seed);
    }

    // x,z are block coordinates
    // szX,szZ are chunk size
    // Values are in range -0.707 - 0.707 (-.5*sqrt(2) to +.5*sqrt(2))
    // Octaves: number of noise iterations
    // Lacunarity: Frequency multipler between octaves (each successive has more detail (higher frequency))
    // Gain: Amplitude multiple between octaves (each successive octave has less impact on the final value)
    public void generate(float[,] noiseData, int x, int z, int szX, int szZ, float scaleX, float scaleY, float frequency=0.02f, int octaves=3, float lacunarity=2.0f, float gain=0.5f)
    {
        generateNoiseCPP(cppObj, noiseData, x, z, szX, szZ, scaleX, scaleY, frequency, octaves, lacunarity, gain);
    }
}
