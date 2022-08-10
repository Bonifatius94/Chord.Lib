namespace Chord.Lib;

/// <summary>
/// An immutable BigInteger key implementation supporting
/// the residue field arithmetics required for Chord.
/// </summary>
public readonly struct ChordKey : IComparable
{
    #region Init

    private static readonly Random rng = new Random();

    /// <summary>
    /// Pick a random key within [0, 2^64), i.e. keySpace=2^64-1.
    /// </summary>
    /// <returns>a new key instance with random id</returns>
    public static ChordKey PickRandom()
        => PickRandom(ulong.MaxValue);

    /// <summary>
    /// Pick a random key within [0, keySpace).
    /// </summary>
    /// <param name="keySpace">The number of possible keys to pick from.</param>
    /// <returns>a new key instance with random id</returns>
    public static ChordKey PickRandom(BigInteger keySpace)
        => new ChordKey(randId(keySpace), keySpace);

    private static BigInteger randId(BigInteger upperBound) {
        var bytes = upperBound.ToByteArray();
        rng.NextBytes(bytes);
        return restMod(new BigInteger(bytes), upperBound);
    }

    /// <summary>
    /// Create a key with the given residue field identity
    /// according to the given key space.
    /// </summary>
    /// <param name="id">The residue field identity that the new key represents.</param>
    /// <param name="keySpace">The upper bound of the residue field.</param>
    public ChordKey(BigInteger id, BigInteger keySpace)
    {
        Id = restMod(id, keySpace);
        this.keySpace = keySpace;
    }

    #endregion Init

    private readonly BigInteger keySpace;
    public BigInteger Id { get; }

    #region Arithmetics

    private ChordKey add(ChordKey other)
        => new ChordKey(id: restMod(Id + other.Id, keySpace), keySpace: keySpace);

    private ChordKey sub(ChordKey other)
        => new ChordKey(id: restMod(Id - other.Id, keySpace), keySpace: keySpace);

    private static BigInteger restMod(BigInteger element, BigInteger classes)
        => (element % classes + classes) % classes;

    #endregion Arithmetics

    #region Comparison

    public static ChordKey operator +(ChordKey a, ChordKey b) => a.add(b);
    public static ChordKey operator -(ChordKey a, ChordKey b) => a.sub(b);
    public static bool operator ==(ChordKey a, ChordKey b) => a.Id == b.Id;
    public static bool operator !=(ChordKey a, ChordKey b) => a.Id != b.Id;
    public static bool operator <(ChordKey a, ChordKey b) => a.Id < b.Id;
    public static bool operator >(ChordKey a, ChordKey b) => a.Id > b.Id;
    public static bool operator <=(ChordKey a, ChordKey b) => a.Id <= b.Id;
    public static bool operator >=(ChordKey a, ChordKey b) => a.Id >= b.Id;

    public override bool Equals([NotNullWhen(true)] object obj)
        => obj?.GetType() == typeof(ChordKey) && ((ChordKey)obj).Id == Id;

    public override int GetHashCode() => 0;

    public int CompareTo(object obj)
    {
        if ((obj?.GetType() == typeof(ChordKey)) != true)
            throw new ArgumentException("Can only compare to other chord keys!");

        var otherKey = (ChordKey)obj;
        return this < otherKey ? -1 : (this > otherKey ? 1 : 0);
    }

    #endregion Comparison

    public override string ToString() => $"{Id}";
}
