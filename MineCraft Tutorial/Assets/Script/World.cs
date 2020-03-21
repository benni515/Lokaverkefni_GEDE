using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour {

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public BlockType[] blocktypes;

    Chunk[,] chunks = new Chunk[Voxel.worldSizeInChunks, Voxel.worldSizeInChunks];

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();

    private void Start() {
        spawnPosition = new Vector3((Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f, Voxel.ChunkHeight + 2f, (Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f);
        GenerateWorld();
    }

    private void Update() {
        CheckViewDistance();
    }

    void GenerateWorld() {
        for(int x = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; x < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; x++) {
            for(int z = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; z < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; z++) {
                CreateNewChunk(x, z);
            }
        }

        player.position = spawnPosition;
    }

    ChunkCoord GetChunkCoordFromVector3(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x / Voxel.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / Voxel.ChunkWidth);

        return new ChunkCoord(x, z);
    }

    void CheckViewDistance() {
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);

        List<ChunkCoord> prevActiveChunks = new List<ChunkCoord>(activeChunks);

        for(int x = coord.x - Voxel.ViewDistanceInChunks; x < coord.x + Voxel.ViewDistanceInChunks; x++) {
            for (int z = coord.z - Voxel.ViewDistanceInChunks; z < coord.z + Voxel.ViewDistanceInChunks; z++) {
                if (IsChunkInWorld(new ChunkCoord(x, z))) {
                    // Chunk has not been generated, generate new
                    if (chunks[x, z] == null) {
                        CreateNewChunk(x, z);
                    } else if (!chunks[x, z].isActive) {
                        chunks[x, z].isActive = true;
                        activeChunks.Add(new ChunkCoord(x, z));
                    }
                }

                // Set new active chunks
                for (int i = 0; i < prevActiveChunks.Count; i++) {
                    if (prevActiveChunks[i].Equals(new ChunkCoord(x, z))) {
                        Debug.Log(x);
                        // Remove
                        prevActiveChunks.RemoveAt(i);
                    }
                }
            }
        }

        foreach (ChunkCoord c in prevActiveChunks)
            chunks[c.x, c.z].isActive = false;
    }

    // Generate main terrain
    public byte GetVoxel(Vector3 pos) {
        if (!IsVoxelInWorld(pos)) {
            return 0;
        }

        if (pos.y < 5)
        {
            return 1;
        }
        return 2;
    }

    void CreateNewChunk(int x, int z) {
        chunks[x, z] = new Chunk(new ChunkCoord(x, z), this);
        activeChunks.Add(new ChunkCoord(x, z));
    }

    bool IsChunkInWorld(ChunkCoord coord) {
        if (coord.x > 0 && coord.x < Voxel.worldSizeInChunks - 1 && coord.z < Voxel.worldSizeInChunks - 1) {
            return true;
        }
        return false;
    }

    bool IsVoxelInWorld(Vector3 pos) {
        if (pos.x >= 0 && pos.x < Voxel.WorldSizeInVoxels && pos.y >= 0 && pos.y < Voxel.ChunkHeight && pos.z >= 0 && pos.z < Voxel.WorldSizeInVoxels) {
            return true;
        }
        return false;
    }
}

    [System.Serializable]
public class BlockType {
    public string blockName;
    public bool isSolid;

    [Header("Texture values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    public int GetTextureID(int faceIndex) {
        switch(faceIndex)
        {
            case 0:
                return backFaceTexture;
            case 1:
                return frontFaceTexture;
            case 2:
                return topFaceTexture;
            case 3:
                return bottomFaceTexture;
            case 4:
                return leftFaceTexture;
            case 5:
                return rightFaceTexture;
            default:
                Debug.Log("Error selecting face index");
                return 0;
        }
    }
}