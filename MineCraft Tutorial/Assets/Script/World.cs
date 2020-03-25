using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class World : MonoBehaviour {
    public int seed;
    public BiomeAttributes biome;

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public BlockType[] blocktypes;

    // Dict keyd on (x,y) pointing to index in chunk array
    Dictionary<ChunkCoord, Chunk> chunkMap = new Dictionary<ChunkCoord, Chunk>();

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    public List<Chunk> chunksToUpdate = new List<Chunk>();
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();

    Queue<Queue<VoxelMod>> modifications = new Queue<Queue<VoxelMod>>();

    bool applyingModifications = false;

    public GameObject debugScreen;

    // thread and lock to create chunks
    Thread ChunkUpdateThread;
    public object ChunkUpdateThreadLock = new object();

    // Thread to clean up old chunks
    Thread CleanupThread;
    public int SleepDuration = 10000;
    public int CleanupDistance = 20; // Distance in chunks

    private void Start() {
        Random.InitState(seed);

        ChunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
        ChunkUpdateThread.Start();

        // NEED TO REMOVE FROM UPDATE AND CREATE LIST FIRST! OR ELSE THIS WILL CAUSE AN ERROR AND BREAK STUFF
        //CleanupThread = new Thread(new ThreadStart(Cleanup));
        //CleanupThread.Start();

        spawnPosition = new Vector3((Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f, Voxel.ChunkHeight - 50f, (Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f);
        GenerateWorld();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
    }

    private void FixedUpdate() {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);

        if (!playerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if(chunksToCreate.Count > 0 ) {
            CreateChunk();
        }

        if (chunksToDraw.Count > 0)
        {
            if (chunksToDraw.Peek().isEditable)
                chunksToDraw.Dequeue().CreateMesh();
        }

        // Enable debug
        if (Input.GetKeyDown(KeyCode.F3))
            debugScreen.SetActive(!debugScreen.activeSelf);
    }

    void GenerateWorld() {
        for(int x = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; x < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; x++) {
            for(int z = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; z < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; z++) {
                ChunkCoord newChunk = new ChunkCoord(x, z);

                chunkMap.Add(newChunk, new Chunk(newChunk, this));
                chunksToCreate.Add(newChunk);
            }
        }

        player.position = spawnPosition;
        CheckViewDistance();
    }

    // Remove blocks from hashmap that are too far away to be used anytime soon
    void Cleanup()
    {
        while (true)
        {
            Thread.Sleep(SleepDuration);
            // Get all keys from the dict and check the distance to the player,
            // if more than CleanupDistance remove the chunk

            List<ChunkCoord> listToRemove = new List<ChunkCoord>();
            listToRemove.Clear();

            lock (ChunkUpdateThreadLock)
            {
                foreach (ChunkCoord key in chunkMap.Keys)
                {
                    if (DistBetweenPointsSQ(key, playerChunkCoord) > (CleanupDistance * CleanupDistance))
                    {
                        listToRemove.Add(key);
                    }
                }

                // Loop through and delete items
                foreach (ChunkCoord item in listToRemove)
                {
                    if (chunkMap.ContainsKey(item))
                    {
                        chunkMap.Remove(item);
                    }
                }
            }
        }
    }

    int DistBetweenPointsSQ(ChunkCoord p1, ChunkCoord p2)
    {
        return ((p1.x - p2.x) * (p1.x - p2.x)) + (p1.z - p2.z) * (p1.z - p2.z);
    }

    void CreateChunk() {
        ChunkCoord c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        chunkMap[new ChunkCoord(c.x, c.z)].Init();
    }

    void UpdateChunks() {
        bool updated = false;
        int index = 0;

        lock (ChunkUpdateThreadLock)
        {
            while(!updated && index < chunksToUpdate.Count-1) {
                if(chunksToUpdate[index].isEditable) {
                    chunksToUpdate[index].UpdateChunk();
                    activeChunks.Add(chunksToUpdate[index].coord);
                    chunksToUpdate.RemoveAt(index);
                    updated = true;
                } else {
                    index++;
                }
            }
        }
    }

    void ThreadedUpdate()
    {
        while (true)
        {
            if (modifications.Count > 0 && !applyingModifications)
            {
                ApplyModifications();
            }

            if (chunksToUpdate.Count > 0)
            {
                UpdateChunks();
            }
        }
    }

    private void OnDisable()
    {
        ChunkUpdateThread.Abort();
    }
    
    void ApplyModifications () {
        applyingModifications = true;
        int count = 0;

        while (modifications.Count > 0)
        {
            Queue<VoxelMod> queue = modifications.Dequeue();

            while (queue.Count > 0)
            {
                VoxelMod v = queue.Dequeue();

                ChunkCoord c = GetChunkCoordFromVector3(v.position);
                ChunkCoord newChunkCoord = new ChunkCoord(c.x, c.z);

                if (!chunkMap.ContainsKey(newChunkCoord))
                {
                    chunkMap.Add(newChunkCoord, new Chunk(c, this));
                    chunksToCreate.Add(c);
                }

                chunkMap[newChunkCoord].modifications.Enqueue(v);
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

        return chunkMap[new ChunkCoord(x, z)];

    }

    void CheckViewDistance() {
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);

        List<ChunkCoord> prevActiveChunks = new List<ChunkCoord>(activeChunks);
        playerLastChunkCoord = playerChunkCoord;
        
        activeChunks.Clear();

        for(int x = coord.x - Voxel.ViewDistanceInChunks; x < coord.x + Voxel.ViewDistanceInChunks; x++) {
            for (int z = coord.z - Voxel.ViewDistanceInChunks; z < coord.z + Voxel.ViewDistanceInChunks; z++) {
                ChunkCoord currChunk = new ChunkCoord(x, z);


                if (IsChunkInWorld(currChunk)) {
                    // Chunk has not been generated, generate new
                    if (!chunkMap.ContainsKey(currChunk)) {
                        chunkMap.Add(currChunk, new Chunk(currChunk, this));
                        chunksToCreate.Add(currChunk);
                    } else if (!chunkMap[currChunk].isActive) {
                        chunkMap[currChunk].isActive = true;
                    }

                    activeChunks.Add(currChunk);
                }

                // Set new active chunks
                for (int i = 0; i < prevActiveChunks.Count; i++) {
                    if (prevActiveChunks[i].Equals(currChunk)) {
                        prevActiveChunks.RemoveAt(i);
                    }
                }
            }
        }

        foreach (ChunkCoord c in prevActiveChunks)
            chunkMap[new ChunkCoord(c.x, c.z)].isActive = false;
    }

    public bool CheckForVoxel (Vector3 pos)
    {
        ChunkCoord thisChunk = new ChunkCoord(pos);


        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > Voxel.ChunkHeight)
            return false;

        if (chunkMap.ContainsKey(thisChunk) && chunkMap[thisChunk].isVoxelMapPopulated)
            return blocktypes[chunkMap[thisChunk].GetVoxelFromGlobalVector3(pos)].isSolid;

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
        else if (yPos > terrainHeight) {
            // I don't wanna use this anymore because i want sky-islands
        }
        else {
            voxelValue = 2;
        }

        
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
                Queue<VoxelMod> hold_queue = new Queue<VoxelMod>();
                // Set voxelValuie = 1, so see what the area will be for the trees
                //voxelValue = 1;
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold) {
                    //voxelValue = 5;
                    int height = Random.Range(biome.minTreeHeight, biome.maxTreeHeight);
                    for(int i = 1; i <= height; i++) {
                        hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x, pos.y + i, pos.z), 6));
                    }
                    for(int i = -2; i <= 2; i++) {
                        for (int j = -2; j <= 2; j++) {
                            for (int z = -1; z <= 2; z++) { 
                                if (i == 0 && j == 0 && z <= 0) continue;
                                hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x + i, pos.y + height + z, pos.z + j), 3));
                            }
                        }
                    }
                    modifications.Enqueue(hold_queue);
                }
            }
        }

        // Island Generation
        if(yPos == terrainHeight + 50) {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.skyIslandZoneScale) > biome.skyIslandZoneThreshold) {
                voxelValue = 1;
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