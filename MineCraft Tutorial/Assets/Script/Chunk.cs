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

    bool[,,] voxelMap = new bool[Voxel.ChunkWidth, Voxel.ChunkHeight, Voxel.ChunkWidth];
        
    void AddVoxelDataToChunk(Vector3 pos) {
        for (int p = 0; p < 6; p++) {

            // Only draw faces if there isn't another face there
            if(!CheckVoxel(pos + Voxel.faceChecks[p])) {

                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 0]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 1]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 2]]);
                vertices.Add(pos + Voxel.voxelVerts[Voxel.voxelTris[p, 3]]);
                uvs.Add(Voxel.voxelUvs[0]);
                uvs.Add(Voxel.voxelUvs[1]);
                uvs.Add(Voxel.voxelUvs[2]);
                uvs.Add(Voxel.voxelUvs[3]);
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
                    voxelMap[i, j, z] = true;
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
        return voxelMap[x, y, z];
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

    void Start() {
        PopulateVoxelMap();
        CreateMeshData();
        CreateMesh();
    }

}
