using Godot;
using Godot.Collections;
using System;

public partial class TerrainRendererBackend: GodotObject
{
    const string VERT_SOURCE = "res://shaders/d_terrain.vert";
    const string FRAG_SOURCE = "res://shaders/d_terrain.frag";

    private RenderingDevice _rd = null;
    private Rid _pipeline;
    private Rid _shader;
    private Rid _vertexPositionBuffer;
    private Rid _vertexColorBuffer;
    private Rid _vertexArray;
    private Color[] _clearColors;
    private Rid _imageTexture;
    private Rid _depthTexture;
    private Rid _screenBuffer;
    private long _vertexFormat;

    public void InitRendering(RenderSceneBuffersRD renderSceneBuffers)
    {
        _rd = RenderingServer.GetRenderingDevice();

        _CompileShader();
        _CreateVertexFormat();
        _CreateVertexArray();

        var rasterizationState = _InitRasterization();
        var multisampleState = _InitMultisample();
        var depthStencilState = _InitDepthStencil();
        var colorBlendState = _InitColorBlend();

        _imageTexture = renderSceneBuffers.GetColorTexture();
        _depthTexture = renderSceneBuffers.GetDepthTexture();
        _screenBuffer = _rd.FramebufferCreate([_imageTexture, _depthTexture]);
        long fbFormat = _rd.FramebufferGetFormat(_screenBuffer);

        _pipeline = _rd.RenderPipelineCreate(_shader,
        fbFormat, _vertexFormat, RenderingDevice.RenderPrimitive.Triangles,
        rasterizationState, multisampleState, depthStencilState, colorBlendState);

        _clearColors = [new Color(0.2f, 0.2f, 0.2f, 1.0f)];
    }

    public void CreateFramebuffers(RenderSceneBuffersRD renderSceneBuffers)
    {
        Rid newImageTexture = renderSceneBuffers.GetColorTexture();
        Rid newDepthTexture = renderSceneBuffers.GetDepthTexture();
        if (newImageTexture == _imageTexture && newDepthTexture == _depthTexture) return;

        _imageTexture = newImageTexture;
        _depthTexture = newDepthTexture;
        if (_rd.FramebufferIsValid(_screenBuffer)) _rd.FreeRid(_screenBuffer);
        _screenBuffer = _rd.FramebufferCreate([_imageTexture, _depthTexture]);
    }

    public void Draw()
    {
        _rd.DrawCommandBeginLabel("Draw a triangle", Colors.White);

        var drawList = _rd.DrawListBegin(_screenBuffer, RenderingDevice.DrawFlags.ClearColor0, _clearColors);
        _rd.DrawListBindRenderPipeline(drawList, _pipeline);
        _rd.DrawListBindVertexArray(drawList, _vertexArray);
        _rd.DrawListDraw(drawList, false, 1, 0);
        _rd.DrawListEnd();

        _rd.DrawCommandEndLabel();
    }

    public void Cleanup()
    {
        if (_rd == null) return;
        if (_shader.IsValid) _rd.FreeRid(_shader);
        if (_vertexArray.IsValid) _rd.FreeRid(_vertexArray);
        if (_vertexPositionBuffer.IsValid) _rd.FreeRid(_vertexPositionBuffer);
        if (_vertexColorBuffer.IsValid) _rd.FreeRid(_vertexColorBuffer);
        if (_screenBuffer.IsValid) _rd.FreeRid(_screenBuffer);
    }

    public bool Initialized()
    {
        return _rd != null;
    }

    private void _CompileShader()
    {
        FileAccess vertFile = FileAccess.Open(VERT_SOURCE, FileAccess.ModeFlags.Read);
        FileAccess fragFile = FileAccess.Open(FRAG_SOURCE, FileAccess.ModeFlags.Read);
        RDShaderSource source = new();
        source.Language = RenderingDevice.ShaderLanguage.Glsl;
        source.SourceVertex = vertFile.GetAsText();
        source.SourceFragment = fragFile.GetAsText();
        RDShaderSpirV spirV = _rd.ShaderCompileSpirVFromSource(source);
        _shader = _rd.ShaderCreateFromSpirV(spirV);
        if (_shader == null)
        {
            GD.PrintErr("Failed to compile shaders!");
        }
    }

