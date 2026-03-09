namespace UFOLEP84.App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();

            // Le client lourd est désormais un wrapper WebView de l'app web (/app),
            // ce qui garantit un rendu et des fonctionnalités identiques.
            Dispatcher.Dispatch(async () =>
            {
                try { await Shell.Current.GoToAsync("//webapp"); } catch { }
            });
        }
    }
}
