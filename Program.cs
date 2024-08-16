using System.Text.Json;
using HtmlAgilityPack;

namespace ServerScraper;

class Program
{
    private static List<string> serverList = new List<string>();
    // private const string APILINK = "http://api.raidmax.org:5000/servers";
    private const string APILINK = "https://master.iw4.zip/servers";
    
    static async Task Main(string[] args)
    {
        var web = new HtmlWeb();
        var doc = new HtmlDocument();

        try
        {
            doc = await web.LoadFromWebAsync(APILINK);
            Console.WriteLine("Fetching from server list.");
            
            //Selects specific div with only H2M servers.
            var H2MServers = doc.GetElementbyId("H2M_Servers");
            //Selects all server rows.
            var serverRows = H2MServers.Descendants("tr");
        
            //Loops over each row in the server rows.
            foreach (var row in serverRows)
            {
                //Stores ip and port for each server.
                string ip = "";
                string port = "";
            
                //Loops through each attribute.
                foreach (var attr in row.Attributes)
                {
                    //Selects the ip and port and stores them in their corresponding variables.
                    if (attr.Name == "data-ip") ip = attr.Value.Trim();
                    if (attr.Name == "data-port") port = attr.Value.Trim();
                }
                //Checks if either the IP or Port is null or empty
                if (!string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(port))
                {
                    //Adds the server to the serverList is checks pass
                    serverList.Add($"{ip}:{port}");
                }
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.Message);
            Console.ReadLine();
            return;
        }
        
        Console.WriteLine("List of fetched servers: ");
        serverList.ForEach(Console.WriteLine);
        
        Console.WriteLine("Serializing server list into JSON format.");
        var jsonString = JsonSerializer.Serialize(serverList);

        try
        {
            Console.WriteLine("Storing server list into \"/players2/favourites.json\"");
            await File.WriteAllTextAsync("./players2/favourites.json", jsonString);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.Message);
            Console.WriteLine("Make sure the exe is inside the root of the game folder and try again.");
            Console.ReadLine();
            return;
        }
        
        Console.WriteLine("Operation Complete. Press any key to exit.");
        Console.ReadLine();
    }
}