using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Chord.Lib;

public readonly struct ChordKey
{
    #region Init

    private static readonly Random rng = new Random();

    public static ChordKey PickRandom()
        => PickRandom(long.MaxValue);

    public static ChordKey PickRandom(BigInteger maxId)
    {
        var newId = restMod(((long)rng.Next(int.MinValue, int.MaxValue) << 31)
            | (long)(uint)rng.Next(int.MinValue, int.MaxValue), maxId);
        return new ChordKey(newId, maxId);
    }

    public ChordKey(long id, BigInteger maxId)
        : this(new BigInteger(id), maxId) {}

    public ChordKey(BigInteger id, BigInteger maxId)
    {
        Id = restMod(id, maxId);
        this.maxId = maxId;
    }

    #endregion Init

    private readonly BigInteger maxId;
    public BigInteger Id { get; }

    #region Arithmetics

    private ChordKey Add(ChordKey other)
        => new ChordKey(id: restMod(Id + other.Id, maxId), maxId: maxId);

    private ChordKey Sub(ChordKey other)
        => new ChordKey(id: restMod(Id - other.Id, maxId), maxId: maxId);

    private static BigInteger restMod(BigInteger element, BigInteger classes)
        => (element % classes + classes) % classes;

    #endregion Arithmetics

    #region Comparison

    public static ChordKey operator +(ChordKey a, ChordKey b) => a.Add(b);
    public static ChordKey operator -(ChordKey a, ChordKey b) => a.Sub(b);
    public static bool operator ==(ChordKey a, ChordKey b) => a.Id == b.Id;
    public static bool operator !=(ChordKey a, ChordKey b) => a.Id != b.Id;
    public static bool operator <(ChordKey a, ChordKey b) => a.Id < b.Id;
    public static bool operator >(ChordKey a, ChordKey b) => a.Id > b.Id;
    public static bool operator <=(ChordKey a, ChordKey b) => a.Id <= b.Id;
    public static bool operator >=(ChordKey a, ChordKey b) => a.Id >= b.Id;

    public override bool Equals([NotNullWhen(true)] object obj)
        => obj?.GetType() == typeof(ChordKey) && ((ChordKey)obj).Id == Id;

    public override int GetHashCode() => (int)(Id % int.MaxValue);

    #endregion Comparison
}
