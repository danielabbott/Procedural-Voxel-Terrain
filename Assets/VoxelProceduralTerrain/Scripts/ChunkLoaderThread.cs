using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;


// N.B. Only one instance of the chunk loader thread should be created
// as the heightmap generation code and mesh generation code is not multithreading capable yet
// TODO ^

public class ChunkLoaderThread
{
    public struct ChunkRequest
    {
        public int cx, cy, cz;

        public ChunkRequest(int x, int y, int z)
        {
            cx = x;
            cy = y;
            cz = z;
        }
    }

    private List<ChunkRequest> chunkRequests = new List<ChunkRequest>();

    // Called by main thread
    // newRequests can be empty
    public void queueChunks(Queue<ChunkRequest> newRequests, Vector3Int playerChunkPosition)
    {
        List<ChunkRequest> ignoredRequests = new List<ChunkRequest>();
        lock (chunkRequests)
        {
            // Remove chunks which are outside the render distance
            for (int i = 0; i < chunkRequests.Count; i++)
            {
                ChunkRequest r = chunkRequests[i];
                int dist = (int)(playerChunkPosition - new Vector3Int(r.cx, r.cy, r.cz)).magnitude;
                if (dist > Constants.RENDER_DISTANCE)
                {
                    ignoredRequests.Add(chunkRequests[i]);
                    chunkRequests.RemoveAt(i);
                }
            }

            // Add new chunks - it is known that they are within the render distance
            chunkRequests.InsertRange(0, newRequests);
            newRequests.Clear();

            chunkRequests.Sort(delegate (ChunkRequest x, ChunkRequest y)
            {
                int a = (int)(playerChunkPosition - new Vector3Int(x.cx, x.cy, x.cz)).magnitude;
                int b = (int)(playerChunkPosition - new Vector3Int(y.cx, y.cy, y.cz)).magnitude;
                return a - b;
            });

            Monitor.Pulse(chunkRequests);
        }

        if(ignoredRequests.Count > 0)
        {
            TerrainGen.IgnoredChunkRequests(ignoredRequests);
        }
    }

    public void start()
    {
        Thread t = new Thread(new ThreadStart(loaderLoop));
        t.Start();
    }

    private volatile bool stopThread = false;

    private void loaderLoop()
    {
        while (!stopThread)
        {
            
            Queue<ChunkRequest> queueCopy = new Queue<ChunkRequest>();
            lock (chunkRequests)
            {
                //  Wait for chunk requests
                while (chunkRequests.Count == 0 && !stopThread)
                {
                    Monitor.Wait(chunkRequests);
                }

                // Copy
                for (int i = 0; i < 3 && chunkRequests.Count > 0; i++) { 
                    queueCopy.Enqueue(chunkRequests[0]);
                    chunkRequests.RemoveAt(0);
                }
            }

            Queue<TerrainGen.ChunkCreateMsg> done = new Queue<TerrainGen.ChunkCreateMsg>();

            while (queueCopy.Count > 0 && !stopThread)
            {
                ChunkRequest r = (ChunkRequest)queueCopy.Dequeue();

                // Heightmap
                HeightMap h = World.getOrCreateHeightMap(r.cx, r.cz);
                h.generate(Constants.WORLD_SEED);

                // Chunk block data
                ChunkData c = new ChunkData(r.cx, r.cy, r.cz);
                c.generate(h);

                // Mesh
                int meshI = -1;
                if (!c.isAir())
                {
                    meshI = c.generateMesh();
                    while (meshI == -1 && !stopThread)
                    {
                        // Wait for the main thread to finish with one of the mesh data objects
                        if (done.Count > 0)
                        {
                            TerrainGen.chunksLoaded(done);
                            done.Clear();
                        }
                        Thread.Sleep(32);
                        meshI = c.generateMesh();
                    }
                }
                TerrainGen.ChunkCreateMsg m = new TerrainGen.ChunkCreateMsg(r.cx, r.cy, r.cz, c, meshI);
                done.Enqueue(m);
            }
            TerrainGen.chunksLoaded(done);
        }
    }

    public void stop()
    {
        stopThread = true;
    }
}
