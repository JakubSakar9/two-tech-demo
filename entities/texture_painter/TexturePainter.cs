using Godot;
using Godot.Collections;
using System;
using System.Runtime.InteropServices;

public struct TexturePainterParams
{
	public Vector4 RotationMat;
	public uint TextureSize;
	public float CarveDepth;
	public Vector2 SpriteCenter;
	public Vector2 SpriteOffet;
	public float DownscaleFactor;
	private float _padding;
}

public partial class TexturePainter : Node
{
	[Export] public uint TextureSize = 1024;
	[Export] public Texture2D FootprintTexture;

	const string SHADER_PATH = "res://shaders/disp_compute.glsl";

	public Texture2Drd DisplacementTexture;
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

	private float[] _imageData;
	private int _yFlip = 1;

	public override void _Ready()
	{
        _device = RenderingServer.GetRenderingDevice();
		_imageData = new float[TextureSize * TextureSize];
        Params = new()
        {
            TextureSize = TextureSize,
			SpriteCenter = new Vector2(0.5f, 0.5f),
			SpriteOffet = new Vector2(-0.2f, 0.0f)
        };
		GD.Randomize();

		SetAngle(0.0f);

		RenderingServer.CallOnRenderThread(Callable.From(InitCompute));
	}
	
	public override void _Process(double delta)
	{
		RenderingServer.CallOnRenderThread(Callable.From(DispatchCompute));
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
			X =  Mathf.Cos(angleRadians) * _yFlip,
			Y = -Mathf.Sin(angleRadians),
			Z =  Mathf.Sin(angleRadians) * _yFlip,
			W =  Mathf.Cos(angleRadians)
		};
	}

	public void FlipSprite()
	{
		_yFlip *= -1;
	}

	private void InitCompute()
	{
		InitShader();
		InitTargetTexture();
		InitFootprintTexture();
		InitUniforms();
		_pipeline = _device.ComputePipelineCreate(_shader);
		DispatchCompute();
	}

	private void InitShader()
	{
		var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH);
		var shaderBytecode = shaderFile.GetSpirV();
		_shader = _device.ShaderCreateFromSpirV(shaderBytecode);
	}

	private void InitTargetTexture()
	{
        _format = new()
        {
            Width = TextureSize,
            Height = TextureSize,
            Format = RenderingDevice.DataFormat.R32Sfloat,
            UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit
                | RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CpuReadBit
                | RenderingDevice.TextureUsageBits.CanCopyFromBit
                | RenderingDevice.TextureUsageBits.SamplingBit
        };

		_view = new();
		DisplacementTexture = new();

		var computeIm = Image.CreateEmpty((int)TextureSize, (int)TextureSize, false, Image.Format.Rf);
		_computeTex = _device.TextureCreate(_format, _view, [computeIm.GetData()]);
		DisplacementTexture.TextureRdRid = _computeTex;
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

	private void InitUniforms()
	{
		_uniforms = [];

		var computeTexUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.Image,
			Binding = 0
		};
		computeTexUniform.AddId(_computeTex);
		_uniforms.Add(computeTexUniform);

		var footprintTexUniform = new RDUniform
		{
			UniformType = RenderingDevice.UniformType.SamplerWithTexture,
			Binding = 1
		};
		footprintTexUniform.AddId(_footprintSampler);
		footprintTexUniform.AddId(_footprintTex);
		_uniforms.Add(footprintTexUniform);

		_uniformSet = _device.UniformSetCreate(_uniforms, _shader, 0);
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

		Params.CarveDepth = 0.0f;
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
