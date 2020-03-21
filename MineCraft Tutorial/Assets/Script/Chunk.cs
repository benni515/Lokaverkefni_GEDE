using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour{
    public ChunkCoord coord;

    GameObject chunkObject;
    MeshRenderer meshrenderer;
    MeshFilter meshfilter;  

    private int vertexIndex = 0;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    byte[,,] voxelMap = new byte[Voxel.ChunkWidth, Voxel.ChunkHeight, Voxel.ChunkWidth];

    World world;

    public Chunk(ChunkCoord _coord, World _world){
        coord = _coord;
        world = _world;
        chunkObject = new GameObject();
        meshfilter = chunkObject.AddComponent<MeshFilter>();
        meshrenderer = chunkObject.AddComponent<MeshRenderer>();

        meshrenderer.material = world.material;
        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(coord.x * Voxel.ChunkWidth, 0f, coord.z * Voxel.ChunkWidth);
        chunkObject.name = "Chunk " + coord.x + ", " + coord.z;

        PopulateVoxelMap();
        CreateMeshData();
        CreateMesh();
    }

    void AddVoxelDataToChunk(Vector3 pos) {
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

    }

    // Is the chunk active
    public bool isActive {
        get { return chunkObject.activeSelf;  }
        set { chunkObject.SetActive(value); }
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

    bool CheckVoxel(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if(!IsVoxelInChunk(x, y, z)) {
            return world.blocktypes[world.GetVoxel(pos + position)].isSolid;
        }

        return world.blocktypes[voxelMap[x, y, z]].isSolid;
    }

    void CreateMeshData() {

        for(int i = 0; i < Voxel.ChunkWidth; i++) {
            for (int j = 0; j < Voxel.ChunkHeight; j++) {
                for (int z = 0; z < Voxel.ChunkWidth; z++) {
                    AddVoxelDataToChunk(new Vector3(i, j, z));
                }
            }
        }
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

    public ChunkCoord(int _x, int _z) {
        x = _x;
        z = _z;
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