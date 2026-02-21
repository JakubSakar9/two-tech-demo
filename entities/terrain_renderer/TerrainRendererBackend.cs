using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using GDArray = Godot.Collections.Array;

struct SurfaceRenderBuffers
{
    public Rid VPositionBuffer;
    public Rid VNormalBuffer;
    public Rid VUvBuffer;
    public Rid IndexBuffer;
    public Rid VertexArray;
    public Rid IndexArray;
};

struct TransformUniformData
{
    public Projection mMat;
    public Projection vMat;
    public Projection pMat;

    public TransformUniformData()
    {
        mMat = Projection.Identity;
        vMat = Projection.Identity;
        pMat = Projection.Identity;
    }
}

public partial class TerrainRendererBackend : GodotObject
{
    const string VERT_SOURCE = "res://shaders/cube.vert";
    const string FRAG_SOURCE = "res://shaders/cube.frag";
    
    const int MAT4_SIZE = 16 * sizeof(float);
    const int TRANSFORM_UNIFORM_SIZE = 3 * MAT4_SIZE;

    private RenderingDevice _device = null;
    private Rid _pipeline;
    private Rid _shader;
    private SurfaceRenderBuffers _surface;
    private Transform3D _surfaceTransform;
    private Color[] _clearColors;
    private Rid _imageTexture;
    private Rid _depthTexture;
    private Rid _screenBuffer;
    private TransformUniformData _transformUniformData;
    private Rid _transformUniformBuffer;
    private long _vertexFormat;

    public void InitRendering(RenderSceneBuffersRD rsb, RenderSceneDataRD rsd)
    {
        _device = RenderingServer.GetRenderingDevice();

        CompileShader();
        CreateVertexFormat();

        var rasterizationState = InitRasterization();
        var multisampleState = InitMultisample();
        var depthStencilState = InitDepthStencil();
        var colorBlendState = InitColorBlend();

        _imageTexture = rsb.GetColorTexture();
        _depthTexture = rsb.GetDepthTexture();
        _screenBuffer = _device.FramebufferCreate([_imageTexture, _depthTexture]);
        long fbFormat = _device.FramebufferGetFormat(_screenBuffer);

        _pipeline = _device.RenderPipelineCreate(_shader,
            fbFormat, _vertexFormat, RenderingDevice.RenderPrimitive.TesselationPatch,
            rasterizationState, multisampleState, depthStencilState, colorBlendState);

        UpdateProjectionView(rsd);
        _transformUniformBuffer = new();
        _transformUniformBuffer = _device.UniformBufferCreate(TRANSFORM_UNIFORM_SIZE, GetTransformUniformBufferData());

        _clearColors = [new Color(0.2f, 0.2f, 0.2f, 1.0f)];
    }

    public void CreateFramebuffers(RenderSceneBuffersRD renderSceneBuffers)
    {
        Rid newImageTexture = renderSceneBuffers.GetColorTexture();
        Rid newDepthTexture = renderSceneBuffers.GetDepthTexture();
        if (newImageTexture == _imageTexture && newDepthTexture == _depthTexture) return;

        _imageTexture = newImageTexture;
        _depthTexture = newDepthTexture;
        if (_device.FramebufferIsValid(_screenBuffer)) _device.FreeRid(_screenBuffer);
        _screenBuffer = _device.FramebufferCreate([_imageTexture, _depthTexture]);
    }

    public void Draw(RenderSceneDataRD rsd)
    {
        UpdateProjectionView(rsd);
        Rid uniformSet = CreateUniformSet();

        _device.DrawCommandBeginLabel("Draw all deformable surfaces", Colors.White);

        var drawList = _device.DrawListBegin(_screenBuffer, RenderingDevice.DrawFlags.ClearColor0, _clearColors);
        
        _device.DrawListBindUniformSet(drawList, uniformSet, 0);
        _device.DrawListBindRenderPipeline(drawList, _pipeline);
        _device.DrawListBindVertexArray(drawList, _surface.VertexArray);
        _device.DrawListBindIndexArray(drawList, _surface.IndexArray);
        _device.DrawListDraw(drawList, true, 1, 0);

        _device.DrawListEnd();
        _device.DrawCommandEndLabel();

        _device.FreeRid(uniformSet);
    }

