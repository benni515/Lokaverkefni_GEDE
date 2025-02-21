﻿using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class Chunk : MonoBehaviour {
    public ChunkCoord coord;

    GameObject chunkObject;
    MeshRenderer meshrenderer;
    MeshFilter meshfilter;
    
    private int vertexIndex = 0;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();


    private List<int> transparentTriangles = new List<int>();
    private Material[] materials = new Material[2];

    public byte[,,] voxelMap = new byte[Voxel.ChunkWidth, Voxel.ChunkHeight, Voxel.ChunkWidth];

    public Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    World world;

    private bool _isActive;
    public bool isVoxelMapPopulated = false;
    private bool _isUpdating = false;
    public bool isInitialized = false;

    public Vector3 position;

    public Chunk(ChunkCoord _coord, World _world){
        coord = _coord;
        world = _world;
        _isActive = true;
    }

    // Initialize chunk
    public void Init()
    {
        chunkObject = new GameObject();
        meshfilter = chunkObject.AddComponent<MeshFilter>();
        meshrenderer = chunkObject.AddComponent<MeshRenderer>();

        materials[0] = world.material;
        materials[1] = world.transparentMaterial;
        meshrenderer.materials = materials;

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(coord.x * Voxel.ChunkWidth, 0f, coord.z * Voxel.ChunkWidth);
        chunkObject.name = "Chunk " + coord.x + ", " + coord.z;
        position = chunkObject.transform.position;

        isInitialized = true;
    }

    void UpdateMeshData(Vector3 pos) {
        byte blockID = voxelMap[(int)pos.x, (int)pos.y, (int)pos.z];
        bool isTransparent = world.blocktypes[blockID].isTransparent;


        for (int p = 0; p < 6; p++) {

            // Only draw faces if there isn't another face there
            if(CheckVoxelTransparent(pos + Voxel.faceChecks[p]) || !CheckVoxel(pos + Voxel.faceChecks[p])) {


                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 0]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 1]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 2]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 3]]);

                // Add correct face to block
                AddTexture(world.blocktypes[blockID].GetTextureID(p));

                if (!isTransparent) {
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 3);
                } else {
                    transparentTriangles.Add(vertexIndex);
                    transparentTriangles.Add(vertexIndex + 1);
                    transparentTriangles.Add(vertexIndex + 2);
                    transparentTriangles.Add(vertexIndex + 2);
                    transparentTriangles.Add(vertexIndex + 1);
                    transparentTriangles.Add(vertexIndex + 3);
                }
                vertexIndex += 4;

            }
        }
    }

    public void CreateMesh() {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();

        mesh.subMeshCount = 2;
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetTriangles(transparentTriangles.ToArray(), 1);

        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();

        meshfilter.mesh = mesh;
    }

    // ONLY TO BE CALLED WHEN INITING THE CHUNK
    public void PopulateVoxelMap() {
        for(int x = 0; x < Voxel.ChunkWidth; x++) {
            for (int y = 0; y < Voxel.ChunkHeight; y++) {
                for (int z = 0; z < Voxel.ChunkWidth; z++) {
                    // Heavy stuff
                    voxelMap[x, y, z] = world.GetVoxel(new Vector3(x, y, z) + position);
                }
            }
        }

        isVoxelMapPopulated = true;

        lock (world.ChunkUpdateThreadLock)
        {
            world.chunksToUpdate.AddLast(this);
        }
    }

    // Is the chunk active
    public bool isActive {
        get { return _isActive;  }
        set {
            _isActive = value;
            if (chunkObject != null)
                chunkObject.SetActive(value);
            }
    }

    bool IsVoxelInChunk(int x, int y, int z) {
        if(x < 0 || x >= Voxel.ChunkWidth || y < 0 || y >= Voxel.ChunkHeight || z < 0 || z >= Voxel.ChunkWidth) {
            return false;
        }
        return true;
    }

    public bool isEditable
    {
        get { 
            if (!isVoxelMapPopulated && !_isUpdating)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    public void EditVoxel(Vector3 pos, byte newID) {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        if(newID == 0) {
            // If we are chaning this with an air block, then we must be destroying it
            // So let's create an object.
            Instantiate(world._pickup_wood, pos + new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity);
        }

        voxelMap[xCheck, yCheck, zCheck] = newID;

        // Lock and update
        lock(world.ChunkUpdateThreadLock)
        {
            world.chunksToUpdate.AddFirst(this);
            UpdateSurroundingVoxels(xCheck, yCheck, zCheck);
        }
    }

    void UpdateSurroundingVoxels(int x, int y, int z) {
        Vector3 thisVoxel = new Vector3(x, y, z);
        for(int p = 0; p < 6; p++) {
            Vector3 currentVoxel = thisVoxel + Voxel.faceChecks[p];

            if(!IsVoxelInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z)) {
                world.chunksToUpdate.AddFirst(world.GetChunkFromVector3(currentVoxel + position));
            }
        }
    }

    bool CheckVoxel(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if(!IsVoxelInChunk(x, y, z)) {
            return world.CheckForVoxel(pos);
        }

        return world.blocktypes[voxelMap[x,y,z]].isSolid;
    }

    bool CheckVoxelTransparent(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if(!IsVoxelInChunk(x, y, z)) {
            return world.CheckIfVoxelTransparent(pos);
        }

        return world.blocktypes[voxelMap[x, y, z]].isTransparent;
    }

    public byte GetVoxelFromGlobalVector3 (Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        return voxelMap[xCheck, yCheck, zCheck];
    }

    public void UpdateChunk() {

        while(modifications.Count > 0) {
            VoxelMod v = modifications.Dequeue();
            Vector3 pos = v.position -= position;
            voxelMap[(int)pos.x, (int)pos.y, (int)pos.z] = v.id;
        }

        _isUpdating = true;
        ClearMeshData();

        for (int y = 0; y < Voxel.ChunkHeight; y++) {
            for(int x = 0; x < Voxel.ChunkWidth; x++) {
                for (int z = 0; z < Voxel.ChunkWidth; z++) {
                    // Only render if block is solid
                    if (world.blocktypes[voxelMap[x, y, z]].isSolid)
                        UpdateMeshData(new Vector3(x, y, z));
                }
            }
        }

        _isUpdating = false;
        world.chunksToDraw.Enqueue(this);
    }
    
    void ClearMeshData() {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
    }

    void AddTexture(int textureID) {
        float y = textureID / Voxel.TextureAtlasSizeInBlocks;
        float x = textureID - (y * Voxel.TextureAtlasSizeInBlocks);

        x *= Voxel.NormalizeBlockTextureSize;
        y *= Voxel.NormalizeBlockTextureSize;

        y = 1f - y - Voxel.NormalizeBlockTextureSize;

        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + Voxel.NormalizeBlockTextureSize));
        uvs.Add(new Vector2(x + Voxel.NormalizeBlockTextureSize, y));
        uvs.Add(new Vector2(x + Voxel.NormalizeBlockTextureSize, y + Voxel.NormalizeBlockTextureSize));
    }

}

public class ChunkCoord
{
    public int x;
    public int z;

    public ChunkCoord()
    {
        x = 0;
        z = 0;
    }

    public ChunkCoord (int _x, int _z) {
        x = _x;
        z = _z;
    }

    public ChunkCoord (Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int zCheck = Mathf.FloorToInt(pos.z);

        x = xCheck / Voxel.ChunkWidth;
        z = zCheck / Voxel.ChunkWidth;
    }

    public bool Equals (ChunkCoord other) {
        if (other == null)
            return false;
        else if (other.x == x && other.z == z)
            return true;
        else
            return false;
    }

    // NEEDS TO BE ACTUALLY FIXED, THESE THREE ARE USED TO LOOK UP IF TWO OBJECTS ARE THE SAME!!

    public override int GetHashCode()
    {
        return (1 << 15) * x +  z;
    }
    public override bool Equals(object obj)
    {
        return obj.GetHashCode() == GetHashCode();
    }
    /*
    public bool Equals(ChunkCoord obj)
    {
        return obj.GetHashCode() == GetHashCode();
    }*/

}