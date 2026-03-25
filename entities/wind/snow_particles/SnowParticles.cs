using Godot;
using System;


public partial class SnowParticles : Node3D
{
    [Export] public bool DebugWindField = false;
    [Export] QuadMesh DefaultParticlePass;
    [Export] RibbonTrailMesh DebugParticlePass;
    [Export] GradientTexture1D DefaultColorRamp;
    [Export] float ParticleGravityMultiplier = 0.3f;

    private GpuParticles3D _particles;

    public override void _Ready()
    {
        _particles = GetNode<GpuParticles3D>("%Particles");
        if (DebugWindField)
        {
            UseDebugParticles();
        }
        else
        {
            UseDefaultParticles();
        }
    }

    private void UseDefaultParticles()
    {
        _particles.DrawPass1 = DefaultParticlePass;
        GD.Print((float)ProjectSettings.GetSetting("default_gravity"));
        (_particles.ProcessMaterial as ParticleProcessMaterial).Gravity = Vector3.Down * (float)ProjectSettings.GetSetting("physics/3d/default_gravity") * ParticleGravityMultiplier;
        _particles.TrailEnabled = false;
    }

    private void UseDebugParticles()
    {
        _particles.DrawPass1 = DebugParticlePass;
        (_particles.ProcessMaterial as ParticleProcessMaterial).Gravity = Vector3.Zero;
        _particles.TrailEnabled = true;
    }
}
