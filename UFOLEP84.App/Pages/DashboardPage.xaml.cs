using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var api = ServiceLocator.Get<ApiService>();
        var authorized = await api.EnsureAuthorizedAsync();
        if (!authorized)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        var user = await api.GetCurrentUserAsync();
        if (user is null)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        UserLabel.Text = $"Connecté : {user.Username} ({user.Role}) — API: {api.BaseUrl}";
    }

    private async void OnOpenPscClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(PscSessionsPage));

    private async void OnOpenGqsClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(GqsSessionsPage));

    private async void OnOpenFormationsClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(FormationsPage));

    private async void OnOpenUfoStreetClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(UfoStreetPage));

    private async void OnOpenConciergerieClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(ConciergeriePage));

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var api = ServiceLocator.Get<ApiService>();
        await api.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }

    private async void OnOpenSettingsClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(SettingsPage));
}
