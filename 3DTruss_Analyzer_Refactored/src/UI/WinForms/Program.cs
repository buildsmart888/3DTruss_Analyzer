namespace TrussAnalyzer.UI.WinForms;

using System;
using System.Windows.Forms;
using TrussAnalyzer.UI.AppShell;

/// <summary>
/// Main entry point for the 3D Truss Analyzer application.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--wpf-shell", StringComparer.OrdinalIgnoreCase))
        {
            var application = new System.Windows.Application();
            application.Run(new GOStructAnalysisShellWindow());
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(true);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        
        Application.Run(new MainForm());
    }
}
