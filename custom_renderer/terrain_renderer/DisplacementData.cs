using Godot;
using System;

public partial class DisplacementData : GodotObject
{
    const string DEBUG_DISPLACEMENT_PATH = "res://assets/textures/test_displacement.png";

    public Image TexDisplacedPatches = null;
    public Image TexDisplacement = null;
    public Rid TexDisplacedPatchesRes;
    public Rid TexDisplacementRes;

    public DisplacementData()
    {
        TexDisplacement = ResourceLoader.Load<Image>(DEBUG_DISPLACEMENT_PATH);
        TexDisplacedPatches = new Image();
    }

    public void ComputeDisplacedPatches(int gridSize)
    {
        int dispSize = TexDisplacement.GetSize().X;
        byte[] displacementData = TexDisplacement.GetData();
        byte[] displacedPatchesData = new byte[gridSize * gridSize];
        for (int i = 0; i < dispSize; i++)
        {
            int y = gridSize * i / dispSize;
            for (int j = 0; j < dispSize; j++)
            {
                int x = gridSize * j / dispSize;
                displacedPatchesData[y * gridSize + x] = Math.Max(displacedPatchesData[y * gridSize + x], (byte) (255 - displacementData[i * dispSize + j]));
            }
        }
        TexDisplacedPatches.SetData(gridSize, gridSize, false, Image.Format.L8, displacedPatchesData);
        TexDisplacedPatches.SavePng("res://assets/textures/displaced_patches.png");
    }

    public void CreateSamplers(ref RenderingDevice rd)
    {
        RDTextureFormat format = new RDTextureFormat();
        format.Format = RenderingDevice.DataFormat.R8Uint;
        format.Width = (uint) TexDisplacedPatches.GetSize().X;
        format.Height = (uint) TexDisplacedPatches.GetSize().Y;
        format.UsageBits = RenderingDevice.TextureUsageBits.SamplingBit;
        format.UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit;
        RDTextureView view = new RDTextureView();
        view.SwizzleR = RenderingDevice.TextureSwizzle.R;
        view.SwizzleG = RenderingDevice.TextureSwizzle.One;
        view.SwizzleB = RenderingDevice.TextureSwizzle.One;
        view.SwizzleA = RenderingDevice.TextureSwizzle.One;
        Godot.Collections.Array<byte[]> texData = [TexDisplacedPatches.GetData()];
        TexDisplacedPatchesRes = rd.TextureCreate(format, view, texData);

        format.Format = RenderingDevice.DataFormat.R8Unorm;
        format.Width = (uint) TexDisplacement.GetSize().X;
        format.Height = (uint) TexDisplacement.GetSize().Y;
        texData = [TexDisplacement.GetData()];
        TexDisplacementRes = rd.TextureCreate(format, view, texData);
    }
}
