using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using L2Toolkit.Utilities;
using System.Text;

namespace L2Toolkit
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            TableManager.EnsureTables();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