    private void _CreateVertexFormat()
    {
        RDVertexAttribute posAtr = new();
        posAtr.Format = RenderingDevice.DataFormat.R32G32Sfloat;
        posAtr.Frequency = RenderingDevice.VertexFrequency.Vertex;
        posAtr.Location = 0;
        posAtr.Offset = 0;
        posAtr.Stride = 2 * sizeof(float);

        RDVertexAttribute colorAtr = new();
        colorAtr.Format = RenderingDevice.DataFormat.R32G32B32Sfloat;
        colorAtr.Frequency = RenderingDevice.VertexFrequency.Vertex;
        colorAtr.Location = 1;
        colorAtr.Offset = 0;
        colorAtr.Stride = 3 * sizeof(float);

        _vertexFormat = _rd.VertexFormatCreate([posAtr, colorAtr]);
    }

    private void _CreateVertexArray()
    {
        float[] positions = new float[]
        {
             0.0f, -0.5f,
             0.5f,  0.5f,
            -0.5f,  0.5f,
        };
        _vertexPositionBuffer = _CreateVertexBuffer(positions);

        float[] colors = new float[]
        {
            1.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f,
            0.0f, 0.0f, 1.0f,
        };
        _vertexColorBuffer = _CreateVertexBuffer(colors);


        var numVertices = (uint)positions.Length / 2;
        _vertexArray = _rd.VertexArrayCreate(numVertices, _vertexFormat,
            [_vertexPositionBuffer, _vertexColorBuffer]);
    }

    private Rid _CreateVertexBuffer(float[] data)
    {
        uint sizeBytes = (uint)data.Length * sizeof(float);
        byte[] dataBytes = new byte[sizeBytes];
        Buffer.BlockCopy(data, 0, dataBytes, 0, (int)sizeBytes);
        return _rd.VertexBufferCreate(sizeBytes, dataBytes);
    }

    private RDPipelineRasterizationState _InitRasterization()
    {
        RDPipelineRasterizationState state = new();
        state.Wireframe = false;
        state.CullMode = RenderingDevice.PolygonCullMode.Disabled;
        state.EnableDepthClamp = false;
        state.LineWidth = 1.0f;
        state.FrontFace = RenderingDevice.PolygonFrontFace.Clockwise;
        state.DepthBiasEnabled = false;
        return state;
    }

    private RDPipelineMultisampleState _InitMultisample()
    {
        RDPipelineMultisampleState state = new();
        state.EnableSampleShading = false;
        state.SampleCount = RenderingDevice.TextureSamples.Samples1;
        state.MinSampleShading = 1.0f;
        return state;
    }

    private RDPipelineDepthStencilState _InitDepthStencil()
    {
        RDPipelineDepthStencilState state = new();
        state.EnableDepthTest = false;
        return state;
    }

    private RDPipelineColorBlendState _InitColorBlend()
    {
        RDPipelineColorBlendState state = new();
        RDPipelineColorBlendStateAttachment attachment = new();

        attachment.EnableBlend = true;
        attachment.WriteA = true;
        attachment.WriteB = true;
        attachment.WriteG = true;
        attachment.WriteR = true;
        attachment.AlphaBlendOp = RenderingDevice.BlendOperation.Add;
        attachment.ColorBlendOp = RenderingDevice.BlendOperation.Add;
        attachment.SrcAlphaBlendFactor = RenderingDevice.BlendFactor.One;
        attachment.DstAlphaBlendFactor = RenderingDevice.BlendFactor.Zero;
        attachment.SrcColorBlendFactor = RenderingDevice.BlendFactor.One;
        attachment.DstColorBlendFactor = RenderingDevice.BlendFactor.Zero;

        state.Attachments.Add(attachment);
        state.EnableLogicOp = false;
        state.LogicOp = RenderingDevice.LogicOperation.Copy;
        return state;
    }
}
