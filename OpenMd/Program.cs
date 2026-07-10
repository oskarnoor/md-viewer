namespace OpenMd;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--smoke-test", StringComparison.OrdinalIgnoreCase))
        {
            return SmokeTester.Run(args.Skip(1).ToArray());
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1(GetInitialPath(args)));
        return 0;
    }

    private static string? GetInitialPath(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith("-", StringComparison.Ordinal) && File.Exists(arg))
            {
                return Path.GetFullPath(arg);
            }
        }

        return null;
    }
}
