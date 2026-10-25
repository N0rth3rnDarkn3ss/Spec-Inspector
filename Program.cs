using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// Setup layout colors
Console.Title = "Advanced System Hardware Inspector";
Console.Clear();

PrintHeader();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    PrintStatus("Detected OS: Windows (Querying WMI Core Infrastructure...)");
    ScanWindows();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    PrintStatus("Detected OS: Linux (Parsing Virtual System Files...)");
    ScanLinux();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    PrintStatus("Detected OS: macOS (Querying Apple System Profiler...)");
    ScanMac();
}
else
{
    PrintError("Unsupported operating system platform detected.");
}

Console.ResetColor();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("\n======================================================================");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("Scan complete. Press any key to safely exit the application.");
Console.ResetColor();
if (!Console.IsInputRedirected) Console.ReadKey();

#region Core UI Methods
void PrintHeader()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("======================================================================");
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine("          ADVANCED CROSS-PLATFORM HARDWARE INSPECTOR (.NET 10)       ");
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("======================================================================");
}

void PrintStatus(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine($"[*] {message}\n");
    Console.ResetColor();
}

void PrintSection(string title)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[▶] {title}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("----------------------------------------------------------------------");
    Console.ResetColor();
}

void PrintMetric(string label, string value)
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write($"  » {label,-22}: ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine(value);
    Console.ResetColor();
}

void PrintError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [X] Error: {message}");
    Console.ResetColor();
}
#endregion

#region Windows Deep Scanner
void ScanWindows()
{
    try
    {
        ExecuteWindowsDeepWmi();
    }
    catch (Exception ex)
    {
        PrintError($"Failed to extract full Windows profiles: {ex.Message}");
    }
}

void ExecuteWindowsDeepWmi()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

    // CPU Metrics
    PrintSection("PROCESSOR (CPU)");
    using var cpuSearch = new System.Management.ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
    foreach (var obj in cpuSearch.Get())
    {
        PrintMetric("Model/Brand", obj["Name"]?.ToString()?.Trim() ?? "Unknown");
        PrintMetric("Physical Cores", obj["NumberOfCores"]?.ToString() ?? "Unknown");
        PrintMetric("Logical Threads", obj["NumberOfLogicalProcessors"]?.ToString() ?? "Unknown");
        if (double.TryParse(obj["MaxClockSpeed"]?.ToString(), out double speedMhz))
            PrintMetric("Max Clock Speed", $"{speedMhz / 1000.0:F2} GHz");
    }

    // GPU Metrics
    PrintSection("GRAPHICS CARD (GPU)");
    using var gpuSearch = new System.Management.ManagementObjectSearcher("SELECT Name, DriverVersion, VideoProcessor, AdapterRAM FROM Win32_VideoController");
    foreach (var obj in gpuSearch.Get())
    {
        PrintMetric("Video Controller", obj["Name"]?.ToString()?.Trim() ?? "Unknown");
        PrintMetric("Architecture", obj["VideoProcessor"]?.ToString() ?? "Unknown");
        PrintMetric("Driver Version", obj["DriverVersion"]?.ToString() ?? "Unknown");
        if (long.TryParse(obj["AdapterRAM"]?.ToString(), out long vramBytes))
            PrintMetric("Dedicated VRAM", $"{vramBytes / (1024.0 * 1024.0 * 1024.0):F2} GB");
    }

    // Motherboard metrics
    PrintSection("MOTHERBOARD (BASEBOARD)");
    using var boardSearch = new System.Management.ManagementObjectSearcher("SELECT Manufacturer, Product, SerialNumber, Version FROM Win32_BaseBoard");
    foreach (var obj in boardSearch.Get())
    {
        PrintMetric("Manufacturer", obj["Manufacturer"]?.ToString()?.Trim() ?? "Unknown");
        PrintMetric("Product Model", obj["Product"]?.ToString()?.Trim() ?? "Unknown");
        PrintMetric("Revision Version", obj["Version"]?.ToString()?.Trim() ?? "Unknown");
        PrintMetric("Serial Number", obj["SerialNumber"]?.ToString()?.Trim() ?? "Unknown");
    }

    // Memory metrics
    PrintSection("PHYSICAL MEMORY (RAM)");
    using var ramSearch = new System.Management.ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory");
    ulong totalBytes = 0;
    int slotIndex = 1;
    foreach (var obj in ramSearch.Get())
    {
        ulong stickBytes = Convert.ToUInt64(obj["Capacity"]);
        totalBytes += stickBytes;
        string manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "Unknown";
        string partNum = obj["PartNumber"]?.ToString()?.Trim() ?? "Unknown";
        string speed = obj["Speed"]?.ToString() ?? "Unknown";

        PrintMetric($"Slot #{slotIndex} Stick", $"{stickBytes / (1024 * 1024 * 1024)} GB {speed}MHz ({manufacturer} | Model: {partNum})");
        slotIndex++;
    }
    PrintMetric("Total System RAM", $"{totalBytes / (1024 * 1024 * 1024)} GB");

    // Storage metrics
    PrintSection("STORAGE DEVICES (SSD/HDD)");
    using var diskSearch = new System.Management.ManagementObjectSearcher("SELECT Model, Size, InterfaceType, MediaType FROM Win32_DiskDrive");
    int diskIndex = 1;
    foreach (var obj in diskSearch.Get())
    {
        string model = obj["Model"]?.ToString()?.Trim() ?? "Unknown Drive";
        string media = obj["MediaType"]?.ToString()?.Trim() ?? "Fixed hard disk media";
        string busType = obj["InterfaceType"]?.ToString()?.Trim() ?? "SATA/NVMe";

        if (ulong.TryParse(obj["Size"]?.ToString(), out ulong sizeBytes))
        {
            double gigaBytes = sizeBytes / (1024.0 * 1024.0 * 1024.0);
            string capString = gigaBytes >= 1000 ? $"{gigaBytes / 1024.0:F2} TB" : $"{gigaBytes:F0} GB";
            PrintMetric($"Drive #{diskIndex}", $"{model} ({capString} | Bus: {busType} | {media})");
        }
        diskIndex++;
    }
}
#endregion

