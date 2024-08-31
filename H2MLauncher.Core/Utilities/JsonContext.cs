using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Utilities
{
    /// <summary>
    /// Class required for trimming file size so compiler knows what types are needed
    /// and prevents them from being removed.
    /// </summary>
    [JsonSerializable(typeof(List<string>))]
    public partial class JsonContext : JsonSerializerContext
    {
    }
}
