using Godot;
using System;

public partial class DisplacementData : GodotObject
{
    const string DEBUG_DISPLACEMENT_PATH = "res://assets/textures/test_displacement.png";

    public Image TexDisplacement = null;
    public Image TexDisplacedPatches = null;

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
}
