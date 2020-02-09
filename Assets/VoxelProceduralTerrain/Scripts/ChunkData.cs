
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

public class ChunkData
{
    int solidBlocks = 0;

    // Code for counting number of visible faces is disabled.
    //int visibleFaces = 0;

    int fluidBlocks = 0;

    // If bit 31 is 1 then it is solid, else fluid
    // Bits 0 - 24 are the colour
    uint[] blocks = null;

    // -1048575 to +1048575 (chunks)
    public int cx;
    public int cy;
    public int cz;

    public ChunkData(int x_, int y_, int z_)
    {
        cx = x_;
        cy = y_;
        cz = z_;
    }

    public bool noSolidBlocks()
    {
        return solidBlocks == 0;
    }

    public bool noFluidBlocks()
    {
        return fluidBlocks == 0;
    }

    public bool isAir()
    {
        return solidBlocks == 0 && fluidBlocks == 0;
    }

    private static readonly uint[,] layers = new uint[,] {
    {
        // From top to bottom
        1, 0x8077e08e, // Grass
        3, 0x804d679e, // Soil
        2, 0x8027395e, // Darker Soil
        2000000000, 0x80d0d0d0, // Stone
    },
    {
        // Ocean terrain
        6, 0x8027395e, // Darker Soil
        2000000000, 0x80d0d0d0, // Stone
        0, 0,
        0, 0,
    }
    };

    public void generate(HeightMap heightMap)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;

        if (heightMap != null && heightMap.values == null)
        {
            throw new System.ArgumentException("Heightmap not generated");
        }

        if (heightMap == null)
        {
            return;
        }


        if ((!heightMap.hasOcean && heightMap.maxValue < cy * CHUNK_SIZE)
                    || (heightMap.hasOcean && heightMap.maxValue < cy * CHUNK_SIZE && Constants.SEA_LEVEL < cy * CHUNK_SIZE))
        {
            return;
        }

        blocks = new uint[CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE];


        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                // For each column, start at the topmost block and fill downwards

                ushort h_ = heightMap.values[x, z];
                bool ocean = (h_ & 0x8000) != 0;
                int h = h_ & 0x7fff;

                if (ocean && Constants.SEA_LEVEL_BLOCKS >= cy * CHUNK_SIZE)
                {
                    int j = Constants.SEA_LEVEL_BLOCKS;
                    if(j >= (cy + 1) * CHUNK_SIZE)
                    {
                        j = (cy + 1) * CHUNK_SIZE - 1;
                    }
                    for (; j > h && j >= cy * CHUNK_SIZE; j--)
                    {
                        initialSetBlock(x, j - cy * CHUNK_SIZE, z, 0x00e03010);
                        fluidBlocks++;
                    }
                }