    public void Cleanup()
    {
        if (_device == null) return;
        if (_shader.IsValid) _device.FreeRid(_shader);
        if (_surface.IndexArray.IsValid) _device.FreeRid(_surface.IndexArray);
        if (_surface.VertexArray.IsValid) _device.FreeRid(_surface.VertexArray);
        if (_surface.IndexBuffer.IsValid) _device.FreeRid(_surface.IndexBuffer);
        if (_surface.VPositionBuffer.IsValid) _device.FreeRid(_surface.VPositionBuffer);
        if (_surface.VNormalBuffer.IsValid) _device.FreeRid(_surface.VNormalBuffer);
        if (_surface.VUvBuffer.IsValid) _device.FreeRid(_surface.VUvBuffer);
        if (_screenBuffer.IsValid) _device.FreeRid(_screenBuffer);
    }

    public bool Initialized()
    {
        return _device != null;
    }

    public void LoadBuffers()
    {
        // Create a debug mesh to test if the pipeline is working
        Mesh mesh = new BoxMesh();
        (mesh as BoxMesh).Size = new Vector3(4.0f, 4.0f, 4.0f);

        CreateVertexArray(in mesh, ref _surface);
        CreateIndexArray(in mesh, ref _surface);
    }

    private void CompileShader()
    {
        FileAccess vertFile = FileAccess.Open(VERT_SOURCE, FileAccess.ModeFlags.Read);
        // FileAccess tescFile = FileAccess.Open(TESC_SOURCE, FileAccess.ModeFlags.Read);
        // FileAccess teseFile = FileAccess.Open(TESE_SOURCE, FileAccess.ModeFlags.Read);
        FileAccess fragFile = FileAccess.Open(FRAG_SOURCE, FileAccess.ModeFlags.Read);
        RDShaderSource source = new();
        source.Language = RenderingDevice.ShaderLanguage.Glsl;
        source.SourceVertex = vertFile.GetAsText();
        // source.SourceTesselationControl = tescFile.GetAsText();
        // source.SourceTesselationEvaluation = teseFile.GetAsText();
        source.SourceFragment = fragFile.GetAsText();
        RDShaderSpirV spirV = _device.ShaderCompileSpirVFromSource(source);
        _shader = _device.ShaderCreateFromSpirV(spirV);
    }

    private void CreateVertexFormat()
    {
        RDVertexAttribute posAtr = new()
        {
            Format = RenderingDevice.DataFormat.R32G32B32Sfloat,
            Frequency = RenderingDevice.VertexFrequency.Vertex,
            Location = 0,
            Offset = 0,
            Stride = 3 * sizeof(float)
        };

        RDVertexAttribute normAtr = new()
        {
            Format = RenderingDevice.DataFormat.R32G32B32Sfloat,
            Frequency = RenderingDevice.VertexFrequency.Vertex,
            Location = 1,
            Offset = 0,
            Stride = 3 * sizeof(float)
        };

        RDVertexAttribute uvAtr = new()
        {
            Format = RenderingDevice.DataFormat.R32G32Sfloat,
            Frequency = RenderingDevice.VertexFrequency.Vertex,
            Location = 2,
            Offset = 0,
            Stride = 2 * sizeof(int)
        };

        _vertexFormat = _device.VertexFormatCreate([posAtr, normAtr, uvAtr]);
    }

    private void CreateVertexArray(ref readonly Mesh mesh, ref SurfaceRenderBuffers buffers)
    {
        // NOTE: Only surface 0 is considered for simplicity
        GDArray dataArrays = mesh.SurfaceGetArrays(0);

        List<float> positionList = new List<float>();
        foreach (Vector3 pos in ((GDArray)dataArrays[(int)Mesh.ArrayType.Vertex]).Select(v => (Vector3)v))
        {
            positionList.Add(pos.X);
            positionList.Add(pos.Y);
            positionList.Add(pos.Z);
        }
        float[] positions = positionList.ToArray();

        List<float> normalList = new List<float>();
        foreach (Vector3 normal in ((GDArray)dataArrays[(int)Mesh.ArrayType.Normal]).Select(v => (Vector3)v))
        {
            normalList.Add(normal.X);
            normalList.Add(normal.Y);
            normalList.Add(normal.Z);
        }
        float[] normals = normalList.ToArray();

        List<float> uvList = new List<float>();
        foreach (Vector2 uv in ((GDArray)dataArrays[(int)Mesh.ArrayType.TexUV]).Select(v => (Vector2)v))
        {
            uvList.Add(uv.X);
            uvList.Add(uv.Y);
        }
        float[] uvs = uvList.ToArray();

        uint numVertices = (uint)((GDArray)dataArrays[(int)Mesh.ArrayType.Vertex]).Count;

        buffers.VPositionBuffer = CreateVertexBuffer(positions);
        buffers.VNormalBuffer = CreateVertexBuffer(normals);
        buffers.VUvBuffer = CreateVertexBuffer(uvs);

        buffers.VertexArray = _device.VertexArrayCreate(numVertices, _vertexFormat,
            [buffers.VPositionBuffer, buffers.VNormalBuffer, buffers.VUvBuffer]);
    }

