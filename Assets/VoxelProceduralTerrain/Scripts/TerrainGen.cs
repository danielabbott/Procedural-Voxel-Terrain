using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class TerrainGen : MonoBehaviour
{
    public GameObject chunkPrefab;

    private static Vector3Int prevPlayerChunkPos;
    private static Vector3Int playerChunkPosition;

    private static ChunkLoaderThread chunkLoaderThread = new ChunkLoaderThread();

    private static Dictionary<ulong, Chunk> allChunks = new Dictionary<ulong, Chunk>();

    // Only used by rendering thread
    private static Queue<Chunk> chunksAwaitingObjectCreation = new Queue<Chunk>();

    //private static bool[,,] nearbyChunksLoaded = new bool[9, 5, 9];

    private static int chunkCoordsToLength(int x, int y, int z)
    {
        return (int)Math.Sqrt(x * x + y * y + z * z);
    }

    private static int getChunkDistanceFromPlayer(int cx, int cy, int cz)
    {
        int x = Math.Abs(playerChunkPosition.x - cx);
        int y = Math.Abs(playerChunkPosition.y - cy);
        int z = Math.Abs(playerChunkPosition.z - cz);
        return chunkCoordsToLength(x, y, z);
    }

    // Producer: Chunk loader thread, Consumer: Render thread
    private static Queue<ChunkCreateMsg> chunksAwaitingMeshUpload = new Queue<ChunkCreateMsg>();
    private static List<ChunkLoaderThread.ChunkRequest> ignoredRequests = new List<ChunkLoaderThread.ChunkRequest>();

    public struct ChunkCreateMsg
    {
        public int cx, cy, cz;
        public ChunkData chunkData;

        // Used to tell the chunk loader thread that the mesh object can be reused
        public int meshI;

        public ChunkCreateMsg(int x, int y, int z, ChunkData c, int meshI_)
        {
            cx = x;
            cy = y;
            cz = z;
            chunkData = c;
            meshI = meshI_;
        }
    }


    // Called by chunk loader thread
    public static void chunkLoaded(ChunkCreateMsg m)
    {
        lock (chunksAwaitingMeshUpload)
        {
            chunksAwaitingMeshUpload.Enqueue(m);
        }
    }

    // Called by chunk loader thread
    public static void chunksLoaded(Queue<ChunkCreateMsg> q)
    {
        lock (chunksAwaitingMeshUpload)
        {
            while (q.Count != 0)
            {
                chunksAwaitingMeshUpload.Enqueue(q.Dequeue());
            }
        }
    }

    public static void IgnoredChunkRequests(List<ChunkLoaderThread.ChunkRequest> ignoredRequests_)
    {
        lock (ignoredRequests)
        {
            ignoredRequests.AddRange(ignoredRequests_);
        }
    }

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

    // Gets the position of the chunk the player is in
    private Vector3Int getPlayerChunkPos()
    {
        Vector3 playerPosition = Camera.main.transform.position;

        int cx = (int)(playerPosition.x / Constants.CHUNK_SIZE);
        if (cx < 0) cx--;
        int cy = (int)(playerPosition.y / Constants.CHUNK_SIZE);
        if (cy < 0) cy--;
        int cz = (int)(playerPosition.z / Constants.CHUNK_SIZE);
        if (cz < 0) cz--;

        return new Vector3Int(cx, cy, cz);
    }

    void Start()
    {
        World.createNoiseObjects(Constants.WORLD_SEED);
        ChunkData.createMeshDataLists();

        //setWorldHeightMapAsTexture();
        //return;

        prevPlayerChunkPos = getPlayerChunkPos();
        playerChunkPosition = prevPlayerChunkPos;
        prevPlayerChunkPos.y += Constants.CHUNK_SIZE;

        chunkLoaderThread.start();


    }

    // Create game objects
    private void createChunkGameObjects()
    {
        foreach (Chunk c in chunksAwaitingObjectCreation)
        {
            GameObject go = Instantiate(chunkPrefab);
            go.GetComponent<MeshFilter>().mesh = c.mesh;
            float scale = Constants.BLOCK_SIZE;
            int CHUNK_SIZE = Constants.CHUNK_SIZE;
            go.transform.localScale = new Vector3(scale, scale, scale);
            go.transform.localPosition = new Vector3(c.cx * (float)CHUNK_SIZE * scale, c.cy * (float)CHUNK_SIZE * scale, c.cz * (float)CHUNK_SIZE * scale);
            c.gameObject = go;
        }
        chunksAwaitingObjectCreation.Clear();
    }

    // Create mesh objects
    private void createChunkMeshObjects()
    {
        // Copy queue
        Queue<ChunkCreateMsg> toCreate = new Queue<ChunkCreateMsg>();
        lock (chunksAwaitingMeshUpload)
        {
            while (chunksAwaitingMeshUpload.Count > 0) toCreate.Enqueue(chunksAwaitingMeshUpload.Dequeue());
        }

        foreach (ChunkCreateMsg m in toCreate)
        {
            Chunk c = (Chunk)allChunks[Chunk.getHashKey1(m.cx, m.cy, m.cz)];
            c.data = m.chunkData;
            if (m.meshI >= 0)
            {
                // Create unity mesh object
                c.mesh = c.data.createMesh(m.meshI);
            }
            chunksAwaitingObjectCreation.Enqueue(c);
        }
    }

    // Hide chunks which are far away
    private void setChunkGameObjectsVisibility()
    {
        Vector3Int p = getPlayerChunkPos();
        foreach (KeyValuePair<ulong, Chunk> pair in allChunks)
        {
            Chunk c = (Chunk)pair.Value;
            if (c.gameObject != null)
            {
                if (c.distance(p.x, p.y, p.z) <= Constants.RENDER_DISTANCE)
                {
                    // Within render distance
                    c.gameObject.SetActive(true);
                }
                else
                {
                    // Outside render distance

                    if (c.distance(p.x, p.y, p.z) > Constants.RENDER_DISTANCE + 2)
                    {
                        // Well outside render distance, free resources
                        Destroy(c.gameObject);
                        c.data = null;
                        c.mesh = null;
                        c.destroyed = true;
                    }
                    else
                    {
                        // Not far out of render distance
                        // Make invisible but keep mesh and block data
                        c.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    // If the chunk loader thread didn't load a chunk, this thread needs to set the
    // destroyed flag in the chunk object so it knows to send a new request if the
    // chunk is needed again
    private void updateIgnoredChunks()
    {
        List<ChunkLoaderThread.ChunkRequest> copy = new List<ChunkLoaderThread.ChunkRequest>();

        lock (ignoredRequests)
        {
            copy.AddRange(ignoredRequests);
            ignoredRequests.Clear();
        }

        foreach (var r in copy)
        {
            if (allChunks.TryGetValue(Chunk.getHashKey1(r.cx, r.cy, r.cz), out Chunk c))
            {
                c.destroyed = true;
            }
        }
    }

    // Find chunks within render distance which are not yet being loaded
    // and tell the chunk loader thread to load them
    private void loadNewChunks()
    {
        Queue<ChunkLoaderThread.ChunkRequest> toQueue = new Queue<ChunkLoaderThread.ChunkRequest>();

        Vector3Int p = getPlayerChunkPos();
        playerChunkPosition = p;

        if (!p.Equals(prevPlayerChunkPos))
        {
            for (int y_ = 0; y_ < Constants.RENDER_DISTANCE; y_++)
            {
                // Alternate back and forth
                //So y= 0,1,-1,2,-2,3,-3, etc.
                int y = -((y_ % 2) * 2 - 1) * ((y_ + 1) / 2);

                // Load each layer of chunks in rings from player position outwards
                for (int i = 0; i < Constants.RENDER_DISTANCE; i++)
                {
                    for (int x = -i; x <= i; x++)
                    {
                        for (int z = -i; z <= i; z++)
                        {
                            if (x == -i || x == i || z == -i || z == i)
                            {
                                if (chunkCoordsToLength(x, y_, z) <= Constants.RENDER_DISTANCE)
                                {
                                    ulong key = Chunk.getHashKey1(p.x + x, p.y + y, p.z + z);
                                    if (!allChunks.ContainsKey(key))
                                    {
                                        // Chunk has not been loaded before, create new chunk object
                                        Chunk c = new Chunk(p.x + x, p.y + y, p.z + z);
                                        allChunks.Add(c.getHashKey(), c);
                                        toQueue.Enqueue(new ChunkLoaderThread.ChunkRequest(p.x + x, p.y + y, p.z + z));
                                    }
                                    else
                                    {
                                        // Chunk has been loaded before
                                        Chunk c = (Chunk)allChunks[key];
                                        if (c.destroyed)
                                        {
                                            // Chunk was loaded but went out of render distance and was freed
                                            c.destroyed = false;
                                            toQueue.Enqueue(new ChunkLoaderThread.ChunkRequest(p.x + x, p.y + y, p.z + z));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        chunkLoaderThread.queueChunks(toQueue, p);
        prevPlayerChunkPos = p;
    }

    void Update()
    {
        createChunkGameObjects();
        createChunkMeshObjects();
        setChunkGameObjectsVisibility();
        updateIgnoredChunks();
        loadNewChunks();
    }

    void OnApplicationQuit()
    {
        chunkLoaderThread.stop();
    }
}
