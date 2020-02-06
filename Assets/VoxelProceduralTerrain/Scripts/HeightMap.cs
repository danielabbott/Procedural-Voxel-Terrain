using UnityEngine;

public class HeightMap
{
    // Bit 15: top blocks. 1 = water, 0 = grass
    // Measured in blocks
    public ushort[,] values;

    private int chunkX;
    private int chunkZ;
    public ushort maxValue = 0;
    public ushort minValue = 9999;
    public bool hasOcean = false;

    public HeightMap(int chunkX, int chunkZ)
    {
        this.chunkX = chunkX;
        this.chunkZ = chunkZ;
    }

    // TODO: Make thread-local
    private static float[,] baseTerrainNoiseData = new float[Constants.CHUNK_SIZE, Constants.CHUNK_SIZE];
    private static float[,] continentData = new float[Constants.CHUNK_SIZE, Constants.CHUNK_SIZE];
    private static float[,] mountainNoiseData = new float[Constants.CHUNK_SIZE, Constants.CHUNK_SIZE];

    public void generate(int seed)
    {
        if(values != null)
        {
            return;
        }

        int CHUNK_SIZE = Constants.CHUNK_SIZE;
        values = new ushort[CHUNK_SIZE, CHUNK_SIZE];        

        float bsz = Constants.BLOCK_SIZE;
        World.baseTerrainNoise.generate(baseTerrainNoiseData, chunkX * CHUNK_SIZE, chunkZ * CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE, bsz, bsz, 0.02f, 2, 3, 0.8f);
        World.continentNoise.generate(continentData, chunkX * CHUNK_SIZE, chunkZ * CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE, bsz, bsz, 0.0001f, 4, 4, 0.2f);
        World.mountainNoise.generate(mountainNoiseData, chunkX * CHUNK_SIZE, chunkZ * CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE, bsz, bsz, 0.00001f, 2);


        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                // Grass, ocean floor
                float baseTerrainNoiseSample = Mathf.Clamp((baseTerrainNoiseData[x, z] + 0.707f) / 1.414f, 0, 1);

                // Controls where oceans and land are
                float continentNoiseSample = Mathf.Clamp((continentData[x, z] + 0.707f) / 1.414f, 0, 1);

                bool ocean = true;

                // Flatten the graph so it is a line on y=0, then a slope, then y=1
                // TODO terrain glitch at seed 353464, chunk 312, 160.
                if (continentNoiseSample > 0.75f)
                {
                    ocean = false;
                    continentNoiseSample = 1.0f;
                }
                else if (continentNoiseSample > 0.75f-Constants.LAND_OCEAN_GRADIENT)
                {
                    continentNoiseSample = (continentNoiseSample - (0.75f - Constants.LAND_OCEAN_GRADIENT)) / Constants.LAND_OCEAN_GRADIENT;
                }
                else
                {
                    continentNoiseSample = 0.0f;
                }

                if(ocean)
                {
                   hasOcean = true;
                }

                // TODO: Make mountains look more natural
                float mountainPositionNoiseSample = Mathf.Clamp((mountainNoiseData[x, z] + 0.707f) / 1.414f, 0, 1);
                if (mountainPositionNoiseSample > 0.8)
                {
                    mountainPositionNoiseSample = 0.75f; // 0
                }
                else if (mountainPositionNoiseSample < 0.75f)
                {
                    mountainPositionNoiseSample = 0.75f; // 0
                }
                mountainPositionNoiseSample = (mountainPositionNoiseSample - 0.75f) / 0.05f;
                mountainPositionNoiseSample = Mathf.Sin(mountainPositionNoiseSample);
                if (mountainPositionNoiseSample > 0.5f)
                {
                    mountainPositionNoiseSample = 1.0f - mountainPositionNoiseSample;
                }
                if (mountainPositionNoiseSample > 0.48f)
                {
                    mountainPositionNoiseSample = 0.48f;
                }

                // h is in meters
                float h = 0;

                // Add sea floor
                h += Constants.OCEAN_FLOOR_HEIGHT;

                // Terrain at bottom of ocean
                h += (1.0f - continentNoiseSample) * (baseTerrainNoiseSample * 6.0f);

                // Terrain at sea level (grass)
                h += continentNoiseSample * 
                    ((Constants.TERRAIN_HEIGHT - Constants.OCEAN_FLOOR_HEIGHT) 
                        + baseTerrainNoiseSample * 1.5f);

                // Add mountain ranges
                h += mountainPositionNoiseSample * (Constants.MOUNTAIN_HEIGHT - Constants.TERRAIN_HEIGHT - Constants.OCEAN_FLOOR_HEIGHT);

                if (h > Constants.TERRAIN_MAX_HEIGHT)
                {
                    h = Constants.TERRAIN_MAX_HEIGHT;
                }
                if (h < 0.0f)
                {
                    h = 0.0f;
                }

                ushort h_ = (ushort)(h / Constants.BLOCK_SIZE);
                if(h_ > maxValue)
                {
                    maxValue = h_;
                }
                if (h_ < minValue)
                {
                    minValue = h_;
                }

                values[x, z] = h_;

                if (ocean)
                {
                    values[x, z] |= 0x8000;
                }
            }
        }
    }
}