using System.Collections.ObjectModel;
using UFOLEP84.App.Models;
using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

public partial class FormationsPage : ContentPage
{
    private readonly ObservableCollection<FormationRow> _items = new();
    private bool _isLoading;

    public FormationsPage()
    {
        InitializeComponent();
        FormationsView.ItemsSource = _items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureAuthAndLoadAsync();
    }

    private async Task EnsureAuthAndLoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        var api = ServiceLocator.Get<ApiService>();
        var authorized = await api.EnsureAuthorizedAsync();
        if (!authorized)
        {
            _isLoading = false;
            await Shell.Current.GoToAsync("//login");
            return;
        }

        await LoadAsync();
        _isLoading = false;
    }

    private async Task LoadAsync()
    {
        try
        {
            InfoLabel.Text = "Chargement...";

            var api = ServiceLocator.Get<ApiService>();
            var formations = await api.GetAdminFormationsAsync();

            _items.Clear();
            foreach (var f in formations)
                _items.Add(new FormationRow(f));

            if (_items.Count == 0 && !string.IsNullOrWhiteSpace(api.LastErrorCode))
            {
                InfoLabel.Text = api.LastErrorCode == "auth_disabled"
                    ? "API: authentification désactivée (secret manquant côté serveur)"
                    : $"API: {api.LastErrorMessage ?? api.LastErrorCode} (URL: {api.LastRequestUrl ?? api.BaseUrl})";
            }
            else
            {
                if (api.LastResultWasCache)
                {
                    var age = api.LastCacheAge;
                    var ageLabel = age is null ? "" : $" — cache ({Math.Max(1, (int)Math.Ceiling(age.Value.TotalMinutes))} min)";
                    InfoLabel.Text = $"{_items.Count} formation(s){ageLabel}";
                }
                else
                {
                    InfoLabel.Text = $"{_items.Count} formation(s)";
                }
            }
        }
        catch (Exception ex)
        {
            InfoLabel.Text = $"Erreur: {ex.Message}";
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
        => await EnsureAuthAndLoadAsync();
}

public class FormationRow
{
    public Formation Formation { get; }

    public FormationRow(Formation formation)
    {
        Formation = formation;
    }

    public string Titre => Formation.Titre;

    public string DateLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Formation.DateFormation))
                return "Date : non définie";

            return $"Date : {Formation.DateFormation}";
        }
    }

    public string LieuLabel => $"Lieu : {(string.IsNullOrWhiteSpace(Formation.Lieu) ? "Non spécifié" : Formation.Lieu)}";

    public string StatutLabel => Formation.EstPublie ? "Statut : Publié" : "Statut : Brouillon";
}
