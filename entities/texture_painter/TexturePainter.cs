using Godot;
using Godot.Collections;
using System;
using System.Runtime.InteropServices;

public struct TexturePainterParams
{
	public uint TextureSize;
	private readonly uint _000;
	public Vector2 MousePos;
	// private readonly uint _001;
}

public partial class TexturePainter : Node
{
	[Export]
	public uint TextureSize = 1024;

	const string SHADER_PATH = "res://shaders/disp_compute.glsl";

	public Texture2Drd DisplacementTexture;

    private RenderingDevice _device;
	private Rid _shader;
	private Rid _pipeline;
	private Rid _computeTex;
	private Rid _uniformSet;

	private Array<RDUniform> _uniforms;

	private RDTextureFormat _format;
	private RDTextureView _view;

	private float[] _imageData;
	private TexturePainterParams _params;
	
	public override void _Ready()
	{
        _device = RenderingServer.GetRenderingDevice();
		_imageData = new float[TextureSize * TextureSize];
        _params = new()
        {
            TextureSize = TextureSize
        };

		RenderingServer.CallOnRenderThread(Callable.From(InitCompute));
	}
	
	public override void _Process(double delta)
	{
		_params.MousePos = GetViewport().GetMousePosition() / TextureSize;
		RenderingServer.CallOnRenderThread(Callable.From(DispatchCompute));
	}


    // public override void _PhysicsProcess(double delta)
    // {
    //     base._PhysicsProcess(delta);
	// 	_params.MousePos = GetViewport().GetMousePosition() / TextureSize;
	// 	RenderingServer.CallOnRenderThread(Callable.From(DispatchCompute));
    // }

	private void InitCompute()
	{
		InitShader();
		InitTexture();
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

	private void InitTexture()
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
