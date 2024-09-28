using System.Text.Json;
using System.Text.Json.Serialization;

using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Utilities
{
    public class ServerConnectionDetailsJsonConverter : JsonConverter<ServerConnectionDetails>
    {
        public override ServerConnectionDetails Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Check if the token is a string
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected a string.");
            }

            // Get the string value (ip:port)
            string? address = reader.GetString();

            // Use the TryParse method to parse the value
            if (!string.IsNullOrEmpty(address) && ServerConnectionDetails.TryParse(address, out var connectionDetails))
            {
                return connectionDetails;
            }

            throw new JsonException($"Invalid format for ServerConnectionDetails: {address}");
        }

        public override void Write(Utf8JsonWriter writer, ServerConnectionDetails value, JsonSerializerOptions options)
        {
            // Convert ServerConnectionDetails back into a string in the format ip:port
            var ipPort = $"{value.Ip}:{value.Port}";
            writer.WriteStringValue(ipPort);
        }
    }
}
