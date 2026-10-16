using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MacTemp.Services;

public sealed class SmcTemperatureService : ITemperatureService, IDisposable
{
    private const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";

    private const uint KernelIndexSmc = 2;
    private const byte SmcCmdReadBytes = 5;
    private const byte SmcCmdReadKeyInfo = 9;

    [DllImport(IOKit)]
    private static extern IntPtr IOServiceMatching(string name);

    [DllImport(IOKit)]
    private static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport(IOKit)]
    private static extern int IOServiceOpen(uint service, uint owningTask, uint type, out uint connect);

    [DllImport(IOKit)]
    private static extern int IOServiceClose(uint connect);

    [DllImport(IOKit)]
    private static extern int IOObjectRelease(uint obj);

    [DllImport(LibSystem)]
    private static extern uint mach_task_self();

    [DllImport(IOKit)]
    private static extern unsafe int IOConnectCallStructMethod(
        uint connection,
        uint selector,
        void* inputStruct,
        nuint inputStructCnt,
        void* outputStruct,
        nuint* outputStructCnt);

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    private unsafe struct SmcKeyData_t
    {
        [FieldOffset(0)] public uint Key;
        [FieldOffset(28)] public uint KeyInfo_dataSize;
        [FieldOffset(32)] public uint KeyInfo_dataType;
        [FieldOffset(36)] public byte KeyInfo_dataAttributes;
        [FieldOffset(40)] public byte Result;
        [FieldOffset(41)] public byte Status;
        [FieldOffset(42)] public byte Data8;
        [FieldOffset(44)] public uint Data32;
        [FieldOffset(48)] public fixed byte Bytes[32];
    }

    private uint _connection;
    private bool _isDisposed;
    private readonly object _syncLock = new();

    private List<string>? _activeCpuKeys;
    private List<string>? _activeGpuKeys;

    // Known candidate keys for Apple Silicon (M1-M5) and Intel
    private static readonly string[] CandidateCpuKeys =
    {
        // Apple Silicon (P-cores, E-cores, SoC)
        "TPD0", "TPD1", "TPD2", "TPD3", "TPD4", "TPD5", "TPD6", "TPD7", "TPD8", "TPD9",
        "Tp00", "Tp01", "Tp04", "Tp05", "Tp08", "Tp09", "Tp0C", "Tp0D", "Tp0G", "Tp0H",
        "Tp0L", "Tp0n", "Tp0O", "Tp0P", "Tp0R", "Tp0r", "Tp0X", "Tp0a", "Tp0b", "Tp0p",
        "Tp0u", "Tp0y", "Tp12", "Tp16", "Tp1E", "Tp1h", "Tp1l", "Tp1p", "Tp1t",
        "Te00", "Te04", "Te08", "Te0C", "Te0R",
        // Intel Mac sensors
        "TC0P", "TC0D", "TC0E", "TC0F", "TC1C", "TC2C", "TC3C", "TC4C", "TC0c"
    };

    private static readonly string[] CandidateGpuKeys =
    {
        // Apple Silicon GPU clusters
        "Tg00", "Tg04", "Tg05", "Tg0C", "Tg0D", "Tg0G", "Tg0j", "Tg0K", "Tg0L", "Tg0m",
        "Tg0O", "Tg0p", "Tg0P", "Tg0R", "Tg0T", "Tg0U", "Tg0v", "Tg0X", "Tg0b", "Tg0d",
        "Tg0f", "Tg0g", "Tg12", "Tg16", "Tg1A", "Tg1b", "Tg1c", "Tg1g", "Tg1I", "Tg1M",
        "Tg1o", "Tg1s", "Tg1Y", "Tg4b",
        // Intel Mac GPU
        "TG0D", "TG0P", "TPCD"
    };

    public SmcTemperatureService()
    {
        EnsureConnected();
    }

    private bool EnsureConnected()
    {
        if (_connection != 0)
        {
            return true;
        }

        try
        {
            IntPtr dict = IOServiceMatching("AppleSMC");
            if (dict == IntPtr.Zero)
            {
                return false;
            }

            uint service = IOServiceGetMatchingService(0, dict);
            if (service == 0)
            {
                return false;
            }

            int res = IOServiceOpen(service, mach_task_self(), 0, out _connection);
            IOObjectRelease(service);

            return res == 0 && _connection != 0;
        }
        catch
        {
            _connection = 0;
            return false;
        }
    }

    public TemperatureData ReadTemperatures()
    {
        lock (_syncLock)
        {
            if (!EnsureConnected())
            {
                return new TemperatureData(null, null);
            }

            // Perform initial discovery of present keys on this specific machine
            if (_activeCpuKeys == null || _activeGpuKeys == null)
            {
                DiscoverKeys();
            }

            double? cpu = ReadAverageFromKeys(_activeCpuKeys);
            double? gpu = ReadAverageFromKeys(_activeGpuKeys);

            // If active keys returned nothing (e.g. system state changed), try re-discovering
            if (cpu == null && _activeCpuKeys != null && _activeCpuKeys.Count > 0)
            {
                _activeCpuKeys = null;
                DiscoverKeys();
                cpu = ReadAverageFromKeys(_activeCpuKeys);
            }

            if (gpu == null && _activeGpuKeys != null && _activeGpuKeys.Count > 0)
            {
                _activeGpuKeys = null;
                DiscoverKeys();
                gpu = ReadAverageFromKeys(_activeGpuKeys);
            }

            return new TemperatureData(cpu, gpu);
        }
    }

    private void DiscoverKeys()
    {
        _activeCpuKeys = new List<string>();
        foreach (var key in CandidateCpuKeys)
        {
            var val = ReadKey(key);
            if (val is >= 10.0 and <= 125.0)
            {
                _activeCpuKeys.Add(key);
            }
        }

        _activeGpuKeys = new List<string>();
        foreach (var key in CandidateGpuKeys)
        {
            var val = ReadKey(key);
            if (val is >= 10.0 and <= 125.0)
            {
                _activeGpuKeys.Add(key);
            }
        }
    }

    private double? ReadAverageFromKeys(IReadOnlyCollection<string>? keys)
    {
        if (keys == null || keys.Count == 0)
        {
            return null;
        }

        double sum = 0;
        int count = 0;

        foreach (var key in keys)
        {
            var temp = ReadKey(key);
            if (temp is >= 10.0 and <= 125.0)
            {
                sum += temp.Value;
                count++;
            }
        }

        return count > 0 ? (sum / count) : null;
    }

    private unsafe double? ReadKey(string key)
    {
        if (_connection == 0 || key.Length != 4)
        {
            return null;
        }

        uint key4cc = StringToFourCC(key);

        SmcKeyData_t input = default;
        SmcKeyData_t output = default;
        input.Key = key4cc;
        input.Data8 = SmcCmdReadKeyInfo;

        nuint inSize = (nuint)sizeof(SmcKeyData_t);
        nuint outSize = (nuint)sizeof(SmcKeyData_t);

        int res = IOConnectCallStructMethod(_connection, KernelIndexSmc, &input, inSize, &output, &outSize);
        if (res != 0)
        {
            return null;
        }

        uint dataType = output.KeyInfo_dataType;
        uint dataSize = output.KeyInfo_dataSize;
        if (dataSize == 0)
        {
            return null;
        }

        string typeStr = FourCCToString(dataType);

        input = default;
        output = default;
        input.Key = key4cc;
        input.KeyInfo_dataSize = dataSize;
        input.Data8 = SmcCmdReadBytes;

        res = IOConnectCallStructMethod(_connection, KernelIndexSmc, &input, inSize, &output, &outSize);
        if (res != 0)
        {
            return null;
        }

        // Float (IEEE 754 32-bit single precision)
        if (typeStr == "flt " && dataSize == 4)
        {
            byte[] bytes = { output.Bytes[0], output.Bytes[1], output.Bytes[2], output.Bytes[3] };
            return BitConverter.ToSingle(bytes, 0);
        }

        // Signed fixed-point 8.8 (standard Intel SMC format)
        if (typeStr == "sp78" && dataSize == 2)
        {
            short raw = (short)((output.Bytes[0] << 8) | output.Bytes[1]);
            return raw / 256.0;
        }

        // Fixed point / 4.0
        if (typeStr == "fpe2" && dataSize == 2)
        {
            ushort raw = (ushort)((output.Bytes[0] << 8) | output.Bytes[1]);
            return raw / 4.0;
        }

        // Fixed point / 16.0
        if (typeStr == "fpc4" && dataSize == 2)
        {
            ushort raw = (ushort)((output.Bytes[0] << 8) | output.Bytes[1]);
            return raw / 16.0;
        }

        return null;
    }

    private static uint StringToFourCC(string s)
    {
        return (uint)((s[0] << 24) | (s[1] << 16) | (s[2] << 8) | s[3]);
    }

    private static string FourCCToString(uint code)
    {
        byte[] bytes =
        {
            (byte)((code >> 24) & 0xFF),
            (byte)((code >> 16) & 0xFF),
            (byte)((code >> 8) & 0xFF),
            (byte)(code & 0xFF)
        };
        return Encoding.ASCII.GetString(bytes);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        lock (_syncLock)
        {
            if (_connection != 0)
            {
                IOServiceClose(_connection);
                _connection = 0;
            }
        }

        GC.SuppressFinalize(this);
    }

    ~SmcTemperatureService()
    {
        Dispose();
    }
}
