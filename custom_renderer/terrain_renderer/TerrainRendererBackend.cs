using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
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
    const string VERT_SOURCE = "res://shaders/d_terrain.vert";
    const string TESC_SOURCE = "res://shaders/d_terrain.tesc";
    const string TESE_SOURCE = "res://shaders/d_terrain.tese";
    const string FRAG_SOURCE = "res://shaders/d_terrain.frag";
    
    const int MAT4_SIZE = 16 * sizeof(float);
    const int TRANSFORM_UNIFORM_SIZE = 3 * MAT4_SIZE;

    private RenderingDevice _rd = null;
    private Rid _pipeline;
    private Rid _shader;
    private SurfaceRenderBuffers[] _surfaces = [];
    private Array<Transform3D> _surfaceTransforms;
    private Color[] _clearColors;
    private Rid _imageTexture;
    private Rid _depthTexture;
    private Rid _screenBuffer;
    private TransformUniformData _transformUniformData;
    private Rid _transformUniformBuffer;
    private long _vertexFormat;
    private Array<DisplacementData> _displacements;

    public void InitRendering(RenderSceneBuffersRD rsb, RenderSceneDataRD rsd)
    {
        _rd = RenderingServer.GetRenderingDevice();

        _CompileShader();
        _CreateVertexFormat();

        var rasterizationState = _InitRasterization();
        var multisampleState = _InitMultisample();
        var depthStencilState = _InitDepthStencil();
        var colorBlendState = _InitColorBlend();

        _imageTexture = rsb.GetColorTexture();
        _depthTexture = rsb.GetDepthTexture();
        _screenBuffer = _rd.FramebufferCreate([_imageTexture, _depthTexture]);
        long fbFormat = _rd.FramebufferGetFormat(_screenBuffer);

        _pipeline = _rd.RenderPipelineCreate(_shader,
            fbFormat, _vertexFormat, RenderingDevice.RenderPrimitive.TesselationPatch,
            rasterizationState, multisampleState, depthStencilState, colorBlendState);

        _UpdateProjectionView(rsd);
        _transformUniformBuffer = new();
        _transformUniformBuffer = _rd.UniformBufferCreate(TRANSFORM_UNIFORM_SIZE, _GetTransformUniformBufferData());

        _clearColors = [new Color(0.2f, 0.2f, 0.2f, 1.0f)];

        _displacements = new Array<DisplacementData>();
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

    public void Draw(RenderSceneDataRD rsd)
    {
        _UpdateProjectionView(rsd);
        Array<Rid> uniformSets = [];
        for (int i = 0; i < _surfaces.Length; i++)
        {
            uniformSets.Add(_CreateUniformSet(i));
        }

        _rd.DrawCommandBeginLabel("Draw all deformable surfaces", Colors.White);

        var drawList = _rd.DrawListBegin(_screenBuffer, RenderingDevice.DrawFlags.ClearColor0, _clearColors);
        for (int i = 0; i < _surfaces.Length; i++)
        {
            var surface = _surfaces[i];
            _rd.DrawListBindUniformSet(drawList, uniformSets[i], 0);
            _rd.DrawListBindRenderPipeline(drawList, _pipeline);
            _rd.DrawListBindVertexArray(drawList, surface.VertexArray);
            _rd.DrawListBindIndexArray(drawList, surface.IndexArray);
            _rd.DrawListDraw(drawList, true, 1, 0);
        }
        _rd.DrawListEnd();

        _rd.DrawCommandEndLabel();

        for (int i = 0; i < _surfaces.Length; i++)
        {
            _rd.FreeRid(uniformSets[i]);
        }
    }

    public void Cleanup()
    {
        if (_rd == null) return;
        if (_shader.IsValid) _rd.FreeRid(_shader);
        foreach (var surface in _surfaces)
        {
            if (surface.IndexArray.IsValid) _rd.FreeRid(surface.IndexArray);
            if (surface.VertexArray.IsValid) _rd.FreeRid(surface.VertexArray);
            if (surface.IndexBuffer.IsValid) _rd.FreeRid(surface.IndexBuffer);
            if (surface.VPositionBuffer.IsValid) _rd.FreeRid(surface.VPositionBuffer);
            if (surface.VNormalBuffer.IsValid) _rd.FreeRid(surface.VNormalBuffer);
            if (surface.VUvBuffer.IsValid) _rd.FreeRid(surface.VUvBuffer);
        }
        if (_screenBuffer.IsValid) _rd.FreeRid(_screenBuffer);
    }

    public bool Initialized()
    {
        return _rd != null;
    }

    public void LoadBuffers()
    {
        DeformableGeometryProcessor.Instance.Synced = true;
        var meshes = DeformableGeometryProcessor.Instance.Meshes;
        _surfaceTransforms = DeformableGeometryProcessor.Instance.GlobalTransforms;
        _surfaces = new SurfaceRenderBuffers[meshes.Count()];
        for (int i = 0; i < meshes.Count(); i++)
        {
            Mesh mesh = meshes[i];
            _CreateVertexArray(in mesh, ref _surfaces[i]);
            _CreateIndexArray(in mesh, ref _surfaces[i]);
            _SetupDisplacementTextures(in mesh, i);
        }
    }

    private void _CompileShader()
    {
        FileAccess vertFile = FileAccess.Open(VERT_SOURCE, FileAccess.ModeFlags.Read);
        FileAccess tescFile = FileAccess.Open(TESC_SOURCE, FileAccess.ModeFlags.Read);
        FileAccess teseFile = FileAccess.Open(TESE_SOURCE, FileAccess.ModeFlags.Read);
        FileAccess fragFile = FileAccess.Open(FRAG_SOURCE, FileAccess.ModeFlags.Read);
        RDShaderSource source = new();
        source.Language = RenderingDevice.ShaderLanguage.Glsl;
        source.SourceVertex = vertFile.GetAsText();
        source.SourceTesselationControl = tescFile.GetAsText();
        source.SourceTesselationEvaluation = teseFile.GetAsText();
        source.SourceFragment = fragFile.GetAsText();
        RDShaderSpirV spirV = _rd.ShaderCompileSpirVFromSource(source);
        _shader = _rd.ShaderCreateFromSpirV(spirV);
    }

    private void _CreateVertexFormat()
    {
        RDVertexAttribute posAtr = new();
        posAtr.Format = RenderingDevice.DataFormat.R32G32B32Sfloat;
        posAtr.Frequency = RenderingDevice.VertexFrequency.Vertex;
        posAtr.Location = 0;
        posAtr.Offset = 0;
        posAtr.Stride = 3 * sizeof(float);

        RDVertexAttribute normAtr = new();
        normAtr.Format = RenderingDevice.DataFormat.R32G32B32Sfloat;
        normAtr.Frequency = RenderingDevice.VertexFrequency.Vertex;
        normAtr.Location = 1;
        normAtr.Offset = 0;
        normAtr.Stride = 3 * sizeof(float);

        RDVertexAttribute uvAtr = new();
        uvAtr.Format = RenderingDevice.DataFormat.R32G32Sfloat;
        uvAtr.Frequency = RenderingDevice.VertexFrequency.Vertex;
        uvAtr.Location = 2;
        uvAtr.Offset = 0;
        uvAtr.Stride = 2 * sizeof(int);

        _vertexFormat = _rd.VertexFormatCreate([posAtr, normAtr, uvAtr]);
    }

    private void _CreateVertexArray(ref readonly Mesh mesh, ref SurfaceRenderBuffers buffers)
    {
        // NOTE: Only surface 0 is considered for now, all other surfaces are discarded.
        GDArray dataArrays = mesh.SurfaceGetArrays(0);

        List<float> positionList = new List<float>();
        foreach (Vector3 pos in (GDArray)dataArrays[(int)Mesh.ArrayType.Vertex])
        {
            positionList.Add(pos.X);
            positionList.Add(pos.Y);
            positionList.Add(pos.Z);
        }
        float[] positions = positionList.ToArray();

        List<float> normalList = new List<float>();
        foreach (Vector3 normal in (GDArray)dataArrays[(int)Mesh.ArrayType.Normal])
        {
            normalList.Add(normal.X);
            normalList.Add(normal.Y);
            normalList.Add(normal.Z);
        }
        float[] normals = normalList.ToArray();

        List<float> uvList = new List<float>();
        foreach (Vector2 uv in (GDArray)dataArrays[(int)Mesh.ArrayType.TexUV])
        {
            uvList.Add(uv.X);
            uvList.Add(uv.Y);
        }
        float[] uvs = uvList.ToArray();

        uint numVertices = (uint)((GDArray)dataArrays[(int)Mesh.ArrayType.Vertex]).Count;

        buffers.VPositionBuffer = _CreateVertexBuffer(positions);
        buffers.VNormalBuffer = _CreateVertexBuffer(normals);
        buffers.VUvBuffer = _CreateVertexBuffer(uvs);

        buffers.VertexArray = _rd.VertexArrayCreate(numVertices, _vertexFormat,
            [buffers.VPositionBuffer, buffers.VNormalBuffer, buffers.VUvBuffer]);
    }

    private void _CreateIndexArray(ref readonly Mesh mesh, ref SurfaceRenderBuffers buffers)
    {
        GDArray dataArrays = mesh.SurfaceGetArrays(0);
        int[] indices = (int[])dataArrays[(int)Mesh.ArrayType.Index];
        uint numIndices = (uint) indices.Length;
        byte[] indicesBytes = new byte[numIndices * sizeof(int)];
        Buffer.BlockCopy(indices, 0, indicesBytes, 0, (int)numIndices * sizeof(int));
        buffers.IndexBuffer = _rd.IndexBufferCreate(numIndices, RenderingDevice.IndexBufferFormat.Uint32, indicesBytes);

        buffers.IndexArray = _rd.IndexArrayCreate(buffers.IndexBuffer, 0, numIndices);
    }

    private Rid _CreateVertexBuffer(float[] data)
    {
        uint sizeBytes = (uint)data.Length * sizeof(float);
        byte[] dataBytes = new byte[sizeBytes];
        Buffer.BlockCopy(data, 0, dataBytes, 0, (int)sizeBytes);
        return _rd.VertexBufferCreate(sizeBytes, dataBytes);
    }

    private Rid _CreateUniformBuffer(byte[] data, uint numBytes)
    {
        Rid uniformBuffer = _rd.UniformBufferCreate(numBytes, data);
        return uniformBuffer;
    }

    private RDPipelineRasterizationState _InitRasterization()
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
        state.EnableDepthTest = true;
        state.BackOpCompare = RenderingDevice.CompareOperator.Less;
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

    private void _UpdateProjectionView(RenderSceneDataRD rsd)
    {
        _transformUniformData.pMat = rsd.GetCamProjection();
        _transformUniformData.vMat = new Projection(rsd.GetCamTransform()).Inverse();
    }

    private Rid _CreateUniformSet(int i)
    {
        _transformUniformData.mMat = new Projection(_surfaceTransforms[0]);
        _rd.BufferUpdate(_transformUniformBuffer, 0, TRANSFORM_UNIFORM_SIZE, _GetTransformUniformBufferData());

        var transformUniform = new RDUniform();
        transformUniform.UniformType = RenderingDevice.UniformType.UniformBuffer;
        transformUniform.Binding = 0;
        transformUniform.AddId(_transformUniformBuffer);

        var displacedPatchesUniform = new RDUniform();
        displacedPatchesUniform.UniformType = RenderingDevice.UniformType.Texture;
        displacedPatchesUniform.Binding = 1;
        displacedPatchesUniform.AddId(_displacements[i].TexDisplacedPatchesRes);

        var displacementUniform = new RDUniform();
        displacementUniform.UniformType = RenderingDevice.UniformType.Texture;
        displacedPatchesUniform.Binding = 2;
        displacedPatchesUniform.AddId(_displacements[i].TexDisplacementRes);

        return _rd.UniformSetCreate([transformUniform, displacedPatchesUniform, displacementUniform], _shader, 0);
    }

    private void _SetupDisplacementTextures(ref readonly Mesh mesh, int surfaceIndex)
    {
        _displacements.Add(new DisplacementData());
        GDArray dataArrays = mesh.SurfaceGetArrays(0);
        var vArray = (GDArray)dataArrays[(int)Mesh.ArrayType.Vertex];
        int patchGridSize = (int) MathF.Ceiling(Mathf.Sqrt(vArray.Count)) - 1;
        _displacements[surfaceIndex].ComputeDisplacedPatches(patchGridSize);
        _displacements[surfaceIndex].CreateSamplers(ref _rd);
    }

    private unsafe byte[] _GetTransformUniformBufferData()
    {
        byte[] targetData = new byte[TRANSFORM_UNIFORM_SIZE];
        fixed (void* srcPtr = &_transformUniformData, dstPtr = &targetData[0])
            Buffer.MemoryCopy(srcPtr, dstPtr, TRANSFORM_UNIFORM_SIZE, TRANSFORM_UNIFORM_SIZE);
        return targetData;
    }
}