#region Linux Deep Scanner
void ScanLinux()
{
    // CPU metrics
    PrintSection("PROCESSOR (CPU)");
    if (File.Exists("/proc/cpuinfo"))
    {
        var lines = File.ReadAllLines("/proc/cpuinfo");
        var model = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase))?.Split(':').Last().Trim();
        var cores = lines.FirstOrDefault(l => l.StartsWith("cpu cores", StringComparison.OrdinalIgnoreCase))?.Split(':').Last().Trim();

        PrintMetric("Model/Brand", model ?? "Unknown");
        PrintMetric("Physical Cores", cores ?? "Unknown");
    }

    // RAM metrics
    PrintSection("PHYSICAL MEMORY (RAM)");
    if (File.Exists("/proc/meminfo"))
    {
        var memLine = File.ReadAllLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemTotal", StringComparison.OrdinalIgnoreCase));
        if (memLine != null)
        {
            var segments = memLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1 && long.TryParse(segments[1], out long kb))
                PrintMetric("Total System RAM", $"{Math.Round(kb / 1024.0 / 1024.0)} GB");
        }
    }

    // GPU metrics
    PrintSection("GRAPHICS CARD (GPU)");
    string glxinfo = RunCommand("glxinfo", "-B");
    if (!string.IsNullOrEmpty(glxinfo))
    {
        var deviceLine = glxinfo.Split('\n').FirstOrDefault(l => l.Contains("Device:", StringComparison.OrdinalIgnoreCase));
        PrintMetric("GPU Device", deviceLine?.Replace("Device:", "").Trim() ?? "Parsing Error");
    }
    else
    {
        string lspci = RunCommand("lspci", " | grep -i vga");
        PrintMetric("PCI Controller", !string.IsNullOrEmpty(lspci) ? lspci.Trim() : "Not Detected");
    }

    // Board metrics
    PrintSection("MOTHERBOARD & SYSTEM");
    string boardVendor = File.Exists("/sys/class/dmi/id/sys_vendor") ? File.ReadAllText("/sys/class/dmi/id/sys_vendor").Trim() : "Unknown";
    string boardName = File.Exists("/sys/class/dmi/id/product_name") ? File.ReadAllText("/sys/class/dmi/id/product_name").Trim() : "Unknown";
    string boardSerial = File.Exists("/sys/class/dmi/id/product_serial") ? File.ReadAllText("/sys/class/dmi/id/product_serial").Trim() : "Access Denied / Blank";

    PrintMetric("Manufacturer", boardVendor);
    PrintMetric("Product Model", boardName);
    PrintMetric("Serial Number", boardSerial);

    // Storage metrics
    PrintSection("STORAGE DEVICES (SSD/HDD)");
    string lsblkResult = RunCommand("lsblk", "-d -n -o NAME,SIZE,MODEL,ROTA");
    if (!string.IsNullOrEmpty(lsblkResult))
    {
        var lines = lsblkResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int driveNum = 1;
        foreach (var line in lines)
        {
            var segments = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                string name = segments[0];
                string size = segments[1];
                string isRotational = segments.Last() == "1" ? "HDD" : "SSD";
                string modelName = string.Join(" ", segments.Skip(2).Take(segments.Length - 3)).Trim();
                if (string.IsNullOrEmpty(modelName)) modelName = $"Generic Block Device ({name})";
                PrintMetric($"Drive #{driveNum}", $"{modelName} ({size} | Type: {isRotational})");
                driveNum++;
            }
        }
    }
    else
    {
        PrintMetric("Drives", "Could not parse storage blocks via lsblk.");
    }
}
#endregion
#region macOS Deep Scanner
void ScanMac()
{
    // CPU metrics
    PrintSection("PROCESSOR (CPU)");
    string cpuBrand = RunCommand("sysctl", "-n machdep.cpu.brand_string");
    string cpuCores = RunCommand("sysctl", "-n hw.physicalcpu");
    string cpuThreads = RunCommand("sysctl", "-n hw.logicalcpu");
    PrintMetric("Processor Model", cpuBrand.Trim());
    PrintMetric("Physical Cores", cpuCores.Trim());
    PrintMetric("Logical Threads", cpuThreads.Trim());
    // GPU metrics
    PrintSection("GRAPHICS CARD (GPU)");
    string gpuInfo = RunCommand("bash", "-c \"system_profiler SPDisplaysDataType\"");
    if (!string.IsNullOrEmpty(gpuInfo))
    {
        var chipset = gpuInfo.Split('\n').FirstOrDefault(l => l.Contains("Chipset Model"));
        var vram = gpuInfo.Split('\n').FirstOrDefault(l => l.Contains("VRAM"));
        PrintMetric("Chipset Model", chipset?.Replace("Chipset Model:", "").Trim() ?? "Integrated / Unified Apple Silicon");
        PrintMetric("VRAM Allocation", vram?.Replace("VRAM (Dynamic, Max):", "").Trim() ?? "Unified System Memory");
    }
    // Motherboard metrics
    PrintSection("SYSTEM ARCHITECTURE");
    string modelId = RunCommand("sysctl", "-n hw.model");
    string serialNum = RunCommand("bash", "-c \"system_profiler SPHardwareDataType | grep Serial\"");
    PrintMetric("Mac Model Identifier", modelId.Trim());
    PrintMetric("Hardware Serial", serialNum.Replace("System Serial Number (system):", "").Trim());
    // RAM metrics
    PrintSection("PHYSICAL MEMORY (RAM)");
    string memInfo = RunCommand("sysctl", "-n hw.memsize");
    if (long.TryParse(memInfo.Trim(), out long bytes))
    {
        PrintMetric("Total Memory Pools", $"{bytes / (1024 * 1024 * 1024)} GB");
        PrintMetric("Memory Class", "Unified Architecture / LPDDR Low Latency Array");
    }
    // Storage metrics
    PrintSection("STORAGE DEVICES (SSD/HDD)");
    string diskutilResult = RunCommand("diskutil", "list physical");
    if (!string.IsNullOrEmpty(diskutilResult))
    {
        var lines = diskutilResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int driveCounter = 1;
        foreach (var line in lines)
        {
            // Something disk related
            if (line.StartsWith("/dev/disk"))
            {
                var contents = line.Split(new[] { "(external", "(internal" }, StringSplitOptions.RemoveEmptyEntries);
                string meta = line.Contains("(internal") ? "Internal" : "External / USB Drive";
                string details = contents[0].Trim();
                // Output Clean-up
                var parts = details.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    string size = $"{parts[^2]} {parts[^1]}";
                    PrintMetric($"Drive #{driveCounter}", $"{meta} Flash Storage Node ({size})");
                    driveCounter++;
                }
            }
        }
    }
}
#endregion
#region Helper Shell Command Execution
string RunCommand(string filename, string arguments)
{
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filename,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);
        if (process == null) return string.Empty;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
    catch
    {
        return string.Empty;
    }
}
#endregion


// If anyone is reading this, I kinda gave up on making everything pretty
// It works and that all that matters