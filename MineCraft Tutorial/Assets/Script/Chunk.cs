using System.Collections.Generic;
using UnityEngine;

public class Chunk {
    public ChunkCoord coord;

    GameObject chunkObject;
    MeshRenderer meshrenderer;
    MeshFilter meshfilter;  

    private int vertexIndex = 0;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    public byte[,,] voxelMap = new byte[Voxel.ChunkWidth, Voxel.ChunkHeight, Voxel.ChunkWidth];

    World world;

    private bool _isActive;
    public bool isVoxelMapPopulated = false;

    public Chunk(ChunkCoord _coord, World _world, bool generateOnLoad){
        coord = _coord;
        world = _world;
        _isActive = true;

        if (generateOnLoad)
            Init();
    }

    // Initialize chunk
    public void Init()
    {
        chunkObject = new GameObject();
        meshfilter = chunkObject.AddComponent<MeshFilter>();
        meshrenderer = chunkObject.AddComponent<MeshRenderer>();

        meshrenderer.material = world.material;
        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(coord.x * Voxel.ChunkWidth, 0f, coord.z * Voxel.ChunkWidth);
        chunkObject.name = "Chunk " + coord.x + ", " + coord.z;

        PopulateVoxelMap();
        UpdateChunck();
    }

    void UpdateMeshData(Vector3 pos) {
        for (int p = 0; p < 6; p++) {

            // Only draw faces if there isn't another face there
            if(!CheckVoxel(pos + Voxel.faceChecks[p])) {

                byte blockID = voxelMap[(int)pos.x, (int)pos.y, (int)pos.z];

                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 0]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 1]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 2]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 3]]);

                // Add correct face to block
                AddTexture(world.blocktypes[blockID].GetTextureID(p));

                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 3);
                vertexIndex += 4;

            }
        }
    }

    void CreateMesh() {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();

        meshfilter.mesh = mesh;
    }

    void PopulateVoxelMap() {
        for(int x = 0; x < Voxel.ChunkWidth; x++) {
            for (int y = 0; y < Voxel.ChunkHeight; y++) {
                for (int z = 0; z < Voxel.ChunkWidth; z++) {
                    voxelMap[x, y, z] = world.GetVoxel(new Vector3(x, y, z) + position);
                }
            }
        }

        isVoxelMapPopulated = true;
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

    public Vector3 position {
        get { return chunkObject.transform.position; }
    }

    bool IsVoxelInChunk(int x, int y, int z) {
        if(x < 0 || x >= Voxel.ChunkWidth || y < 0 || y >= Voxel.ChunkHeight || z < 0 || z >= Voxel.ChunkWidth) {
            return false;
        }
        return true;
    }

    public void EditVoxel(Vector3 pos, byte newID) {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        voxelMap[xCheck, yCheck, zCheck] = newID;

        // Update surrounding Chunks

        UpdateChunck();
    }

    void UpdateSurroundingVoxels(int x, int y, int z) {
        Vector3 thisVoxel = new Vector3(x, y, z);
        for(int p = 0; p < 6; p++) {
            Vector3 currentVoxel = thisVoxel + Voxel.faceChecks[p];

            if(!IsVoxelInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z)) {
                world.GetChunkFromVector3(currentVoxel + position).UpdateChunck();
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

        return world.blocktypes[voxelMap[x, y, z]].isSolid;
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

    void UpdateChunck() {

        ClearMeshData();

        for(int x = 0; x < Voxel.ChunkWidth; x++) {
            for (int y = 0; y < Voxel.ChunkHeight; y++) {
                for (int z = 0; z < Voxel.ChunkWidth; z++) {
                    // Only render if block is solid
                    if (world.blocktypes[voxelMap[x, y, z]].isSolid)
                        UpdateMeshData(new Vector3(x, y, z));
                }
            }
        }

        CreateMesh();
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

public class ChunkCoord {
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
}