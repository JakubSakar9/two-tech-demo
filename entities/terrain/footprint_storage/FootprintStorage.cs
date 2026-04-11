using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class FootprintStorage : Node
{
    [Export] public int StepLimit = 65536;
    [Export] public int RenderBatchSize = 64;

    float[] _lData;
    float[] _rData;
    private int _lIdx = -1;
    private int _rIdx = -1;
    private int _blockIdx = 0;
    private int _blockOffset = 0;
    private List<Vector2I> _lChunks = [];
    private List<Vector2I> _rChunks = [];
    
    private Dictionary<Vector2I, List<int>> _lStartIds;
    private Dictionary<Vector2I, List<int>> _rStartIds;
    private Dictionary<Vector2I, List<int>> _lEndIds;
    private Dictionary<Vector2I, List<int>> _rEndIds;
    private Dictionary<int, List<Vector2I>> _lStartChunks;
    private Dictionary<int, List<Vector2I>> _rStartChunks;
    private Dictionary<int, List<Vector2I>> _lEndChunks;
    private Dictionary<int, List<Vector2I>> _rEndChunks;

    public override void _Ready()
    {
        base._Ready();

        _lData = new float[4 * StepLimit];
        _rData = new float[4 * StepLimit];
        _lChunks = [];
        _rChunks = [];
        _lIdx = StepLimit - 1;
        _rIdx = StepLimit - 1;

        _lStartIds = new();
        _rStartIds = new();
        _lEndIds = new();
        _rEndIds = new();

        _lStartChunks = new();
        _rStartChunks = new();
        _lEndChunks = new();
        _rEndChunks = new();
    }

    public void EnterLeft(Vector2I chunkCoord)
    {
        _lChunks.Add(chunkCoord);
        _lStartIds[chunkCoord] = [];
        _lEndIds[chunkCoord] = [];
        GD.Print("Stored enter indices and chunks:");
        foreach (int idx in _lStartChunks.Keys)
        {
            GD.Print(idx + ": " + _lStartChunks[idx][0]);
        }
        foreach(Vector2I ch in _lStartIds.Keys)
        {
            if (_lStartIds[ch].Count > 0)
            {
                GD.Print(ch + ": " + _lStartIds[ch][0]);
            }
            else
            {
                GD.Print(ch + ": ");
            }
        }
        GD.Print("Stored end indices and chunks:");
        foreach (int idx in _lEndChunks.Keys)
        {
            GD.Print(idx + ": " + _lEndChunks[idx][0]);
        }
        foreach(Vector2I ch in _lEndIds.Keys)
        {
            if (_lEndIds[ch].Count > 0)
            {
                GD.Print(ch + ": " + _lEndIds[ch][0]);
            }
            else
            {
                GD.Print(ch + ": ");
            }
        }
    }

    public void EnterRight(Vector2I chunkCoord)
    {
        _rChunks.Add(chunkCoord);
        _rStartIds[chunkCoord] = [];
        _rEndIds[chunkCoord] = [];
    }

    public void ExitLeft(Vector2I chunkCoord)
    {
        _lChunks.Remove(chunkCoord);
    }

    public void ExitRight(Vector2I chunkCoord)
    {
        _rChunks.Remove(chunkCoord);
    }

    public void SaveEntryLeft(Vector2 position, float carveDepth, float angle)
    {
        int prevIdx = _lIdx;
        _lIdx = (_lIdx + 1) % StepLimit;
        _lData[4 * _lIdx + 0] = position.X;
        _lData[4 * _lIdx + 1] = position.Y;
        _lData[4 * _lIdx + 2] = carveDepth;
        _lData[4 * _lIdx + 3] = angle;

        // Data block overwrite handling
        if (_lEndChunks.ContainsKey(_lIdx))
        {
            // Overwriting end, remove block entirely
            foreach (Vector2I chunk in _lEndChunks[_lIdx])
            {
                RemLeftStart(chunk, _lIdx);
                RemLeftEnd(chunk, _lIdx);
            }
            _lStartChunks.Remove(_lIdx);
        }
        else if (_lStartChunks.ContainsKey(_lIdx))
        {
            // Overwriting start, move block start forward
            int nextIdx = (_lIdx + 1) % StepLimit;
            foreach (Vector2I chunk in _lEndChunks[_lIdx])
            {
                RemLeftStart(chunk, _lIdx);
                AddLeftStart(chunk, nextIdx);
            }
            _lStartChunks.Remove(_lIdx);
        }

        _lStartChunks[_lIdx] = [];
        _lEndChunks[_lIdx] = [];
        foreach (Vector2I curChunk in _lChunks)
        {
            
            if (!_lEndIds.ContainsKey(curChunk)) _lEndIds[curChunk] = [];

            if (!_lEndChunks.ContainsKey(prevIdx) || !_lEndChunks[prevIdx].Contains(curChunk) || _lIdx == 0)
            {
                // This is the first index with this chunk, add start
                if (!_lStartIds.ContainsKey(curChunk)) _lStartIds[curChunk] = [];
                AddLeftStart(curChunk, _lIdx);
            }
            else
            {
                // This is not the first index with this chunk, remove former end
                RemLeftEnd(curChunk, prevIdx);
                if (_lEndChunks[prevIdx].Count == 0) _lEndChunks.Remove(prevIdx);
            }

            // Add new end
            AddLeftEnd(curChunk, _lIdx);
        }
        if (_lStartChunks[_lIdx].Count == 0) _lStartChunks.Remove(_lIdx);
    }

    public void SaveEntryRight(Vector2 position, float carveDepth, float angle)
    {
        int prevIdx = _rIdx;
        _rIdx = (_rIdx + 1) % StepLimit;

        _rData[4 * _rIdx + 0] = position.X;
        _rData[4 * _rIdx + 1] = position.Y;
        _rData[4 * _rIdx + 2] = carveDepth;
        _rData[4 * _rIdx + 3] = angle;

        // Data block overwrite handling
        if (_rEndChunks.ContainsKey(_rIdx))
        {
            // Overwriting end, remove block entirely
            foreach (Vector2I chunk in _rEndChunks[_rIdx])
            {
                RemRightStart(chunk, _rIdx);
                RemRightEnd(chunk, _rIdx);
            }
            _rStartChunks.Remove(_rIdx);
        }
        else if (_rStartChunks.ContainsKey(_rIdx))
        {
            // Overwriting start, move block start forward
            int nextIdx = (_rIdx + 1) % StepLimit;
            foreach (Vector2I chunk in _rEndChunks[_rIdx])
            {
                RemRightStart(chunk, _rIdx);
                AddRightStart(chunk, nextIdx);
            }
            _rStartChunks.Remove(_rIdx);
        }

        _rStartChunks[_rIdx] = [];
        _rEndChunks[_rIdx] = [];
        foreach (Vector2I curChunk in _rChunks)
        {
            
            if (!_rEndIds.ContainsKey(curChunk)) _rEndIds[curChunk] = [];

            if (!_rEndChunks.ContainsKey(prevIdx) || !_rEndChunks[prevIdx].Contains(curChunk) || _rIdx == 0)
            {
                // This is the first index with this chunk, add start
                if (!_rStartIds.ContainsKey(curChunk)) _rStartIds[curChunk] = [];
                AddRightStart(curChunk, _rIdx);
            }
            else
            {
                // This is not the first index with this chunk, remove former end
                RemRightEnd(curChunk, prevIdx);
                if (_rEndChunks[prevIdx].Count == 0) _rEndChunks.Remove(prevIdx);
            }

            // Add new end
            AddRightEnd(curChunk, _rIdx);
        }
        if (_rStartChunks[_rIdx].Count == 0) _rStartChunks.Remove(_rIdx);
    }

    public bool HasChunkLeft(Vector2I chunk)
    {
        return _lEndIds.ContainsKey(chunk) && _lEndIds[chunk].Count > 0;
    }

    public bool HasChunkRight(Vector2I chunk)
    {
        return _rEndIds.ContainsKey(chunk) && _rEndIds[chunk].Count > 0;
    }


    /// <summary>
    /// Populates SSBO given by RID with portion of left footprint data for the given chunk
    /// </summary>
    /// <param name="device">Local rendering device used for batched footprint rendering</param>
    /// <param name="buffer">RID of the SSBO to populate</param>
    /// <param name="chunk">Integer coordinates of the reconstructed chunk</param>
    /// <returns>False if given batch of footprints is not the last one, true otherwise.</returns>
    public bool PopulateBufferChunkLeft(ref readonly RenderingDevice device, ref Rid buffer, ref int fpCount, Vector2I chunk)
    {
        byte[] bufferData = new byte[RenderBatchSize * 4 * sizeof(float)];
        int startIdx = _lStartIds[chunk][_blockIdx];
        int startIdxO = startIdx + _blockOffset;
        int endIdx = _lEndIds[chunk][_blockIdx];
        fpCount = Math.Min(RenderBatchSize, endIdx - startIdxO + 1);

        Buffer.BlockCopy(_lData, 4 * startIdxO, bufferData, 0, 4 * fpCount);
        device.BufferUpdate(buffer, 0, (uint)bufferData.Length, bufferData);

        if (startIdxO + fpCount <= endIdx)
        {
            _blockOffset += RenderBatchSize;
            return false;
        }
        _blockOffset = 0;
        if (++_blockIdx == _lEndIds[chunk].Count)
        {
            _blockIdx = 0;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Populates SSBO given by RID with portion of right footprint data for the given chunk
    /// </summary>
    /// <param name="device">Local rendering device used for batched footprint rendering</param>
    /// <param name="buffer">RID of the SSBO to populate</param>
    /// <param name="chunk">Integer coordinates of the reconstructed chunk</param>
    /// <returns>False if given batch of footprints is not the last one, true otherwise.</returns>
    public bool PopulateBufferChunkRight(ref readonly RenderingDevice device, ref Rid buffer, ref int fpCount, Vector2I chunk)
    {
        byte[] bufferData = new byte[RenderBatchSize * 4 * sizeof(float)];
        int startIdx = _rStartIds[chunk][_blockIdx];
        int startIdxO = startIdx + _blockOffset;
        int endIdx = _rEndIds[chunk][_blockIdx];
        fpCount = Math.Min(RenderBatchSize, endIdx - startIdxO + 1);

        Buffer.BlockCopy(_rData, 4 * startIdxO, bufferData, 0, 4 * fpCount);
        device.BufferUpdate(buffer, 0, (uint)bufferData.Length, bufferData);

        if (startIdxO + fpCount <= endIdx)
        {
            _blockOffset += RenderBatchSize;
            return false;
        }
        _blockOffset = 0;
        if (++_blockIdx == _rEndIds[chunk].Count)
        {
            _blockIdx = 0;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddLeftStart(Vector2I chunk, int idx)
    {
        _lStartIds[chunk].Add(idx);
        _lStartChunks[idx].Add(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddRightStart(Vector2I chunk, int idx)
    {
        _rStartIds[chunk].Add(idx);
        _rStartChunks[idx].Add(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddLeftEnd(Vector2I chunk, int idx)
    {
        _lEndIds[chunk].Add(idx);
        _lEndChunks[idx].Add(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddRightEnd(Vector2I chunk, int idx)
    {
        _rEndIds[chunk].Add(idx);
        _rEndChunks[idx].Add(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemLeftStart(Vector2I chunk, int idx)
    {
        _lStartIds[chunk].Remove(idx);
        _lStartChunks[idx].Remove(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemRightStart(Vector2I chunk, int idx)
    {
        _rStartIds[chunk].Remove(idx);
        _rStartChunks[idx].Remove(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemLeftEnd(Vector2I chunk, int idx)
    {
        _lEndIds[chunk].Remove(idx);
        _lEndChunks[idx].Remove(chunk);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemRightEnd(Vector2I chunk, int idx)
    {
        _rEndIds[chunk].Remove(idx);
        _rEndChunks[idx].Remove(chunk);
    }
}