    private void CreateIndexArray(ref readonly Mesh mesh, ref SurfaceRenderBuffers buffers)
    {
        GDArray dataArrays = mesh.SurfaceGetArrays(0);
        int[] indices = (int[])dataArrays[(int)Mesh.ArrayType.Index];
        uint numIndices = (uint) indices.Length;
        byte[] indicesBytes = new byte[numIndices * sizeof(int)];
        Buffer.BlockCopy(indices, 0, indicesBytes, 0, (int)numIndices * sizeof(int));
        buffers.IndexBuffer = _device.IndexBufferCreate(numIndices, RenderingDevice.IndexBufferFormat.Uint32, indicesBytes);

        buffers.IndexArray = _device.IndexArrayCreate(buffers.IndexBuffer, 0, numIndices);
    }

    private Rid CreateVertexBuffer(float[] data)
    {
        uint sizeBytes = (uint)data.Length * sizeof(float);
        byte[] dataBytes = new byte[sizeBytes];
        Buffer.BlockCopy(data, 0, dataBytes, 0, (int)sizeBytes);
        return _device.VertexBufferCreate(sizeBytes, dataBytes);
    }

    private Rid CreateUniformBuffer(byte[] data, uint numBytes)
    {
        Rid uniformBuffer = _device.UniformBufferCreate(numBytes, data);
        return uniformBuffer;
    }

    private RDPipelineRasterizationState InitRasterization()
    {
        RDPipelineRasterizationState state = new();
        state.Wireframe = false;
        state.CullMode = RenderingDevice.PolygonCullMode.Back;
        state.EnableDepthClamp = false;
        state.LineWidth = 1.0f;
        state.FrontFace = RenderingDevice.PolygonFrontFace.Clockwise;
        state.DepthBiasEnabled = false;
        state.PatchControlPoints = 3;
        return state;
    }

    private RDPipelineMultisampleState InitMultisample()
    {
        RDPipelineMultisampleState state = new();
        state.EnableSampleShading = false;
        state.SampleCount = RenderingDevice.TextureSamples.Samples1;
        state.MinSampleShading = 1.0f;
        return state;
    }

    private RDPipelineDepthStencilState InitDepthStencil()
    {
        RDPipelineDepthStencilState state = new();
        state.EnableDepthTest = true;
        state.BackOpCompare = RenderingDevice.CompareOperator.Less;
        return state;
    }

    private RDPipelineColorBlendState InitColorBlend()
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

    private void UpdateProjectionView(RenderSceneDataRD rsd)
    {
        _transformUniformData.pMat = rsd.GetCamProjection();
        _transformUniformData.vMat = new Projection(rsd.GetCamTransform()).Inverse();
    }

    private Rid CreateUniformSet()
    {
        _transformUniformData.mMat = new Projection(_surfaceTransform);
        _device.BufferUpdate(_transformUniformBuffer, 0, TRANSFORM_UNIFORM_SIZE, GetTransformUniformBufferData());

        var transformUniform = new RDUniform();
        transformUniform.UniformType = RenderingDevice.UniformType.UniformBuffer;
        transformUniform.Binding = 0;
        transformUniform.AddId(_transformUniformBuffer);

        return _device.UniformSetCreate([transformUniform], _shader, 0);
    }

    private unsafe byte[] GetTransformUniformBufferData()
    {
        byte[] targetData = new byte[TRANSFORM_UNIFORM_SIZE];
        fixed (void* srcPtr = &_transformUniformData, dstPtr = &targetData[0])
            Buffer.MemoryCopy(srcPtr, dstPtr, TRANSFORM_UNIFORM_SIZE, TRANSFORM_UNIFORM_SIZE);
        return targetData;
    }
}
