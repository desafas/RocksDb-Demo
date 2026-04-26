using System.Runtime.InteropServices;

namespace RocksDb_Demo.Benchmarks;

internal static class WindowsPageCacheFlusher
{
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const int SePrivilegeEnabled = 0x00000002;

    private static bool? _available;

    [DllImport("ntdll.dll")]
    private static extern uint NtSetSystemInformation(int infoClass, IntPtr buffer, int length);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges,
        ref TokenPrivileges newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass,
        out int tokenInformation, int tokenInformationLength, out int returnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    public static void Flush()
    {
        if (_available == false)
            return;

        if (_available is null)
            TryEnableProfilePrivilege();

        var buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(buffer, 4); // MemoryPurgeStandbyList
            var status = NtSetSystemInformation(0x0050, buffer, sizeof(int));

            if (_available is null)
            {
                _available = status == 0;
                if (!_available.Value)
                {
                    var elevationNote = IsProcessElevated()
                        ? "process is elevated but privilege was denied"
                        : "process is NOT elevated — right-click your terminal and select 'Run as Administrator'";
                    Console.WriteLine($"  WARNING: page cache flush failed (NTSTATUS 0x{status:X8}): {elevationNote}.");
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void TryEnableProfilePrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
            return;
        try
        {
            LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out var luid);
            var tp = new TokenPrivileges { PrivilegeCount = 1, Luid = luid, Attributes = SePrivilegeEnabled };
            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static bool IsProcessElevated()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out var token))
            return false;
        try
        {
            GetTokenInformation(token, 20 /* TokenElevation */, out var elevation, sizeof(int), out _);
            return elevation != 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    // Pack = 4 is required: LUID is two DWORDs (4-byte aligned), but C# would pad long to offset 8 without it
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TokenPrivileges
    {
        public int PrivilegeCount;
        public long Luid;
        public int Attributes;
    }
}