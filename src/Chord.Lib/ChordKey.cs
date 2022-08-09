using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Chord.Lib;

/// <summary>
/// An immutable BigInteger key implementation supporting
/// the residue field arithmetics required for Chord.
/// </summary>
public readonly struct ChordKey
{
    #region Init

    private static readonly Random rng = new Random();

    /// <summary>
    /// Pick a random key within [0, 2^64).
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
    {
        // TODO: this only produces keys within [0, 2^64) mod keySpace
        //       -> make this really expand into the BigInteger space within [0, keySpace)
        var newId = restMod(((ulong)rng.Next(int.MinValue, int.MaxValue) << 31)
            | (ulong)(uint)rng.Next(int.MinValue, int.MaxValue), keySpace);
        return new ChordKey(newId, keySpace);
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

    #endregion Comparison
}
