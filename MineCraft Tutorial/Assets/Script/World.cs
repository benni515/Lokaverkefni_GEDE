using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.Collections.Concurrent;

public class World : MonoBehaviour {
    public int seed;
    public BiomeAttributes[] biomes;

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public Material transparentMaterial;
    public BlockType[] blocktypes;

    // Pickupable objects
    public GameObject _pickup_wood;

    // Dict keyd on (x,y) pointing to index in chunk array
    ConcurrentDictionary<ChunkCoord, Chunk> chunkMap = new ConcurrentDictionary<ChunkCoord, Chunk>();

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    public LinkedList<Chunk> chunksToUpdate = new LinkedList<Chunk>();
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

    private static System.Random rnd;

    private void Start() {
        UnityEngine.Random.InitState(seed);
        rnd = new System.Random(seed);

        ChunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
        ChunkUpdateThread.Start();

        // NEED TO REMOVE FROM UPDATE AND CREATE LIST FIRST! OR ELSE THIS WILL CAUSE AN ERROR AND BREAK STUFF
        //CleanupThread = new Thread(new ThreadStart(Cleanup));
        //CleanupThread.Start();

        spawnPosition = new Vector3((Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f, Voxel.ChunkHeight - 50f, (Voxel.worldSizeInChunks * Voxel.ChunkWidth) / 2f);
        GenerateWorld();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
    }

    public bool done = false;

    private void FixedUpdate() {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);
        
        if (!playerChunkCoord.Equals(playerLastChunkCoord)) { 
            CheckViewDistance();
        }

        if (chunksToCreate.Count > 0)
        {
            CreateChunk();
        }

        if (chunksToDraw.Count > 0)
        {
            if (chunksToDraw.Peek().isEditable && chunksToDraw.Peek().isVoxelMapPopulated) {
                Chunk hold = chunksToDraw.Dequeue();
                hold.CreateMesh();
            } else {
                Chunk hold = chunksToDraw.Dequeue();
                chunksToDraw.Enqueue(hold);
            }
        }
        

        // Enable debug
        if (Input.GetKeyDown(KeyCode.F3))
            debugScreen.SetActive(!debugScreen.activeSelf);
    }

    void CreateChunk()
    {
        ChunkCoord c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        chunkMap[new ChunkCoord(c.x, c.z)].Init();
    }

