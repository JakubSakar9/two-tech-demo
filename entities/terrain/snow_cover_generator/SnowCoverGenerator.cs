using Godot;
using System;
using System.Collections.Generic;

public enum SCComputePass
{
    Temperature,
    Precipitation,
    Diffuse,
    Advect,
    Melting
}

public partial class SnowCoverGenerator : Node
{
    public readonly Dictionary<SCComputePass, string> SC_SHADER_FILES = new Dictionary<SCComputePass, string>
    {
        {SCComputePass.Temperature,"res://shaders/sc_temperature_compute.glsl"},
        {SCComputePass.Precipitation, "res://shaders/sc_precipitation_compute.glsl"},
        {SCComputePass.Diffuse, "res://shaders/sc_diffuse_compute.glsl"},
        {SCComputePass.Advect, "res://shaders/sc_advect_compute.glsl"},
        {SCComputePass.Melting, "res://shaders/sc_melting_compute.glsl"}
    };

    private RenderingDevice _device;
    private Dictionary<SCComputePass, Rid> _shaders;
    private Dictionary<SCComputePass, Rid> _pipelines;
    private Dictionary<SCComputePass, Rid> _uniformSets;
    private Rid[] _hmImages;
    private uint _texSize;

    private uint _swapIdx = 0;

    public void Init(uint texSize, uint mipCount)
    {
        _texSize = texSize;
        _device = RenderingServer.CreateLocalRenderingDevice();
        
        _shaders = new Dictionary<SCComputePass, Rid>();
        _pipelines = new Dictionary<SCComputePass, Rid>();
        _uniformSets = new Dictionary<SCComputePass, Rid>();

        CreatePipelines();
        CreateImages(mipCount);
    }
    
    public void UseHeightMap(ref readonly HeightMap heightMap)
    {
        _swapIdx = 0;
        byte[] bytes = heightMap.heightImage.GetData();
        _device.TextureUpdate(_hmImages[_swapIdx], 0, bytes);
    }

    public void UpdateHeightMap(ref HeightMap heightMap)
    {
        byte[] bytes = _device.TextureGetData(_hmImages[_swapIdx], 0);
        heightMap.heightImage = Image.CreateFromData((int)_texSize, (int)_texSize, true, Image.Format.Rgbaf, bytes);
        heightMap.heightImage.GenerateMipmaps();
    }

    public void Preprocess()
    {
        ComputeTemperature();
        ComputePrecipitation();
    }

    public void Iterate(uint nIters)
    {
        for (uint i = 0; i < nIters; i++)
        {
            ComputeDiffuse();
        }
        for (uint i = 0; i < nIters; i++)
        {
            ComputeAdvect();
        }
    }

    public void Postprocess()
    {
        ComputeMelting();
    }

    private void CreatePipelines()
    {
        foreach (SCComputePass pass in Enum.GetValues(typeof(SCComputePass)))
        {
            var shaderFile = GD.Load<RDShaderFile>(SC_SHADER_FILES[pass]);
            var shaderBytecode = shaderFile.GetSpirV();
            _shaders[pass] = _device.ShaderCreateFromSpirV(shaderBytecode);
            _pipelines[pass] = _device.ComputePipelineCreate(_shaders[pass]);
        }
    }

    private void CreateImages(uint mipCount)
    {
        var format = new RDTextureFormat
		{
			Width = _texSize,
			Height = _texSize,
			Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CanUpdateBit
                | RenderingDevice.TextureUsageBits.CpuReadBit,
			Mipmaps = mipCount + 1
            // Mipmaps = 1
		};
        var view = new RDTextureView();
        for (uint i = 0; i < 2; i++)
        {
            _hmImages[i] = _device.TextureCreate(format, view);
        }
    }

    private void ComputeTemperature()
    {
        BindTemperatureUniforms();
        // TODO
    }
    
    private void ComputePrecipitation()
    {
        BindPrecipitationUniforms();
        // TODO
    }

    private void ComputeDiffuse()
    {
        BindDiffuseUniforms();
        // TODO
    }

    private void ComputeAdvect()
    {
        BindAdvectUniforms();
        // TODO
    }

