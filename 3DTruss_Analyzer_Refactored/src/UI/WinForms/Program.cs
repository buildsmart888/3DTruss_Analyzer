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
        if (args.Any(a => string.Equals(a, "--wpf-shell", StringComparison.OrdinalIgnoreCase)))
        {
            var wpf = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose
            };
            wpf.Run(new GOStructAnalysisShellWindow());
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(true);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        
        Application.Run(new MainForm());
    }
}
