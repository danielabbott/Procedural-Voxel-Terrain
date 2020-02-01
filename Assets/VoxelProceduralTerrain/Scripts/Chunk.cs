using UnityEngine;
using System.Collections;
using System;

public class Chunk
{

    // -1048575 to +1048575 (chunks)
    public int cx;
    public int cy;
    public int cz;

    public ChunkData data = null;
    public Mesh mesh = null;
    public GameObject gameObject = null;

    // Set to true when chunk goes out of render distance and gameObject and data are set to null
    // Set back to false if the chunk is regenerated
    public bool destroyed = false;

    public Chunk(int x_, int y_, int z_)
    {
        cx = x_;
        cy = y_;
        cz = z_;
    }

    public int distance(int x_, int y_, int z_)
    {
        int x = Math.Abs(x_ - cx);
        int y = Math.Abs(y_ - cy);
        int z = Math.Abs(z_ - cz);
        return (int)Math.Sqrt(x * x + y * y + z * z);
    }

    // Hash key used as key in hash table

    public static ulong getHashKey1(int cx, int cy, int cz)
    {
        return (((ulong)((uint)(cx & 0x1FFFFF))) << 42) | ((ulong)((uint)(cy & 0x1FFFFF)) << 21) | (ulong)((uint)(cz & 0x1FFFFF));

    }

    public ulong getHashKey()
    {
        return getHashKey1(cx, cy, cz);
    }
}
