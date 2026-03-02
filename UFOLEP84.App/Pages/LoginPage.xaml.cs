using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnOpenSettingsClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(SettingsPage));

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        StatusLabel.IsVisible = false;

        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (username == "" || password == "")
        {
            StatusLabel.Text = "Identifiants requis.";
            StatusLabel.IsVisible = true;
            return;
        }

        var api = ServiceLocator.Get<ApiService>();
        var ok = await api.LoginAsync(username, password);

        if (!ok)
        {
            if (string.Equals(api.LastErrorCode, "auth_disabled", StringComparison.OrdinalIgnoreCase))
            {
                StatusLabel.Text = "Connexion impossible : l’auth API est désactivée sur ce serveur (UFOLEP_API_SECRET manquant).";
            }
            else if (string.Equals(api.LastErrorCode, "rate_limited", StringComparison.OrdinalIgnoreCase))
            {
                StatusLabel.Text = $"API: {api.LastErrorMessage ?? "HTTP 429"}. Réessaie dans quelques secondes.";
            }
            else if (!string.IsNullOrWhiteSpace(api.LastErrorMessage))
            {
                StatusLabel.Text = $"API: {api.LastErrorMessage} (URL: {api.LastRequestUrl ?? api.BaseUrl})";
            }
            else
            {
                StatusLabel.Text = "Connexion échouée ou accès refusé (réservé admin_ufo / superadmin).";
            }
            StatusLabel.IsVisible = true;
            return;
        }

        await Shell.Current.GoToAsync("//dashboard");
    }
}
