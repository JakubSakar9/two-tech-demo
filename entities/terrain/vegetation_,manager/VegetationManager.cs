using Godot;
using System;

struct ForestPatch
{
    public Vector2I PatchCoord;
    public int NInstances;
    public Rid[] Rids;
}

public partial class VegetationManager : Node3D
{
    const int N_FOREST_PATCHES = 9;

    [Export] public Godot.Collections.Array<Mesh> TreeMeshes;
    [Export] public int TreeDensity = 64;
    [Export] public float HeightThreshold = 32.0f;

    private ForestPatch[] _patches;
    private int _seed = 0;
    private int _treeChunkLimit = 0;

    public override void _Ready()
    {
        base._Ready();
        _treeChunkLimit = TreeDensity * TreeDensity;
        _patches = new ForestPatch[N_FOREST_PATCHES];
        for (int i = 0; i < _patches.Length; i++)
        {
            _patches[i] = new()
            {
                PatchCoord =  new(0, 0),
                NInstances = 0,
                Rids = new Rid[_treeChunkLimit]
            };
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        foreach (ForestPatch chunk in _patches)
        {
            for (int i = 0; i < chunk.NInstances; i++)
            {
                RenderingServer.FreeRid(chunk.Rids[i]);
            }
        }
    }

    public void GenerateInitial(HeightMap hm, Vector2 chunkCoord)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Generate(hm, new Vector2I(x, y), chunkCoord);
            }
        }
    }

    private void Generate(HeightMap hm, Vector2I patchCoord, Vector2 chunkCoord)
    {
        if (TreeMeshes.Count == 0)
        {
            GD.PushWarning("Couldn't find any tree mesh");
            return;
        }
        
        int patchX = (patchCoord.X % 3 + 4) % 3;
        int patchY = (patchCoord.Y % 3 + 4) % 3;
        int patchIdx = 3 * patchY + patchX;
        int imWidth = (int)hm.HeightImage.GetWidth();
        float patchSize = hm.HeightImage.GetWidth() / 3.0f;
        float treeStep = patchSize / TreeDensity;
        Vector2 patchOrigin = patchSize * (Vector2)patchCoord;

        if (_patches[patchIdx].NInstances > 0)
        {
            for (int i = 0; i < _patches[patchIdx].NInstances; i++)
            {
                RenderingServer.FreeRid(_patches[patchIdx].Rids[i]);
            }
        }

        Rid scenario = GetWorld3D().Scenario;

        var treeRid = TreeMeshes[0].GetRid();

        int iIdx = 0;
        for (int x = 0; x < TreeDensity; x++)
        {
            float xOffset = x * treeStep - patchSize / 2.0f;
            int px = (int)(patchOrigin.X + patchSize) + (int)(x * treeStep);
            px = Math.Min(px, imWidth);
            for (int z = 0; z < TreeDensity; z++)
            {
                int pz = (int)(patchOrigin.Y + patchSize) + (int)(z * treeStep);
                pz = Math.Min(pz, imWidth);
                float hVal = hm.HeightImage.GetPixel(px, pz).R;
                // Start with and if condition that filters out trees that would be too high up
                if (hVal > HeightThreshold)
                {
                    continue;
                }

                _patches[patchIdx].Rids[iIdx] = RenderingServer.InstanceCreate();
                RenderingServer.InstanceSetBase(_patches[patchIdx].Rids[iIdx], treeRid);
                RenderingServer.InstanceSetScenario(_patches[patchIdx].Rids[iIdx], scenario);
                
                float zOffset = z * treeStep - patchSize / 2.0f;
                float scale = 1.0f * (HeightThreshold - hVal) / HeightThreshold + 1.0f;
                Transform3D transform = new()
                {
                    Basis = Transform.Basis.Scaled(scale * Vector3.One),
                    Origin = new(patchOrigin.X + xOffset, hVal, patchOrigin.Y + zOffset)
                };
                RenderingServer.InstanceSetTransform(_patches[patchIdx].Rids[iIdx], transform);
                RenderingServer.InstanceSetVisible(_patches[patchIdx].Rids[iIdx], true);
                RenderingServer.InstanceSetLayerMask(_patches[patchIdx].Rids[iIdx], 1);

                iIdx++;
            }
        }
        GD.Print("Created instances: " + iIdx);
        _patches[patchIdx].NInstances = iIdx;
    }
}
