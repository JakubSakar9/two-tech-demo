using Godot;
using System;
using System.Collections.Generic;

public partial class TerrainPatch : StaticBody3D
{
    public Texture2D HeightMap;
    public int SubdivisionCount = 63;
    public float PatchSize = 20.0f;
    public float PatchHeight = 4.0f;
    
    private MeshInstance3D _patchMeshInstance;
    private CollisionShape3D _patchCollider;
    private HeightMapShape3D _heightmapShape;
    private ArrayMesh _patchMesh;

    public override void _Ready()
    {
        base._Ready();
        _patchMeshInstance = new MeshInstance3D();
        _patchMesh = new ArrayMesh();
        AddChild(_patchMeshInstance);

        _heightmapShape = new HeightMapShape3D();
        _patchCollider = new CollisionShape3D();
        _patchCollider.Shape = _heightmapShape;
        AddChild(_patchCollider);
    }

    public void GenerateFromHeightMap(Texture2D heightMap)
    {
        HeightMap = heightMap;

        Godot.Collections.Array surfaceArray = [];
        surfaceArray.Resize((int)Mesh.ArrayType.Max);

        List<Vector3> verts = [];
        List<Vector2> uvs = [];
        List<Vector3> normals = [];
        List<int> indices = [];

        Image hmImage = heightMap.GetImage();
        Image collisionImage = hmImage;
        collisionImage.Convert(Image.Format.Rf);
        collisionImage.Resize((int)PatchSize + 1, (int)PatchSize + 1);
        _heightmapShape.UpdateMapDataFromImage(collisionImage, 0.0f, PatchHeight);

        // Generate indices and uvs
        for (int i = 0; i < SubdivisionCount + 2; i++)
        {
            for (int j = 0; j < SubdivisionCount + 2; j++)
            {
                float u = (float)j / ((float)SubdivisionCount + 1.0f);
                float v = (float)i / ((float)SubdivisionCount + 1.0f);
                uvs.Add(new Vector2(u, v));
                
                float x = (u - 0.5f) * PatchSize;
                float z = (v - 0.5f) * PatchSize;

                // Sample heightmap
                float uScaled = u * (hmImage.GetSize().X - 1);
                float vScaled = v * (hmImage.GetSize().Y - 1);
                Vector2I tl = new Vector2I((int)MathF.Floor(uScaled), (int)MathF.Floor(vScaled));
                Vector2I br = new Vector2I((int)MathF.Ceiling(uScaled), (int)MathF.Ceiling(vScaled));
                
                float h1 = hmImage.GetPixel(tl.X, tl.Y).R;
                float h2 = hmImage.GetPixel(br.X, tl.Y).R;
                float h3 = hmImage.GetPixel(tl.X, br.Y).R;
                float h4 = hmImage.GetPixel(br.X, br.Y).R;

                float s = uScaled - tl.X;
                float t = vScaled - tl.Y;
                float h12 = (1.0f - s) * h1 + s * h2;
                float h23 = (1.0f - s) * h3 + s * h4;
                float yNorm = (1.0f - t) * h12 + t * h23;
                float y = PatchHeight * yNorm;

                verts.Add(new Vector3(x, y, z));
                normals.Add(Vector3.Up); // TODO: Compute normals properly
            }
        }

        GD.Print(_heightmapShape.GetMaxHeight());

        for (int i = 0; i < SubdivisionCount + 1; i++)
        {
            for (int j = 0; j < SubdivisionCount + 1; j++)
            {
                int idx = i * (SubdivisionCount + 2) + j;
                indices.Add(idx);
                indices.Add(idx + 1);
                indices.Add(idx + SubdivisionCount + 2);
                indices.Add(idx + 1);
                indices.Add(idx + SubdivisionCount + 3);
                indices.Add(idx + SubdivisionCount + 2);
            }
        }

        surfaceArray[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        surfaceArray[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        surfaceArray[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        surfaceArray[(int)Mesh.ArrayType.Index] = indices.ToArray();

        _patchMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);
        _patchMeshInstance.Mesh = _patchMesh;
    }

    public MeshInstance3D GetMeshInstance()
    {
        return _patchMeshInstance;
    }
}
