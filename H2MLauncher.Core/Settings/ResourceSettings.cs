using System.Text.Json.Serialization;

namespace H2MLauncher.Core.Settings
{
    public class ResourceSettings
    {
        private List<IW4MMapPack> _mapPacks = [];
        private List<IW4MObjectMap> _gameTypes = [];
        private readonly Dictionary<string, IW4MObjectMap> _mapNameDict = [];
        private readonly Dictionary<string, IW4MObjectMap> _gameTypeMap = [];

        public List<IW4MMapPack> MapPacks
        {
            get => _mapPacks; set
            {
                _mapPacks = value;
                _mapNameDict.Clear();
                foreach (IW4MObjectMap oMap in MapPacks.SelectMany(mappack => mappack.Maps))
                {
                    _mapNameDict.TryAdd(oMap.Name, oMap);

                }
            }
        }

        public List<IW4MObjectMap> GameTypes
        {
            get => _gameTypes; set
            {
                _gameTypes = value;
                _gameTypeMap.Clear();

                foreach (IW4MObjectMap oMap in GameTypes)
                {
                    _gameTypeMap.TryAdd(oMap.Name, oMap);
                }
            }
        }

        [JsonIgnore]
        public IReadOnlyDictionary<string, IW4MObjectMap> MapNameDict => _mapNameDict;

        [JsonIgnore]
        public IReadOnlyDictionary<string, IW4MObjectMap> GameTypeDict => _gameTypeMap;

        public string GetMapDisplayName(string name)
        {
            return MapNameDict.GetValueOrDefault(name)?.Alias ?? name;
        }

        public string GetGameTypeDisplayName(string name)
        {
            return GameTypeDict.GetValueOrDefault(name)?.Alias ?? name;
        }
    }

    public record IW4MObjectMap(string Name, string Alias);

    public class IW4MMapPack()
    {
        public required string Name { get; init; }
        public required string Id { get; init; }
        public List<IW4MObjectMap> Maps { get; init; } = [];
    }
}
