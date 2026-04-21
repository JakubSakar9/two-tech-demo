using Godot;
using System;
using System.Diagnostics;
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

	[Export] public Vector2 BaseWindVelocity = new Vector2(0.3f, 0.4f);
	[Export] public int LayerCount = 8;
	[Export] public float VenturiStrength = 0.5f;
	[Export] public float TopographicStrength = 0.6f;
	[Export] public float MaxWindSpeed = 32.0f;
	[Export] public float SkyHeightRatio = 0.25f;
	[Export] public bool SaveDebugSurfaceTexture = true;

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
	private HeightMap _heightMap;
	private Terrain _terrain;
	private int _texSize;

    public override void _ExitTree()
    {
        base._ExitTree();

		_device.FreeRid(_uniformSet3D);
		_device.FreeRid(_uniformSetSurface);

		_device.FreeRid(_surfaceTexture);
		_device.FreeRid(_windTexture);
		_device.FreeRid(_heightTexture);

		_device.FreeRid(_pipelineSurface);
		_device.FreeRid(_pipeline3D);

		_device.FreeRid(_shaderSurface);
		_device.FreeRid(_shader3D);

		_device.Free();
    }

	public void Init(int texSize, ref Texture3Drd windTexture)
	{
		_device = RenderingServer.GetRenderingDevice();
		_texSize = texSize;
		_terrain = GetTree().GetFirstNodeInGroup("terrain") as Terrain;
		InitParams();
		InitShaders();
        InitSurfaceTexture();
        InitWindTexture();
		InitHeightTexture();
		_pipelineSurface = _device.ComputePipelineCreate(_shaderSurface);
        _pipeline3D = _device.ComputePipelineCreate(_shader3D);

		windTexture.TextureRdRid = _windTexture;
	}

	public void Generate(ref HeightMap heightMap)
	{
		_heightMap = heightMap;
		DispatchCompute();
		CopyWindSurface();
	}

	private void CopyWindSurface()
	{
		var copyLambda = (byte[] data) =>
		{
			Image surfaceImage = Image.CreateFromData(_texSize, _texSize, false, Image.Format.Rgbaf, data);
			if (SaveDebugSurfaceTexture)
			{
				surfaceImage.SaveExr("res://debug_output/wind_surface.exr");
			}
			if (_terrain == null)
			{
				GD.Print("Terrain is null");
			}
			_terrain.CallDeferred(Terrain.MethodName.SyncWindSurface, surfaceImage);
		};

		_device.TextureGetDataAsync(_surfaceTexture, 0, Callable.From(copyLambda));
		if (_terrain == null)
		{
			GD.Print("Terrain is null even here");
		}
	}

	public Rid GetSurfaceTextureRid()
	{
		return _surfaceTexture;
	}

	private void InitParams()
	{
		_params = new()
		{
			BaseWindVelocity = BaseWindVelocity,
			VenturiStrength = VenturiStrength,
			TopographicStrength = TopographicStrength,
			MaxWindSpeed = MaxWindSpeed,
			MaxAltitude = _terrain.MaxAltitude,
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

    private void InitSurfaceTexture()
    {
		var format = new RDTextureFormat
		{
			Width = (uint)_texSize,
			Height = (uint)_texSize,
			Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CanCopyFromBit,
			Mipmaps = 1,
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
				| RenderingDevice.TextureUsageBits.CanCopyFromBit
				| RenderingDevice.TextureUsageBits.SamplingBit,
			Mipmaps = 1,
			TextureType = RenderingDevice.TextureType.Type3D,
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

	private void DispatchCompute()
	{
		_device.TextureUpdate(_heightTexture, 0, _heightMap.Bytes);
		BindSurfaceUniforms();
        Bind3DUniforms();
        uint xGroups = (uint)_texSize / 8;
		uint yGroups = 1;
		uint zGroups = (uint)_texSize / 8;

		var computeList = _device.ComputeListBegin();
		
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

        _device.ComputeListEnd();
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

		var windFormat = _device.TextureGetFormat(_windTexture);
		GD.Print("Wind layers - " + GetPath() + ": " + windFormat.ArrayLayers);

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
