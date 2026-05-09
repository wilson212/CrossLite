using System;

namespace CrossLite;

/// <summary>
/// A lightweight, allocation-free composite key for the IdentityMap.
/// Stores up to 5 key components inline (no boxing for common int/long PKs)
/// and provides a fast, collision-resistant hash.
/// </summary>
public readonly struct EntityKey : IEquatable<EntityKey>
{
    // Store the raw values — for single-key entities (the vast majority),
    // only Value1 is used and the rest are default.
    private readonly object _v1;
    private readonly object _v2;
    private readonly object _v3;
    private readonly object _v4;
    private readonly object _v5;
    private readonly int _count;
    private readonly int _hashCode;
    
    public EntityKey(object v1)
    {
        _v1 = v1; _v2 = null; _v3 = null; _v4 = null; _v5 = null;
        _count = 1;
        _hashCode = v1?.GetHashCode() ?? 0;
    }

    public EntityKey(object v1, object v2)
    {
        _v1 = v1; _v2 = v2; _v3 = null; _v4 = null; _v5 = null;
        _count = 2;
        _hashCode = HashCode.Combine(v1, v2);
    }

    public EntityKey(object v1, object v2, object v3)
    {
        _v1 = v1; _v2 = v2; _v3 = v3; _v4 = null; _v5 = null;
        _count = 3;
        _hashCode = HashCode.Combine(v1, v2, v3);
    }

    public EntityKey(object v1, object v2, object v3, object v4)
    {
        _v1 = v1; _v2 = v2; _v3 = v3; _v4 = v4; _v5 = null;
        _count = 4;
        _hashCode = HashCode.Combine(v1, v2, v3, v4);
    }

    public EntityKey(object v1, object v2, object v3, object v4, object v5)
    {
        _v1 = v1; _v2 = v2; _v3 = v3; _v4 = v4; _v5 = v5;
        _count = 5;
        _hashCode = HashCode.Combine(v1, v2, v3, v4, v5);
    }

    public bool Equals(EntityKey other)
    {
        if (_count != other._count || _hashCode != other._hashCode)
            return false;

        return _count switch
        {
            1 => Equals(_v1, other._v1),
            2 => Equals(_v1, other._v1) && Equals(_v2, other._v2),
            3 => Equals(_v1, other._v1) && Equals(_v2, other._v2) && Equals(_v3, other._v3),
            4 => Equals(_v1, other._v1) && Equals(_v2, other._v2) && Equals(_v3, other._v3) && Equals(_v4, other._v4),
            5 => Equals(_v1, other._v1) && Equals(_v2, other._v2) && Equals(_v3, other._v3) && Equals(_v4, other._v4) && Equals(_v5, other._v5),
            _ => false
        };
    }

    public override bool Equals(object obj) => obj is EntityKey other && Equals(other);
    public override int GetHashCode() => _hashCode;

    public static bool operator ==(EntityKey left, EntityKey right) => left.Equals(right);
    public static bool operator !=(EntityKey left, EntityKey right) => !left.Equals(right);
}