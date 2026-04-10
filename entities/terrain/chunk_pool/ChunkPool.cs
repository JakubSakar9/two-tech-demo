using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// TODO:
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
    public int RowChunks;
    public int NChunks;

    private RenderingDevice _device;
    private RDTextureFormat _format;
    private RDTextureView _view;
    private DTChunk[] _pool = null;
    private FootprintStorage _fpStorage;
    private List<uint> _activeChunks;
    private uint _chunkIdx;
    private Vector2I _chunk;
    private int _radiusChunks;

    public void Initialize(uint chunkRange, uint textureSize, ref readonly RenderingDevice device, ref FootprintStorage fpStorage)
    {
        _device = device;
        _radiusChunks = (int)chunkRange;
        _fpStorage = fpStorage;
        RowChunks = 2 * _radiusChunks + 1;
        NChunks = RowChunks * RowChunks;
        _pool = new DTChunk[NChunks];
        _chunkIdx = (uint)NChunks / 2;

        CreateSharedResources(textureSize);
        for (uint i = 0; i < RowChunks; i++)
        {
            for (uint j = 0; j < RowChunks; j++)
            {
                uint idx = i * (uint)RowChunks + j;
                ref DTChunk curChunk = ref _pool[idx];
                curChunk = new();
                CreateTexture((int)textureSize, ref curChunk);
                curChunk.ChunkCoord = new Vector2I((int)j - (int)_radiusChunks, (int)i - (int)_radiusChunks);
            }
        }
        _activeChunks = [];
        UpdateActiveChunks(Vector2.Zero);
    }

    public void Cleanup(ref readonly RenderingDevice device)
    {
        for (uint i = 0; i < NChunks; i++)
        {
            device.FreeRid(_pool[i].TexRid);
        }
    }

    public void UpdateActiveChunks(Vector2 playerPosition)
    {
        Vector2 rawCoords = (playerPosition + Vector2.One * 0.5f * DisplacementMapRange) / DisplacementMapRange;
        if (float.IsNaN(rawCoords.X)) return;
        rawCoords = rawCoords.Floor();

        Vector2I prevChunk = _chunk;
        _chunk = new Vector2I((int)rawCoords.X, (int)rawCoords.Y);
        int xCoord = (_chunk.X % RowChunks) + RowChunks;
        int yCoord = (_chunk.Y % RowChunks) + RowChunks;
        xCoord = (xCoord + _radiusChunks) % RowChunks;
        yCoord = (yCoord + _radiusChunks) % RowChunks;
        
        uint prevIdx = _chunkIdx;
        _chunkIdx = (uint)(yCoord * RowChunks + xCoord);
        if (_chunkIdx != prevIdx)
        {
            // GD.Print("Changing from chunk " + prevChunk + " to chunk " + _chunkIdx);
            HandleChunkTransition((int)prevIdx);

            _fpStorage.ExitLeft(prevChunk);
            _fpStorage.ExitRight(prevChunk);
            _fpStorage.EnterLeft(_chunk);
            _fpStorage.EnterRight(_chunk);
        }

        // Enter chunks
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

    public ref readonly Texture2Drd GetTextureAtIdx(uint idx)
    {
        return ref _pool[idx].Displacement;
    }

    private void CreateSharedResources(uint textureSize)
    {
        _format = new()
        {
            Width = textureSize,
            Height = textureSize,
            Format = RenderingDevice.DataFormat.R8Unorm,
            UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit
                | RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CpuReadBit
                | RenderingDevice.TextureUsageBits.CanCopyFromBit
                | RenderingDevice.TextureUsageBits.SamplingBit
        };
        _view = new();
    }

    private void CreateTexture(int textureSize, ref DTChunk targetChunk)
    {
        // var im = Image.CreateEmpty(textureSize, textureSize, false, Image.Format.Rf);
        byte[] clearData = new byte[textureSize * textureSize];
        targetChunk.TexRid = _device.TextureCreate(_format, _view, [clearData]);
        targetChunk.Displacement = new();
        targetChunk.Displacement.TextureRdRid = targetChunk.TexRid;
    }

    private void HandleChunkTransition(int prevChunk)
    {
        int prevX = prevChunk % RowChunks;
        int prevY = prevChunk / RowChunks;
        int curX = (int)_chunkIdx % RowChunks;
        int curY = (int)_chunkIdx / RowChunks;
        if (prevX != curX)
        {
            int xDiff = curX - prevX;
            if (xDiff < -1) xDiff = 1;
            if (xDiff > 1) xDiff = -1;
            int clearX = (RowChunks + curX + RowChunks / 2 * xDiff) % RowChunks;
            uint texSize = (uint)_pool[0].Displacement.GetSize().X;
            byte[] clearData = new byte[texSize * texSize];
            for (uint i = 0; i < RowChunks; i++)
            {
                _device.TextureUpdate(_pool[i * RowChunks + clearX].TexRid, 0, clearData);
                _pool[i * RowChunks + clearX].ChunkCoord += new Vector2I(RowChunks * xDiff, 0);
            }
        }
        if (prevY != curY)
        {
            int yDiff = curY - prevY;
            if (yDiff < -1) yDiff = 1;
            if (yDiff > 1) yDiff = -1;
            int clearY = (RowChunks + curY + RowChunks / 2 * yDiff) % RowChunks;
            uint texSize = (uint)_pool[0].Displacement.GetSize().X;
            byte[] clearData = new byte[texSize * texSize];
            for (uint i = 0; i < RowChunks; i++)
            {
                _device.TextureUpdate(_pool[clearY * RowChunks + i].TexRid, 0, clearData);
                _pool[clearY * RowChunks + i].ChunkCoord += new Vector2I(0, RowChunks * yDiff);
            }
        }
    }
}