    void GenerateWorld() {
        for(int x = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; x < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; x++) {
            for(int z = (Voxel.worldSizeInChunks / 2) - Voxel.ViewDistanceInChunks; z < (Voxel.worldSizeInChunks / 2) + Voxel.ViewDistanceInChunks; z++) {
                ChunkCoord newChunk = new ChunkCoord(x, z);

                chunkMap.TryAdd(newChunk, new Chunk(newChunk, this));
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
                        var a = chunkMap[item];
                        chunkMap.TryRemove(item, out a);
                    }
                }
            }
        }
    }

    int DistBetweenPointsSQ(ChunkCoord p1, ChunkCoord p2)
    {
        return ((p1.x - p2.x) * (p1.x - p2.x)) + (p1.z - p2.z) * (p1.z - p2.z);
    }

    void UpdateChunks() {
        bool updated = false;
        int counter = 0;

        lock (ChunkUpdateThreadLock)
        {
            while(!updated && counter < chunksToUpdate.Count-1) { 
                if(chunksToUpdate.First.Value.isEditable) {
                    Chunk hold = chunksToUpdate.First.Value;
                    if (!hold.isInitialized && !hold.isVoxelMapPopulated)
                    {
                        chunksToUpdate.RemoveFirst();
                        chunksToUpdate.AddLast(hold);
                        break;
                    }

                    chunksToUpdate.RemoveFirst();
                    hold.UpdateChunk();
                    activeChunks.Add(hold.coord);
                    updated = true;
                } else {
                    Chunk hold = chunksToUpdate.First.Value;
                    chunksToUpdate.RemoveFirst();
                    chunksToUpdate.AddLast(hold);
                    counter += 1;
                }
            }
        }
    }

    void ThreadedUpdate()
    {
        while (true)
        {
            // Loop through chunks and init the voxel map
            foreach (var c in chunkMap.Keys)
            {
                if (!chunkMap[c].isVoxelMapPopulated && chunkMap[c].isInitialized)
                {
                    chunkMap[c].PopulateVoxelMap();
                }
            }

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
                    chunkMap.TryAdd(newChunkCoord, new Chunk(c, this));
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
                        chunkMap.TryAdd(currChunk, new Chunk(currChunk, this));
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

     public bool CheckIfVoxelTransparent (Vector3 pos)
    {
        ChunkCoord thisChunk = new ChunkCoord(pos);



        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > Voxel.ChunkHeight)
            return false;

        if (chunkMap.ContainsKey(thisChunk) && chunkMap[thisChunk].isVoxelMapPopulated)
            return blocktypes[chunkMap[thisChunk].GetVoxelFromGlobalVector3(pos)].isTransparent;

        return blocktypes[GetVoxel(pos)].isTransparent;
    }

    public static float PerlinNoise3D(float x, float y, float z) {
        y += 1;
        z += 2;
        float xy = _perlin3DFixed(x, y);
        float xz = _perlin3DFixed(x, z);
        float yz = _perlin3DFixed(y, z);
        float yx = _perlin3DFixed(y, x);
        float zx = _perlin3DFixed(z, x);
        float zy = _perlin3DFixed(z, y);
        return xy * xz * yz * yx * zx * zy;
    }
    static float _perlin3DFixed(float a, float b) {
        return Mathf.Sin(Mathf.PI * Noise.Get2DPerlin(new Vector2(a, b), 0.0f, 0.9f));

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

        // BIOME SELECTION PASS

        int index = 0;
        float value = Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 142124, 0.05f);
        float prob_off = 0;

        float hold_terrainHeight = 0;
        float hold_terrainScale = 0;
        float hold_solidGroundHeight = 0;

        for(int i = 0; i < biomes.Length; i++) {
            prob_off += biomes[i].biomeProbability;
            if(Math.Abs(value - prob_off) < 0.05 && Math.Abs(prob_off - 1.0f) > 0.05) {
                if (value < prob_off && index + 1 != biomes.Length) {
                    float distance = (prob_off-value)/0.05f;
                    float distance2 = 1.0f - distance;

                    hold_terrainHeight = (biomes[i].terrainHeight * distance + biomes[i + 1].terrainHeight * distance2);
                    hold_solidGroundHeight = (biomes[i].solidGroundHeight * distance + biomes[i + 1].solidGroundHeight * distance2);
                    hold_terrainScale = (biomes[i].terrainScale * distance + biomes[i + 1].terrainScale * distance2);
                    index = i;
                    break;
                }
                else{
                    float distance = (value-prob_off)/0.05f;
                    float distance2 = 1.0f - distance;


                    hold_terrainHeight = (biomes[i+1].terrainHeight * distance + biomes[i].terrainHeight * distance2);
                    hold_solidGroundHeight = (biomes[i+1].solidGroundHeight * distance + biomes[i].solidGroundHeight * distance2);
                    hold_terrainScale = (biomes[i+1].terrainScale * distance + biomes[i].terrainScale * distance2);
                    index = i + 1;
                    break;
                }
            } if(value <= prob_off) {
                hold_terrainHeight = biomes[i].terrainHeight;
                hold_solidGroundHeight = biomes[i].solidGroundHeight;
                hold_terrainScale = biomes[i].terrainScale;
                index = i;
                break;
            } 
        }

        BiomeAttributes biome = biomes[index];


        // BASIC TERRAIN PASS

        int terrainHeight = Mathf.FloorToInt(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale) * biome.terrainHeight) + (int)hold_solidGroundHeight;
        byte voxelValue = 0;

        if (yPos == terrainHeight) {            
            if (yPos >= (87 + rnd.Next(-1, 1))) {
                // So high up we get snow
                voxelValue = biome.highLevelBlock;
            } else {
                voxelValue = biome.normalLevelBlock;
            }
         }
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = biome.littleBelowNormalBlock;
        else if (yPos > terrainHeight) {
            // I don't wanna use this anymore because i want sky-islands
        }
        else {
            float perlin3dvalue = PerlinNoise3D(pos.x, pos.y, pos.z);
            if (perlin3dvalue > 0.6f) {
                voxelValue = 0;
            }
            else {
                voxelValue = 2;
            }
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

        // Make biome specific structures.

        // TREE PASS
        if(yPos == terrainHeight && biome.biomeName == "Plain") {
            
            if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold) {
                Queue<VoxelMod> hold_queue = new Queue<VoxelMod>();
                // Set voxelValuie = 1, so see what the area will be for the trees
                //voxelValue = 1;
                
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold) {
                    //voxelValue = 5;
                    int height = rnd.Next(biome.minTreeHeight, biome.maxTreeHeight);
                    for(int i = 1; i <= height; i++) {
                        hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x, pos.y + i, pos.z), 6));
                    }
                    for(int i = -2; i <= 2; i++) {
                        for (int j = -2; j <= 2; j++) {
                            for (int z = -1; z <= 2; z++) { 
                                if (i == 0 && j == 0 && z <= 0) continue;
                                hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x + i, pos.y + height + z, pos.z + j), 7));
                            }
                        }
                    }
                    modifications.Enqueue(hold_queue);
                }
            }
        }
        

        // Make cactus
        if(yPos == terrainHeight && biome.biomeName == "Desert") {
            
            if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, 0.99f) > 0.5f) {
                Queue<VoxelMod> hold_queue = new Queue<VoxelMod>();
                // Set voxelValuie = 1, so see what the area will be for the trees
                if(rnd.Next(1,80) == 1) {
                    //voxelValue = 5;
                    int height = rnd.Next(3,4);
                    for(int i = 1; i <= height; i++) {
                        if (i == height) {
                            hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x, pos.y + i, pos.z), 15));
                        }
                        else {
                            hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x, pos.y + i, pos.z), 14));
                        }
                    }
                    modifications.Enqueue(hold_queue);
                }
            }
        }
        
        // Make pyramid in desert

         if(yPos == terrainHeight && biome.biomeName == "Desert") {
            
            if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 570, biome.treeZoneScale) > 0.7) {
                Queue<VoxelMod> hold_queue = new Queue<VoxelMod>();
                // Set voxelValuie = 1, so see what the area will be for the houses
                //voxelValue = 1;
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > 0.95) {
                    //voxelValue = 5;

                    int size = 2*rnd.Next(5, 10);

                    for(int i = 1; i <= size; i++) {
                        for (int j = 1; j <= size; j++) {
                            hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x+i, pos.y, pos.z+j), 5));
                        }
                    }


                    for(int z = 1; z <= size/2; z++) {
                        for (int j = 1 + (z - 1); j <= (size - (z - 1)); j++) {
                            for (int i = 1 + (z - 1); i <= (size - (z - 1)); i++) {
                                hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x+i, pos.y + z, pos.z+j), 16));
                            }
                        }
                    }



                    
                    modifications.Enqueue(hold_queue);
                }
            }
        }

        
        // Make houses
         if(yPos == terrainHeight && biome.biomeName == "Plain") {
            
            if(Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 570, biome.treeZoneScale) > 0.7) {
                Queue<VoxelMod> hold_queue = new Queue<VoxelMod>();
                // Set voxelValuie = 1, so see what the area will be for the houses
                //voxelValue = 1;
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > 0.92) {
                    //voxelValue = 5;


                    // Clear everything inside the house so there isnt any dirt and shit
                    // Doesn't actually work, first generate the map then do the addianl additions
                    for(int i = 2; i <= 4; i++) {
                        for(int j = 2; j <= 4; j++) {
                            for(int z = 2; z <= 4; z++) {
                                hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x+i, pos.y+z, pos.z+j), 0));
                            }
                        }
                    }

                    // Make floor
                    for(int i = 1; i <= 5; i++) {
                        for (int j = 1; j <= 5; j++) {
                            hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x+i, pos.y, pos.z+j), 13));
                        }
                    }

                    // Make the dirt underneath so its even
                    for(int i = 1; i <= 5; i++) {
                        for (int j = 1; j <= 5; j++) {
                            for (int z = -1; z >= -3; z--) {
                                hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x + i, pos.y + z, pos.z + j), 4));
                            }
                        }
                    }

                    // Make around the house
                    for(int i = 1; i <= 5; i++) {
                        for(int j = 1; j <= 5; j++) {
                            int counter = 0;
                            if (i != 1 && i != 5) counter += 1;
                            if (j != 1 && j != 5) counter += 1;
                            if (counter == 2) continue;
                            for(int z = 1; z <= 4; z++) {
                                if (i == 3 && j == 1 && z <= 2) continue;
                                hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x+i, pos.y+z, pos.z+j), 13));
                            }
                        }
                    }

                    // Make the roof
                    for(int i = 2; i <= 4; i++) {
                        for (int j = 2; j <= 4; j++) {
                            hold_queue.Enqueue(new VoxelMod(new Vector3(pos.x+i, pos.y+4, pos.z+j), 13));
                        }
                    }



                    
                    modifications.Enqueue(hold_queue);
                }
            }
        }

        // Island Generation
        if(Math.Abs(yPos - (120 + rnd.Next(-1,1))) <= 1) {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.skyIslandZoneScale) > biome.skyIslandZoneThreshold) {
                voxelValue = 8;
            }
        }  
        
        // Make Lakes


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
    public bool isTransparent;

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