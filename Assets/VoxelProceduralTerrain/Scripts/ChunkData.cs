
using UnityEngine;
using System.Collections.Generic;
using System;

public class ChunkData
{
    int cx;
    int cy;
    int cz;


    int solidBlocks = 0;
    int visibleFaces = 0;
    int fluidBlocks = 0;

    // If bit 31 is 1 then it is fluid, else solid
    // Bits 0 - 24 are the colour
    uint[] blocks = null;

    Mesh mesh = null;

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

    private static readonly int[,] layers = new int[,] {
    {
        // From top to bottom
        1, 0x77e08e, // Grass
        3, 0x4d679e, // Soil
        2, 0x27395e, // Darker Soil
        2000000000, 0xd0d0d0, // Stone
    },
    {
        // Ocean terrain
        6, 0x27395e, // Darker Soil
        2000000000, 0xd0d0d0, // Stone
        0, 0,
        0, 0,
    }
};

    // Scale is in blocks
    public void generate(HeightMap heightMap)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;

        if (heightMap != null && heightMap.values == null)
        {
            throw new System.ArgumentException("Heightmap not generated");
        }

        if (heightMap == null || heightMap.maxValue < cy * CHUNK_SIZE)
        {
            return;
        }

        blocks = new uint[CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE];


        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                ushort h_ = heightMap.values[x, z];
                int h = h_ & 0x7fff;

                if(h < cy * CHUNK_SIZE)
                {
                    continue;
                }

                int l = ((h_ & 0x8000) != 0) ? 1 : 0;

