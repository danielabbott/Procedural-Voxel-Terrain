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

    private Queue<ChunkRequest> queue = new Queue<ChunkRequest>();

    // Called by main thread
    public void queueChunk(ChunkRequest r)
    {
        lock (queue)
        {
            queue.Enqueue(r);
            Monitor.Pulse(queue);
        }
    }

    // Called by main thread
    // TODO Take current player chunk position as parameter and sort the queue (including chunks already in the queue) to prioritise nearer chunks
    public void queueChunks(Queue<ChunkRequest> r)
    {
        lock (queue)
        {
            while (r.Count > 0)
            {
                queue.Enqueue(r.Dequeue());
            }
            Monitor.Pulse(queue);
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
            lock (queue)
            {
                //  Wait for chunk requests
                while (queue.Count == 0 && !stopThread)
                {
                    Monitor.Wait(queue);
                }
                // Copy
                for (int i = 0; i < 3 && queue.Count > 0; i++) { 
                    queueCopy.Enqueue(queue.Dequeue()); 
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
