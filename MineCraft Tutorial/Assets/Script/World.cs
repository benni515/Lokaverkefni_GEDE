using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour {
    public int seed;
    public BiomeAttributes biome;

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public BlockType[] blocktypes;

    Chunk[,] chunks = new Chunk[Voxel.worldSizeInChunks, Voxel.worldSizeInChunks];

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    List<Chunk> chunksToUpdate = new List<Chunk>();


    Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    bool applyingModifications = false;

    public GameObject debugScreen;

    private void Start() {
        Random.InitState(seed);

        spawnPosition = new Vector3((Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f, Voxel.ChunkHeight - 50f, (Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f);
        GenerateWorld();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
    }

    private void Update() {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);

        if (!playerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if(modifications.Count > 0 && !applyingModifications) {
            StartCoroutine(ApplyModifications());
        }
        if(chunksToCreate.Count > 0 ) {
            CreateChunk();
        }
        if(chunksToUpdate.Count > 0 ) {
            UpdateChunks();
        }

        // Enable debug
        if (Input.GetKeyDown(KeyCode.F3))
            debugScreen.SetActive(!debugScreen.activeSelf);
    }

    void GenerateWorld() {
        for(int x = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; x < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; x++) {
            for(int z = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; z < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; z++) {
                chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, true);
                activeChunks.Add(new ChunkCoord(x, z));
            }
        }
        while(modifications.Count > 0) {
            VoxelMod v = modifications.Dequeue();
            ChunkCoord c = GetChunkCoordFromVector3(v.position);

            if(chunks[c.x, c.z] == null ) {
                chunks[c.x, c.z] = new Chunk(c, this, true);
                activeChunks.Add(c);
            }

            chunks[c.x, c.z].modifications.Enqueue(v);

            if(!chunksToUpdate.Contains(chunks[c.x, c.z])) {
                chunksToUpdate.Add(chunks[c.x, c.z]);
            }
        }
        for(int i = 0; i < chunksToUpdate.Count; i++) {
            chunksToUpdate[i].UpdateChunk();
        }
        chunksToUpdate.Clear();

        player.position = spawnPosition;
    }

    void CreateChunk() {
        ChunkCoord c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        activeChunks.Add(c);
        chunks[c.x, c.z].Init();
    }

    void UpdateChunks() {
        bool updated = false;
        int index = 0;
        while(!updated && index < chunksToUpdate.Count-1) {
            if(chunksToUpdate[index].isVoxelMapPopulated) {
                chunksToUpdate[index].UpdateChunk();
                chunksToUpdate.RemoveAt(index);
                updated = true;
            } else {
                index += 1;
            }
        }
    }
    
    IEnumerator ApplyModifications () {
        applyingModifications = true;
        int count = 0;

        while (modifications.Count > 0) {
            VoxelMod v = modifications.Dequeue();

            ChunkCoord c = GetChunkCoordFromVector3(v.position);

            if (chunks[c.x, c.z] == null) {
                chunks[c.x, c.z] = new Chunk(c, this, true);
                activeChunks.Add(c);
            }

            chunks[c.x, c.z].modifications.Enqueue(v);

            if (!chunksToUpdate.Contains(chunks[c.x, c.z])) {
                chunksToUpdate.Add(chunks[c.x, c.z]);
            }

            count++;
            if(count > 200) {
                count = 0;
                yield return null;
            }
        }

        applyingModifications = false;
    }


    ChunkCoord GetChunkCoordFromVector3(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x / Voxel.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / Voxel.ChunkWidth);

        return new ChunkCoord(x, z);
    }

    public Chunk GetChunkFromVector3(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x / Voxel.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / Voxel.ChunkWidth);

        return chunks[x, z];

    }

    void CheckViewDistance() {
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);

        List<ChunkCoord> prevActiveChunks = new List<ChunkCoord>(activeChunks);
        playerLastChunkCoord = playerChunkCoord;

        for(int x = coord.x - Voxel.ViewDistanceInChunks; x < coord.x + Voxel.ViewDistanceInChunks; x++) {
            for (int z = coord.z - Voxel.ViewDistanceInChunks; z < coord.z + Voxel.ViewDistanceInChunks; z++) {
                if (IsChunkInWorld(new ChunkCoord(x, z))) {
                    // Chunk has not been generated, generate new
                    if (chunks[x, z] == null) {
                        chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, false);
                        chunksToCreate.Add(new ChunkCoord(x, z));
                    } else if (!chunks[x, z].isActive) {
                        chunks[x, z].isActive = true;
                    }

                    activeChunks.Add(new ChunkCoord(x, z));
                }

                // Set new active chunks
                for (int i = 0; i < prevActiveChunks.Count; i++) {
                    if (prevActiveChunks[i].Equals(new ChunkCoord(x, z))) {
                        prevActiveChunks.RemoveAt(i);
                    }
                }
            }
        }

        foreach (ChunkCoord c in prevActiveChunks) {
            chunks[c.x, c.z].isActive = false;
            activeChunks.Remove(new ChunkCoord(c.x, c.z));
        }
    }

    public bool CheckForVoxel (Vector3 pos)
    {
        ChunkCoord thisChunk = new ChunkCoord(pos);


        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > Voxel.ChunkHeight)
            return false;

        if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isVoxelMapPopulated)
            return blocktypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isSolid;

        return blocktypes[GetVoxel(pos)].isSolid;
    }

    // Generate main terrain
    public byte GetVoxel(Vector3 pos) {

        int yPos = Mathf.FloorToInt(pos.y);

        // IMMUTABLE PASS

        if (!IsVoxelInWorld(pos))
            return 0;

        // If bottom, return bedrock
        if (yPos == 0)
            return 1;

        // BASIC TERRAIN PASS

        int terrainHeight = Mathf.FloorToInt(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale) * biome.terrainHeight) + biome.solidGroundHeight;
        byte voxelValue = 0;

        if (yPos == terrainHeight)
            voxelValue = 3;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = 4;
        else if (yPos > terrainHeight)
            return 0;
        else
            voxelValue = 2;

        // SECOND PASS
        if (voxelValue == 2)
        {
            foreach(Lode lode in biome.lodes)
            {
                if (yPos > lode.minHeight && yPos < lode.maxHeight)
                    if (Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))
                        voxelValue = lode.blockID;
            }
        } 

        // TREE PASS
        if(yPos == terrainHeight) {
            
            if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold) {
                // Set voxelValuie = 1, so see what the area will be for the trees
                voxelValue = 1;
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold) {
                    //voxelValue = 5;
                    int height = Random.Range(biome.minTreeHeight, biome.maxTreeHeight);
                    for(int i = 1; i <= height; i++) {
                        modifications.Enqueue(new VoxelMod(new Vector3(pos.x, pos.y + i, pos.z), 6));
                    }
                    for(int i = -2; i <= 2; i++) {
                        for (int j = -2; j <= 2; j++) {
                            for (int z = -1; z <= 2; z++) { 
                                if (i == 0 && j == 0 && z <= 0) continue;
                                modifications.Enqueue(new VoxelMod(new Vector3(pos.x + i, pos.y + height + z, pos.z + j), 3));
                            }
                        }
                    }
                }
            }
        }

        return voxelValue;
    }

    bool IsChunkInWorld(ChunkCoord coord) {
        if (coord.x > 0 && coord.x < Voxel.worldSizeInChunks - 1 && coord.z > 0  && coord.z < Voxel.worldSizeInChunks - 1) {
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

public class VoxelMod {
    public Vector3 position;
    public byte id;

    public VoxelMod() {
        position = new Vector3();
        id = 0;
    }

    public VoxelMod(Vector3 _position, byte _id) {
        position = _position;
        id = _id;
    }
}