                int i = h;
                int layerIndex = 0;
                int layerBlocksRemaining = layers[l, 0];
                for (; i >= 0; i--)
                {
                    
                    if (i < (cy + 1) * CHUNK_SIZE)
                    {
                        initialSetBlock(x, i - cy * CHUNK_SIZE, z, (uint)layers[l, layerIndex * 2 + 1]);

                        if (i <= cy * CHUNK_SIZE)
                        {
                            break;
                        }
                    }

                    layerBlocksRemaining--;

                    if (layerBlocksRemaining == 0)
                    {
                        layerIndex += 1;
                        layerBlocksRemaining = layers[l, layerIndex * 2];
                    }
                }
                solidBlocks += h - cy * CHUNK_SIZE;


            }
        }

        if (solidBlocks == 0)
        {
            blocks = null;
        }
    }

    private static int CHUNK_SIZE = Constants.CHUNK_SIZE;

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

    public void generateMesh()
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;


        mesh = new Mesh();

        List<Vector3> vertices = new List<Vector3>(visibleFaces * 4);
        List<Vector3> normals = new List<Vector3>(visibleFaces * 4);
        List<int> tris = new List<int>(visibleFaces * 2 * 3);

        List<Color32> colourArray = new List<Color32>(visibleFaces * 4);



        for (int face = 0; face < 6; face++)
        {
            // For each face direction (-x,+x,-y,+y,-z,+z)

            int blockIndex = blockPositionToIndex(startingBlocks[face, 0], startingBlocks[face, 1],
                startingBlocks[face, 2]);

            for (int layer = 0; layer < CHUNK_SIZE; layer++)
            {
                // For each layer of faces

                for (int i = 0; i < CHUNK_SIZE; i++)
                {
                    uint blockStripColour = 0;
                    int blockStripLength = 0;
                    Vector3Int blockStripStart = new Vector3Int();

                    for (int j = 0; j < CHUNK_SIZE; j++, blockIndex += nextBlockIncrements[face])
                    {
                        uint thisBlock = blocks[blockIndex];
                        uint adjacentBlock = 0;
                        if (layer > 0)
                        {
                            adjacentBlock = blocks[blockIndex - nextLayerIncrements[face]];
                        }

                        if (thisBlock != 0 && adjacentBlock == 0)
                        {
                            if (blockStripLength == 0)
                            {
                                blockStripLength = 1;
                                blockStripColour = thisBlock;
                                blockStripStart = blockIndexToPosition(blockIndex);
                                blockStripStart.x += blockLayerOffset[face, 0];
                                blockStripStart.y += blockLayerOffset[face, 1];
                                blockStripStart.z += blockLayerOffset[face, 2];
                            }
                            else if (blockStripColour == thisBlock)
                            {
                                blockStripLength++;
                            }
                            else
                            {
                                meshAddFace(face, blockIndex, tris, vertices, normals, colourArray, blockStripLength, blockStripColour, blockStripStart);
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
                            meshAddFace(face, blockIndex, tris, vertices, normals, colourArray, blockStripLength, blockStripColour, blockStripStart);
                            blockStripLength = 0;
                        }


                    }
                    if (blockStripLength != 0)
                    {
                        meshAddFace(face, blockIndex, tris, vertices, normals, colourArray, blockStripLength, blockStripColour, blockStripStart);
                    }
                    blockIndex -= CHUNK_SIZE * nextBlockIncrements[face];
                    blockIndex += nextRowIncrements[face];
                }
                blockIndex -= CHUNK_SIZE * nextRowIncrements[face];
                blockIndex += nextLayerIncrements[face];
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colourArray);
        mesh.SetTriangles(tris, 0, false);
        mesh.bounds = new Bounds(new Vector3(CHUNK_SIZE * 0.5f, CHUNK_SIZE * 0.5f, CHUNK_SIZE * 0.5f),
            new Vector3(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE));
        mesh.UploadMeshData(true);
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

    public Mesh getMesh()
    {
        return mesh;
    }

    private int blockPositionToIndex(int x, int y, int z)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        return x * CHUNK_SIZE * CHUNK_SIZE + z * CHUNK_SIZE + y;
    }

    private Vector3Int blockIndexToPosition(int i)
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

        if (old == 0 && colour != 0)
        {
            // Placed a block
            solidBlocks++;



            visibleFaces += (x == 0 || blocks[blockPositionToIndex(x - 1, y, z)] == 0) ? 1 : -1;
            visibleFaces += (x == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x + 1, y, z)] == 0) ? 1 : -1;
            visibleFaces += (y == 0 || blocks[blockPositionToIndex(x, y - 1, z)] == 0) ? 1 : -1;
            visibleFaces += (y == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y + 1, z)] == 0) ? 1 : -1;
            visibleFaces += (z == 0 || blocks[blockPositionToIndex(x, y, z - 1)] == 0) ? 1 : -1;
            visibleFaces += (z == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y, z + 1)] == 0) ? 1 : -1;



        }
        else if (old != 0 && colour == 0)
        {
            // Removed a block
            solidBlocks--;

            visibleFaces += (x == 0 || blocks[blockPositionToIndex(x - 1, y, z)] == 0) ? -1 : 1;
            visibleFaces += (x == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x + 1, y, z)] == 0) ? -1 : 1;
            visibleFaces += (y == 0 || blocks[blockPositionToIndex(x, y - 1, z)] == 0) ? -1 : 1;
            visibleFaces += (y == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y + 1, z)] == 0) ? -1 : 1;
            visibleFaces += (z == 0 || blocks[blockPositionToIndex(x, y, z - 1)] == 0) ? -1 : 1;
            visibleFaces += (z == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y, z + 1)] == 0) ? -1 : 1;

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
    private void initialSetBlock(int x, int y, int z, uint colour)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;

        blocks[blockPositionToIndex(x, y, z)] = colour;

        visibleFaces += (x == 0 || blocks[blockPositionToIndex(x - 1, y, z)] == 0) ? 1 : -1;
        visibleFaces += (x == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x + 1, y, z)] == 0) ? 1 : -1;
        visibleFaces += (y == 0 || blocks[blockPositionToIndex(x, y - 1, z)] == 0) ? 1 : -1;
        visibleFaces += (y == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y + 1, z)] == 0) ? 1 : -1;
        visibleFaces += (z == 0 || blocks[blockPositionToIndex(x, y, z - 1)] == 0) ? 1 : -1;
        visibleFaces += (z == CHUNK_SIZE - 1 || blocks[blockPositionToIndex(x, y, z + 1)] == 0) ? 1 : -1;


    }

    public uint getBlock(int x, int y, int z)
    {
        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        return blocks[blockPositionToIndex(x, y, z)];
    }

}