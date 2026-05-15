using Godot;
using System;

public partial class SunLight : DirectionalLight3D
{
	[Export] public WorldEnvironment Environment;
	[Export] public Color SkyClearColor;
	[Export] public Color SkyCloudyColor;
	[Export] public Color HorizonCloudyColor;
	[Export] public float CloudyShadowOpacity = 0.4f;
	[Export] public float ClearShadowOpacity = 0.9f;

	private int _lightingQuality = 0;
	private bool _cloudy = false;

    public override void _Ready()
    {
        base._Ready();
		UpdateLighting();
		UpdateSky();
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.IsPressed() && keyEvent.Keycode == Key.F2)
			{
				_lightingQuality = (_lightingQuality + 1) % 3;
				UpdateLighting();
			}
			if (keyEvent.IsPressed() && keyEvent.Keycode == Key.F3)
			{
				_cloudy ^= true;
				UpdateSky();
			}
		}
    }

	private void UpdateLighting()
	{
		if (_lightingQuality == 0)
		{
			ShadowEnabled = false;
		}
		else if (_lightingQuality == 1)
		{
			ShadowEnabled = true;
			DirectionalShadowMode = ShadowMode.Orthogonal;
		}
		else if (_lightingQuality == 2)
		{
			DirectionalShadowMode = ShadowMode.Parallel4Splits;
		}
	}

	private void UpdateSky()
	{
		if (_cloudy)
		{
			(Environment.Environment.Sky.SkyMaterial as ProceduralSkyMaterial).SkyTopColor = SkyCloudyColor;
			(Environment.Environment.Sky.SkyMaterial as ProceduralSkyMaterial).SkyHorizonColor = HorizonCloudyColor;
			(Environment.Environment.Sky.SkyMaterial as ProceduralSkyMaterial).GroundHorizonColor = HorizonCloudyColor;
			Environment.Environment.FogSkyAffect = 0.7f;
			ShadowOpacity = CloudyShadowOpacity;
			LightEnergy = CloudyShadowOpacity;
			LightIndirectEnergy = CloudyShadowOpacity;
			LightVolumetricFogEnergy = CloudyShadowOpacity;
		}
		else
		{
			(Environment.Environment.Sky.SkyMaterial as ProceduralSkyMaterial).SkyTopColor = SkyCloudyColor;
			(Environment.Environment.Sky.SkyMaterial as ProceduralSkyMaterial).SkyHorizonColor = HorizonCloudyColor;
			(Environment.Environment.Sky.SkyMaterial as ProceduralSkyMaterial).GroundHorizonColor = HorizonCloudyColor;
			Environment.Environment.FogSkyAffect = 0.0f;
			ShadowOpacity = ClearShadowOpacity;
			LightEnergy = ClearShadowOpacity;
			LightIndirectEnergy = ClearShadowOpacity;
			LightVolumetricFogEnergy = ClearShadowOpacity;
		}
	}
}
