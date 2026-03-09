using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public struct DTChunk
{
    public Texture2Drd Displacement;
    public Aabb BBox;
    public Vector2I ChunkCoord;
    public Rid TexRid;
}

public partial class ChunkPool : Node
{
    private RDTextureFormat _format;
    private RDTextureView _view;
    private DTChunk[] _pool = null;
    private List<uint> _activeChunks;
    private uint _chunkIdx;
    private uint _chunkRange;
    private uint _chunkRow;
    private uint _nChunks;

    public void Initialize(uint chunkRange, uint textureSize, ref readonly RenderingDevice device)
    {
        _chunkRange = chunkRange;
        _chunkRow = 2 * chunkRange + 1;
        _nChunks = _chunkRow * _chunkRow;
        _pool = new DTChunk[_nChunks];

        CreateSharedResources(textureSize, in device);
        for (uint i = 0; i < _chunkRow; i++)
        {
            for (uint j = 0; j < _chunkRow; j++)
            {
                uint idx = i * _chunkRow + j;
                ref DTChunk curChunk = ref _pool[idx];
                curChunk = new();
                CreateTexture((int)textureSize, ref curChunk, in device);
                curChunk.ChunkCoord = new Vector2I((int)j - (int)_chunkRange, (int)i - (int)_chunkRange);
            }
        }
        _activeChunks = new();
    }

    public void Cleanup(ref readonly RenderingDevice device)
    {
        for (uint i = 0; i < _nChunks; i++)
        {
            device.FreeRid(_pool[i].TexRid);
        }
    }

    public void UpdateActiveChunks()
    {
        _chunkIdx = _nChunks / 2;
        _activeChunks.Clear();
        _activeChunks.Add(_chunkIdx);
    }

    public List<DTChunk> GetTargetChunks()
    {
        List<DTChunk> chunks = new();
        for (uint i = 0; i < _activeChunks.Count; i++)
        {
            chunks.Add(_pool[i]);
        }
        return chunks;
    }

    public ref readonly Texture2Drd GetCurrentTexture()
    {
        return ref _pool[_chunkIdx].Displacement;
    }

    private void CreateSharedResources(uint textureSize, ref readonly RenderingDevice device)
    {
        _format = new()
        {
            Width = textureSize,
            Height = textureSize,
            Format = RenderingDevice.DataFormat.R32Sfloat,
            UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit
                | RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CpuReadBit
                | RenderingDevice.TextureUsageBits.CanCopyFromBit
                | RenderingDevice.TextureUsageBits.SamplingBit
				| RenderingDevice.TextureUsageBits.CanUpdateBit
        };
        _view = new();
    }

    private void CreateTexture(int textureSize, ref DTChunk targetChunk, ref readonly RenderingDevice device)
    {
        var im = Image.CreateEmpty(textureSize, textureSize, false, Image.Format.Rf);
        targetChunk.TexRid = device.TextureCreate(_format, _view, [im.GetData()]);
        targetChunk.Displacement = new();
        targetChunk.Displacement.TextureRdRid = targetChunk.TexRid;
    }
}
