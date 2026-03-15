using Godot;
using System;
using System.Threading;

public partial class WindGenerator : Node
{
	[Signal] public delegate void ComputeDoneEventHandler();
	
	const string SHADER_PATH = "res://shaders/wind_compute.glsl";
	const int WINDTEX_SWAP_COUNT = 4;

	private RenderingDevice _device;
	private Rid _shader;
	private Rid _pipeline;
	private Rid[] _windTextures;
	private Rid _heightTexture;
	private Rid _uniformSet;

	private Image _heightImage;
	private int _texSize;

	public void Initialize(int texSize)
	{
		_device = RenderingServer.CreateLocalRenderingDevice();
		_texSize = texSize;
		InitShader();
		InitWindTextures();
		InitHeightTexture();
	}

	public void BindWindTextureRid(uint idx, ref Texture3Drd tex)
	{
		tex.TextureRdRid = _windTextures[idx];
	}

	public void Generate(uint idx, ref Image heightImage)
	{
		_heightImage = heightImage;
		DispatchCompute(idx);
		_device.Sync();
	}

	public void GenerateAsync(uint idx, ref Image heightImage)
	{
		_heightImage = heightImage;
		DispatchCompute(idx);
		Thread waitThread = new(() => {
			_device.Sync();
			CallDeferred("emit_signal", SignalName.ComputeDone);
		});
	}

	private void InitShader()
	{
		var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH);
		var shaderBytecode = shaderFile.GetSpirV();
		_shader = _device.ShaderCreateFromSpirV(shaderBytecode);
	}

	private void InitWindTextures()
	{
		_windTextures = new Rid[WINDTEX_SWAP_COUNT];
		var format = new RDTextureFormat {
			Width = (uint)_texSize,
			Height = 1,
			Depth = (uint)_texSize,
			Format = RenderingDevice.DataFormat.R32G32B32Sfloat,
			TextureType = RenderingDevice.TextureType.Type3D,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CpuReadBit
                | RenderingDevice.TextureUsageBits.CanCopyFromBit
                | RenderingDevice.TextureUsageBits.SamplingBit
		};
		var view = new RDTextureView();

		byte[] initData = new byte[3 * sizeof(float) * _texSize * _texSize];
		for (uint i = 0; i < WINDTEX_SWAP_COUNT; i++)
		{
			_windTextures[i] = _device.TextureCreate(format, view, [initData]);
		}
	}

	private void InitHeightTexture()
	{
		_heightImage = Image.CreateEmpty(_texSize, _texSize, true, Image.Format.Rf);
		var format = new RDTextureFormat
		{
			Width = (uint)_texSize,
			Height = (uint)_texSize,
			Format = RenderingDevice.DataFormat.R32Sfloat,
			UsageBits = RenderingDevice.TextureUsageBits.SamplingBit
				| RenderingDevice.TextureUsageBits.CanUpdateBit,
			Mipmaps = (uint)_heightImage.GetMipmapCount()
		};
		var view = new RDTextureView();
		_heightTexture = _device.TextureCreate(format, view, [_heightImage.GetData()]);
	}

	private void DispatchCompute(uint idx)
	{
		_device.TextureUpdate(_heightTexture, 0, _heightImage.GetData());
		BindUniforms(idx);
		uint xGroups = (uint)_texSize / 16;
		uint yGroups = 1;
		uint zGroups = (uint)_texSize / 16;

		var computeList = _device.ComputeListBegin();
		_device.ComputeListBindComputePipeline(computeList, _pipeline);
		_device.ComputeListBindUniformSet(computeList, _uniformSet, 0);
		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
		_device.ComputeListEnd();
		_device.Submit();
	}

	private void BindUniforms(uint idx)
	{
		var windTexUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = 0
		};
		windTexUniform.AddId(_windTextures[idx]);
		var heightmapUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = 1
		};
		heightmapUniform.AddId(_heightTexture);

		Godot.Collections.Array<RDUniform> uniforms = [windTexUniform, heightmapUniform];
		if (_uniformSet.IsValid && _device.UniformSetIsValid(_uniformSet)) _device.FreeRid(_uniformSet);
		_uniformSet = _device.UniformSetCreate(uniforms, _shader, 0);
	}
}
