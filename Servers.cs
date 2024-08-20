using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace H2M_Launcher
{
    internal static class Servers
    {
        private static List<ServerInfo> serverList = new List<ServerInfo>();
        // private const string APILINK = "http://api.raidmax.org:5000/servers";
        private const string APILINK = "https://master.iw4.zip/servers";

        internal static List<ServerInfo> GetServers()
        {
            serverList.Clear();
            var web = new HtmlWeb();
            var doc = new HtmlAgilityPack.HtmlDocument();

            try
            {
                doc = web.Load(APILINK);
                Console.WriteLine("Fetching from server list.");

                //Selects specific div with only H2M servers.
                var H2MServers = doc.GetElementbyId("H2M_Servers");
                //Selects all server rows.
                var serverRows = H2MServers.Descendants("tr");

                //Loops over each row in the server rows.
                foreach (var row in serverRows)
                {
                    var clientNum = row.SelectSingleNode(".//td[@data-clientnum]")?.InnerText.Trim();

                    var serverInfo = new ServerInfo
                    {
                        Ip = row.Attributes["data-ip"]?.Value.Trim(),
                        Port = row.Attributes["data-port"]?.Value.Trim(),
                        Hostname = row.SelectSingleNode(".//td[@data-hostname]")?.InnerText.Trim(),
                        Map = row.SelectSingleNode(".//td[@data-map]")?.InnerText.Trim(),
                        ClientNum = clientNum != null ? clientNum.Split('/')[0].Trim() : "0",
                        MaxClientNum = clientNum != null ? clientNum.Split('/')[1].Trim() : "0",
                        GameType = row.SelectSingleNode(".//td[@data-gametype]")?.InnerText.Trim()
                    };

                    // Add to list if IP and Port are valid
                    if (!string.IsNullOrEmpty(serverInfo.Ip) && !string.IsNullOrEmpty(serverInfo.Port))
                    {
                        serverList.Add(serverInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
            }

            return serverList;
        }

        internal async static void SaveServerList()
        {
            Console.WriteLine("Serializing server list into JSON format.");
            // Create a list of "Ip:Port" strings
            var ipPortList = serverList.ConvertAll(server => $"{server.Ip}:{server.Port}");

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

