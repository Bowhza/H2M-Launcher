using System.Text.Json.Serialization;
using System.Text.Json;

namespace MatchmakingServer.Tests;

public static class JsonSerialization
{
    public readonly static JsonSerializerOptions SerializerOptions;

    static JsonSerialization()
    {
        SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
}
