using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

public partial class WebAppPage : ContentPage
{
    private bool _initialNavigationDone;
    private string? _lastLoadedUrl;

    public WebAppPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var url = BuildWebAppUrl();

        if (!_initialNavigationDone || !string.Equals(_lastLoadedUrl, url, StringComparison.OrdinalIgnoreCase))
        {
            _initialNavigationDone = true;
            NavigateToWebApp(url);
        }
    }

    private string BuildWebAppUrl()
    {
        var api = ServiceLocator.Get<ApiService>();
        var apiBase = api.BaseUrl;

        var siteBase = DeriveSiteBaseFromApiBase(apiBase);
        return siteBase.TrimEnd('/') + "/app/";
    }

    private void NavigateToWebApp(string url)
    {
        _lastLoadedUrl = url;
        LoadingOverlay.IsVisible = true;
        AppWebView.Source = url;
    }

    private static string DeriveSiteBaseFromApiBase(string apiBase)
    {
        var v = (apiBase ?? string.Empty).Trim();
        if (v.Length == 0)
            return "https://ufolep84.fr";

        v = v.TrimEnd('/');

        // Ex: https://ufolep84.fr/API ou https://ufolep84.fr/API/ -> https://ufolep84.fr
        if (v.EndsWith("/API", StringComparison.OrdinalIgnoreCase))
            v = v[..^4];

        return v.TrimEnd('/');
    }

    private void OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        LoadingOverlay.IsVisible = true;
    }

    private async void OnNavigated(object sender, WebNavigatedEventArgs e)
    {
        LoadingOverlay.IsVisible = false;

        if (e.Result == WebNavigationResult.Success)
            return;

        // En local ou si l'URL est mauvaise, l'app web ne pourra pas se charger.
        // Fallback: guider vers la page paramètres API (qui pilote aussi l'URL du site via /API).
        var action = await DisplayActionSheet(
            "Impossible de charger l’app web.",
            "Annuler",
            null,
            "Ouvrir les paramètres API",
            "Réessayer"
        );

        if (action == "Ouvrir les paramètres API")
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
            return;
        }

        if (action == "Réessayer")
        {
            var url = BuildWebAppUrl();
            NavigateToWebApp(url);
        }
    }
}
