// Copyright (c) 2024 Matthias Wolf, Mawosoft.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
[assembly: SupportedOSPlatform("windows")]

namespace TestConsolePalWindows3;

internal sealed partial class Program
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr SetStdHandle(int nStdHandle, IntPtr hHandle);

    public const int STD_INPUT_HANDLE = -10;
    public const int STD_OUTPUT_HANDLE = -11;
    public const int STD_ERROR_HANDLE = -12;

    public const int INPUT_KEYBOARD = 1; // struct INPUT has KEYBDINPUT

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public INPUTUNION union;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mouseInput;
        [FieldOffset(0)] public KEYBDINPUT keyboardInput;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    };

    // KEYBDINPUT.dwFlags
    public const int KEYEVENTF_EXTENDEDKEY = 1;
    public const int KEYEVENTF_KEYUP = 2;
    public const int KEYEVENTF_UNICODE = 4;
    public const int KEYEVENTF_SCANCODE = 8;

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    };

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial int SendInput(int cInputs, ReadOnlySpan<INPUT> pInputs, int cbSize);

    internal static void Main(string[] args)
    {
        if (args.Length == 2)
        {
            Console.WriteLine($"Redirected in/out/err: {Console.IsInputRedirected}/{Console.IsOutputRedirected}/{Console.IsErrorRedirected}");
            // Allocate a new console to allow simulating console input on CI runner, but keep output redirections.
            IntPtr hout = GetStdHandle(STD_OUTPUT_HANDLE);
            IntPtr herr = GetStdHandle(STD_ERROR_HANDLE);
            bool bfree = FreeConsole();
            bool balloc = AllocConsole();
            SetStdHandle(STD_OUTPUT_HANDLE, hout);
            SetStdHandle(STD_ERROR_HANDLE, herr);
            Console.WriteLine($"Free/AllocConsole: {bfree} {balloc}");
            Console.WriteLine($"Starting client...");
            using Process p = new();
            p.StartInfo.UseShellExecute = false;
            //p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = args[0];
            p.StartInfo.ArgumentList.Add(args[1]);
            p.Start();
            p.WaitForExit();
            return;
        }

        // Client mode

        Console.WriteLine($"Redirected in/out/err: {Console.IsInputRedirected}/{Console.IsOutputRedirected}/{Console.IsErrorRedirected}");

        // Send console input
        INPUT[] inputs = new INPUT[4];
        inputs[0].type = INPUT_KEYBOARD;
        // Alt keydown
        inputs[0].union.keyboardInput.wVk = 0x12;
        inputs[0].union.keyboardInput.wScan = 0x38;
        inputs[0].union.keyboardInput.dwFlags = 0;
        // Numpad1 keydown
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].union.keyboardInput.wVk = (ushort)ConsoleKey.NumPad1;
        inputs[1].union.keyboardInput.wScan = 0x4f;
        inputs[1].union.keyboardInput.dwFlags = 0;
        // Numpad1 keyup
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].union.keyboardInput.wVk = (ushort)ConsoleKey.NumPad1;
        inputs[2].union.keyboardInput.wScan = 0x4f;
        inputs[2].union.keyboardInput.dwFlags = KEYEVENTF_KEYUP;
        // Alt keyup
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].union.keyboardInput.wVk = 0x12; // Alt
        inputs[3].union.keyboardInput.wScan = 0x38;
        inputs[3].union.keyboardInput.dwFlags = KEYEVENTF_KEYUP;
        // Unless there is a arguments error, SendInput always returns success.
        // Need to always check the last PInvoke error.
        int sent = SendInput(inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        int error = Marshal.GetLastPInvokeError();
        Console.WriteLine($"SendInput: sent: {sent}, last error: 0x{error:X}");

        // Wait for available console input and read it, with timeout against unexpected hijinks.
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (Console.KeyAvailable) break;
        }
        sw.Stop();
        if (sw.Elapsed < timeout)
        {
            var cki = Console.ReadKey(intercept: true);
            Console.WriteLine($"ReadKey: key: {cki.Key}, mod: {cki.Modifiers}, char: 0x{(int)cki.KeyChar:X}");

        }
        else
        {
            Console.WriteLine("KeyAvailable time out.");
        }

        // Ensure new console is visible if run locally w/o redirections.
        if (!Console.IsOutputRedirected)
        {
            Console.WriteLine("App started without redirected std handles. Press <ENTER> to close this window.");
            Console.ReadLine();
        }
    }
}
