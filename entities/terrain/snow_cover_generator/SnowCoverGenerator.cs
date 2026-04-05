using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public enum SCComputePass
{
    Temperature,
    Precipitation,
    Diffuse, // Currently not in use
    Advect,
    Melting
}

public struct TemperatureParams
{
    public float DirectSunTemperatureIncrease;
    public float SunIntensity;
    public float AltitudeTemperatureDecrease;
    public float SeaTemperature;
}

public struct PrecipitationParams
{
    public float MaxAltitude;
    public float SnowThresholdAltitude;
    public float MaxSnowHeight;
    public float PowderyRatio;
};

public struct AdvectionParams
{
    public Vector2 WindVec; // TODO: Replace this with surface wind field sampling later
    public float WindStrengthStepMultiplier;
    uint _padding0;
}

public struct MeltingParams
{
    public float MeltingRate;
    public float MeltingPoint;
    uint _padding0;
    uint _padding1;

}

public partial class SnowCoverGenerator : Node
{
    [ExportCategory("General")]
    [Export] public int EventCycleCount = 3;
    [Export] public bool SaveDebugTexture = true;
    [ExportCategory("Temperature")]
    [Export] public float SeaTemperature = 279.0f;
    [Export] public float TopTemperature = 255.0f;
    [Export] public float DirectSunlightHeat = 5.0f;
    
    [ExportCategory("Precipitation")]
    [Export] public float SnowingAltitude = 8.0f;
    [Export] public float MaxSnowPerHour = 0.1f;
    [Export] public float MaxSnowingDurationHours = 4.0f;
    /// <summary>
    /// Controls how much snow on steep snows becomes powdery.
    /// If set to zero, steep slopes do not get covered with any snow and wind advection event does nothing.
    /// </summary>
    [Export] public float PowderySnowRatio = 0.6f;
    
    [ExportCategory("Advection")]
    [Export] public int AdvectionIterations = 4;
    /// <summary>
    /// Controls how far does powdery snow get shifted after all advection iterations.
    /// The total distance is calculated as a product of the length of the wind vector and this parameter.
    /// </summary>
    [Export] public float AdvectionInfluence = 0.7f;
    [ExportCategory("Melting")]
    /// <summary>
    /// Snow melting rate given in time units per meter per kelvins above melting point
    /// </summary>
    [Export] public float MeltingRate = 0.05f;
    /// <summary>
    /// Snow melting point given in kelvins
    /// </summary>
    [Export] public float MeltingPoint = 274.0f;


    public readonly Dictionary<SCComputePass, string> SC_SHADER_FILES = new()
    {
        {SCComputePass.Temperature,"res://shaders/sc_temperature_compute.glsl"},
        {SCComputePass.Precipitation, "res://shaders/sc_precipitation_compute.glsl"},
        {SCComputePass.Diffuse, "res://shaders/sc_diffuse_compute.glsl"},
        {SCComputePass.Advect, "res://shaders/sc_advect_compute.glsl"},
        {SCComputePass.Melting, "res://shaders/sc_melting_compute.glsl"}
    };

    private WindGenerator _windGen;
    private RenderingDevice _device;
    private Dictionary<SCComputePass, Rid> _shaders;
    private Dictionary<SCComputePass, Rid> _pipelines;
    private Dictionary<SCComputePass, Rid> _uniformSets;
    private Rid[] _hmImages;

    private TemperatureParams _tParams;
    private PrecipitationParams _pParams;
    private AdvectionParams _aParams;
    private MeltingParams _mParams;

    private string[] _eventList;
    private long _computeList;
    private uint _texSize;
    private uint _debugTextureStep = 0;

    private uint _swapIdx = 0;

    public void Init(uint texSize, WindGenerator windGen)
    {
        _texSize = texSize;
        _windGen = windGen;
        InitCompute();
        InitParams();
    }
    
    /// <summary>
    /// Passes the height map to the compute pipeline and initiates the compute list.
    /// </summary>
    /// <param name="heightMap">Height map struct</param>
    public void UseHeightMap(ref readonly HeightMap heightMap)
    {
        _swapIdx = 0;
        _device.TextureUpdate(_hmImages[_swapIdx], 0, heightMap.bytes);
        _computeList = _device.ComputeListBegin();
    }

