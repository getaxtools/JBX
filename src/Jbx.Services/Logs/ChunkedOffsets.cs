namespace Jbx.Services.Logs;

/// <summary>
/// A growing list of byte offsets kept in fixed-size chunks. A plain list doubles its array as it grows,
/// which for twenty million lines means briefly holding two arrays of hundreds of megabytes.
/// </summary>
internal sealed class ChunkedOffsets
{
    private const int ChunkShift = 16;
    private const int ChunkSize = 1 << ChunkShift;
    private const int ChunkMask = ChunkSize - 1;

    private readonly List<long[]> _chunks = [];

    public int Count { get; private set; }

    public long this[int index] => _chunks[index >> ChunkShift][index & ChunkMask];

    public long Last => this[Count - 1];

    public void Add(long offset)
    {
        if ((Count & ChunkMask) == 0)
        {
            _chunks.Add(new long[ChunkSize]);
        }

        _chunks[Count >> ChunkShift][Count & ChunkMask] = offset;
        Count++;
    }
}
