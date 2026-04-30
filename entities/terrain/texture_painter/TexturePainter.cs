using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

public struct TexturePainterDecayParams
{
    public float DecayFactor;
    uint _padding0;
    uint _padding1;
    uint _padding2;
}

public partial class TexturePainter : Node
{
    [Export] public Texture2D FootprintTexture;
    [Export] public uint TextureSize = 1024;
	[Export] public ChunkPool Pool;
    [Export] public FootprintStorage FpStorage;
    [Export] public float DecayPerSecond = 0.994f;

    const string SHADER_PATH = "res://shaders/disp_compute.glsl";
    const string SHADER_DECAY_PATH = "res://shaders/disp_decay_compute.glsl";
    const string SHADER_BATCH_PATH = "res://shaders/disp_batch_compute.glsl";

    // public Texture2Drd DisplacementTexture;
    public TexturePainterParams Params;
    public TexturePainterDecayParams DecayParams;
    public TexturePainterBatchParams BatchParams;

    private RenderingDevice _device;
    private Rid _shader;
    private Rid _shaderDecay;
    private Rid _shaderBatch;
    private Rid _pipeline;
    private Rid _pipelineDecay;
    private Rid _pipelineBatch;
    private Rid _footprintBuffer;
    private Rid _decayBuffer;
    private Rid _footprintTex;
    private Rid _footprintSampler;
    private Rid _uniformSet;
    private Rid _uniformSetDecay;
    private Rid _uniformSetBatch;

    private Array<RDUniform> _uniforms;
    private Array<RDUniform> _uniformsDecay;
    private Array<RDUniform> _uniformsBatch;
    private RDTextureFormat _format;
    private RDTextureView _view;

    private int _reconstructionPhase = 0;
    private bool _reconstructionInProgress;
    private bool _reconstructionDrawn = false;
    private float _decayClock = 1.0f;


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
        DecayParams = new()
        {
            DecayFactor = DecayPerSecond
        };

        SetAngle(0.0f);
        Pool.ChunkInQueue += StartReconstruction;

