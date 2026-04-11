using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public struct TexturePainterParams
{
	public Vector4 RotationMat;
    public Vector2 CenterLeft;
    public Vector2 CenterRight;
    public float DepthLeft;
    public float DepthRight;
    public uint TextureSize;
	public float DownscaleFactor;
}

public struct TexturePainterBatchParams
{
    public Vector2I ChunkCoord;
    public uint TextureSize;
    public int FootprintCount;
    public float DownscaleFactor;
    uint _padding0;
    uint _padding1;
    uint _padding2;
}

public partial class TexturePainter : Node
{
    [Export] public Texture2D FootprintTexture;
    [Export] public uint TextureSize = 1024;
	[Export] public ChunkPool Pool;

    const string SHADER_PATH = "res://shaders/disp_compute.glsl";
    const string SHADER_BATCH_PATH = "res://shaders/disp_batch_compute.glsl";

    // public Texture2Drd DisplacementTexture;
    public TexturePainterParams Params;
    public TexturePainterBatchParams BatchParams;
    public Vector2I ReconstructedChunk;

    private RenderingDevice _device;
    private Rid _shader;
    private Rid _shaderBatch;
    private Rid _pipeline;
    private Rid _pipelineBatch;
    private Rid _displacementTex;
    private Rid _displacementTexBatch;
    private Rid _fpBuffer;
    private Rid _footprintTex;
    private Rid _footprintSampler;
    private Rid _uniformSet;
    private Rid _uniformSetBatch;

    private Array<RDUniform> _uniforms;
    private Array<RDUniform> _uniformsBatch;
    private RDTextureFormat _format;
    private RDTextureView _view;
    private FootprintStorage _fpStorage;

    private int _reconstructionPhase = 0;
    private bool _reconstructionInProgress;


    public override void _Ready()
    {
        _device = RenderingServer.GetRenderingDevice();
        Params = new()
        {
            TextureSize = TextureSize,
            CenterLeft = new Vector2(0.5f, 0.5f),
            CenterRight = new Vector2(0.5f, 0.5f),
            DepthLeft = 0.0f,
            DepthRight = 0.0f
        };
        BatchParams = new()
        {
            TextureSize = TextureSize,
            FootprintCount = 0
        };

        SetAngle(0.0f);

        RenderingServer.CallOnRenderThread(Callable.From(InitCompute));
    }

    public override void _Process(double delta)
    {
        RenderingServer.CallOnRenderThread(Callable.From(DrawTextures));
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _device.FreeRid(_shader);
        _device.FreeRid(_pipeline);
        _device.FreeRid(_displacementTex);
        _device.FreeRid(_footprintTex);
        _device.FreeRid(_footprintSampler);
        _device.FreeRid(_uniformSet);
    }

    public void SetAngle(float angleRadians)
    {
        Params.RotationMat = new()
        {
            X = Mathf.Cos(angleRadians),
            Y = -Mathf.Sin(angleRadians),
            Z = Mathf.Sin(angleRadians),
            W = Mathf.Cos(angleRadians)
        };
    }

    public void InitPool(uint chunkRange, ref FootprintStorage fpStorage)
    {
        Pool.Initialize(chunkRange, TextureSize, in _device, ref fpStorage);
    }

    private void InitCompute()
	{
		InitShader();
		InitFootprintTexture();
        InitFootprintBuffer();
		_pipeline = _device.ComputePipelineCreate(_shader);
        _pipeline = _device.ComputePipelineCreate(_shaderBatch);
	}

