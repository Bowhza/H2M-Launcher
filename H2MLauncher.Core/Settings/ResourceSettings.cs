namespace H2MLauncher.Core.Settings
{
    public class ResourceSettings
    {
        public List<IW4MMapPack> MapPacks { get; set; } = [];
        public List<IW4MObjectMap> GameTypes { get; set; } = [];
    }

    public record IW4MObjectMap(string Name, string Alias);

    public class IW4MMapPack()
    {
        public required string Name { get; init; }
        public required string Id { get; init; }
        public List<IW4MObjectMap> Maps { get; init; } = [];
    }
}
