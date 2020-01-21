using System.Collections;
public class World
{
    //public static int seed;
    private static Hashtable heightMaps = new Hashtable();

    // Combines 2 integers into 1 long
    private static ulong f(int x, int z)
    {
        return ((ulong)((uint)x) << 32) | (ulong)((uint)z);
    }

    public static void addHeightMap(HeightMap h, int x, int z)
    {
        heightMaps.Add(f(x,z), h);
    }
    public static HeightMap getHeightMap(int x, int z)
    {
        return (HeightMap)heightMaps[f(x, z)];
    }

    public static HeightMap getOrCreateHeightMap(int x, int z)
    {
        HeightMap h = getHeightMap(x, z);
        if(h == null)
        {
            h = new HeightMap(x, z);
            heightMaps.Add(f(x, z), h);
        }
        return h;
    }

}
