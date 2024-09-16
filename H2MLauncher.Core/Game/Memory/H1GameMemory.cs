using System.Diagnostics;
using System.Runtime.InteropServices;

namespace H2MLauncher.Core.Services;

public class H1GameMemory : IDisposable
{
    // Memory offsets for game variables
    const nint PLAYER_NAME_OFFSET_H1 = 0x3516F83;
    const nint DISCORD_ACTIVITY_OFFSET_H2MMOD = 0x56FF29;
    const nint CONNECTION_STATE_H1 = 0x2EC82C8;
    const nint LEVEL_ENTITY_ID_H1 = 0xB1100B0;
    const nint CLIENT_STATE_PTR_H1 = 0x2EC84F0;
    const nint CONNECT_STATE_PTR_H1 = 0x2EC8510;
    const nint SV_SERVERID_H1 = 0xB7F9630;
    const nint VIRTUAL_LOBBY_LOADED_H1 = 0x2E6EC9D;
    const nint MAPS_H1 = 0x926C80;

    public Process Process { get; }

    private readonly IntPtr _processHandle;
    private readonly IntPtr _moduleBaseAddress;

    public H1GameMemory(Process process, string moduleName)
    {
        var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => m.ModuleName.Equals(moduleName));
        if (module is null)
        {
            throw new Exception("Game module not found in process");
        }

        _moduleBaseAddress = module.BaseAddress;
        _processHandle = ProcessMemory.OpenProcess(process);
        Process = process;
    }

    public void Dispose()
    {
        ProcessMemory.CloseProcess(_processHandle);
    }

    public int GetSvServerId()
    {
        if (ProcessMemory.ReadProcessMemoryInt(_processHandle, _moduleBaseAddress + SV_SERVERID_H1, out int sv_serverid))
        {
            return sv_serverid;
        }

        return 0;
    }

    public ClientState? GetClientState()
    {
        if (ProcessMemory.ReadStructFromMemoryPtr(_processHandle, _moduleBaseAddress + CLIENT_STATE_PTR_H1, out ClientState? clientState))
        {
            return clientState;
        }

        return null;
    }

    public ConnectState? GetConnectState()
    {
        if (ProcessMemory.ReadStructFromMemoryPtr(_processHandle, _moduleBaseAddress + CONNECT_STATE_PTR_H1, out ConnectState? connectState))
        {
            return connectState;
        }

        return null;
    }

    public ConnectionState? GetConnectionState()
    {
        if (ProcessMemory.ReadProcessMemoryInt(_processHandle, _moduleBaseAddress + CONNECTION_STATE_H1, out int connectionState))
        {
            return (ConnectionState)connectionState;
        }

        return null;
    }

    public bool? GetVirtualLobbyLoaded()
    {
        if (ProcessMemory.ReadProcessMemoryBool(_processHandle, _moduleBaseAddress + VIRTUAL_LOBBY_LOADED_H1, out bool loaded))
        {
            return loaded;
        }

        return null;
    }

    public IEnumerable<(int id, string name)> GetInGameMaps()
    {
        IntPtr mapsPointer = _moduleBaseAddress + MAPS_H1;

        int structSize = Marshal.SizeOf<Map_t>();

        // Iterate through the array
        for (int i = 0; ; i++)
        {
            // Calculate the pointer to the current map_t struct in the array
            IntPtr currentMapPointer = IntPtr.Add(mapsPointer, i * structSize);

            if (!ProcessMemory.ReadStructFromMemory(_processHandle, currentMapPointer, out Map_t currentMap))
            {
                yield break;
            }

            // Check if the 'unk' field is zero (end of the array)
            if (currentMap.Unk == 0)
            {
                break;
            }

            // Read 64 chars of the name
            if (!ProcessMemory.ReadProcessMemoryString(_processHandle, currentMap.NameAddress, 64, out string? name))
            {
                continue;
            }

            // trim everything after and including first null char
            int firstNullCharIndex = name.IndexOf('\0');
            string mapName = firstNullCharIndex == -1 ? name : name[..firstNullCharIndex];

            yield return (currentMap.Id, mapName);
        }
    }
}
