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

public partial class TexturePainter : Node
{
    [Export] public Texture2D FootprintTexture;
    [Export] public uint TextureSize = 1024;
	[Export] public ChunkPool Pool;

    const string SHADER_PATH = "res://shaders/disp_compute.glsl";

    // public Texture2Drd DisplacementTexture;
    public TexturePainterParams Params;

    private RenderingDevice _device;
    private Rid _shader;
    private Rid _pipeline;
    private Rid _computeTex;
    private Rid _footprintTex;
    private Rid _footprintSampler;
    private Rid _uniformSet;

    private Array<RDUniform> _uniforms;
    private RDTextureFormat _format;
    private RDTextureView _view;


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
        _device.FreeRid(_computeTex);
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

    public void InitPool(uint chunkRange)
    {
        Pool.Initialize(chunkRange, TextureSize, in _device);
    }

    private void InitCompute()
	{
		InitShader();
		InitFootprintTexture();
		_pipeline = _device.ComputePipelineCreate(_shader);
	}

	private void InitShader()
	{
		var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH);
		var shaderBytecode = shaderFile.GetSpirV();
		_shader = _device.ShaderCreateFromSpirV(shaderBytecode);
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

			var computeTexUniform = new RDUniform
			{
				UniformType = RenderingDevice.UniformType.Image,
				Binding = 0
			};
			computeTexUniform.AddId(chunk.TexRid);
			
			_uniforms.Add(computeTexUniform);
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
}
