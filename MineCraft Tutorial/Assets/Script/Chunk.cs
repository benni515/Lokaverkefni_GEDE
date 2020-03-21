using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public MeshRenderer meshrenderer;
    public MeshFilter meshfilter;
    

    private int vertexIndex = 0;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    byte[,,] voxelMap = new byte[Voxel.ChunkWidth, Voxel.ChunkHeight, Voxel.ChunkWidth];

    World world;

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
        for(int i = 0; i < Voxel.ChunkWidth; i++) {
            for (int j = 0; j < Voxel.ChunkHeight; j++) {
                for (int z = 0; z < Voxel.ChunkWidth; z++) {
                    // Block can be changed HERE!
                    voxelMap[i, j, z] = 1;
                }
            }
        }

    }

    bool CheckVoxel(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);
        if(x < 0 || x >= Voxel.ChunkWidth || y < 0 || y >= Voxel.ChunkHeight || z < 0 || z >= Voxel.ChunkWidth) {
            return false;
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

    void Start() {
        world = GameObject.Find("World").GetComponent<World>();
        PopulateVoxelMap();
        CreateMeshData();
        CreateMesh();
    }

}
