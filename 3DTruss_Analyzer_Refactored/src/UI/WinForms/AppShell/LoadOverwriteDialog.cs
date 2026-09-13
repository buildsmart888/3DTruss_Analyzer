namespace TrussAnalyzer.UI.AppShell;

using System.Windows;
using System.Windows.Controls;
using TrussAnalyzer.Core.Application;

public static class LoadOverwriteDialog
{
    public static bool Confirm(Window owner, IReadOnlyList<LoadOverwriteWarning> warnings)
    {
        var message = "Generated loads will replace manual assignments:\n\n" + string.Join("\n", warnings.Select(item => $"• {item.ExistingLabel} → {item.GeneratedLabel}"));
        return MessageBox.Show(owner, message + "\n\nContinue?", "Load overwrite warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
