using System.Diagnostics.CodeAnalysis;

namespace MatchmakingServer.Authorization;

public static class RouteValueDictionaryExtensions
{
    public static bool TryGetGuidValue(this RouteValueDictionary routeValues, string key, [NotNullWhen(true)] out Guid? value)
    {
        value = null;

        // Try to get the key from route data
        if (!routeValues.TryGetValue(key, out object? valueObj))
        {
            return false;
        }

        if (valueObj is string valueStr)
        {
            if (Guid.TryParse(valueStr, out Guid valueGuid))
            {
                value = valueGuid;
                return true;
            }
            else
            {
                return false;
            }
        }
        else if (valueObj is Guid valueGuid) // If model binding already parsed it to Guid
        {
            // Directly use the GUID
            value = valueGuid;
            return true;
        }
        else
        {
            return false;
        }
    }
}
