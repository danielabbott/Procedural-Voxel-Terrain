public class Constants
{
    public static readonly int WORLD_SEED = 353464;

    // Cube root of number of blocks in each chunk
    public static readonly int CHUNK_SIZE = 32;

    // Length of edge of block (meters)
    // Controls the detail in the terrain
    public static readonly float BLOCK_SIZE = 1.0f;

    // Height in metres that the grass starts at
    public static readonly float TERRAIN_HEIGHT = 60;

    // Height in metres that the ocean floor starts at
    public static readonly float OCEAN_FLOOR_HEIGHT = 5;

    // Controls the steepness of the slope between the land and the ocean floor
    public static readonly float LAND_OCEAN_GRADIENT = 0.02f;

    // Height in metres of the mountains
    public static readonly float MOUNTAIN_HEIGHT = 140;

    // 0 = Only 1 chunk rendered
    public static readonly int RENDER_DISTANCE = 6;

    // Derived constants

    // ish
    public static readonly float TERRAIN_MAX_HEIGHT = TERRAIN_HEIGHT + MOUNTAIN_HEIGHT + 5;

}
