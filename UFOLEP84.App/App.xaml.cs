namespace UFOLEP84.App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();

            // Login-first : si une session valide existe, on saute la page login.
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    var api = Services.ServiceLocator.Get<ApiService>();
                    var authorized = await api.EnsureAuthorizedAsync();
                    if (authorized)
                        await Shell.Current.GoToAsync("//dashboard");
                    else
                        await Shell.Current.GoToAsync("//login");
                }
                catch
                {
                    // Best effort : rester sur login en cas d’erreur.
                    try { await Shell.Current.GoToAsync("//login"); } catch { }
                }
            });
        }
    }
}
