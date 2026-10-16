using System;
using System.Runtime.InteropServices;

namespace CherryHud.Services;

public sealed class SystemMetricsService : ISystemMetricsService
{
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";

    [DllImport(LibSystem)]
    private static extern uint mach_host_self();

    [StructLayout(LayoutKind.Sequential)]
    private struct HostCpuLoadInfo
    {
        public uint User;
        public uint System;
        public uint Idle;
        public uint Nice;
    }

    [DllImport(LibSystem)]
    private static extern int host_statistics64(uint host, int flavor, ref HostCpuLoadInfo info, ref int count);

    [StructLayout(LayoutKind.Sequential)]
    private struct VmStatistics64
    {
        public uint free_count;
        public uint active_count;
        public uint inactive_count;
        public uint wire_count;
        public ulong zero_fill_count;
        public ulong reactivations;
        public ulong pageins;
        public ulong pageouts;
        public ulong faults;
        public ulong cow_faults;
        public ulong lookups;
        public ulong hits;
        public ulong purges;
        public uint purgeable_count;
        public uint speculative_count;
        public ulong decompressions;
        public ulong compressions;
        public ulong swapins;
        public ulong swapouts;
        public uint compressor_page_count;
        public uint throttled_count;
        public uint external_page_count;
        public uint internal_page_count;
        public ulong total_uncompressed_pages_in_compressor;
    }

    [DllImport(LibSystem)]
    private static extern int host_statistics64(uint host, int flavor, ref VmStatistics64 info, ref int count);

    [DllImport(LibSystem)]
    private static extern int host_page_size(uint host, out nuint pageSize);

    [DllImport(LibSystem)]
    private static extern int sysctlbyname(string name, out ulong value, ref nuint size, IntPtr newp, nuint newsize);

    private readonly uint _host;
    private readonly double _totalRamGb;
    private readonly nuint _pageSize;
    private HostCpuLoadInfo _previousCpu;
    private bool _hasPreviousCpu;
    private readonly object _lock = new();

    public SystemMetricsService()
    {
        _host = mach_host_self();

        // Get total RAM once
        nuint size = (nuint)sizeof(ulong);
        if (sysctlbyname("hw.memsize", out ulong totalRamBytes, ref size, IntPtr.Zero, 0) == 0 && totalRamBytes > 0)
        {
            _totalRamGb = totalRamBytes / (1024.0 * 1024.0 * 1024.0);
        }
        else
        {
            _totalRamGb = 16.0;
        }

        // Get host page size
        if (host_page_size(_host, out _pageSize) != 0 || _pageSize == 0)
        {
            _pageSize = 16384; // Standard Apple Silicon page size (16KB)
        }
    }

    public SystemResourceMetrics ReadResourceMetrics()
    {
        lock (_lock)
        {
            double cpuPercent = 0.0;

            // 1. CPU Load
            HostCpuLoadInfo currentCpu = default;
            int cpuCount = Marshal.SizeOf<HostCpuLoadInfo>() / 4;
            if (host_statistics64(_host, 3, ref currentCpu, ref cpuCount) == 0)
            {
                if (_hasPreviousCpu)
                {
                    ulong dUser = currentCpu.User - _previousCpu.User;
                    ulong dSys = currentCpu.System - _previousCpu.System;
                    ulong dIdle = currentCpu.Idle - _previousCpu.Idle;
                    ulong dNice = currentCpu.Nice - _previousCpu.Nice;
                    ulong total = dUser + dSys + dIdle + dNice;

                    if (total > 0)
                    {
                        cpuPercent = Math.Clamp((double)(dUser + dSys + dNice) * 100.0 / total, 0.0, 100.0);
                    }
                }

                _previousCpu = currentCpu;
                _hasPreviousCpu = true;
            }

            // 2. RAM Usage
            double usedRamGb = 0.0;
            double ramPercent = 0.0;

            VmStatistics64 vm = default;
            int vmCount = Marshal.SizeOf<VmStatistics64>() / 4;
            if (host_statistics64(_host, 4, ref vm, ref vmCount) == 0)
            {
                ulong usedBytes = (ulong)(vm.active_count + vm.wire_count + vm.compressor_page_count) * (ulong)_pageSize;
                usedRamGb = usedBytes / (1024.0 * 1024.0 * 1024.0);
                if (_totalRamGb > 0)
                {
                    ramPercent = Math.Clamp((usedRamGb / _totalRamGb) * 100.0, 0.0, 100.0);
                }
            }

            return new SystemResourceMetrics(cpuPercent, usedRamGb, _totalRamGb, ramPercent);
        }
    }
}
