using Godot;
using System;

public partial class WindGenerator : Node
{
	const string SHADER_PATH = "res://shaders/wind_compute.glsl";

	private RenderingDevice _device;
	private Rid _shader;
	private Rid _pipeline;
	private Rid _windTex;
	private Rid _heightTex;
	private Rid _heightSampler;
	private Rid _uniformSet;

	private Image _heightImage;

	public override void _Process(double delta)
	{
	}

	public void Initialize()
	{
		_device = RenderingServer.CreateLocalRenderingDevice();
	}

	private void InitShader()
	{
		var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH);
		var shaderBytecode = shaderFile.GetSpirV();
		_shader = _device.ShaderCreateFromSpirV(shaderBytecode);
	}

	private void InitFootprintTexture()
	{
		// WIP
	}
}
