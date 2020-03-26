using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Voxel {

    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkHeight = 128;
    // Some arbitrary size in order to have a "Center" that can be accessed and used as X,Y coords without going into minus
    // Not too large since it will cause FP errors
    public static readonly int worldSizeInChunks = 5000;

    public static int WorldSizeInVoxels {
        get { return worldSizeInChunks * ChunkWidth; }
    }

    public static readonly int ViewDistanceInChunks = 7;

    public static readonly int TextureAtlasSizeInBlocks = 24;
    public static float NormalizeBlockTextureSize {
        get { return 1.0f / (float)TextureAtlasSizeInBlocks; }
    }

    public static readonly Vector3[] voxelVerts = new Vector3[8] {
        new Vector3(0.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 1.0f)
    };

    public static readonly Vector3[] faceChecks = new Vector3[6] {
        new Vector3(0.0f, 0.0f, -1.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, -1.0f, 0.0f),
        new Vector3(-1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f)
    };

    public static readonly int[,] voxelTris = new int[6, 4]  {
        { 0,3,1,2}, // Back Face
        {5,6,4,7 }, // Front Face
        { 3,7,2,6}, // Top Face
        {1,5,0,4}, // Bottom Face
        {4,7,0,3 }, // Left Face
        {1,2,5,6 } // Right Face
    };

    public static readonly Vector2[] voxelUvs = new Vector2[4]  {
        new Vector2(0.0f, 0.0f),
        new Vector2(0.0f, 1.0f),
        new Vector2(1.0f, 0.0f),
        new Vector2(1.0f, 1.0f)
    };
}

