using System.Runtime.InteropServices;

/// <summary>
/// Presses a key the way the keyboard does, so a hotkey's message burst can be
/// counted. <c>keybd_event</c> with a real scan code, which is what a hardware
/// press produces.
/// </summary>
internal static class Program
{
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint vk, uint type);

    private static int Main(string[] args)
    {
        byte vk = args.Length > 0 ? Convert.ToByte(args[0], 16) : (byte)0x2C;  // PrtSc
        int presses = args.Length > 1 ? int.Parse(args[1]) : 3;
        int gapMs = args.Length > 2 ? int.Parse(args[2]) : 1200;
        byte scan = (byte)MapVirtualKey(vk, 0);

        for (int i = 0; i < presses; i++)
        {
            keybd_event(vk, scan, 0, IntPtr.Zero);
            System.Threading.Thread.Sleep(40);
            keybd_event(vk, scan, 2, IntPtr.Zero);
            System.Console.WriteLine($"press {i + 1}: vk=0x{vk:x2} scan=0x{scan:x2}");
            if (i < presses - 1) System.Threading.Thread.Sleep(gapMs);
        }
        return 0;
    }
}
