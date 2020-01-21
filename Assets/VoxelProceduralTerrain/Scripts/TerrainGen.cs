using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class TerrainGen : MonoBehaviour
{
    public GameObject chunkPrefab;

    void setWorldHeightMapAsTexture()
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        Texture2D t = new Texture2D(4096, 4096);

        HeightMap[,] heightMaps = new HeightMap[4096 / CHUNK_SIZE, 4096 / CHUNK_SIZE];
        Profiler.BeginSample("Generate heightmaps");
        for (int hx = 0; hx < heightMaps.GetLength(0); hx++)
        {
            for (int hz = 0; hz < heightMaps.GetLength(1); hz++)
            {
                heightMaps[hx, hz] = World.getOrCreateHeightMap(hx - heightMaps.GetLength(0) / 2, hz - heightMaps.GetLength(0) / 2);
                heightMaps[hx, hz].generate(Constants.WORLD_SEED);
            }
        }
        Profiler.EndSample();

        Profiler.BeginSample("Generate texture");
        Color[] c = new Color[4096 * 4096];
        int i = 0;
        for (int y = 0; y < 4096; y++)
        {
            for (int x = 0; x < 4096; x++, i++)
            {
                float v = heightMaps[x / CHUNK_SIZE, y / CHUNK_SIZE].values[x % CHUNK_SIZE, y % CHUNK_SIZE] / Constants.TERRAIN_MAX_HEIGHT;
                c[i] = new Color(v, v, v);
            }
        }
        Profiler.EndSample();
        Profiler.BeginSample("upload data");
        t.SetPixels(c);
        t.Apply();
        Profiler.EndSample();
        GetComponent<Renderer>().material.SetTexture("_BaseMap", t);

    }

    void setHeightMapAsTexture(int chunkX, int chunkZ)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        HeightMap h = World.getOrCreateHeightMap(chunkX, chunkZ);
        h.generate(0);
        Texture2D t = new Texture2D(CHUNK_SIZE, CHUNK_SIZE);
        Color[] c = new Color[CHUNK_SIZE * CHUNK_SIZE];
        int i = 0;
        for (int y = 0; y < CHUNK_SIZE; y++)
        {
            for (int x = 0; x < CHUNK_SIZE; x++, i++)
            {
                float v = h.values[x, y] / Constants.TERRAIN_MAX_HEIGHT;
                c[i] = new Color(v, v, v);
            }
        }
        t.SetPixels(c);
        t.Apply();

        GetComponent<Renderer>().material.SetTexture("_BaseMap", t);
    }

    void Start()
    {
        //setWorldHeightMapAsTexture();
        //return;

        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        float scale = Constants.BLOCK_SIZE;

        int poscz = 20;

        int area = 10;
        HeightMap[,] heightMaps = new HeightMap[area*2+1, area*2+1];
        for (int y = 0; y < 19; y++)
        {
            for (int x = -area; x <= area; x++)
            {
                for (int z = -area; z <= area; z++)
                {
                    if (y == 0)
                    {
                        Profiler.BeginSample("Generate heightmaps");
                        heightMaps[x + area, z + area] = World.getOrCreateHeightMap(x, z + poscz);
                        heightMaps[x + area, z + area].generate(Constants.WORLD_SEED);
                        Profiler.EndSample();
                    }
                    ChunkData c = new ChunkData(x, y, z);
                    Profiler.BeginSample("Generate block data");
                    c.generate(heightMaps[x + area, z + area]);
                    Profiler.EndSample();
                    if (!c.isAir())
                    {
                        Profiler.BeginSample("Generate mesh");
                        c.generateMesh();
                        Profiler.EndSample();
                        GameObject go = Instantiate(chunkPrefab);
                        go.GetComponent<MeshFilter>().mesh = c.getMesh();
                        go.transform.localScale = new Vector3(scale, scale, scale);
                        go.transform.localPosition = new Vector3(x * (float)CHUNK_SIZE * scale, y * (float)CHUNK_SIZE * scale, z * (float)CHUNK_SIZE * scale);
                    }
                }
            }
        }
    }

    void Update()
    {

    }
}
