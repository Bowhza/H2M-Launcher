using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Drawing;

partial class Program
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void CBuff_AddTextDelegate(int localClientNum, int controllerIndex, IntPtr text);

    // Import the necessary Windows API functions
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);


    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize,
                                            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

    // Constants for process access rights
    const int PROCESS_CREATE_THREAD = 0x0002;
    const int PROCESS_QUERY_INFORMATION = 0x0400;
    const int PROCESS_VM_OPERATION = 0x0008;
    const int PROCESS_VM_WRITE = 0x0020;
    const int PROCESS_VM_READ = 0x0010;

    const uint MEM_COMMIT = 0x00001000;
    const uint MEM_RESERVE = 0x00002000;
    const uint PAGE_READWRITE = 0x04;
    const uint PAGE_EXECUTE_READWRITE = 0x40;
    const uint MEM_RELEASE = 0x8000;




    // Enums matching the C++ enums
    enum netadrtype_t
    {
        NA_BOT = 0x0,
        NA_BAD = 0x1,
        NA_LOOPBACK = 0x2,
        NA_BROADCAST = 0x3,
        NA_IP = 0x4,
    }

    enum netsrc_t
    {
        NS_CLIENT1 = 0x0,
        NS_MAXCLIENTS = 0x1,
        NS_SERVER = 0x2,
        NS_PACKET = 0x3,
        NS_INVALID_NETSRC = 0x4,
    }

    // Struct matching netadr_s
    [StructLayout(LayoutKind.Sequential)]
    struct netadr_s
    {
        public netadrtype_t type;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] ip;
        public ushort port;
        public netsrc_t localNetID;
        public uint addrHandleIndex;
    }

    // Struct matching connect_state_t
    [StructLayout(LayoutKind.Sequential)]
    struct connect_state_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] // To match the __pad0[0xC]
        public byte[] __pad0;
        public netadr_s address;
    }

    struct client_state_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 19024)]
        public byte[] __pad0;
        public int ping;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] __pad1;
        public int num_players;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] __pad2;
        public int serverTime;
    };


    static void ReadClientState(IntPtr hProcess, IntPtr baseAddress)
    {
        // Size of the client_state_t struct (12 bytes for padding + netadr_s size)
        int structSize = Marshal.SizeOf(typeof(client_state_t));
        byte[] buffer = new byte[structSize];
        int bytesRead;

        // Read the memory
        bool success = ReadProcessMemory(hProcess, baseAddress, buffer, buffer.Length, out bytesRead);

        if (success && bytesRead == structSize)
        {
            // Convert the byte array into a client_state_t struct
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            client_state_t connectState = (client_state_t)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(client_state_t));
            handle.Free();

            // Example: Access the fields in client_state_t
            Console.WriteLine("Ping: " + connectState.ping);
            Console.WriteLine("NumPlayers: " + connectState.num_players);
            Console.WriteLine("ServerTime: " + connectState.serverTime);
        }
        else
        {
            Console.WriteLine("Failed to read memory.");
        }
    }

    static void ReadConnectState(IntPtr hProcess, IntPtr baseAddress)
    {
        // Size of the connect_state_t struct (12 bytes for padding + netadr_s size)
        int structSize = Marshal.SizeOf(typeof(connect_state_t));
        byte[] buffer = new byte[structSize];
        int bytesRead;

        // Read the memory
        bool success = ReadProcessMemory(hProcess, baseAddress, buffer, buffer.Length, out bytesRead);

        if (success && bytesRead == structSize)
        {
            // Convert the byte array into a connect_state_t struct
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            connect_state_t connectState = (connect_state_t)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(connect_state_t));
            handle.Free();

            // Example: Access the fields in connect_state_t
            Console.WriteLine("Type: " + connectState.address.type);
            Console.WriteLine("IP: " + string.Join(".", connectState.address.ip));
            Console.WriteLine("Port: " + connectState.address.port);
            Console.WriteLine("LocalNetID: " + connectState.address.localNetID);
            Console.WriteLine("AddrHandleIndex: " + connectState.address.addrHandleIndex);
        }
        else
        {
            Console.WriteLine("Failed to read memory.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);


    // Define a structure to hold the parameters
    [StructLayout(LayoutKind.Sequential)]
    public struct CBuff_AddTextParams
    {
        public int localClientNum;
        public int controllerIndex;
        public IntPtr text;
    }

    const nint PLAYER_NAME_OFFSET_H1 = 0x3516F83;
    const nint DISCORD_ACTIVITY_OFFSET_H2MMOD = 0x56FF29;
    const nint CONNECTION_STATE_H1 = 0x2EC82C8;
    const nint LEVEL_ENTITY_ID_H1 = 0xB1100B0;
    // last server (after leaving): h1_mp64_ship.exe+C9561B3


    enum connstate_t
    {
        CA_DISCONNECTED = 0x0,
        CA_CINEMATIC = 0x1,
        CA_LOGO = 0x2,
        CA_CONNECTING = 0x3,
        CA_CHALLENGING = 0x4,
        CA_CONNECTED = 0x5,
        CA_SENDINGSTATS = 0x6,
        CA_SYNCHRONIZING_DATA = 0x7,
        CA_LOADING = 0x8,
        CA_PRIMED = 0x9,
        CA_ACTIVE = 0xA,
    };

    static bool ReadProcessMemoryInt2(nint hProcess, nint lpBaseAddress, out int value)
    {
        byte[] buffer = new byte[sizeof(int)]; // Size of an int (4 bytes)
        int bytesRead;

        bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out bytesRead);
        if (success)
        {
            // Convert the byte array to an integer
            int newValue = BitConverter.ToInt32(buffer, 0);
            value = newValue;
            return true;
        }
        value = 0;
        return false;
    }

    static bool ReadProcessMemoryInt(nint hProcess, nint lpBaseAddress, out int value)
    {
        byte[] buffer = new byte[sizeof(int)]; // Size of an int (4 bytes)
        int bytesRead;

        bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out bytesRead);
        if (success)
        {
            // Convert the byte array to an integer
            int newValue = BitConverter.ToInt32(buffer, 0);
            value = newValue;
            return true;
        }
        value = 0;
        return false;
    }

    static bool ReadProcessMemoryUInt(nint hProcess, nint lpBaseAddress, out uint value)
    {
        byte[] buffer = new byte[sizeof(int)]; // Size of an int (4 bytes)
        int bytesRead;

        bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out bytesRead);
        if (success)
        {
            // Convert the byte array to an integer
            uint newValue = BitConverter.ToUInt32(buffer, 0);
            value = newValue;
            return true;
        }
        value = 0;
        return false;
    }

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBase,
        ref uint lpBuffer,
        int nSize,
        int lpNumberOfBytesRead
        );

    static async Task Main()
    {
        // Example: Read integer from an external process's memory
        // Get the target process (e.g., process named "notepad")
        Process targetProcess = Process.GetProcessesByName("h2m-mod")[0];

        // Open the process with read access
        IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, targetProcess.Id);

        // Define the address you want to read from (you need to know this)
        IntPtr baseAddress = new IntPtr(
            targetProcess.Modules.Cast<ProcessModule>().First(m => m.ModuleName.Contains("h1_mp64")).BaseAddress);

        // Create a buffer to store the read data
        byte[] buffer = new byte[64]; // Size of an int (4 bytes)
        int bytesRead;
        while (true)
        {
            ReadProcessMemory(hProcess, baseAddress + PLAYER_NAME_OFFSET_H1, buffer, 64, out bytesRead);

            // Read memory
            bool success = ReadProcessMemoryUInt(hProcess, baseAddress + LEVEL_ENTITY_ID_H1, out uint levelId);
            bool success2 = ReadProcessMemoryInt(hProcess, baseAddress + CONNECTION_STATE_H1, out int connectionState);
            if (success && success)
            {
                if (levelId == 0 && connectionState >= (int)connstate_t.CA_CONNECTED)
                {
                    Console.WriteLine("Connected");
                }
                else
                {
                    Console.WriteLine("Disconnected");
                }

                //Console.WriteLine(((connstate_t)value).ToString());
                Console.WriteLine("Read string: " + Encoding.ASCII.GetString(buffer));
            }
            else
            {
                Console.WriteLine("Failed to read memory.");
            }

            await Task.Delay(1000);
        }

        Console.ReadLine();

        // Close the handle to the process
        CloseHandle(hProcess);

        return;
        //// Open the target process
        //IntPtr hProcess = OpenProcess(
        //    (int)(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ), 
        //    false, 
        //    targetProcess.Id);


        //IntPtr CBuff_AddText_Address = IntPtr.Add(baseAddress, 0x1CF480);
        //// Create a remote thread to execute the function
        //IntPtr threadId;
        //// Allocate memory in the remote process for the string parameter

        //string message = "quit";
        //byte[] textBytes = Encoding.ASCII.GetBytes(message + "\0");  // Add null terminator for string
        //IntPtr remoteTextAddress = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)textBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

        //if (remoteTextAddress == IntPtr.Zero)
        //{
        //    Console.WriteLine("Failed to allocate memory in target process.");
        //    CloseHandle(hProcess);
        //    return;
        //}

        //// Write the string into the allocated memory
        //WriteProcessMemory(hProcess, remoteTextAddress, textBytes, (uint)textBytes.Length, out _);

        //// Prepare the structure of parameters to pass to the remote function
        //CBuff_AddTextParams parameters = new CBuff_AddTextParams
        //{
        //    localClientNum = 0,  // Example value
        //    controllerIndex = 0,  // Example value
        //    text = remoteTextAddress  // Pointer to the string in the remote process
        //};

        //// Allocate memory for the structure in the remote process
        //IntPtr remoteStructAddress = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)Marshal.SizeOf(parameters), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

        //if (remoteStructAddress == IntPtr.Zero)
        //{
        //    Console.WriteLine("Failed to allocate memory for parameters in target process.");
        //    VirtualFreeEx(hProcess, remoteTextAddress, 0, MEM_RELEASE);
        //    CloseHandle(hProcess);
        //    return;
        //}

        //// Write the structure into the allocated memory
        //byte[] structBytes = new byte[Marshal.SizeOf(parameters)];
        //IntPtr structPtr = Marshal.AllocHGlobal(Marshal.SizeOf(parameters));
        //Marshal.StructureToPtr(parameters, structPtr, false);
        //Marshal.Copy(structPtr, structBytes, 0, structBytes.Length);
        //Marshal.FreeHGlobal(structPtr);

        //WriteProcessMemory(hProcess, remoteStructAddress, structBytes, (uint)structBytes.Length, out _);


        //// Create a remote thread to call the function

        //IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, CBuff_AddText_Address, remoteStructAddress, 0, out threadId);

        //if (hThread == IntPtr.Zero)
        //{
        //    Console.WriteLine("Failed to create remote thread.");
        //    VirtualFreeEx(hProcess, remoteTextAddress, 0, MEM_RELEASE);  // Free allocated memory
        //    VirtualFreeEx(hProcess, remoteStructAddress, 0, MEM_RELEASE);  // Free allocated memory
        //    CloseHandle(hProcess);
        //    return;
        //}

        //Console.WriteLine($"Remote thread created with ID: {threadId}");

        //// Clean up
        //VirtualFreeEx(hProcess, remoteTextAddress, 0, MEM_RELEASE);
        //VirtualFreeEx(hProcess, remoteStructAddress, 0, MEM_RELEASE);
        //CloseHandle(hThread);
        //CloseHandle(hProcess);


        //return;



        //string exePath = @"G:\SteamLibrary\steamapps\common\Call of Duty Modern Warfare Remastered\h1_mp64_ship.exe";

        ////IntPtr moduleHandle = LoadLibrary(exePath);
        ////if (moduleHandle == IntPtr.Zero)
        ////{
        ////    int errorCode = Marshal.GetLastWin32Error();
        ////    Console.WriteLine($"Failed to load module. Error code: {errorCode}");
        ////    return;
        ////}

        ////IntPtr CBuff_AddText_Address = GetProcAddress(moduleHandle, "Cbuff_AddText");
        //IntPtr CBuff_AddText_Address = IntPtr.Add(baseAddress, 0x1CF480);

        //if (CBuff_AddText_Address == IntPtr.Zero)
        //{
        //    int errorCode = Marshal.GetLastWin32Error();
        //    Console.WriteLine($"Failed to get function address. Error code: {errorCode}");
        //    //FreeLibrary(moduleHandle);
        //    return;
        //}

        //CBuff_AddTextDelegate Cbuff_AddText = Marshal.GetDelegateForFunctionPointer<CBuff_AddTextDelegate>(CBuff_AddText_Address);

        //int localClientNum = 0;
        //int controllerIndex = 0;
        //string command = "quit";

        //IntPtr textPtr = Marshal.StringToHGlobalAnsi(command);

        //try
        //{
        //    Cbuff_AddText(localClientNum, controllerIndex, textPtr);
        //}
        //catch (AccessViolationException ex)
        //{
        //    Console.WriteLine($"Access violation exception: {ex.Message}");
        //}
        //catch (Exception ex)
        //{
        //    Console.WriteLine($"Exception: {ex.Message}");
        //}
        //finally
        //{
        //    Marshal.FreeHGlobal(textPtr);
        //    //FreeLibrary(moduleHandle);
        //}
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

