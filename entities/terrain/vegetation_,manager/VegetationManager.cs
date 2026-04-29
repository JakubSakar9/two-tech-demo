using Godot;
using System;

struct ForestChunk
{
    public Vector2I ChunkCoord;
    public int NInstances;
    public Rid[] Rids;
}

public partial class VegetationManager : Node3D
{
    const int N_FOREST_CHUNKS = 9;

    [Export] public Godot.Collections.Array<Mesh> TreeMeshes;
    [Export] public int TreeDensity = 64;
    [Export] public float HeightThreshold = 32.0f;

    private ForestChunk[] _chunks;
    private int _seed = 0;
    private int _treeChunkLimit = 0;

    public override void _Ready()
    {
        base._Ready();
        _treeChunkLimit = TreeDensity * TreeDensity;
        _chunks = new ForestChunk[N_FOREST_CHUNKS];
        for (int i = 0; i < _chunks.Length; i++)
        {
            _chunks[i] = new()
            {
                ChunkCoord =  new(0, 0),
                NInstances = 0,
                Rids = new Rid[_treeChunkLimit]
            };
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        foreach (ForestChunk chunk in _chunks)
        {
            for (int i = 0; i < chunk.NInstances; i++)
            {
                RenderingServer.FreeRid(chunk.Rids[i]);
            }
        }
    }

    public void Generate(HeightMap hm, Vector2I chunkCoord)
    {
        if (TreeMeshes.Count == 0)
        {
            GD.PushWarning("Couldn't find any tree mesh");
            return;
        }
        
        int chunkX = (chunkCoord.X % 3 + 4) % 3;
        int chunkY = (chunkCoord.Y % 3 + 4) % 3;
        int chunkIdx = 3 * chunkY + chunkX;
        float chunkSize = hm.HeightImage.GetWidth() / 3.0f;
        float treeStep = chunkSize / (TreeDensity - 1);
        Vector2 chunkOrigin = chunkSize * (Vector2)chunkCoord;
        BoxMesh debugMesh = new();

        if (_chunks[chunkIdx].NInstances > 0)
        {
            for (int i = 0; i < _chunks[chunkIdx].NInstances; i++)
            {
                RenderingServer.FreeRid(_chunks[chunkIdx].Rids[i]);
            }
        }

        Rid scenario = GetWorld3D().Scenario;

        var treeRid = TreeMeshes[0].GetRid();

        int iIdx = 0;
        for (int x = 0; x < TreeDensity; x++)
        {
            float xOffset = x * treeStep - chunkSize / 2.0f;
            int px = (int)chunkSize + (int)(x * treeStep);
            for (int z = 0; z < TreeDensity; z++)
            {
                int pz = (int)chunkSize + (int)(z * treeStep);
                float hVal = hm.HeightImage.GetPixel(px, pz).R;
                // Start with and if condition that filters out trees that would be too high up
                if (hVal > HeightThreshold)
                {
                    continue;
                }

                _chunks[chunkIdx].Rids[iIdx] = RenderingServer.InstanceCreate();
                RenderingServer.InstanceSetBase(_chunks[chunkIdx].Rids[iIdx], treeRid);
                RenderingServer.InstanceSetScenario(_chunks[chunkIdx].Rids[iIdx], scenario);
                
                float zOffset = z * treeStep - chunkSize / 2.0f;
                float scale = 1.0f * (HeightThreshold - hVal) / HeightThreshold + 1.0f;
                Transform3D transform = new()
                {
                    Basis = Transform.Basis.Scaled(scale * Vector3.One),
                    Origin = new(chunkOrigin.X + xOffset, hVal, chunkOrigin.Y + zOffset)
                };
                RenderingServer.InstanceSetTransform(_chunks[chunkIdx].Rids[iIdx], transform);
                RenderingServer.InstanceSetVisible(_chunks[chunkIdx].Rids[iIdx], true);
                RenderingServer.InstanceSetLayerMask(_chunks[chunkIdx].Rids[iIdx], 1);

                iIdx++;
            }
        }
        GD.Print("Created instances: " + iIdx);
        _chunks[chunkIdx].NInstances = iIdx;
    }
}