                if (h >= cy * CHUNK_SIZE)
                {
                    // Index into layers array (normal layers / ocean floor layers)
                    int l = ocean ? 1 : 0;

                    int i = h;
                    int layerIndex = 0;
                    uint layerBlocksRemaining = layers[l, 0];
                    for (; i >= cy * CHUNK_SIZE; i--)
                    {

                        if (i < (cy + 1) * CHUNK_SIZE)
                        {
                            initialSetBlock(x, i - cy * CHUNK_SIZE, z, (uint)layers[l, layerIndex * 2 + 1]);
                            solidBlocks++;
                        }

                        layerBlocksRemaining--;

                        if (layerBlocksRemaining == 0)
                        {
                            layerIndex += 1;
                            layerBlocksRemaining = layers[l, layerIndex * 2];
                        }
                    }
                }

            }
        }

        if (solidBlocks == 0 && fluidBlocks == 0)
        {
            // Chunk was air, free memory.
            blocks = null;
        }
    }

    private readonly static int CHUNK_SIZE = Constants.CHUNK_SIZE;

    // Constant data for mesh generation algorithm
    // The algortihm runs 6 times (once for each direction), using a different array index each time

    private static readonly int[,] startingBlocks = new int[,]
        {
            {0, 0, 0}, // -x
            {CHUNK_SIZE-1, CHUNK_SIZE-1, CHUNK_SIZE-1}, // +x

            {0, 0, 0}, // -y
            {CHUNK_SIZE-1, CHUNK_SIZE-1, CHUNK_SIZE-1}, // +y

            {0, 0, 0}, // -z
            {CHUNK_SIZE-1, CHUNK_SIZE-1, CHUNK_SIZE-1}, // +z
        };

    private static readonly int[] nextBlockIncrements = new int[] {
            // -x. Move in +y direction
            1,

            // +x. Move in -y direction
            -1,

            // -y. Move in +z direction
            CHUNK_SIZE,

            -CHUNK_SIZE,

            // -z. Move in +y direction
            1,

            // +z. Move in -y direction
            -1,
        };

    private static readonly int[,] blockRowDirection = new int[,]
    {
            // -x
            {0, 1, 0},
            // +x
            {0, -1, 0},
            // -y
            {0, 0, 1},

            {0, 0, -1},

            {0, 1, 0},
            {0, -1, 0}
    };

    private static readonly int[] nextRowIncrements = new int[] {
            // -x. Move in +z direction
            CHUNK_SIZE,

            // +x. Move in -z direction
            -CHUNK_SIZE,

            // -y. Move in +x direction
            CHUNK_SIZE*CHUNK_SIZE,

            -CHUNK_SIZE*CHUNK_SIZE,

            // -z. Move in +x direction
            CHUNK_SIZE*CHUNK_SIZE,

            -CHUNK_SIZE*CHUNK_SIZE
        };

    private static readonly int[,] blockColumnDirection = new int[,]
    {
            {0, 0, 1},
            {0, 0, 1},
            {1, 0, 0},
            {1, 0, 0},
            {1, 0, 0},
            {1, 0, 0}
    };

    private static readonly int[] nextLayerIncrements = new int[] {
            // -x. Move in +x direction
            CHUNK_SIZE*CHUNK_SIZE,

            // +x. Move in -x direction
            -CHUNK_SIZE*CHUNK_SIZE,

            // -y. Move in +y direction
            1,

            -1,

            CHUNK_SIZE,
            -CHUNK_SIZE,
        };

    private static readonly int[,] blockLayerOffset = new int[,]
    {
            // -x
            {0, 0, 0},
            // +x
            {1, 1, 0},
            // -y
            {0, 0, 0},
            {0, 1, 1},

            {0, 0, 0},
            {0, 1, 1}
    };

    private static readonly bool[] reverseWindingOrder = new bool[]
    {
            false,
            false,
            false,
            false,
            true,
            true,
    };

    private static readonly Vector3[] faceNormals = new Vector3[]
    {
            new Vector3(-1,0,0),
            new Vector3(1,0,0),
            new Vector3(0,-1,0),
            new Vector3(0,1,0),
            new Vector3(0,0,-1),
            new Vector3(0,0,1),
    };

    private static readonly int MAX_FACES = Constants.CHUNK_SIZE * Constants.CHUNK_SIZE * Constants.CHUNK_SIZE * 6;

    class MeshData
    {
        public List<Vector3> vertices = new List<Vector3>(MAX_FACES * 4);
        public List<Vector3> normals = new List<Vector3>(MAX_FACES * 4);
        public List<int> tris = new List<int>(MAX_FACES * 2 * 3);
        public List<Color32> colourArray = new List<Color32>(MAX_FACES * 4);
    }

    // TODO These can't be static if we want multiple chunk loading threads. Make it thread-local

    private static MeshData[] meshDataObjects = new MeshData[6];
    private static volatile bool[] meshDataInUse = new bool[6];

    public static void createMeshDataLists()
    {
        for (int i = 0; i < 6; i++)
        {
            meshDataObjects[i] = new MeshData();
        }
    }

    private void DoMeshRow(int blockIndex, int face, int layer, MeshData md, bool fluid)
    {
        // Keep track of how many faces of the same colour are in a row
        // They are combined into one
        // A 'block strip' is multiple square block faces combined into a rectangle
        uint blockStripColour = 0;
        int blockStripLength = 0;
        Vector3Int blockStripStart = new Vector3Int();

        for (int j = 0; j < CHUNK_SIZE; j++, blockIndex += nextBlockIncrements[face])
        {
            // For each face in the row

            uint thisBlock = blocks[blockIndex];
            uint adjacentBlock = fluid ? 0x80000000 : 0;
            if (layer > 0)
            {
                adjacentBlock = blocks[blockIndex - nextLayerIncrements[face]];
            }

            if ( (!fluid && (thisBlock & 0x80000000) != 0 && (adjacentBlock & 0x80000000) == 0)
                || (fluid && (thisBlock & 0x80000000) == 0 && thisBlock != 0 && adjacentBlock == 0))
            {
                // This face is visible

                if (blockStripLength == 0)
                {
                    // Start of strip

                    blockStripLength = 1;
                    blockStripColour = thisBlock;
                    blockStripStart = blockIndexToPosition(blockIndex);
                    blockStripStart.x += blockLayerOffset[face, 0];
                    blockStripStart.y += blockLayerOffset[face, 1];
                    blockStripStart.z += blockLayerOffset[face, 2];
                }
                else if (blockStripColour == thisBlock)
                {
                    // Continue strip

                    blockStripLength++;
                }
                else
                {
                    // New strip

                    // Add previous strip
                    meshAddFace(face, blockIndex, md.tris, md.vertices, md.normals, md.colourArray, blockStripLength, blockStripColour, blockStripStart);

                    // New strip
                    blockStripLength = 1;
                    blockStripColour = thisBlock;
                    blockStripStart = blockIndexToPosition(blockIndex);
                    blockStripStart.x += blockLayerOffset[face, 0];
                    blockStripStart.y += blockLayerOffset[face, 1];
                    blockStripStart.z += blockLayerOffset[face, 2];
                }
            }
            else if (blockStripLength != 0)
            {
                // End of strip
                meshAddFace(face, blockIndex, md.tris, md.vertices, md.normals, md.colourArray, blockStripLength, blockStripColour, blockStripStart);
                blockStripLength = 0;
            }


        }
        if (blockStripLength != 0)
        {
            // Add last strip
            meshAddFace(face, blockIndex, md.tris, md.vertices, md.normals, md.colourArray, blockStripLength, blockStripColour, blockStripStart);
        }
    }


    // Returns (solid blocks mesh, fluids mesh)
    public Vector2Int generateMesh()
    {
        if (solidBlocks == 0 && fluidBlocks == 0)
        {
            return new Vector2Int(-1, -1);
        }

        int CHUNK_SIZE = Constants.CHUNK_SIZE;

        MeshData md = null;
        int mdI = -1;

        MeshData mdFluid = null;
        int mdFluidI = -1;

        // Find a mesh data object to fill
        lock (meshDataInUse)
        {
            int i = 0;
            if (solidBlocks > 0)
            {
                for (; i < 6; i++)
                {
                    if (!meshDataInUse[i])
                    {
                        meshDataInUse[i] = true;
                        md = meshDataObjects[i];
                        mdI = i;
                        break;
                    }
                }
            }
            if (fluidBlocks > 0)
            {
                for (; i < 6; i++)
                {
                    if (!meshDataInUse[i])
                    {
                        meshDataInUse[i] = true;
                        mdFluid = meshDataObjects[i];
                        mdFluidI = i;
                        break;
                    }
                }
            }
        }

        if ((solidBlocks > 0 && md == null) || (fluidBlocks > 0 && mdFluid == null))
        {
            // Not enough mesh data objects available

            if (md != null) meshDataInUse[mdI] = false;
            if (mdFluid != null) meshDataInUse[mdFluidI] = false;
            // Return error (-1)
            return new Vector2Int(-1, -1);
        }

        if(md != null)
        {
            md.vertices.Clear();
            md.normals.Clear();
            md.tris.Clear();
            md.colourArray.Clear();
        }

        if (mdFluid != null)
        {
            mdFluid.vertices.Clear();
            mdFluid.normals.Clear();
            mdFluid.tris.Clear();
            mdFluid.colourArray.Clear();
        }


        for (int face = 0; face < 6; face++)
        {
            // For each face direction (-x,+x,-y,+y,-z,+z)

            int blockIndex = blockPositionToIndex(startingBlocks[face, 0], startingBlocks[face, 1],
                startingBlocks[face, 2]);

            for (int layer = 0; layer < CHUNK_SIZE; layer++)
            {
                // For each layer (cross-section) of faces

                for (int i = 0; i < CHUNK_SIZE; i++)
                {
                    // For each row of faces

                    if (md != null)
                    {
                        DoMeshRow(blockIndex, face, layer, md, false);
                    }
                    if (mdFluid != null)
                    {
                        DoMeshRow(blockIndex, face, layer, mdFluid, true);
                    }
                    blockIndex += nextRowIncrements[face];
                }
                blockIndex -= CHUNK_SIZE * nextRowIncrements[face];
                blockIndex += nextLayerIncrements[face];
            }
        }

        return new Vector2Int(mdI, mdFluidI);
    }

    public Mesh createMesh(int i)
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(meshDataObjects[i].vertices);
        mesh.SetNormals(meshDataObjects[i].normals);
        mesh.SetColors(meshDataObjects[i].colourArray);
        mesh.SetTriangles(meshDataObjects[i].tris, 0, false);
        mesh.bounds = new Bounds(new Vector3(CHUNK_SIZE * 0.5f, CHUNK_SIZE * 0.5f, CHUNK_SIZE * 0.5f),
            new Vector3(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE));
        mesh.UploadMeshData(true);
        lock (meshDataInUse)
        {
            meshDataInUse[i] = false;
        }
        return mesh;
    }

    private void meshAddFace(int face, int blockIndex, List<int> tris, List<Vector3> vertices, List<Vector3> normals, List<Color32> colourArray, int blockStripLength, uint blockStripColour, Vector3Int blockStripStart)
    {
        if (reverseWindingOrder[face])
        {
            tris.Add(vertices.Count + 2);
            tris.Add(vertices.Count + 1);
            tris.Add(vertices.Count + 0);
            tris.Add(vertices.Count + 0);
            tris.Add(vertices.Count + 3);
            tris.Add(vertices.Count + 2);
        }
        else
        {
            tris.Add(vertices.Count + 0);
            tris.Add(vertices.Count + 1);
            tris.Add(vertices.Count + 2);
            tris.Add(vertices.Count + 2);
            tris.Add(vertices.Count + 3);
            tris.Add(vertices.Count + 0);
        }

        vertices.Add(new Vector3(blockStripStart.x, blockStripStart.y, blockStripStart.z));
        vertices.Add(new Vector3(
            blockStripStart.x + blockColumnDirection[face, 0],
            blockStripStart.y + blockColumnDirection[face, 1],
            blockStripStart.z + blockColumnDirection[face, 2]));
        vertices.Add(new Vector3(
            blockStripStart.x + blockRowDirection[face, 0] * blockStripLength + blockColumnDirection[face, 0],
            blockStripStart.y + blockRowDirection[face, 1] * blockStripLength + blockColumnDirection[face, 1],
            blockStripStart.z + blockRowDirection[face, 2] * blockStripLength + blockColumnDirection[face, 2]));
        vertices.Add(new Vector3(
            blockStripStart.x + blockRowDirection[face, 0] * blockStripLength,
            blockStripStart.y + blockRowDirection[face, 1] * blockStripLength,
            blockStripStart.z + blockRowDirection[face, 2] * blockStripLength));

        normals.Add(faceNormals[face]);
        normals.Add(faceNormals[face]);
        normals.Add(faceNormals[face]);
        normals.Add(faceNormals[face]);

        Color32 c = new Color32(
            (byte)((blockStripColour & 255)),
            (byte)(((blockStripColour >> 8) & 255)),
            (byte)(((blockStripColour >> 16) & 255)),
            255
        );

        colourArray.Add(c);
        colourArray.Add(c);
        colourArray.Add(c);
        colourArray.Add(c);
    }


    private int blockPositionToIndex(int x, int y, int z)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        return x * CHUNK_SIZE * CHUNK_SIZE + z * CHUNK_SIZE + y;
    }

    private static Vector3Int blockIndexToPosition(int i)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        return new Vector3Int(i / (CHUNK_SIZE * CHUNK_SIZE), i % CHUNK_SIZE, (i % (CHUNK_SIZE * CHUNK_SIZE)) / CHUNK_SIZE);
    }

    public void setBlock(int x, int y, int z, uint colour)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;

        if (x < 0 || y < 0 || z < 0 || x >= CHUNK_SIZE || y >= CHUNK_SIZE || z >= CHUNK_SIZE)
        {
            throw new System.ArgumentException("Invalid block coordinate: " + x + "," + y + "," + z);
        }

        uint old = blocks[blockPositionToIndex(x, y, z)];
        blocks[blockPositionToIndex(x, y, z)] = colour;

        if ((old & 0x80000000) == 0 && (colour & 0x80000000) != 0)
        {
            // Placed a block
            solidBlocks++;



            /*visibleFaces += (x == 0 || blocks[blockPositionToIndex(x - 1, y, z)] == 0) ? 1 : -1;
            visibleFaces += (x == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x + 1, y, z)] == 0) ? 1 : -1;
            visibleFaces += (y == 0 || blocks[blockPositionToIndex(x, y - 1, z)] == 0) ? 1 : -1;
            visibleFaces += (y == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y + 1, z)] == 0) ? 1 : -1;
            visibleFaces += (z == 0 || blocks[blockPositionToIndex(x, y, z - 1)] == 0) ? 1 : -1;
            visibleFaces += (z == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y, z + 1)] == 0) ? 1 : -1;*/



        }
        else if ((old & 0x80000000) != 0 && (colour & 0x80000000) == 0)
        {
            // Removed a block
            solidBlocks--;

            /*visibleFaces += (x == 0 || blocks[blockPositionToIndex(x - 1, y, z)] == 0) ? -1 : 1;
            visibleFaces += (x == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x + 1, y, z)] == 0) ? -1 : 1;
            visibleFaces += (y == 0 || blocks[blockPositionToIndex(x, y - 1, z)] == 0) ? -1 : 1;
            visibleFaces += (y == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y + 1, z)] == 0) ? -1 : 1;
            visibleFaces += (z == 0 || blocks[blockPositionToIndex(x, y, z - 1)] == 0) ? -1 : 1;
            visibleFaces += (z == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y, z + 1)] == 0) ? -1 : 1;*/

        }
        else
        {
            // Changed a block's colour or replaced air with air
        }

    }

    // Used when generating terrain 
    // assumes that block is currently air and being set to non-air
    // no input validation
    // Does not update solidBlocks
    // Assumes blocks are set from top to bottom
    private void initialSetBlock(int x, int y, int z, uint colour)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;

        blocks[blockPositionToIndex(x, y, z)] = colour;

        /*
        // TODO does not account for fluid blocks
        visibleFaces += (x == 0 || blocks[blockPositionToIndex(x - 1, y, z)] == 0) ? 1 : -1;
        visibleFaces += (x == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x + 1, y, z)] == 0) ? 1 : -1;
        visibleFaces += 1;
        visibleFaces += (y == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y + 1, z)] == 0) ? 1 : -1;
        visibleFaces += (z == 0 || blocks[blockPositionToIndex(x, y, z - 1)] == 0) ? 1 : -1;
        visibleFaces += (z == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y, z + 1)] == 0) ? 1 : -1;*/


    }

    public uint getBlock(int x, int y, int z)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        return blocks[blockPositionToIndex(x, y, z)];
    }

}