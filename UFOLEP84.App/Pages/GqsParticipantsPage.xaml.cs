using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using UFOLEP84.App.Models;
using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(DateLabel), "date")]
[QueryProperty(nameof(Formateur), "formateur")]
[QueryProperty(nameof(Lieu), "lieu")]
[QueryProperty(nameof(PlacesMax), "placesMax")]
[QueryProperty(nameof(ParticipantsCount), "participantsCount")]
[QueryProperty(nameof(PlacesRestantes), "placesRestantes")]
public partial class GqsParticipantsPage : ContentPage
{
    public string SessionId { get; set; } = "0";
    public string DateLabel { get; set; } = "";
    public string Formateur { get; set; } = "";
    public string Lieu { get; set; } = "";
    public string PlacesMax { get; set; } = "";
    public string ParticipantsCount { get; set; } = "";
    public string PlacesRestantes { get; set; } = "";

    private readonly ObservableCollection<GqsParticipantRow> _items = new();
    private bool _isLoading;

    public GqsParticipantsPage()
    {
        InitializeComponent();
        ParticipantsView.ItemsSource = _items;
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
        await LoadAsync();
        _isLoading = false;
    }

    private async Task LoadAsync()
    {
        var sessionId = int.TryParse(SessionId, out var sid) ? sid : 0;
        if (sessionId <= 0)
        {
            InfoLabel.Text = "Session invalide";
            return;
        }

        FillHeader();

        var api = ServiceLocator.Get<ApiService>();
        var authorized = await api.EnsureAuthorizedAsync();
        if (!authorized)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        InfoLabel.Text = "Chargement...";

        var participants = await api.GetGqsParticipantsAsync(sessionId);

        _items.Clear();
        foreach (var p in participants)
            _items.Add(new GqsParticipantRow(p));

        if (_items.Count == 0 && !string.IsNullOrWhiteSpace(api.LastErrorCode))
        {
            InfoLabel.Text = api.LastErrorCode == "auth_disabled"
                ? "API: authentification désactivée (secret manquant côté serveur)"
                : $"API: {api.LastErrorMessage ?? api.LastErrorCode}";
        }
        else
        {
            InfoLabel.Text = $"{_items.Count} participant(s)";
        }
    }

    private void FillHeader()
    {
        var dateText = (DateLabel ?? string.Empty).Trim();
        if (TryPrettyDate(dateText, out var pretty))
            dateText = pretty;

        DateValue.Text = string.IsNullOrWhiteSpace(dateText) ? "Session" : dateText;
        FormateurValue.Text = string.IsNullOrWhiteSpace(Formateur) ? "" : Formateur;
        LieuValue.Text = string.IsNullOrWhiteSpace(Lieu) ? "" : Lieu;

        if (int.TryParse(PlacesMax, out var pm) && pm > 0)
        {
            var placesInfo = "";
            if (int.TryParse(ParticipantsCount, out var pc) && pc >= 0)
                placesInfo = $"Inscrits : {pc}";

            if (int.TryParse(PlacesRestantes, out var pr) && pr >= 0)
                placesInfo = string.IsNullOrWhiteSpace(placesInfo) ? $"Places restantes : {pr}" : placesInfo + $" • Places restantes : {pr}";

            PlacesValue.Text = string.IsNullOrWhiteSpace(placesInfo)
                ? $"Places max : {pm}"
                : $"Places max : {pm} • {placesInfo}";
        }
        else
        {
            PlacesValue.Text = string.Empty;
        }
    }

    private static bool TryPrettyDate(string input, out string pretty)
    {
        pretty = input;
        var s = (input ?? string.Empty).Trim();
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || DateOnly.TryParseExact(s, "dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out d))
        {
            pretty = d.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM", CultureInfo.GetCultureInfo("fr-FR"));
            return true;
        }
        return false;
    }

    private async void OnRefreshClicked(object sender, EventArgs e) => await EnsureAuthAndLoadAsync();

    private async void OnEmailTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not GqsParticipantRow row) return;
        var email = (row.Participant.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email)) return;
        await Launcher.Default.OpenAsync(new Uri($"mailto:{email}"));
    }

    private async void OnPhoneTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not GqsParticipantRow row) return;
        var phone = (row.PhoneRaw ?? string.Empty).Trim();
        phone = SanitizePhone(phone);
        if (string.IsNullOrWhiteSpace(phone)) return;
        await Launcher.Default.OpenAsync(new Uri($"tel:{phone}"));
    }

    private static string SanitizePhone(string input)
    {
        var s = (input ?? string.Empty).Trim();
        if (s.Length == 0) return string.Empty;
        var chars = s.Where(c => char.IsDigit(c) || c == '+').ToArray();
        return new string(chars);
    }
}

public class GqsParticipantRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public GqsParticipant Participant { get; }

    public GqsParticipantRow(GqsParticipant participant)
    {
        Participant = participant;
    }

    public string FullName => $"{Participant.Nom} {Participant.Prenom}".Trim();

    public string Email => string.IsNullOrWhiteSpace(Participant.Email) ? "" : Participant.Email!;
    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);

    public string? PhoneRaw
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Participant.Portable))
                return Participant.Portable;
            if (!string.IsNullOrWhiteSpace(Participant.Telephone))
                return Participant.Telephone;
            return null;
        }
    }

    public string Phone
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Participant.Portable))
                return Participant.Portable!;
            return Participant.Telephone ?? "";
        }
    }

    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);

    public bool HasTarifLabel => !string.IsNullOrWhiteSpace(TarifLabel);
    public string TarifLabel
    {
        get
        {
            if (Participant.TarifCents is not { } cents || cents <= 0)
                return string.Empty;
            return $"Tarif : {(cents / 100.0m):0.00} €";
        }
    }

    public string LicenceLabel
    {
        get
        {
            var lic = Participant.NumeroLicence;
            var isLicencie = (Participant.IsLicencieUfolep ?? 0) == 1;
            if (isLicencie)
            {
                return string.IsNullOrWhiteSpace(lic) ? "Tarif licencié" : $"Licencié — {lic}";
            }

            return string.IsNullOrWhiteSpace(lic) ? "" : $"Licence — {lic}";
        }
    }
}
