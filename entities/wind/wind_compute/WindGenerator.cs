using Godot;
using System;
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
	[Export] public bool SaveDebugSurfaceTexture = true;

	private RenderingDevice _device;
	private Rid _shaderSurface;
    private Rid _shader3D;
    private Rid _pipelineSurface;
	private Rid _pipeline3D;
    private Rid _surfaceBuffer;
    private Rid _windBuffer;
	private Rid _heightTexture;
	private Rid _uniformSetSurface;
    private Rid _uniformSet3D;

    private WindComputeParameters _params;
	private HeightMap _heightMap;
	private int _texSize;


	public void Init(int texSize)
	{
		_device = RenderingServer.CreateLocalRenderingDevice();
		_texSize = texSize;
		InitParams();
		InitShaders();
        InitSurfaceBuffer();
        InitWindBuffers();
		InitHeightTexture();
		_pipelineSurface = _device.ComputePipelineCreate(_shaderSurface);
        _pipeline3D = _device.ComputePipelineCreate(_shader3D);
	}

	public void CopyWindTexture(ref ImageTexture3D tex, ref Godot.Collections.Array<Image> imgs)
	{
		Godot.Collections.Array<Image> images = [];
		uint strideBytes = 4 * sizeof(float) * (uint)_texSize * (uint)LayerCount;
		for (uint i = 0; i < _texSize; i++)
		{
			byte[] layerData = _device.BufferGetData(_windBuffer, i * strideBytes, strideBytes);
			Image layerImage = Image.CreateFromData(_texSize, LayerCount, false, Image.Format.Rgbaf, layerData);
			layerImage.Convert(Image.Format.Rgba8);
			images.Add(layerImage);
		}
		tex.Update(images);
		imgs = images;
	}

	public void Generate(ref HeightMap heightMap)
	{
		_heightMap = heightMap;
		DispatchCompute();
		_device.Sync();
	}

	public void LoadWindSurface(RenderingDevice device, Rid targetTex)
	{
		uint size = 4 * sizeof(float) * (uint)_texSize * (uint)_texSize;
		byte[] surfaceData = _device.BufferGetData(_surfaceBuffer);
		if (SaveDebugSurfaceTexture)
		{
			Image.CreateFromData(_texSize, _texSize, false, Image.Format.Rgbaf, surfaceData)
			.SaveExr("res://debug_output/wind_surface.exr");
		}
		device.TextureUpdate(targetTex, 0, surfaceData);
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
        int dataSize = 4 * sizeof(float) * _texSize * _texSize;
		byte[] initData = new byte[dataSize];
		_surfaceBuffer = _device.StorageBufferCreate((uint)dataSize, initData);
    }

    private void InitWindBuffers()
	{
		int dataSize = 4 * sizeof(float) * _texSize * LayerCount * _texSize;
		byte[] initData = new byte[dataSize];
		_windBuffer = _device.StorageBufferCreate((uint)dataSize, initData);
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
		_device.TextureUpdate(_heightTexture, 0, _heightMap.bytes);
		BindSurfaceUniforms();
        Bind3DUniforms();
        uint xGroups = (uint)_texSize / 16;
		uint yGroups = 1;
		uint zGroups = (uint)_texSize / 16;

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
        _device.Submit();
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
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 1
		};
		windSurfUniform.AddId(_surfaceBuffer);
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
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 1
		};
        var wind3DUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 2
        };

		heightMapUniform.AddId(_heightTexture);
		windSurfUniform.AddId(_surfaceBuffer);
        wind3DUniform.AddId(_windBuffer);

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
