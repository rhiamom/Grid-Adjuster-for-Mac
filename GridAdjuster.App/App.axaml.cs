/**************************************************************************
 *   GridAdjuster for Mac                                                 *
 *   TS2GridAdjuster © 2009-2010 Mootilda (http://Mootilda.ModTheSims.info) *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                  *
 *   Ported with Claude (Anthropic)                                       *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Avalonia application entry: creates the main window.                 *
 *************************************************************************/

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GridAdjuster.App.Views;

namespace GridAdjuster.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => window.SaveHerSettings();   // Cmd-Q
            desktop.Exit += (_, _) => window.SaveHerSettings();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
