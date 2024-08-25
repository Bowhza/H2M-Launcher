namespace H2MLauncher.Core.Settings
{
    public class ResourceSettings
    {
        public List<IW4MObjectMap> Maps { get; set; } = [];
        public List<IW4MObjectMap> GameTypes { get; set; } = [];
    }

    public record IW4MObjectMap(string Name, string Alias);
}
