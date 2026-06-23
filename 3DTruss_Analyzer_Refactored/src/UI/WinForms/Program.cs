namespace TrussAnalyzer.UI.WinForms;

using System;
using System.Windows.Forms;

/// <summary>
/// Main entry point for the 3D Truss Analyzer application.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(true);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        
        Application.Run(new MainForm());
    }
}
