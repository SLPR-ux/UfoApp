namespace UFOLEP84.App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(Pages.WebAppPage), typeof(Pages.WebAppPage));
            Routing.RegisterRoute(nameof(Pages.LoginPage), typeof(Pages.LoginPage));
            Routing.RegisterRoute(nameof(Pages.DashboardPage), typeof(Pages.DashboardPage));
            Routing.RegisterRoute(nameof(Pages.PscSessionsPage), typeof(Pages.PscSessionsPage));
            Routing.RegisterRoute(nameof(Pages.PscParticipantsPage), typeof(Pages.PscParticipantsPage));
            Routing.RegisterRoute(nameof(Pages.GqsSessionsPage), typeof(Pages.GqsSessionsPage));
            Routing.RegisterRoute(nameof(Pages.GqsParticipantsPage), typeof(Pages.GqsParticipantsPage));
            Routing.RegisterRoute(nameof(Pages.FormationsPage), typeof(Pages.FormationsPage));
            Routing.RegisterRoute(nameof(Pages.UfoStreetPage), typeof(Pages.UfoStreetPage));
            Routing.RegisterRoute(nameof(Pages.ConciergeriePage), typeof(Pages.ConciergeriePage));
            Routing.RegisterRoute(nameof(Pages.SettingsPage), typeof(Pages.SettingsPage));
        }
    }
}