        RenderingServer.CallOnRenderThread(Callable.From(InitCompute));
    }

    public override void _Process(double delta)
    {
        RenderingServer.CallOnRenderThread(Callable.From(DrawTextures));
        _decayClock -= (float)delta;
        if (_decayClock < 0.0f)
        {
            _decayClock++;
            FpStorage.Tick();
            RenderingServer.CallOnRenderThread(Callable.From(DecayTextures));
        }
        if (_reconstructionInProgress)
        {
            RenderingServer.CallOnRenderThread(Callable.From(DrawBatch));
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _device.FreeRid(_footprintBuffer);
        _device.FreeRid(_decayBuffer);

        _device.FreeRid(_footprintTex);
        _device.FreeRid(_footprintSampler);

        _device.FreeRid(_pipeline);
        _device.FreeRid(_pipelineDecay);
        _device.FreeRid(_pipelineBatch);
        
        _device.FreeRid(_shader);
        _device.FreeRid(_shaderDecay);
        _device.FreeRid(_shaderBatch);

        Pool.Cleanup(in _device);
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
		InitPipelines();
		InitFootprintTexture();
        InitBuffers();
	}

	private void InitPipelines()
	{
		var shaderFile1 = GD.Load<RDShaderFile>(SHADER_PATH);
		var shaderBytecode1 = shaderFile1.GetSpirV();
		_shader = _device.ShaderCreateFromSpirV(shaderBytecode1);
        _pipeline = _device.ComputePipelineCreate(_shader);
        
        var shaderFile2 = GD.Load<RDShaderFile>(SHADER_DECAY_PATH);
        var shaderBytecode2 = shaderFile2.GetSpirV();
		_shaderDecay = _device.ShaderCreateFromSpirV(shaderBytecode2);
        _pipelineDecay = _device.ComputePipelineCreate(_shaderDecay);
        
        var shaderFile3 = GD.Load<RDShaderFile>(SHADER_BATCH_PATH);
        var shaderBytecode3 = shaderFile3.GetSpirV();
		_shaderBatch = _device.ShaderCreateFromSpirV(shaderBytecode3);
        _pipelineBatch = _device.ComputePipelineCreate(_shaderBatch);
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

    private void InitBuffers()
    {
        _footprintBuffer = _device.StorageBufferCreate((uint)(FpStorage.RenderBatchSize * 4 * sizeof(float)));
        _decayBuffer = _device.StorageBufferCreate((uint)(FpStorage.RenderBatchSize * sizeof(float)));
    }

    private void StartReconstruction()
    {
        if (_reconstructionInProgress) return;
        _reconstructionInProgress = true;
        _reconstructionPhase = 0;
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
            if (!_device.TextureIsValid(chunk.TexRid)) return;
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

    private void DecayTextures()
    {
        for (int i = 0; i < Pool.UsedChunks.Count; i++)
        {
            if (!Pool.UsedChunks[i]) continue;
            var displacementTexUniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.Image,
                Binding = 0
            };
            Rid dtRid = Pool.GetTextureRidAtIdx((uint)i);
            if (!_device.TextureIsValid(dtRid)) return;
            displacementTexUniform.AddId(dtRid);
            _uniformsDecay = [displacementTexUniform];
            _uniformSetDecay = _device.UniformSetCreate(_uniformsDecay, _shaderDecay, 0);

            DispatchDecayCompute();
            _device.FreeRid(_uniformSetDecay);
        }
    }

    private void DrawBatch()
    {
        var reconstructedChunk = Pool.GetReconstructedChunk();
        BatchParams.ChunkCoord = reconstructedChunk;
        if (_reconstructionPhase == 2)
        {
            _reconstructionPhase = 0;
            _reconstructionInProgress = false;
            Pool.FinishReconstruction(_reconstructionDrawn);
            _reconstructionDrawn = false;
            return;
        }
        if (_reconstructionPhase == 0)
        {
            if (!FpStorage.HasChunkLeft(reconstructedChunk))
            {
                _reconstructionPhase++;
                return;
            }
            bool res = FpStorage.PopulateBufferChunkLeft(in _device, ref _footprintBuffer, ref _decayBuffer,
                ref BatchParams.FootprintCount, reconstructedChunk, DecayPerSecond);
            _reconstructionDrawn = true;
            if (res)
            {
                _reconstructionPhase++;
            }
        }
        else if (_reconstructionPhase == 1)
        {
            if (!FpStorage.HasChunkLeft(reconstructedChunk))
            {
                _reconstructionPhase++;
                return;
            }
            bool res = FpStorage.PopulateBufferChunkRight(in _device, ref _footprintBuffer, ref _decayBuffer,
                ref BatchParams.FootprintCount, reconstructedChunk, DecayPerSecond);
            _reconstructionDrawn = true;
            if (res)
            {
                _reconstructionPhase++;
            }
        }

        var displacementTexUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        if (!_device.TextureIsValid(Pool.GetReconstructedTexture())) return;
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
		fpBufferUniform.AddId(_footprintBuffer);
        
        var dcBufferUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 3
		};
		dcBufferUniform.AddId(_decayBuffer);

        _uniformsBatch = [displacementTexUniform, footprintTexUniform, fpBufferUniform, dcBufferUniform];
        _uniformSetBatch = _device.UniformSetCreate(_uniformsBatch, _shaderBatch, 0);

        DispatchBatchCompute();
        _device.FreeRid(_uniformSetBatch);
    }

    private void DispatchCompute()
	{
		uint xGroups = TextureSize / 8;
		uint yGroups = TextureSize / 8;
		uint zGroups = 1;

		var computeList = _device.ComputeListBegin();
		_device.ComputeListBindComputePipeline(computeList, _pipeline);
		_device.ComputeListBindUniformSet(computeList, _uniformSet, 0);
		
		byte[] paramsData = ParamsToBytes();
		_device.ComputeListSetPushConstant(computeList, paramsData, (uint)paramsData.Length);

		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
		_device.ComputeListEnd();
	}

    private void DispatchDecayCompute()
    {
        uint xGroups = TextureSize / 8;
		uint yGroups = TextureSize / 8;
		uint zGroups = 1;

		var computeList = _device.ComputeListBegin();
		_device.ComputeListBindComputePipeline(computeList, _pipelineDecay);
		_device.ComputeListBindUniformSet(computeList, _uniformSetDecay, 0);
		
		byte[] paramsData = DecayParamsToBytes();
		_device.ComputeListSetPushConstant(computeList, paramsData, (uint)paramsData.Length);

		_device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
		_device.ComputeListEnd();
    }

    private void DispatchBatchCompute()
    {
        uint xGroups = TextureSize / 8;
		uint yGroups = TextureSize / 8;
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

    private byte[] DecayParamsToBytes()
	{
		int size = Marshal.SizeOf(DecayParams);
		byte[] output = new byte[size];
		IntPtr ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(DecayParams, ptr, true);
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
