namespace Nomax.BuildingBlocks.Hosting;

/// <summary>
/// Keeps secrets that travel in URLs out of the logs (docs/02 §15): the ICS feed token is part of the path.
/// Query strings, which may carry ?access_token= on /hubs, are never logged at all.
/// </summary>
public static class LogRedaction
{
    private const string IcalPrefix = "/ical/";

    public static string RedactPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return path.StartsWith(IcalPrefix, StringComparison.OrdinalIgnoreCase) ? IcalPrefix + "[redacted]" : path;
    }
}
