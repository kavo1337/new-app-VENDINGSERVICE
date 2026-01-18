using System.Windows;

namespace app.CLIENT;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Пытаемся загрузить сохраненную сессию
        if (Session.Load() && !string.IsNullOrWhiteSpace(Session.AccessToken) && Session.User != null)
        {
            // Если сессия найдена, открываем главное окно
            var shell = new ShellWindow();
            shell.Show();
            MainWindow = shell;
        }
        else
        {
            // Если сессии нет, показываем окно входа
            var login = new LoginWindow();
            login.Show();
            MainWindow = login;
        }
    }
}

