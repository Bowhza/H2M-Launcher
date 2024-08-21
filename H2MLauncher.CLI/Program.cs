using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using System.Text.RegularExpressions;

H2MLauncherService h2MLauncherService = new(new HttpClient());
Console.WriteLine("Checking for updates..");
bool needsUpdate = await h2MLauncherService.IsLauncherUpToDateAsync(CancellationToken.None);
if (needsUpdate)
    Console.WriteLine("Update the launcher!");
else
    Console.WriteLine("Launcher is up to date!");

RaidMaxService raidMaxService = new(new HttpClient());
List<RaidMaxServer> servers = await raidMaxService.GetServerInfosAsync(CancellationToken.None);

servers.ForEach(PrintServer);

void PrintServer(RaidMaxServer server)
{
    MatchCollection matches = Regex.Matches(server.HostName, @"(\^\d|\^\:)([^\^]*?)(?=\^\d|\^:|$)");
    if (matches.Any())
    {
        if (matches[0].Index != 0)
        {
            Console.Write(server.HostName[..matches[0].Index]);
        }
        foreach (Match match in matches)
        {
            string ma = match.Groups[1].Value;
            Console.ForegroundColor = ma switch
            {
                "^0" => ConsoleColor.Black,
                "^1" => ConsoleColor.Red,
                "^2" => ConsoleColor.Green,
                "^3" => ConsoleColor.Yellow,
                "^4" => ConsoleColor.Blue,
                "^5" => ConsoleColor.Cyan,
                "^6" => ConsoleColor.Magenta,
                "^7" => ConsoleColor.White,
                "^8" => ConsoleColor.Black,
                _ => ConsoleColor.White, // ^: rainbow
            };
            Console.Write(match.Groups[2].Value);
        }
    }
    else
    {
        Console.Write(server.HostName);
    }
    Console.Write(Environment.NewLine);
    Console.ForegroundColor = ConsoleColor.White;
}

