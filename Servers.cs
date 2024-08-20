using HtmlAgilityPack;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace H2M_Launcher
{
    internal static class Servers
    {
        private const string APILINK = "https://master.iw4.zip/servers";
        private static readonly List<ServerInfo> serversInfos = [];


        internal static async Task<List<ServerInfo>> GetServerInfosAsync(CancellationToken cancellationToken)
        {
            List<ServerInfo> servers = [];
            serversInfos.Clear();

            HtmlWeb web = new();
            HtmlAgilityPack.HtmlDocument? document = null;

            try
            {
                document = await web.LoadFromWebAsync(APILINK, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
            }

            // Selects specific div with only H2M servers.
            HtmlNode? h2mServersNode = document?.GetElementbyId("H2M_Servers");
            if (h2mServersNode is not null)
            {
                //Loops over each row in the server rows.
                foreach (var row in h2mServersNode.Descendants("tr"))
                {
                    string? serverIp = row.Attributes["data-ip"]?.Value.Trim();
                    string? serverPort = row.Attributes["data-port"]?.Value.Trim();

                    if (string.IsNullOrEmpty(serverIp) || string.IsNullOrEmpty(serverPort))
                        continue;

                    var clientNum = row.SelectSingleNode(".//td[@data-clientnum]")?.InnerText.Trim();

                    if (cancellationToken.IsCancellationRequested)
                        return servers;

                    servers.Add(new()
                    {
                        Ip = serverIp,
                        Port = serverPort,
                        Hostname = WebUtility.HtmlDecode(row.SelectSingleNode(".//td[@data-hostname]")?.InnerText.Trim()),
                        Map = row.SelectSingleNode(".//td[@data-map]")?.InnerText.Trim(),
                        ClientNum = clientNum != null ? clientNum.Split('/')[0].Trim() : "0",
                        MaxClientNum = clientNum != null ? clientNum.Split('/')[1].Trim() : "0",
                        GameType = row.SelectSingleNode(".//td[@data-gametype]")?.InnerText.Trim(),
                    });
                }
            }
            serversInfos.AddRange(servers);
            return servers;
        }


        internal async static void SaveServerList()
        {
            Console.WriteLine("Serializing server list into JSON format.");
            // Create a list of "Ip:Port" strings
            var ipPortList = serversInfos.ConvertAll(server => $"{server.Ip}:{server.Port}");

            // Serialize the list into JSON format
            var jsonString = JsonSerializer.Serialize(ipPortList, JsonContext.Default.ListString);

            try
            {
                //Store the server list into the corresponding directory
                Console.WriteLine("Storing server list into \"/players2/favourites.json\"");
                await File.WriteAllTextAsync("./players2/favourites.json", jsonString);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
                MessageBox.Show("Could not save favourites.json file. Make sure the exe is inside the root of the game folder.");
            }
        }
    }

    /// <summary>
    /// Class required for trimming file size so compiler knows what types are needed
    /// and prevents them from being removed.
    /// </summary>
    [JsonSerializable(typeof(List<string>))]
    public partial class JsonContext : JsonSerializerContext
    {
    }
}

