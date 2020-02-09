using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityTemplateProjects;
using UnityEngine.Rendering;

public class TerrainGen : MonoBehaviour
{
    public GameObject chunkPrefab;
    public GameObject chunkFluidPrefab;

    private static Vector3Int prevPlayerChunkPos;
    private static Vector3Int playerChunkPosition;

    private static ChunkLoaderThread chunkLoaderThread = new ChunkLoaderThread();

    private static Dictionary<ulong, Chunk> allChunks = new Dictionary<ulong, Chunk>();

    // Only used by rendering thread
    private static Queue<Chunk> chunksAwaitingObjectCreation = new Queue<Chunk>();

    //private static bool[,,] nearbyChunksLoaded = new bool[9, 5, 9];

    private SimpleCameraController cameraControl;

    private UnityEngine.UI.RawImage fluidOverlay;

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

        // Used to tell the chunk loader thread that the mesh object(s) can be reused
        public Vector2Int meshI;

        public ChunkCreateMsg(int x, int y, int z, ChunkData c, Vector2Int meshI_)
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

    void setWorldHeightMapAsTexture(int chunkX, int chunkZ)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        Texture2D t = new Texture2D(4096, 4096);

        HeightMap[,] heightMaps = new HeightMap[4096 / CHUNK_SIZE, 4096 / CHUNK_SIZE];
        Profiler.BeginSample("Generate heightmaps");
        for (int hx = 0; hx < heightMaps.GetLength(0); hx++)
        {
            for (int hz = 0; hz < heightMaps.GetLength(1); hz++)
            {
                heightMaps[hx, hz] = World.getOrCreateHeightMap(hx - heightMaps.GetLength(0) / 2 + chunkX, hz - heightMaps.GetLength(0) / 2 + chunkZ);
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
                float v = (heightMaps[x / CHUNK_SIZE, y / CHUNK_SIZE].values[x % CHUNK_SIZE, y % CHUNK_SIZE] & 0x7fff) / Constants.TERRAIN_MAX_HEIGHT;
                c[i] = new Color(v, v, v);
            }
        }
        Profiler.EndSample();
        Profiler.BeginSample("upload data");
        t.SetPixels(c);
        t.filterMode = FilterMode.Point;
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
                float v = (h.values[x, y] & 0x7fff) / Constants.TERRAIN_MAX_HEIGHT;
                c[i] = new Color(v, v, v);
            }
        }
        t.SetPixels(c);
        t.filterMode = FilterMode.Point;
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
        fluidOverlay = GameObject.Find("Canvas").transform.Find("FluidOverlay").GetComponent<UnityEngine.UI.RawImage>();

        World.createNoiseObjects(Constants.WORLD_SEED);
        ChunkData.createMeshDataLists();
        cameraControl = GameObject.Find("Main Camera").GetComponent<SimpleCameraController>();

        //setWorldHeightMapAsTexture(312, 160);
        //setHeightMapAsTexture(312, 160);
        //return;

        prevPlayerChunkPos = getPlayerChunkPos();
        playerChunkPosition = prevPlayerChunkPos;
        prevPlayerChunkPos.y += Constants.CHUNK_SIZE;

        chunkLoaderThread.start();


    }

    private GameObject createGO(Chunk c, GameObject prefab, Mesh mesh)
    {
        GameObject go = Instantiate(prefab);
        go.GetComponent<MeshFilter>().mesh = mesh;
        float scale = Constants.BLOCK_SIZE;
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        go.transform.localScale = new Vector3(scale, scale, scale);
        go.transform.localPosition = new Vector3(c.cx * (float)CHUNK_SIZE * scale, c.cy * (float)CHUNK_SIZE * scale, c.cz * (float)CHUNK_SIZE * scale);
        return go;
    }

    // Create game objects
    private void createChunkGameObjects()
    {
        foreach (Chunk c in chunksAwaitingObjectCreation)
        {
            if (c.mesh != null) c.gameObject = createGO(c, chunkPrefab, c.mesh);
            if(c.fluidMesh != null) c.fluidGameObject = createGO(c, chunkFluidPrefab, c.fluidMesh);
        }
        chunksAwaitingObjectCreation.Clear();
    }

    // Create mesh objects
    private void createChunkMeshObjects()
    {
        // Copy queue
        Queue<ChunkCreateMsg> toCreate;
        lock (chunksAwaitingMeshUpload)
        {
            toCreate = new Queue<ChunkCreateMsg>(chunksAwaitingMeshUpload.Count);
            while (chunksAwaitingMeshUpload.Count > 0) toCreate.Enqueue(chunksAwaitingMeshUpload.Dequeue());
        }

        foreach (ChunkCreateMsg m in toCreate)
        {
            Chunk c = (Chunk)allChunks[Chunk.getHashKey1(m.cx, m.cy, m.cz)];
            c.data = m.chunkData;
            if (m.meshI.x >= 0)
            {
                // Create unity mesh object
                c.mesh = c.data.createMesh(m.meshI.x);
            }
            if (m.meshI.y >= 0)
            {
                // Create unity mesh object
                c.fluidMesh = c.data.createMesh(m.meshI.y);
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
            if (c.gameObject != null || c.fluidGameObject != null)
            {
                if (c.distance(p.x, p.y, p.z) <= Constants.RENDER_DISTANCE)
                {
                    // Within render distance
                    c.SetActive(true);
                }
                else
                {
                    // Outside render distance

                    if (c.distance(p.x, p.y, p.z) > Constants.RENDER_DISTANCE + 2)
                    {
                        // Well outside render distance, free resources
                        if (c.gameObject != null) Destroy(c.gameObject);
                        if(c.fluidGameObject != null) Destroy(c.fluidGameObject);
                        c.data = null;
                        c.mesh = null;
                        c.fluidMesh = null;
                        c.destroyed = true;
                    }
                    else
                    {
                        // Not far out of render distance
                        // Make invisible but keep mesh and block data
                        c.SetActive(false);
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

        Vector3Int p = getPlayerChunkPos();
        playerChunkPosition = p;

        if (!p.Equals(prevPlayerChunkPos))
        {
            Queue<ChunkLoaderThread.ChunkRequest> toQueue = new Queue<ChunkLoaderThread.ChunkRequest>();

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

            // Get players direction in each axis (-1/0/1) and send chunk requests

            Vector3Int playerDirection = new Vector3Int();
            Vector3 rotation = cameraControl.GetRotation();
            rotation.y %= 360.0f;
            if (rotation.y < 0)
            {
                rotation.y += 360;
            }

            playerDirection.y = rotation.x > 15.0f ? -1 : (rotation.x > -15.0f ? 0 : 1);

            if ((rotation.y > 360 - 15 || rotation.y < 15) || (rotation.y > 180 - 15 && rotation.y < 180 + 15))
            {
                playerDirection.x = 0;
            }
            else
            {
                playerDirection.x = rotation.y < 180 ? 1 : -1;
            }

            if ((rotation.y > 90 - 15 && rotation.y < 90 + 15) || (rotation.y > 270 - 15 && rotation.y < 270 + 15))
            {
                playerDirection.z = 0;
            }
            else
            {
                playerDirection.z = (rotation.y > 90 && rotation.y < 270) ? -1 : 1;
            }

            chunkLoaderThread.queueChunks(toQueue, p, playerDirection);
        }



        prevPlayerChunkPos = p;
    }

    private Vector3Int playerBlockCoordinates()
    {
        Vector3 playerPosition = Camera.main.transform.position;
        int x = (int)playerPosition.x;
        if (x < 0) x--;
        int y = (int)playerPosition.y;
        if (y < 0) y--;
        int z = (int)playerPosition.z;
        if (z < 0) z--;
        return new Vector3Int(x, y, z);
    }

    private uint playerInBlock()
    {
        Vector3Int blockPos = playerBlockCoordinates();

        Chunk chunk;
        if(allChunks.TryGetValue(Chunk.getHashKey1(playerChunkPosition.x, playerChunkPosition.y, playerChunkPosition.z), out chunk))
        {
            if(chunk.data != null && !chunk.data.isAir())
            {
                int x = blockPos.x;
                int y = blockPos.y;
                int z = blockPos.z;

                if (x >= 0) x = x % Constants.CHUNK_SIZE;
                else x = Constants.CHUNK_SIZE + ((x + 1) % Constants.CHUNK_SIZE);
                if (y >= 0) y = y % Constants.CHUNK_SIZE;
                else y = Constants.CHUNK_SIZE + ((y + 1) % Constants.CHUNK_SIZE);
                if (z >= 0) z = z % Constants.CHUNK_SIZE;
                else z = Constants.CHUNK_SIZE + ((z + 1) % Constants.CHUNK_SIZE);

                return chunk.data.getBlock(x, y, z);
            }
        }
        return 0;
    }

    void Update()
    {
        Profiler.BeginSample("Create chunk game object");
        createChunkGameObjects();
        Profiler.EndSample();

        Profiler.BeginSample("Upload meshes");
        createChunkMeshObjects();
        Profiler.EndSample();

        Profiler.BeginSample("Set game objects visibility");
        setChunkGameObjectsVisibility();
        Profiler.EndSample();

        Profiler.BeginSample("Update ignored chunks");
        updateIgnoredChunks();
        Profiler.EndSample();

        Profiler.BeginSample("Load new chunks");
        loadNewChunks();
        Profiler.EndSample();

        uint block = playerInBlock();
        if (block != 0 && (block & 0x80000000) == 0)
        {
            fluidOverlay.enabled = true;
            fluidOverlay.color = new Color(
                (block & 0xff) / 255.0f,
                ((block >> 8) & 0xff) / 255.0f,
                ((block >> 16) & 0xff) / 255.0f,
                0.5f
                );
            // TODO make the effect fade in
        }
        else
        {
            fluidOverlay.enabled = false;
        }

    }

    void OnApplicationQuit()
    {
        chunkLoaderThread.stop();
    }
}
