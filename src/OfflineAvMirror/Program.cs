using System.Runtime.InteropServices;

using OfflineAvMirror.Core;

namespace OfflineAvMirror;

public static partial class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
            return Gui.Run(args);

        AttachToParentConsole();
        return Cli.Run(args);
    }

    /// <summary>
    /// The program is built as a windowed application so that a double click
    /// does not leave a console behind the window. A windowed application has no
    /// console of its own, so the command-line mode borrows the one it was
    /// started from — otherwise <c>--help</c> would print into nothing.
    /// </summary>
    /// <remarks>
    /// A stream that is already redirected is left alone. Reopening it after
    /// attaching the console sent the output to the console instead of the file,
    /// so <c>offline-av-mirror.exe download &gt; log.txt</c> produced an empty
    /// log — measured on Windows, and that redirect is how anyone would capture
    /// a run to send it on.
    /// </remarks>
    private static void AttachToParentConsole()
    {
        if (!OperatingSystem.IsWindows() || !AttachConsole(-1))
            return;

        if (!Console.IsOutputRedirected)
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        if (!Console.IsErrorRedirected)
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
