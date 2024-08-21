namespace CodeServerTools.CLI.Structs;

public readonly struct TunnelId : IComparable<TunnelId>, IEquatable<TunnelId>
{
    public string Value { get; }

    public TunnelId(string value)
    {
        Value = value;
    }

    public bool Equals(TunnelId other) => this.Value.Equals(other.Value);
    public int CompareTo(TunnelId other) => Value.CompareTo(other.Value);

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }
        return obj is TunnelId other && Equals(other);
    }

    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();

    public static bool operator ==(TunnelId a, TunnelId b) => a.CompareTo(b) == 0;
    public static bool operator !=(TunnelId a, TunnelId b) => !(a == b);
}
