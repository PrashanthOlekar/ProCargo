namespace ProCargo.Domain.ValueObjects;

/// <summary>A WGS-84 coordinate.</summary>
public readonly record struct GeoPoint(decimal Latitude, decimal Longitude)
{
    private const double EarthRadiusKm = 6371.0088;

    public bool IsValid => Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;

    /// <summary>Great-circle distance in kilometres (haversine).</summary>
    public double DistanceKmTo(GeoPoint other)
    {
        var lat1 = ToRadians((double)Latitude);
        var lat2 = ToRadians((double)other.Latitude);
        var dLat = lat2 - lat1;
        var dLon = ToRadians((double)(other.Longitude - Longitude));

        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
