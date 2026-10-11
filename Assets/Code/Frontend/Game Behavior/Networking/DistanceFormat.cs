/// <summary>
/// Distance labels: metres up close, AU beyond. The game's AU is scaled to its systems
/// (planets orbit at roughly 0.3 to 5 AU), not the real 1.5e11 m.
/// </summary>
public static class DistanceFormat
{
    public const float UnitsPerAU = 10000f;
    private const float MetreLimit = 1000f;

    public static string Format(float distance)
    {
        if (distance < MetreLimit) return $"{distance:0} m";
        float au = distance / UnitsPerAU;
        return au < 1f ? $"{au:0.00} AU" : $"{au:0.0} AU";
    }
}
