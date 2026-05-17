using Godot;
using System;
using System.Collections.Generic;

public struct DTChunk
{
    public Texture2Drd Displacement;
    public Aabb BBox;
    public Vector2I ChunkCoord;
    public Rid TexRid;
}

public partial class ChunkPool : Node
{
    [Signal]
    public delegate void ChunkInQueueEventHandler();
    
    [Export] public bool HighPrecisionTextures = true;

    public float DisplacementMapRange;
    public int RowChunks;
    public int NChunks;
    public Godot.Collections.Array<bool> UsedChunks;

    private RenderingDevice _device;
    private RDTextureFormat _format;
    private RDTextureView _view;
    private DTChunk[] _pool = null;
    private FootprintStorage _fpStorage;
    private List<uint> _activeChunks;
    private uint _chunkIdx;
    private Vector2I _chunk;
    private int _radiusChunks;

    private Queue<int> _reconstructionQueue;

    public void Initialize(uint chunkRange, uint textureSize, ref readonly RenderingDevice device, ref FootprintStorage fpStorage, Vector2 playerPos)
    {
        _device = device;
        _radiusChunks = (int)chunkRange;
        _fpStorage = fpStorage;
        RowChunks = 2 * _radiusChunks + 1;
        NChunks = RowChunks * RowChunks;
        _pool = new DTChunk[NChunks];

        RecomputeChunk(playerPos);
        int xCoord = (_chunk.X % RowChunks) + RowChunks;
        int yCoord = (_chunk.Y % RowChunks) + RowChunks;
        xCoord = (xCoord + _radiusChunks) % RowChunks;
        yCoord = (yCoord + _radiusChunks) % RowChunks;
        _chunkIdx = (uint)(yCoord * RowChunks + xCoord);
        Vector2I centralChunk = _chunk + _radiusChunks * Vector2I.One - new Vector2I(xCoord, yCoord);
        
        _reconstructionQueue = new();
        UsedChunks = new();
        UsedChunks.Resize(NChunks);

        CreateSharedResources(textureSize);
        for (int i = 0; i < RowChunks; i++)
        {
            for (int j = 0; j < RowChunks; j++)
            {
                int idx = i * RowChunks + j;
                ref DTChunk curChunk = ref _pool[idx];
                curChunk = new();
                CreateTexture((int)textureSize, ref curChunk);
                curChunk.ChunkCoord = new Vector2I(j - _radiusChunks + centralChunk.X, i - _radiusChunks + centralChunk.Y);
            }
        }
        _activeChunks = [];
        UpdateActiveChunks(playerPos);
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
        Vector2I prevChunk = _chunk;

        RecomputeChunk(playerPosition);

        int xCoord = (_chunk.X % RowChunks) + RowChunks;
        int yCoord = (_chunk.Y % RowChunks) + RowChunks;
        xCoord = (xCoord + _radiusChunks) % RowChunks;
        yCoord = (yCoord + _radiusChunks) % RowChunks;
        
        uint prevIdx = _chunkIdx;
        _chunkIdx = (uint)(yCoord * RowChunks + xCoord);
        if (_chunkIdx != prevIdx)
        {
            HandleChunkTransition((int)prevIdx);

            _fpStorage.ExitLeft(prevChunk);
            _fpStorage.ExitRight(prevChunk);
            _fpStorage.EnterLeft(_chunk);
            _fpStorage.EnterRight(_chunk);
        }

        // Enter chunks
        _activeChunks.Clear();
        _activeChunks.Add(_chunkIdx);
        UsedChunks[(int)_chunkIdx] = true;
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

    public ref readonly Rid GetTextureRidAtIdx(uint idx)
    {
        return ref _pool[idx].TexRid;
    }

    public Rid GetReconstructedTexture()
    {
        int idx = _reconstructionQueue.Peek();
        return _pool[idx].TexRid;
    }

    public Vector2I GetReconstructedChunk()
    {
        int idx = _reconstructionQueue.Peek();
        return _pool[idx].ChunkCoord;
    }

    public void FinishReconstruction(bool populated = false)
    {
        int idx = _reconstructionQueue.Peek();
        _reconstructionQueue.Dequeue();
        if (_reconstructionQueue.Count > 0)
        {
            EmitSignal(SignalName.ChunkInQueue);
        }
        UsedChunks[idx] = populated;

    }

    private void CreateSharedResources(uint textureSize)
    {
        _format = new()
        {
            Width = textureSize,
            Height = textureSize,
            Format = HighPrecisionTextures ? RenderingDevice.DataFormat.R32Sfloat : RenderingDevice.DataFormat.R16Sfloat,
            UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit
                | RenderingDevice.TextureUsageBits.StorageBit
				| RenderingDevice.TextureUsageBits.CpuReadBit
                | RenderingDevice.TextureUsageBits.CanCopyFromBit
                | RenderingDevice.TextureUsageBits.SamplingBit
        };
        _view = new();
    }

    private void CreateTexture(int texSize, ref DTChunk targetChunk)
    {
        int dataSize = 2 * texSize * texSize;
        if (HighPrecisionTextures) dataSize *= 2;
        byte[] clearData = new byte[dataSize];
        targetChunk.TexRid = _device.TextureCreate(_format, _view, [clearData]);
        targetChunk.Displacement = new()
        {
            TextureRdRid = targetChunk.TexRid
        };
    }

    private void RecomputeChunk(Vector2 playerPosition)
    {
        Vector2 rawCoords = (playerPosition + Vector2.One * 0.5f * DisplacementMapRange) / DisplacementMapRange;
        if (float.IsNaN(rawCoords.X)) return;
        rawCoords = rawCoords.Floor();
        _chunk = new Vector2I((int)rawCoords.X, (int)rawCoords.Y);
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
            int texSize = (int)_pool[0].Displacement.GetSize().X;
            int dataSize = 2 * texSize * texSize;
            if (HighPrecisionTextures) dataSize *= 2;
            byte[] clearData = new byte[dataSize];
            for (int i = 0; i < RowChunks; i++)
            {
                int clearIdx = i * RowChunks + clearX;
                _device.TextureUpdate(_pool[clearIdx].TexRid, 0, clearData);
                _pool[clearIdx].ChunkCoord += new Vector2I(RowChunks * xDiff, 0);
                _reconstructionQueue.Enqueue(clearIdx);
            }
            EmitSignal(SignalName.ChunkInQueue);
        }
        if (prevY != curY)
        {
            int yDiff = curY - prevY;
            if (yDiff < -1) yDiff = 1;
            if (yDiff > 1) yDiff = -1;
            int clearY = (RowChunks + curY + RowChunks / 2 * yDiff) % RowChunks;
            int texSize = (int)_pool[0].Displacement.GetSize().X;
            int dataSize = 2 * texSize * texSize;
            if (HighPrecisionTextures) dataSize *= 2;
            byte[] clearData = new byte[dataSize];
            for (int i = 0; i < RowChunks; i++)
            {
                int clearIdx = clearY * RowChunks + i;
                _device.TextureUpdate(_pool[clearIdx].TexRid, 0, clearData);
                _pool[clearIdx].ChunkCoord += new Vector2I(0, RowChunks * yDiff);
                _reconstructionQueue.Enqueue(clearIdx);
            }
            EmitSignal(SignalName.ChunkInQueue);
        }
    }
}
