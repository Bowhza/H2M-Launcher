using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using System.Text.RegularExpressions;

using System;
using System.Runtime.InteropServices;

partial class Program
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [UnmanagedFunctionPointer(CallingConvention.FastCall)]
    private delegate void CBuff_AddTextDelegate(int localClientNum, int controllerIndex, IntPtr text);

    static void Main()
    {
        string exePath = @"G:\SteamLibrary\steamapps\common\Call of Duty Modern Warfare Remastered\h1_mp64_ship.exe";

        IntPtr moduleHandle = LoadLibrary(exePath);
        if (moduleHandle == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Console.WriteLine($"Failed to load module. Error code: {errorCode}");
            return;
        }

        //IntPtr CBuff_AddText_Address = GetProcAddress(moduleHandle, "Cbuff_AddText");
        IntPtr CBuff_AddText_Address = IntPtr.Add(moduleHandle, 0x1CF480);

        if (CBuff_AddText_Address == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Console.WriteLine($"Failed to get function address. Error code: {errorCode}");
            FreeLibrary(moduleHandle);
            return;
        }

        CBuff_AddTextDelegate Cbuff_AddText = Marshal.GetDelegateForFunctionPointer<CBuff_AddTextDelegate>(CBuff_AddText_Address);

        int localClientNum = 0;
        int controllerIndex = 0;
        string command = "quit";

        IntPtr textPtr = Marshal.StringToHGlobalAnsi(command);

        try
        {
            Cbuff_AddText(localClientNum, controllerIndex, textPtr);
        }
        catch (AccessViolationException ex)
        {
            Console.WriteLine($"Access violation exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
        finally
        {
            Marshal.FreeHGlobal(textPtr);
            FreeLibrary(moduleHandle);
        }
    }
}

//H2MLauncherService h2MLauncherService = new(new HttpClient());
//Console.WriteLine("Checking for updates..");
//bool needsUpdate = await h2MLauncherService.IsLauncherUpToDateAsync(CancellationToken.None);
//if (needsUpdate)
//    Console.WriteLine("Update the launcher!");
//else
//    Console.WriteLine("Launcher is up to date!");

//RaidMaxService raidMaxService = new(new HttpClient());
//List<RaidMaxServer> servers = await raidMaxService.GetServerInfosAsync(CancellationToken.None);

//servers.ForEach(PrintServer);

//void PrintServer(RaidMaxServer server)
//{
//    MatchCollection matches = Regex.Matches(server.HostName, @"(\^\d|\^\:)([^\^]*?)(?=\^\d|\^:|$)");
//    if (matches.Any())
//    {
//        if (matches[0].Index != 0)
//        {
//            Console.Write(server.HostName[..matches[0].Index]);
//        }
//        foreach (Match match in matches)
//        {
//            string ma = match.Groups[1].Value;
//            Console.ForegroundColor = ma switch
//            {
//                "^0" => ConsoleColor.Black,
//                "^1" => ConsoleColor.Red,
//                "^2" => ConsoleColor.Green,
//                "^3" => ConsoleColor.Yellow,
//                "^4" => ConsoleColor.Blue,
//                "^5" => ConsoleColor.Cyan,
//                "^6" => ConsoleColor.Magenta,
//                "^7" => ConsoleColor.White,
//                "^8" => ConsoleColor.Black,
//                _ => ConsoleColor.White, // ^: rainbow
//            };
//            Console.Write(match.Groups[2].Value);
//        }
//    }
//    else
//    {
//        Console.Write(server.HostName);
//    }
//    Console.Write(Environment.NewLine);
//    Console.ForegroundColor = ConsoleColor.White;
//}

