using Godot;
using System;
using System.Threading;

public partial class WindGenerator : Node
{
	[Signal] public delegate void ComputeDoneEventHandler();
	
	const string SHADER_PATH_SURFACE = "res://shaders/wind_surf_compute.glsl";
	const string SHADER_PATH_3D = "res://shaders/wind_3d_compute.glsl";
	const int WINDTEX_SWAP_COUNT = 4;

	private RenderingDevice _device;
	private Rid _shaderSurface;
    private Rid _shader3D;
    private Rid _pipelineSurface;
	private Rid _pipeline3D;
    private Rid _surfaceBuffer;
    private Rid[] _windBuffers;
	private Rid _heightTexture;
	private Rid _uniformSetSurface;
    private Rid _uniformSet3D;

    private Image _heightImage;
	private int _texSize;
	private int _layerCount;

	public void Initialize(int texSize, int layerCount)
	{
		_device = RenderingServer.CreateLocalRenderingDevice();
		_texSize = texSize;
		_layerCount = layerCount;
		InitShaders();
        InitSurfaceBuffer();
        InitWindBuffers();
		InitHeightTexture();
		_pipelineSurface = _device.ComputePipelineCreate(_shaderSurface);
        _pipeline3D = _device.ComputePipelineCreate(_shader3D);
	}

	public void CopyWindTexture(uint idx, ref ImageTexture3D tex)
	{
		Godot.Collections.Array<Image> images = [];
		uint strideBytes = 4 * sizeof(float) * (uint)_texSize * (uint)_layerCount;
		for (uint i = 0; i < _texSize; i++)
		{
			byte[] layerData = _device.BufferGetData(_windBuffers[idx], i * strideBytes, strideBytes);
			Image layerImage = Image.CreateFromData(_texSize, _layerCount, false, Image.Format.Rgbaf, layerData);
			layerImage.Convert(Image.Format.Rgba8);
			images.Add(layerImage);
		}
		tex.Update(images);
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
        waitThread.Start();
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
		int dataSize = 4 * sizeof(float) * _texSize * _layerCount * _texSize;
		byte[] initData = new byte[dataSize];
		_windBuffers = new Rid[WINDTEX_SWAP_COUNT];
		for (uint i = 0; i < WINDTEX_SWAP_COUNT; i++)
		{
			_windBuffers[i] = _device.StorageBufferCreate((uint)dataSize, initData);
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
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CanUpdateBit,
			Mipmaps = (uint)_heightImage.GetMipmapCount() + 1
		};
		var view = new RDTextureView();
		_heightTexture = _device.TextureCreate(format, view, [_heightImage.GetData()]);
	}

	private void DispatchCompute(uint idx)
	{
		_device.TextureUpdate(_heightTexture, 0, _heightImage.GetData());
		BindSurfaceUniforms();
        Bind3DUniforms(idx);
        uint xGroups = (uint)_texSize / 16;
		uint yGroups = 1;
		uint zGroups = (uint)_texSize / 16;

		var computeList = _device.ComputeListBegin();
		
		_device.ComputeListBindComputePipeline(computeList, _pipelineSurface);
		_device.ComputeListBindUniformSet(computeList, _uniformSetSurface, 0);
		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
		_device.ComputeListAddBarrier(computeList);
		
		yGroups = (uint)_layerCount;
		_device.ComputeListBindComputePipeline(computeList, _pipeline3D);
        _device.ComputeListBindUniformSet(computeList, _uniformSet3D, 0);
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

    private void Bind3DUniforms(uint idx)
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
        var wind3DUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 2
        };

		heightmapUniform.AddId(_heightTexture);
		windSurfUniform.AddId(_surfaceBuffer);
        wind3DUniform.AddId(_windBuffers[idx]);

		Godot.Collections.Array<RDUniform> uniforms = [heightmapUniform, windSurfUniform, wind3DUniform];
		if (_uniformSet3D.IsValid && _device.UniformSetIsValid(_uniformSet3D)) _device.FreeRid(_uniformSet3D);
		_uniformSet3D = _device.UniformSetCreate(uniforms, _shader3D, 0);
	}
}
