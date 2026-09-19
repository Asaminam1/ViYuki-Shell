using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ViYuki.SystemIntegration;

public sealed class SystemInfoService
{
    public void WriteReport()
    {
        var memory = GetMemoryStatus();
        var previousColor = Console.ForegroundColor;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("ViYuki System Information");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string('─', 58));

        WriteSection("System");
        WriteValue("System name", Environment.MachineName);
        WriteValue("Manufacturer", OperatingSystem.IsWindows() ? GetBiosValue("SystemManufacturer") : "unavailable");
        WriteValue("System model", OperatingSystem.IsWindows() ? GetBiosValue("SystemProductName") : "unavailable");
        WriteValue("Operating system", RuntimeInformation.OSDescription.Trim());
        WriteValue("Architecture", RuntimeInformation.OSArchitecture.ToString());
        WriteValue("Boot device", OperatingSystem.IsWindows() ? GetBootDevice() : "unavailable");
        WriteValue("BIOS version", OperatingSystem.IsWindows() ? GetBiosVersion() : "unavailable");
        WriteValue("Current user", $"{Environment.UserDomainName}\\{Environment.UserName}");
        WriteValue("Time zone", TimeZoneInfo.Local.DisplayName);
        WriteValue("Uptime", FormatDuration(TimeSpan.FromMilliseconds(Environment.TickCount64)));

        WriteSection("Processor and memory");
        WriteValue("CPU", OperatingSystem.IsWindows() ? GetProcessorName() : "unavailable");
        WriteValue("Logical processors", Environment.ProcessorCount.ToString());
        WriteValue("Total memory", FormatBytes(memory.TotalPhysical));
        WriteValue("Available memory", FormatBytes(memory.AvailablePhysical));
        WriteValue("Page file", $"{FormatBytes(memory.TotalPageFile - memory.AvailablePageFile)} used / {FormatBytes(memory.TotalPageFile)}");

        WriteSection("Graphics and display");
        var graphicsAdapters = OperatingSystem.IsWindows() ? GetGraphicsAdapters() : [];
        if (graphicsAdapters.Count == 0)
        {
            WriteValue("GPU", "unavailable");
        }
        else
        {
            for (var index = 0; index < graphicsAdapters.Count; index++)
            {
                var adapter = graphicsAdapters[index];
                var label = index == 0 ? "GPU" : $"GPU {index + 1}";
                WriteValue(label, adapter.Name);
                WriteValue(index == 0 ? "VRAM" : $"VRAM {index + 1}", adapter.VramBytes is null ? "unavailable" : FormatBytes(adapter.VramBytes.Value));
            }
        }

        WriteValue("Screen resolution", GetScreenResolution());

        WriteSection("Network");
        var adapters = GetActiveNetworkAdapters();
        if (adapters.Count == 0)
        {
            WriteValue("Local network", "unavailable");
        }
        else
        {
            foreach (var adapter in adapters)
            {
                WriteValue("Adapter", adapter.Name);
                WriteValue("Local IPv4", adapter.Ipv4Address);
                WriteValue("MAC address", adapter.MacAddress);
            }
        }