    /// <summary>
    /// Updates the height map based on the computation result. Ends the compute list and blocks the current thread until the result is retrieved.s
    /// </summary>
    /// <param name="heightMap">Height map struct</param>
    public void UpdateHeightMap(ref HeightMap heightMap)
    {
        _device.ComputeListEnd();
        _device.Submit();
        _device.Sync();
        heightMap.bytes = _device.TextureGetData(_hmImages[_swapIdx], 0);
        heightMap.heightImage = Image.CreateFromData((int)_texSize, (int)_texSize, false, Image.Format.Rgbaf, heightMap.bytes);

        if (SaveDebugTexture)
        {
            string suffix = _debugTextureStep++.ToString("D3") + ".exr";
            heightMap.heightImage.SaveExr("res://debug_output/height_map" + suffix);
        }

        heightMap.heightImage.GenerateMipmaps();
        heightMap.height.SetImage(heightMap.heightImage);
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
            ComputeAdvect();
            // ComputeDiffuse();
        }
    }

    public void Postprocess()
    {
        ComputeMelting();
    }

    private void InitCompute()
    {
        _device = RenderingServer.CreateLocalRenderingDevice();
        
        _shaders = [];
        _pipelines = [];
        _uniformSets = [];

        foreach (SCComputePass pass in Enum.GetValues(typeof(SCComputePass)))
        {
            var shaderFile = GD.Load<RDShaderFile>(SC_SHADER_FILES[pass]);
            var shaderBytecode = shaderFile.GetSpirV();
            _shaders[pass] = _device.ShaderCreateFromSpirV(shaderBytecode);
            _pipelines[pass] = _device.ComputePipelineCreate(_shaders[pass]);
            _uniformSets[pass] = new Rid();
        }
        CreateImages();
    }

    private void InitParams()
    {
        float maxAltitude = GetParent<Terrain>().MaxAltitude;
        
        _tParams = new()
        {
            DirectSunTemperatureIncrease = DirectSunlightHeat,
            SunIntensity = 1.0f,
            AltitudeTemperatureDecrease = (SeaTemperature - TopTemperature) / maxAltitude,
            SeaTemperature = SeaTemperature
        };

        _pParams = new()
        {
            MaxAltitude = maxAltitude,
            SnowThresholdAltitude = SnowingAltitude,
            MaxSnowHeight = MaxSnowingDurationHours * MaxSnowPerHour,
            PowderyRatio = PowderySnowRatio
        };

        _aParams = new()
        {
            WindVec = _windGen.BaseWindVelocity,
            WindStrengthStepMultiplier = 4.0f
        };

        _mParams = new()
        {
            MeltingRate = MeltingRate,
            MeltingPoint = MeltingPoint
        };
    }

    private void CreateImages()
    {
        var format = new RDTextureFormat
		{
			Width = _texSize,
			Height = _texSize,
			Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CanUpdateBit
                | RenderingDevice.TextureUsageBits.CpuReadBit
                | RenderingDevice.TextureUsageBits.CanCopyFromBit,
            Mipmaps = 1
		};
        var view = new RDTextureView();
        _hmImages = new Rid[2];
        for (uint i = 0; i < 2; i++)
        {
            _hmImages[i] = _device.TextureCreate(format, view);
        }
    }

    private void ComputeTemperature()
    {
        BindTemperatureUniforms();
        DispatchCompute(SCComputePass.Temperature);
    }
    
    private void ComputePrecipitation()
    {
        BindPrecipitationUniforms();
        DispatchCompute(SCComputePass.Precipitation);
    }

    private void ComputeDiffuse()
    {
        BindDiffuseUniforms();
        DispatchCompute(SCComputePass.Diffuse);
    }

    private void ComputeAdvect()
    {
        BindAdvectUniforms();
        DispatchCompute(SCComputePass.Advect);
    }

    private void ComputeMelting()
    {
        BindMeltingUniforms();
        DispatchCompute(SCComputePass.Melting);
    }

    private void DispatchCompute(SCComputePass pass)
    {
        uint xGroups = _texSize / 16;
		uint yGroups = _texSize / 16;
        uint zGroups = 1;

        _device.ComputeListBindComputePipeline(_computeList, _pipelines[pass]);
        _device.ComputeListBindUniformSet(_computeList, _uniformSets[pass], 0);
        byte[] paramsData = ParamsToBytes(pass);
		_device.ComputeListSetPushConstant(_computeList, paramsData, (uint)paramsData.Length);
        _device.ComputeListDispatch(_computeList, xGroups, yGroups, zGroups);
        _device.ComputeListAddBarrier(_computeList);
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

    private byte[] ParamsToBytes(SCComputePass pass)
    {
        if (pass == SCComputePass.Diffuse)
        {
            GD.PrintErr("Diffuse shader has no push constants!");
            return [];
        }
        
        int size = GetParamsSize(pass);
        byte[] output = new byte[size];
		IntPtr ptr = IntPtr.Zero;
		try
		{
			ptr = Marshal.AllocHGlobal(size);
            switch(pass)
            {
                case SCComputePass.Temperature:
                    Marshal.StructureToPtr(_tParams, ptr, true);
                    break;
                case SCComputePass.Precipitation:
                    Marshal.StructureToPtr(_pParams, ptr, true);
                    break;
                case SCComputePass.Advect:
                    Marshal.StructureToPtr(_aParams, ptr, true);
                    break;
                case SCComputePass.Melting:
                    Marshal.StructureToPtr(_mParams, ptr, true);
                    break;
            }
			Marshal.Copy(ptr, output, 0, size);
		}
		finally
		{
			Marshal.FreeHGlobal(ptr);
		}
		return output;
    }

    private int GetParamsSize(SCComputePass pass) => pass switch
    {
        SCComputePass.Temperature => Marshal.SizeOf(_tParams),
        SCComputePass.Precipitation => Marshal.SizeOf(_pParams),
        SCComputePass.Advect => Marshal.SizeOf(_aParams),
        SCComputePass.Melting => Marshal.SizeOf(_mParams),
        _ => 0
    };
}