using Godot;
using System;
using System.Diagnostics;

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
    [Export] public Godot.Collections.Array<float> SnowHeightLimits;
    [Export] public Texture2D BlueNoise;
    [Export] public int TreeCountLimit = 8192;
    [Export] public int TreeScatterDetail = 512;
    [Export] public float PerturbStrength = 1.0f;
    [Export] public float NoiseThreshold = 0.98f;
    [Export] public float HeightThreshold = 32.0f;
    [Export] public float GradientThreshold = 0.5f;

    private ForestPatch[] _patches;
    private Godot.Collections.Array<Vector2I> _treePositions;
    private Vector2I _centralPatchCoord;

    public override void _Ready()
    {
        base._Ready();
        _patches = new ForestPatch[N_FOREST_PATCHES];
        for (int i = 0; i < _patches.Length; i++)
        {
            _patches[i] = new()
            {
                PatchCoord =  new(0, 0),
                NInstances = 0,
                Rids = new Rid[TreeCountLimit]
            };
        }
        _centralPatchCoord = Vector2I.Zero;
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

    public void GenerateInitial(HeightMap hm)
    {
        GenerateTreePositions();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Generate(hm, new Vector2I(x, y));
            }
        }
    }

    public void GenerateRow(HeightMap hm, Vector2I direction)
    {
        _centralPatchCoord += direction;

        if (direction.X == 0)
        {
            for (int i = -1; i <= 1; i++)
            {
                Generate(hm, _centralPatchCoord + direction + new Vector2I(i, 0));
            }
        }
        else
        {
            for (int i = -1; i <= 1; i++)
            {
                Generate(hm, _centralPatchCoord + direction + new Vector2I(0, i));
            }
        }
    }

    private void GenerateTreePositions()
    {
        Image blueNoiseImage = BlueNoise.GetImage();
        int bnSize = blueNoiseImage.GetWidth();
        _treePositions = [];

        for (int x = 0; x < bnSize; x++)
        {
            for (int y = 0; y < bnSize; y++)
            {
                if (blueNoiseImage.GetPixel(x, y).R <= NoiseThreshold) continue;
                for (int i = x; i < TreeScatterDetail; i += bnSize)
                {
                    for (int j = y; j < TreeScatterDetail; j += bnSize)
                    {
                        _treePositions.Add(new Vector2I(i, j));
                    }
                }
            }
        }
        GD.Print("N tree positions: " + _treePositions.Count);
    }

    private void Generate(HeightMap hm, Vector2I patchCoord)
    {
        Stopwatch stw = new();
        stw.Start();
        if (TreeMeshes.Count == 0)
        {
            GD.PushWarning("Couldn't find any tree mesh");
            return;
        }
        
        int patchX = (patchCoord.X % 3 + 4) % 3;
        int patchY = (patchCoord.Y % 3 + 4) % 3;
        int patchIdx = 3 * patchY + patchX;
        int imWidth = hm.HeightImage.GetWidth();
        float patchSize = hm.HeightImage.GetWidth() / 3.0f;
        float treeStep = patchSize / TreeScatterDetail;
        Vector2 patchOrigin = patchSize * (Vector2)patchCoord;
        Vector2 patchOffset = patchSize * (Vector2)(patchCoord - _centralPatchCoord);

        if (_patches[patchIdx].NInstances > 0)
        {
            for (int i = 0; i < _patches[patchIdx].NInstances; i++)
            {
                RenderingServer.FreeRid(_patches[patchIdx].Rids[i]);
            }
        }

        Rid scenario = GetWorld3D().Scenario;

        int iIdx = 0;
        foreach (Vector2I pos in _treePositions)
        {
            float xBase = pos.X * treeStep;
            int px = (int)(patchOffset.X + patchSize) + (int)xBase;
            px = Math.Clamp(px, 0, imWidth - 1);

            float zBase = pos.Y * treeStep;
            int pz = (int)(patchOffset.Y + patchSize) + (int)zBase;
            pz = Math.Clamp(pz, 0, imWidth - 1);
            float h = hm.HeightImage.GetPixel(px, pz).R;
            if (h > HeightThreshold)
            {
                continue;
            }

            int imgW = hm.HeightImage.GetWidth();
            int imgH = hm.HeightImage.GetHeight();

            float hmx = hm.HeightImage.GetPixel(Mathf.Max(px - 1, 0),        pz).R;
            float hpx = hm.HeightImage.GetPixel(Mathf.Min(px + 1, imgW - 1), pz).R;
            float hmz = hm.HeightImage.GetPixel(px, Mathf.Max(pz - 1, 0)       ).R;
            float hpz = hm.HeightImage.GetPixel(px, Mathf.Min(pz + 1, imgH - 1)).R;

            float gradPX = Mathf.Abs(hpx - h);
            float gradMX = Mathf.Abs(hmx - h);
            float gradPZ = Mathf.Abs(hpz - h);
            float gradMZ = Mathf.Abs(hmz - h);
            float gradientMagnitude = Mathf.Sqrt(gradPX * gradMX + gradPZ * gradMZ);

            if (gradientMagnitude > GradientThreshold)
            {
                continue;
            }

            int treeIdx;
            for (treeIdx = 0; treeIdx < TreeMeshes.Count; treeIdx++)
            {
                if (h < SnowHeightLimits[treeIdx]) break;
            }
            var treeRid = TreeMeshes[treeIdx].GetRid();

            _patches[patchIdx].Rids[iIdx] = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetBase(_patches[patchIdx].Rids[iIdx], treeRid);
            RenderingServer.InstanceSetScenario(_patches[patchIdx].Rids[iIdx], scenario);
            
            float xOffset = xBase - patchSize / 2.0f;
            float zOffset = zBase - patchSize / 2.0f;
            float scale = 1.0f * (HeightThreshold - h) / HeightThreshold + 1.0f;
            Transform3D transform = new()
            {
                Basis = Transform.Basis.Scaled(scale * Vector3.One),
                Origin = new(patchOrigin.X + xOffset, h, patchOrigin.Y + zOffset)
            };
            RenderingServer.InstanceSetTransform(_patches[patchIdx].Rids[iIdx], transform);
            RenderingServer.InstanceSetVisible(_patches[patchIdx].Rids[iIdx], true);
            RenderingServer.InstanceSetLayerMask(_patches[patchIdx].Rids[iIdx], 1);

            iIdx++;

            if (iIdx == TreeCountLimit)
            {
                GD.PushWarning("Tree limit exceeded! Either increase the limit or set the noise threshold to a higher value.");
                return;
            }
        }
        _patches[patchIdx].NInstances = iIdx;
        stw.Stop();
        GD.Print("Tree patch generated in: " + stw.Elapsed.TotalMilliseconds + "ms");
    }
}