        WriteSection("Disks");
        var drives = DriveInfo.GetDrives().Where(drive => drive.IsReady).OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var drive in drives)
        {
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            var usedPercent = drive.TotalSize == 0 ? 0 : (int)Math.Round(used * 100d / drive.TotalSize);
            WriteValue($"{drive.Name} ({drive.DriveFormat})", $"{FormatBytes((ulong)used)} used / {FormatBytes((ulong)drive.TotalSize)} ({usedPercent}%)");
        }

        Console.ForegroundColor = previousColor;
    }

    private static void WriteSection(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[{title}]");
    }

    private static void WriteValue(string name, string value)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {name,-19}");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(value);
    }

    [SupportedOSPlatform("windows")]
    private static string GetBiosValue(string valueName)
    {
        return ReadRegistryString(Registry.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", valueName) ?? "unavailable";
    }

    [SupportedOSPlatform("windows")]
    private static string GetBiosVersion()
    {
        var version = GetBiosValue("BIOSVersion");
        var date = GetBiosValue("BIOSReleaseDate");
        return date == "unavailable" ? version : $"{version} ({date})";
    }

    [SupportedOSPlatform("windows")]
    private static string GetBootDevice()
    {
        return ReadRegistryString(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control", "SystemBootDevice")
            ?? Path.GetPathRoot(Environment.SystemDirectory)
            ?? "unavailable";
    }

    [SupportedOSPlatform("windows")]
    private static string GetProcessorName()
    {
        return ReadRegistryString(
                   Registry.LocalMachine,
                   @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                   "ProcessorNameString")?.Trim()
               ?? $"{Environment.ProcessorCount} logical processors";
    }

    [SupportedOSPlatform("windows")]
    private static List<GraphicsAdapter> GetGraphicsAdapters()
    {
        var adapters = new List<GraphicsAdapter>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var videoKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (videoKey is null)
            {
                return adapters;
            }

            foreach (var adapterId in videoKey.GetSubKeyNames())
            {
                using var adapterKey = videoKey.OpenSubKey($@"{adapterId}\0000");
                if (adapterKey is null)
                {
                    continue;
                }

                var name = adapterKey.GetValue("DriverDesc") as string;
                if (string.IsNullOrWhiteSpace(name) || name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase) || !seenNames.Add(name))
                {
                    continue;
                }

                var vramValue = adapterKey.GetValue("HardwareInformation.qwMemorySize");
                adapters.Add(new GraphicsAdapter(name.Trim(), ConvertToUnsignedLong(vramValue)));
            }
        }
        catch (UnauthorizedAccessException)
        {
        }

        return adapters;
    }

    private static List<NetworkAdapter> GetActiveNetworkAdapters()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .Where(network => network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Where(network => network.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .Select(network =>
            {
                var properties = network.GetIPProperties();
                var ipv4 = properties.UnicastAddresses
                    .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString()
                    ?? "unavailable";
                var physicalAddress = network.GetPhysicalAddress().ToString();
                var mac = string.IsNullOrEmpty(physicalAddress)
                    ? "unavailable"
                    : string.Join(':', Enumerable.Range(0, physicalAddress.Length / 2).Select(index => physicalAddress.Substring(index * 2, 2)));
                var hasDefaultGateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !gateway.Address.Equals(System.Net.IPAddress.Any));
                return new NetworkAdapter(network.Name, ipv4, mac, hasDefaultGateway);
            })
            .Where(adapter => adapter.Ipv4Address != "unavailable")
            .OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var primaryAdapters = adapters.Where(adapter => adapter.HasDefaultGateway).ToList();
        return primaryAdapters.Count > 0 ? primaryAdapters : adapters;
    }

    private static string GetScreenResolution()
    {
        var width = GetSystemMetrics(0);
        var height = GetSystemMetrics(1);
        return width > 0 && height > 0 ? $"{width} x {height}" : "unavailable";
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistryString(RegistryKey root, string path, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            return key?.GetValue(valueName) switch
            {
                string text => text.Trim(),
                string[] values => string.Join("; ", values.Where(value => !string.IsNullOrWhiteSpace(value))),
                _ => null
            };
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static ulong? ConvertToUnsignedLong(object? value)
    {
        return value switch
        {
            int number when number > 0 => (ulong)number,
            uint number when number > 0 => number,
            long number when number > 0 => (ulong)number,
            ulong number when number > 0 => number,
            _ => null
        };
    }

    private static MemoryStatusEx GetMemoryStatus()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? status : default;
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / gigabyte:0.0} GB";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.Days > 0
            ? $"{duration.Days}d {duration.Hours}h {duration.Minutes}m"
            : $"{duration.Hours}h {duration.Minutes}m";
    }

    private sealed record GraphicsAdapter(string Name, ulong? VramBytes);

    private sealed record NetworkAdapter(string Name, string Ipv4Address, string MacAddress, bool HasDefaultGateway);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
