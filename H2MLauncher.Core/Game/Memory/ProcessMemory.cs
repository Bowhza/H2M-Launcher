using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace H2MLauncher.Core.Services;

public sealed partial class ProcessMemory
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    // Constants for process access rights
    public const int PROCESS_CREATE_THREAD = 0x0002;
    public const int PROCESS_QUERY_INFORMATION = 0x0400;
    public const int PROCESS_VM_OPERATION = 0x0008;
    public const int PROCESS_VM_WRITE = 0x0020;
    public const int PROCESS_VM_READ = 0x0010;

    public static IntPtr OpenProcess(Process process)
    {
        return OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, process.Id);
    }

    public static bool CloseProcess(IntPtr processHandle)
    {
        return CloseHandle(processHandle);
    }

    public static bool ReadPointerFromMemory(IntPtr processHandle, IntPtr address, out nint ptrValue)
    {
        // Assume the pointer is an IntPtr (which is 4 bytes on x86 or 8 bytes on x64)
        int pointerSize = Marshal.SizeOf(typeof(IntPtr));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(pointerSize);

        try
        {
            // Read the memory containing the pointer
            if (ReadProcessMemory(processHandle, address, buffer, pointerSize, out int bytesRead))
            {
                // Convert the byte array to an IntPtr (which holds the address of the struct)
                Span<byte> span = new(buffer, 0, IntPtr.Size);
                ptrValue = MemoryMarshal.Read<IntPtr>(span);
                return true;
            }
            //else
            //{
            //    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            //}

            ptrValue = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool ReadStructFromMemory<T>(IntPtr processHandle, IntPtr address, out T value) where T : struct
    {
        // Get the size of the struct
        int size = Marshal.SizeOf(typeof(T));

        // Allocate a buffer for the struct
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            // Read the memory
            if (ReadProcessMemory(processHandle, address, buffer, size, out int bytesRead))
            {
                // Convert the byte array to the struct
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    value = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
                    return true;
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }
            //else
            //{
            //    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            //}

            value = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool ReadStructFromMemoryPtr<T>(IntPtr processHandle, IntPtr address, out T? value) where T : struct
    {
        if (!ReadPointerFromMemory(processHandle, address, out nint ptrValue))
        {
            value = default;
            return false;
        }

        if (ptrValue == IntPtr.Zero)
        {
            // null pointer
            value = default;
            return true;
        }

        if (ReadStructFromMemory<T>(processHandle, ptrValue, out var val))
        {
            value = val;
            return true;
        }

        value = default;
        return false;
    }


    public static bool ReadProcessMemoryInt(nint hProcess, nint lpBaseAddress, out int value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(sizeof(int));
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out _);
            if (success)
            {
                // Convert the byte array to an integer
                value = BitConverter.ToInt32(buffer);
                return true;
            }
            value = 0;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool ReadProcessMemoryBool(nint hProcess, nint lpBaseAddress, out bool value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(sizeof(bool));
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out _);
            if (success)
            {
                // Convert the byte array to an integer
                value = BitConverter.ToBoolean(buffer);
                return true;
            }
            value = false;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool ReadProcessMemoryUInt(nint hProcess, nint lpBaseAddress, out uint value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(sizeof(uint));
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out _);
            if (success)
            {
                // Convert the byte array to an integer
                value = BitConverter.ToUInt32(buffer);
                return true;
            }
            value = 0;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool ReadProcessMemoryString(nint hProcess, nint lpBaseAddress, int length, [MaybeNullWhen(false)] out string value)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            bool success = ReadProcessMemory(hProcess, lpBaseAddress, buffer, buffer.Length, out int bytesRead);
            if (success)
            {
                value = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                return true;
            }

            value = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool ReadProcessMemoryStruct<T>(nint hProcess, nint lpBaseAddress, int size, out T value) where T : struct
    {
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            if (ReadProcessMemory(hProcess, lpBaseAddress, buffer, size, out _))
            {
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {

                    // Convert the byte array into a struct                            
                    T? readValue = (T?)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                    if (readValue.HasValue)
                    {
                        value = readValue.Value;
                        return true;
                    }

                }
                finally
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }

            value = default;
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private class MemoryReadSource
    {
        private readonly int _size;
        protected byte[] _buffer;
        private readonly IntPtr _handle;
        private readonly IntPtr _baseAddress;
        protected int _lastBytesRead;

        public ReadOnlyMemory<byte> ReadMemory => _buffer.AsMemory(0, _lastBytesRead);

        public MemoryReadSource(int size, IntPtr handle, IntPtr baseAddress)
        {
            _size = size;
            _buffer = new byte[size];
            _handle = handle;
            _baseAddress = baseAddress;
        }

        public virtual bool ReadNext()
        {
            return ReadProcessMemory(_handle, _baseAddress, _buffer, _size, out _lastBytesRead);
        }


        public bool ReadNext(out byte[] buffer)
        {
            var rbuffer = new byte[_size];
            ReadProcessMemory(_handle, _baseAddress, rbuffer, _size, out _lastBytesRead);

            buffer = rbuffer;

            return true;
        }
    }

    private class MemoryReadSourceUInt(IntPtr handle, IntPtr baseAddress) : MemoryReadSource(sizeof(uint), handle, baseAddress)
    {
        public uint Value => BitConverter.ToUInt32(ReadMemory.Span);

        public bool ReadNextValue(out uint value)
        {
            if (ReadNext())
            {
                value = BitConverter.ToUInt32(ReadMemory.Span);
                return true;
            }

            value = default;
            return false;
        }
    }

    private class MemoryReadSourceInt(IntPtr handle, IntPtr baseAddress) : MemoryReadSource(sizeof(int), handle, baseAddress)
    {
        public int Value => BitConverter.ToInt32(ReadMemory.Span);

        public bool ReadNextValue(out int value)
        {
            if (ReadNext())
            {
                value = BitConverter.ToInt32(ReadMemory.Span);
                return true;
            }

            value = default;
            return false;
        }
    }

    private class MemoryReadSourceString : MemoryReadSource
    {
        private readonly Encoding _encoding;
        public MemoryReadSourceString(int length, IntPtr handle, IntPtr baseAddress, Encoding encoding) : base(length, handle, baseAddress)
        {
            _encoding = encoding;
        }

        //public string Value => encoding.GetString(ReadMemory.Span);

        public bool ReadNextValue([MaybeNullWhen(false)] out string value)
        {
            if (ReadNext())
            {
                value = System.Text.Encoding.ASCII.GetString(_buffer, 0, _lastBytesRead);
                return true;
            }

            value = default;
            return false;
        }
    }

    private class MemoryReadSource<T> : MemoryReadSource where T : struct
    {
        public MemoryReadSource(IntPtr handle, IntPtr baseAddress) : base(Marshal.SizeOf(default(T)), handle, baseAddress)
        {

        }

        public bool ReadNextValue(out T value)
        {
            if (ReadNext())
            {
                GCHandle handle = GCHandle.Alloc(ReadMemory, GCHandleType.Pinned);
                try
                {
                    // Convert the byte array into a struct                            
                    T? readValue = (T?)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                    if (readValue.HasValue)
                    {
                        value = readValue.Value;
                        return true;
                    }
                }
                finally
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }

            value = default;
            return false;
        }
    }
}