    private void ComputeMelting()
    {
        BindMeltingUniforms();
        // TODO
    }

    private void DispatchCompute(SCComputePass pass)
    {
        uint xGroups = _texSize / 16;
		uint yGroups = 1;
		uint zGroups = _texSize / 16;

		var computeList = _device.ComputeListBegin();

        _device.ComputeListBindComputePipeline(computeList, _pipelines[pass]);
        _device.ComputeListBindUniformSet(computeList, _uniformSets[pass], 0);
        _device.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);

        _device.ComputeListEnd();
        _device.Submit();
    }

    private void BindTemperatureUniforms()
    {
        var heightMapUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        heightMapUniform.AddId(_hmImages[_swapIdx]);

        Godot.Collections.Array<RDUniform> uniforms = [heightMapUniform];
        if (_uniformSets[SCComputePass.Temperature].IsValid && _device.UniformSetIsValid(_uniformSets[SCComputePass.Temperature]))
        {
            _device.FreeRid(_uniformSets[SCComputePass.Temperature]);
        }
        _uniformSets[SCComputePass.Temperature] = _device.UniformSetCreate(uniforms, _shaders[SCComputePass.Temperature], 0);
    }

    private void BindPrecipitationUniforms()
    {
        var heightMapUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        heightMapUniform.AddId(_hmImages[_swapIdx]);

        Godot.Collections.Array<RDUniform> uniforms = [heightMapUniform];
        if (_uniformSets[SCComputePass.Precipitation].IsValid && _device.UniformSetIsValid(_uniformSets[SCComputePass.Precipitation]))
        {
            _device.FreeRid(_uniformSets[SCComputePass.Precipitation]);
        }
        _uniformSets[SCComputePass.Precipitation] = _device.UniformSetCreate(uniforms, _shaders[SCComputePass.Precipitation], 0);
    }

    private void BindDiffuseUniforms()
    {
        var heightMapInUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        var heightMapOutUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 1
        };
        heightMapInUniform.AddId(_hmImages[_swapIdx]);
        heightMapOutUniform.AddId(_hmImages[1 - _swapIdx]);

        Godot.Collections.Array<RDUniform> uniforms = [heightMapInUniform, heightMapOutUniform];
        if (_uniformSets[SCComputePass.Diffuse].IsValid && _device.UniformSetIsValid(_uniformSets[SCComputePass.Diffuse]))
        {
            _device.FreeRid(_uniformSets[SCComputePass.Diffuse]);
        }
        _uniformSets[SCComputePass.Diffuse] = _device.UniformSetCreate(uniforms, _shaders[SCComputePass.Diffuse], 0);
        _swapIdx = 1 - _swapIdx;
    }

    private void BindAdvectUniforms()
    {
        var heightMapInUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        var heightMapOutUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 1
        };
        heightMapInUniform.AddId(_hmImages[_swapIdx]);
        heightMapOutUniform.AddId(_hmImages[1 - _swapIdx]);

        Godot.Collections.Array<RDUniform> uniforms = [heightMapInUniform, heightMapOutUniform];
        if (_uniformSets[SCComputePass.Advect].IsValid && _device.UniformSetIsValid(_uniformSets[SCComputePass.Advect]))
        {
            _device.FreeRid(_uniformSets[SCComputePass.Advect]);
        }
        _uniformSets[SCComputePass.Advect] = _device.UniformSetCreate(uniforms, _shaders[SCComputePass.Advect], 0);
        _swapIdx = 1 - _swapIdx;
    }

    private void BindMeltingUniforms()
    {
        var heightMapUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0
        };
        heightMapUniform.AddId(_hmImages[_swapIdx]);

        Godot.Collections.Array<RDUniform> uniforms = [heightMapUniform];
        if (_uniformSets[SCComputePass.Melting].IsValid && _device.UniformSetIsValid(_uniformSets[SCComputePass.Melting]))
        {
            _device.FreeRid(_uniformSets[SCComputePass.Melting]);
        }
        _uniformSets[SCComputePass.Melting] = _device.UniformSetCreate(uniforms, _shaders[SCComputePass.Melting], 0);
    }

}