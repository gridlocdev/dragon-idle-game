using System.Globalization;

namespace DragonIdle;

/// <summary>Arbitrary-magnitude number stored as mantissa [1,10) * 10^exponent. Lets the game scale forever.</summary>
public readonly struct BigNum : IComparable<BigNum>
{
    public readonly double M;
    public readonly long E;

    public static readonly BigNum Zero = new(0, 0);
    public static readonly BigNum One = new(1, 0);

    BigNum(double m, long e) { M = m; E = e; }

    public static BigNum Make(double m, long e)
    {
        if (m == 0 || double.IsNaN(m)) return Zero;
        if (double.IsInfinity(m)) return new(m > 0 ? 9.99 : -9.99, 308 + e);
        int shift = (int)Math.Floor(Math.Log10(Math.Abs(m)));
        m /= Math.Pow(10, shift);
        e += shift;
        if (Math.Abs(m) >= 10) { m /= 10; e++; }
        else if (Math.Abs(m) < 1) { m *= 10; e--; }
        return new(m, e);
    }

    public static implicit operator BigNum(double d) => Make(d, 0);

    public static BigNum operator +(BigNum a, BigNum b)
    {
        if (a.M == 0) return b;
        if (b.M == 0) return a;
        if (a.E < b.E) (a, b) = (b, a);
        long diff = a.E - b.E;
        if (diff > 17) return a;
        return Make(a.M + b.M / Math.Pow(10, diff), a.E);
    }

    public static BigNum operator -(BigNum a) => new(-a.M, a.E);
    public static BigNum operator -(BigNum a, BigNum b) => a + (-b);
    public static BigNum operator *(BigNum a, BigNum b) => a.M == 0 || b.M == 0 ? Zero : Make(a.M * b.M, a.E + b.E);
    public static BigNum operator /(BigNum a, BigNum b) => b.M == 0 || a.M == 0 ? Zero : Make(a.M / b.M, a.E - b.E);

    public int CompareTo(BigNum o)
    {
        int sa = Math.Sign(M), sb = Math.Sign(o.M);
        if (sa != sb) return sa.CompareTo(sb);
        if (sa == 0) return 0;
        int c = E != o.E ? E.CompareTo(o.E) : M.CompareTo(o.M);
        if (E != o.E && sa < 0) c = -c;
        return c;
    }

    public static bool operator <(BigNum a, BigNum b) => a.CompareTo(b) < 0;
    public static bool operator >(BigNum a, BigNum b) => a.CompareTo(b) > 0;
    public static bool operator <=(BigNum a, BigNum b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BigNum a, BigNum b) => a.CompareTo(b) >= 0;

    public bool IsZero => M == 0;
    public bool IsPositive => M > 0;

    public double Log10() => M <= 0 ? double.NegativeInfinity : Math.Log10(M) + E;

    public static BigNum FromLog10(double l)
    {
        if (double.IsNegativeInfinity(l)) return Zero;
        long e = (long)Math.Floor(l);
        return Make(Math.Pow(10, l - e), e);
    }

    public static BigNum Pow(double b, double p) => b <= 0 ? Zero : FromLog10(Math.Log10(b) * p);
    public static BigNum Max(BigNum a, BigNum b) => a >= b ? a : b;
    public static BigNum Min(BigNum a, BigNum b) => a <= b ? a : b;

    public double ToDouble()
    {
        if (E > 308) return M > 0 ? double.MaxValue : double.MinValue;
        if (E < -308) return 0;
        return M * Math.Pow(10, E);
    }

    public BigNum Floor() => E >= 15 ? this : Make(Math.Floor(ToDouble()), 0);
    public BigNum Ceil() => E >= 15 ? this : Make(Math.Ceiling(ToDouble() - 1e-9), 0);

    static readonly string[] Suffixes =
    {
        "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc",
        "UDc", "DDc", "TDc", "QaDc", "QiDc", "SxDc", "SpDc", "OcDc", "NoDc", "Vg",
    };

    public string Format()
    {
        if (M == 0) return "0";
        if (M < 0) return "-" + (-this).Format();
        if (E < 3)
        {
            double v = ToDouble();
            if (v < 10 && Math.Abs(v - Math.Round(v)) > 0.05) return v.ToString("0.0", CultureInfo.InvariantCulture);
            return Math.Floor(v + 1e-9).ToString("0", CultureInfo.InvariantCulture);
        }
        long tier = E / 3;
        double mant = M * Math.Pow(10, E % 3);
        string suf;
        if (tier < Suffixes.Length) suf = Suffixes[tier];
        else
        {
            long t = tier - Suffixes.Length;
            if (t < 26 * 26) suf = $"{(char)('a' + t / 26)}{(char)('a' + t % 26)}";
            else return $"{M.ToString("0.00", CultureInfo.InvariantCulture)}e{E}";
        }
        string fmt = mant >= 100 ? "0" : mant >= 10 ? "0.0" : "0.00";
        string s = (Math.Floor(mant * (mant >= 100 ? 1 : mant >= 10 ? 10 : 100)) / (mant >= 100 ? 1 : mant >= 10 ? 10 : 100))
            .ToString(fmt, CultureInfo.InvariantCulture);
        return s + suf;
    }

    public override string ToString() => Format();

    public string Serialize() => $"{M.ToString("R", CultureInfo.InvariantCulture)}|{E}";

    public static BigNum Parse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return Zero;
        var p = s.Split('|');
        if (p.Length != 2) return Zero;
        return Make(double.Parse(p[0], CultureInfo.InvariantCulture), long.Parse(p[1], CultureInfo.InvariantCulture));
    }
}