	private void InitShader()
	{
		var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH);
		var shaderBytecode = shaderFile.GetSpirV();
		_shader = _device.ShaderCreateFromSpirV(shaderBytecode);
        shaderFile = GD.Load<RDShaderFile>(SHADER_BATCH_PATH);
        shaderBytecode = shaderFile.GetSpirV();
		_shaderBatch = _device.ShaderCreateFromSpirV(shaderBytecode);
	}

	private void InitFootprintTexture()
	{
		int fpSize = FootprintTexture.GetWidth();
		var format = new RDTextureFormat
		{
			Width = (uint)fpSize,
			Height = (uint)fpSize,
			Format = RenderingDevice.DataFormat.R8Unorm,
			UsageBits = RenderingDevice.TextureUsageBits.SamplingBit,
			Mipmaps = 8
		};
		var view = new RDTextureView();
		var footprintIm = FootprintTexture.GetImage();
		_footprintTex = _device.TextureCreate(format, view, [footprintIm.GetData()]);

        RDSamplerState samplerState = new()
        {
            MinFilter = RenderingDevice.SamplerFilter.Linear,
            MagFilter = RenderingDevice.SamplerFilter.Linear,
			RepeatU = RenderingDevice.SamplerRepeatMode.ClampToBorder,
			RepeatV = RenderingDevice.SamplerRepeatMode.ClampToBorder,
        };
        _footprintSampler = _device.SamplerCreate(samplerState);
	}

    private void InitFootprintBuffer()
    {
        _device.StorageBufferCreate((uint)(_fpStorage.RenderBatchSize * 4 * sizeof(float)));
    }

    private void DrawBatch()
    {
        if (_reconstructionPhase == 0)
        {
            if (!_fpStorage.HasChunkLeft(ReconstructedChunk))
            {
                _reconstructionPhase++;
                return;
            }
            bool res = _fpStorage.PopulateBufferChunkLeft(in _device, ref _fpBuffer, ref BatchParams.FootprintCount, ReconstructedChunk);
            if (res)
            {
                _reconstructionPhase++;
            }
        }
        else
        {
            if (!_fpStorage.HasChunkLeft(ReconstructedChunk))
            {
                _reconstructionPhase = 0;
                _reconstructionInProgress = false;
                return;
            }
            bool res = _fpStorage.PopulateBufferChunkLeft(in _device, ref _fpBuffer, ref BatchParams.FootprintCount, ReconstructedChunk);
            if (res)
            {
                _reconstructionPhase = 0;
                _reconstructionInProgress = false;
            }
        }

        var displacementTexUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        displacementTexUniform.AddId(Pool.GetReconstructedTexture());

        var footprintTexUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.SamplerWithTexture,
			Binding = 1
		};
        footprintTexUniform.AddId(_footprintSampler);
		footprintTexUniform.AddId(_footprintTex);

        var fpBufferUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 2
		};
		fpBufferUniform.AddId(_fpBuffer);

        _uniformsBatch = [displacementTexUniform, footprintTexUniform, fpBufferUniform];
        _uniformSetBatch = _device.UniformSetCreate(_uniformsBatch, _shaderBatch, 0);

        DispatchBatchCompute();

        _device.FreeRid(_uniformSetBatch);
    }

    private void DrawTextures()
    {
		var footprintTexUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.SamplerWithTexture,
			Binding = 1
		};
        footprintTexUniform.AddId(_footprintSampler);
		footprintTexUniform.AddId(_footprintTex);

        Vector2 cl = Params.CenterLeft;
        Vector2 cr = Params.CenterRight;

        List<DTChunk> chunks = Pool.GetTargetChunks();
        foreach (var chunk in chunks)
        {
            _uniforms = [];

			var displacementTexUniform = new RDUniform
			{
				UniformType = RenderingDevice.UniformType.Image,
				Binding = 0
			};
			displacementTexUniform.AddId(chunk.TexRid);
			
			_uniforms.Add(displacementTexUniform);
            _uniforms.Add(footprintTexUniform);
            _uniformSet = _device.UniformSetCreate(_uniforms, _shader, 0);

            Params.CenterLeft = cl - (Vector2)chunk.ChunkCoord;
            Params.CenterRight = cr - (Vector2)chunk.ChunkCoord;

            DispatchCompute();

            _device.FreeRid(_uniformSet);
        }

        Params.CenterLeft = cl;
        Params.CenterRight = cr;
    }

    private void DispatchCompute()
	{
		uint xGroups = TextureSize / 16;
		uint yGroups = TextureSize / 16;
		uint zGroups = 1;

		var computeList = _device.ComputeListBegin();
		_device.ComputeListBindComputePipeline(computeList, _pipeline);
		_device.ComputeListBindUniformSet(computeList, _uniformSet, 0);
		
		byte[] paramsData = ParamsToBytes();
		_device.ComputeListSetPushConstant(computeList, paramsData, (uint)paramsData.Length);

		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
		_device.ComputeListEnd();
	}

    private void DispatchBatchCompute()
    {
        uint xGroups = TextureSize / 16;
		uint yGroups = TextureSize / 16;
		uint zGroups = 1;

		var computeList = _device.ComputeListBegin();
		_device.ComputeListBindComputePipeline(computeList, _pipelineBatch);
		_device.ComputeListBindUniformSet(computeList, _uniformSetBatch, 0);
		
		byte[] paramsData = BatchParamsToBytes();
		_device.ComputeListSetPushConstant(computeList, paramsData, (uint)paramsData.Length);

		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
		_device.ComputeListEnd();
    }

	private byte[] ParamsToBytes()
	{
		int size = Marshal.SizeOf(Params);
		byte[] output = new byte[size];
		IntPtr ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(Params, ptr, true);
			Marshal.Copy(ptr, output, 0, size);
		}
		finally
		{
			Marshal.FreeHGlobal(ptr);
		}
		return output;
	}

    private byte[] BatchParamsToBytes()
	{
		int size = Marshal.SizeOf(BatchParams);
		byte[] output = new byte[size];
		IntPtr ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(BatchParams, ptr, true);
			Marshal.Copy(ptr, output, 0, size);
		}
		finally
		{
			Marshal.FreeHGlobal(ptr);
		}
		return output;
	}
}
