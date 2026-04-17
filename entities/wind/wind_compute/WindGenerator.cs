using Godot;
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

public struct WindComputeParameters
{
	public Vector2 BaseWindVelocity;
	public float VenturiStrength;
	public float TopographicStrength;
	public float MaxWindSpeed;
	public float MaxAltitude;
	public float SkyHeightRatio;
	uint _padding;
}

public partial class WindGenerator : Node
{
	[Signal] public delegate void ComputeDoneEventHandler();
	
	const string SHADER_PATH_SURFACE = "res://shaders/wind_surf_compute.glsl";
	const string SHADER_PATH_3D = "res://shaders/wind_3d_compute.glsl";
	const int WINDTEX_SWAP_COUNT = 4;

	[Export] public Vector2 BaseWindVelocity = new Vector2(0.3f, 0.4f);
	[Export] public int LayerCount = 8;
	[Export] public float VenturiStrength = 0.5f;
	[Export] public float TopographicStrength = 0.6f;
	[Export] public float MaxWindSpeed = 32.0f;
	[Export] public float SkyHeightRatio = 0.25f;

	private RenderingDevice _device;
	private Rid _shaderSurface;
    private Rid _shader3D;
    private Rid _pipelineSurface;
	private Rid _pipeline3D;
	private Rid _surfaceTexture;
	private Rid _windTexture;
	private Rid _heightTexture;
	private Rid _uniformSetSurface;
    private Rid _uniformSet3D;

    private WindComputeParameters _params;
	private Terrain _terrain;
	private int _texSize;


	public void Init(int texSize)
	{
		_device = RenderingServer.GetRenderingDevice();
		_texSize = texSize;
		_terrain = GetTree().GetFirstNodeInGroup("terrain") as Terrain;
		InitParams();
		InitShaders();
        InitSurfaceBuffer();
        InitWindTexture();
		InitHeightTexture();
		_pipelineSurface = _device.ComputePipelineCreate(_shaderSurface);
        _pipeline3D = _device.ComputePipelineCreate(_shader3D);
	}

	public void UpdateHeightmap(ref HeightMap heightMap)
	{
		_device.TextureUpdate(_heightTexture, 0, heightMap.bytes);
	}

	public void Generate(long computeList)
	{
		
		DispatchCompute(computeList);
		CopyWindTexture();
	}

	public Rid GetSurfaceTextureRid()
	{
		return _surfaceTexture;
	}

	private void InitParams()
	{
		var tr = GetTree().GetFirstNodeInGroup("terrain") as Terrain;
		_params = new()
		{
			BaseWindVelocity = BaseWindVelocity,
			VenturiStrength = VenturiStrength,
			TopographicStrength = TopographicStrength,
			MaxWindSpeed = MaxWindSpeed,
			MaxAltitude = tr.MaxAltitude,
			SkyHeightRatio = SkyHeightRatio
		};
	}

	private void InitShaders()
	{
		var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH_SURFACE);
		var shaderBytecode = shaderFile.GetSpirV();
		_shaderSurface = _device.ShaderCreateFromSpirV(shaderBytecode);
		
