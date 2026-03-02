using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();

        var api = ServiceLocator.Get<ApiService>();
        ApiBaseUrlEntry.Text = api.BaseUrl;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        StatusLabel.IsVisible = false;

        var raw = ApiBaseUrlEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            StatusLabel.Text = "URL requise";
            StatusLabel.IsVisible = true;
            return;
        }

        var api = ServiceLocator.Get<ApiService>();
        api.SetBaseUrl(raw);

        // On invalide la session car un token n’est pas portable d’un serveur à l’autre.
        await api.LogoutAsync();

        await DisplayAlert("OK", "Paramètres enregistrés. Merci de vous reconnecter.", "OK");
        await Shell.Current.GoToAsync("//login");
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        var api = ServiceLocator.Get<ApiService>();
        api.SetBaseUrl("https://ufolep84.fr/API");
        ApiBaseUrlEntry.Text = api.BaseUrl;
        StatusLabel.IsVisible = false;
    }

    private async void OnTestClicked(object sender, EventArgs e)
    {
        StatusLabel.TextColor = Colors.Black;
        StatusLabel.Text = "Test en cours...";
        StatusLabel.IsVisible = true;

        try
        {
            var api = ServiceLocator.Get<ApiService>();
            var ok = await api.TestHealthAsync();
            if (ok)
            {
                StatusLabel.TextColor = Colors.Green;
                StatusLabel.Text = $"OK: {api.BaseUrl}/health";
            }
            else
            {
                StatusLabel.TextColor = Colors.OrangeRed;
                StatusLabel.Text = $"KO: {api.LastErrorMessage ?? api.LastErrorCode ?? "Erreur"} (URL: {api.BaseUrl})";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.TextColor = Colors.OrangeRed;
            StatusLabel.Text = $"Erreur: {ex.Message}";
        }
    }
}
