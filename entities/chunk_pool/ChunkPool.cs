using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// TODO:
// - Update the texture painting shader to take chunk offset into account
// - Update the terrain displacement shader to use multiple textures at the same time with appropriate offsets
// - Allow for multiple render targets at once during edge cases
// - Clear some textures and update their chunk indices as player goes further

public struct DTChunk
{
    public Texture2Drd Displacement;
    public Aabb BBox;
    public Vector2I ChunkCoord;
    public Rid TexRid;
}

public partial class ChunkPool : Node
{
    public float DisplacementMapRange;

    private RDTextureFormat _format;
    private RDTextureView _view;
    private DTChunk[] _pool = null;
    private List<uint> _activeChunks;
    private uint _chunkIdx;
    private int _radiusChunks;
    private int _rowChunks;
    private int _nChunks;

    public void Initialize(uint chunkRange, uint textureSize, ref readonly RenderingDevice device)
    {
        _radiusChunks = (int)chunkRange;
        _rowChunks = 2 * _radiusChunks + 1;
        _nChunks = _rowChunks * _rowChunks;
        _pool = new DTChunk[_nChunks];

        CreateSharedResources(textureSize, in device);
        for (uint i = 0; i < _rowChunks; i++)
        {
            for (uint j = 0; j < _rowChunks; j++)
            {
                uint idx = i * (uint)_rowChunks + j;
                ref DTChunk curChunk = ref _pool[idx];
                curChunk = new();
                CreateTexture((int)textureSize, ref curChunk, in device);
                curChunk.ChunkCoord = new Vector2I((int)j - (int)_radiusChunks, (int)i - (int)_radiusChunks);
            }
        }
        _activeChunks = [];
        UpdateActiveChunks(Vector2.Zero);
    }

    public void Cleanup(ref readonly RenderingDevice device)
    {
        for (uint i = 0; i < _nChunks; i++)
        {
            device.FreeRid(_pool[i].TexRid);
        }
    }

    public void UpdateActiveChunks(Vector2 playerPosition)
    {
        Vector2 rawCoords = (playerPosition + Vector2.One * 0.5f * DisplacementMapRange) / DisplacementMapRange;
        Vector2I chunkCoords = Vector2I.Zero;
        chunkCoords.X = (((int)Mathf.Floor(rawCoords.X)) % _rowChunks) + _rowChunks;
        chunkCoords.Y = (((int)Mathf.Floor(rawCoords.Y)) % _rowChunks) + _rowChunks;
        int xCoord = (chunkCoords.X + _radiusChunks) % _rowChunks;
        int yCoord = (chunkCoords.Y + _radiusChunks) % _rowChunks;
        _chunkIdx = (uint)(yCoord * _rowChunks + xCoord);
        _activeChunks.Clear();
        _activeChunks.Add(_chunkIdx);
    }

    public List<DTChunk> GetTargetChunks()
    {
        List<DTChunk> chunks = new();
        foreach (uint idx in _activeChunks)
        {
            chunks.Add(_pool[idx]);
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