		shaderFile = GD.Load<RDShaderFile>(SHADER_PATH_3D);
		shaderBytecode = shaderFile.GetSpirV();
        _shader3D = _device.ShaderCreateFromSpirV(shaderBytecode);

    }

    private void InitSurfaceBuffer()
    {
		var format = new RDTextureFormat
		{
			Width = (uint)_texSize,
			Height = (uint)_texSize,
			Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CanCopyFromBit
				| RenderingDevice.TextureUsageBits.CpuReadBit,
			Mipmaps = 1,
			TextureType = RenderingDevice.TextureType.Type2D,
		};
		var view = new RDTextureView();
		_surfaceTexture = _device.TextureCreate(format, view);
    }

    private void InitWindTexture()
	{
		var format = new RDTextureFormat
		{
			Width = (uint)_texSize,
			Height = (uint)LayerCount,
			Depth = (uint)_texSize,
			Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CanCopyFromBit,
			Mipmaps = 1,
			TextureType = RenderingDevice.TextureType.Type3D
		};
		var view = new RDTextureView();
		_windTexture = _device.TextureCreate(format, view);
	}

	private void InitHeightTexture()
	{
		byte[] bytes = new byte[4 * sizeof(float) * _texSize * _texSize];
		var format = new RDTextureFormat
		{
			Width = (uint)_texSize,
			Height = (uint)_texSize,
			Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CanUpdateBit,
			Mipmaps = 1
		};
		var view = new RDTextureView();
		_heightTexture = _device.TextureCreate(format, view, [bytes]);
	}

	private void CopyWindTexture()
	{
		var lambda = (byte[] data) =>
		{
			Godot.Collections.Array<Image> images = [];
			int strideBytes = 4 * _texSize * LayerCount;
			for (int i = 0; i < _texSize; i++)
			{
				byte[] layerData = new byte[strideBytes];
				Buffer.BlockCopy(data, i * strideBytes, layerData, 0, strideBytes);
				Image layerImage = Image.CreateFromData(_texSize, LayerCount, false, Image.Format.Rgba8, layerData);
				images.Add(layerImage);
			}
			if (_terrain == null)
			{
				GD.Print("Terrain is null here");
			}
			_terrain.CallDeferred(Terrain.MethodName.SyncWindField, images);
		};
		
		_device.TextureGetDataAsync(_windTexture, 0, Callable.From(lambda));
	}

	private void DispatchCompute(long computeList)
	{
		BindSurfaceUniforms();
        Bind3DUniforms();
        uint xGroups = (uint)_texSize / 8;
		uint yGroups = 1;
		uint zGroups = (uint)_texSize / 8;
		
		_device.ComputeListBindComputePipeline(computeList, _pipelineSurface);
		_device.ComputeListBindUniformSet(computeList, _uniformSetSurface, 0);
		byte[] paramData = ParamsToBytes();
		_device.ComputeListSetPushConstant(computeList, paramData, (uint)paramData.Length);
		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
		_device.ComputeListAddBarrier(computeList);
		
		yGroups = (uint)LayerCount;
		_device.ComputeListBindComputePipeline(computeList, _pipeline3D);
        _device.ComputeListBindUniformSet(computeList, _uniformSet3D, 0);
		_device.ComputeListSetPushConstant(computeList, paramData, (uint)paramData.Length);
		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
    }

	private void BindSurfaceUniforms()
	{
		var heightmapUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = 0
		};
		var windSurfUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = 1
		};
		windSurfUniform.AddId(_surfaceTexture);
		heightmapUniform.AddId(_heightTexture);

		Godot.Collections.Array<RDUniform> uniforms = [heightmapUniform, windSurfUniform];
		if (_uniformSetSurface.IsValid && _device.UniformSetIsValid(_uniformSetSurface)) _device.FreeRid(_uniformSetSurface);
		_uniformSetSurface = _device.UniformSetCreate(uniforms, _shaderSurface, 0);
	}

    private void Bind3DUniforms()
    {
		var heightMapUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = 0
		};
		var windSurfUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = 1
		};
        var wind3DUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 2
        };

		heightMapUniform.AddId(_heightTexture);
		windSurfUniform.AddId(_surfaceTexture);
        wind3DUniform.AddId(_windTexture);

		Godot.Collections.Array<RDUniform> uniforms = [heightMapUniform, windSurfUniform, wind3DUniform];
		if (_uniformSet3D.IsValid && _device.UniformSetIsValid(_uniformSet3D)) _device.FreeRid(_uniformSet3D);
		_uniformSet3D = _device.UniformSetCreate(uniforms, _shader3D, 0);
	}

	private byte[] ParamsToBytes()
	{
		int size = Marshal.SizeOf(_params);
		byte[] output = new byte[size];
		IntPtr ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(_params, ptr, true);
			Marshal.Copy(ptr, output, 0, size);
		}
		finally
		{
			Marshal.FreeHGlobal(ptr);
		}
		return output;
	}
}
