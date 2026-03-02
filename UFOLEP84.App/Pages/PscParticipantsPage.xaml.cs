using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UFOLEP84.App.Models;
using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(DateLabel), "date")]
[QueryProperty(nameof(Formateur), "formateur")]
[QueryProperty(nameof(PlacesMax), "placesMax")]
[QueryProperty(nameof(ParticipantsCount), "participantsCount")]
[QueryProperty(nameof(PresentCount), "presentCount")]
[QueryProperty(nameof(AbsentCount), "absentCount")]
[QueryProperty(nameof(UnknownCount), "unknownCount")]
[QueryProperty(nameof(Mode), "mode")]
public partial class PscParticipantsPage : ContentPage
{
    public string SessionId { get; set; } = "0";
    public string DateLabel { get; set; } = "";
    public string Formateur { get; set; } = "";
    public string PlacesMax { get; set; } = "";
    public string ParticipantsCount { get; set; } = "";
    public string PresentCount { get; set; } = "";
    public string AbsentCount { get; set; } = "";
    public string UnknownCount { get; set; } = "";
    public string Mode { get; set; } = "details";

    private readonly ObservableCollection<PscParticipantRow> _items = new();
    private bool _isLoading;

    public PscParticipantsPage()
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

        var participants = await api.GetPscParticipantsAsync(sessionId);

        _items.Clear();
        foreach (var p in participants)
            _items.Add(new PscParticipantRow(p));

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

        ParticipantsCountValue.Text = string.IsNullOrWhiteSpace(ParticipantsCount) ? "—" : ParticipantsCount;
        PresentCountValue.Text = string.IsNullOrWhiteSpace(PresentCount) ? "—" : PresentCount;
        AbsentCountValue.Text = string.IsNullOrWhiteSpace(AbsentCount) ? "—" : AbsentCount;
        UnknownCountValue.Text = string.IsNullOrWhiteSpace(UnknownCount) ? "—" : UnknownCount;

        if (int.TryParse(PlacesMax, out var pm) && pm > 0)
            PlacesMaxValue.Text = $"Places max : {pm}";
        else
            PlacesMaxValue.Text = string.Empty;

        var isCall = string.Equals((Mode ?? "").Trim(), "call", StringComparison.OrdinalIgnoreCase);
        ModeBadge.IsVisible = isCall;
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

    private async Task SetStatusAsync(object? param, string status)
    {
        if (param is not PscParticipantRow row)
            return;

        var api = ServiceLocator.Get<ApiService>();
        var ok = await api.SetPscAttendanceAsync(row.Participant.Id, status, null);
        if (ok)
        {
            row.Participant.AttendanceStatus = status;
            row.NotifyChanged();
        }
        else
        {
            await DisplayAlert("Erreur", "Mise à jour échouée", "OK");
        }
    }

    private async void OnMarkPresent(object sender, EventArgs e) => await SetStatusAsync(((SwipeItem)sender).CommandParameter, "present");
    private async void OnMarkAbsent(object sender, EventArgs e) => await SetStatusAsync(((SwipeItem)sender).CommandParameter, "absent");

    private async void OnRefreshClicked(object sender, EventArgs e) => await EnsureAuthAndLoadAsync();

    private async void OnEmailTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not PscParticipantRow row) return;
        var email = (row.Participant.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email)) return;
        await Launcher.Default.OpenAsync(new Uri($"mailto:{email}"));
    }

    private async void OnPhoneTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not PscParticipantRow row) return;
        var phone = (row.Participant.Telephone ?? string.Empty).Trim();
        phone = SanitizePhone(phone);
        if (string.IsNullOrWhiteSpace(phone)) return;
        await Launcher.Default.OpenAsync(new Uri($"tel:{phone}"));
    }

    private async void OnPrescriberContactTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not PscParticipantRow row) return;
        var contact = (row.PrescriberContactRaw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(contact)) return;

        if (contact.Contains('@'))
        {
            await Launcher.Default.OpenAsync(new Uri($"mailto:{contact}"));
            return;
        }

        var phone = SanitizePhone(contact);
        if (!string.IsNullOrWhiteSpace(phone))
        {
            await Launcher.Default.OpenAsync(new Uri($"tel:{phone}"));
        }
    }

    private static string SanitizePhone(string input)
    {
        var s = (input ?? string.Empty).Trim();
        if (s.Length == 0) return string.Empty;
        // garder + et chiffres
        var chars = s.Where(c => char.IsDigit(c) || c == '+').ToArray();
        return new string(chars);
    }
}

public class PscParticipantRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public PscParticipant Participant { get; }

    public PscParticipantRow(PscParticipant participant)
    {
        Participant = participant;
    }

    public string FullName => $"{Participant.Nom} {Participant.Prenom}".Trim();
    public string Email => (Participant.Email ?? string.Empty).Trim();
    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);

    public string Phone => (Participant.Telephone ?? string.Empty).Trim();
    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);

    public bool HasPrescriber => !string.IsNullOrWhiteSpace(PrescriberName) || !string.IsNullOrWhiteSpace(PrescriberContact);

    public string PrescriberTitle
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PrescriberName) && string.IsNullOrWhiteSpace(PrescriberContact))
                return string.Empty;
            return "Prescripteur";
        }
    }

    public string PrescriberName
    {
        get
        {
            var name = (Participant.NomPrescripteur ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            // parfois prescripteur contient du texte utile (nom/structure)
            var raw = (Participant.Prescripteur ?? string.Empty).Trim();
            if (raw.Contains('@'))
                return string.Empty;
            if (raw.Any(char.IsDigit))
                return string.Empty;
            return raw;
        }
    }

    public string? PrescriberContactRaw
    {
        get
        {
            var raw = (Participant.Prescripteur ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (raw.Contains('@')) return raw;
            if (raw.Any(char.IsDigit)) return raw;
            return null;
        }
    }

    public string PrescriberContact
    {
        get
        {
            var raw = (PrescriberContactRaw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            if (raw.Contains('@')) return raw;
            return raw;
        }
    }

    public bool HasPrescriberContact => !string.IsNullOrWhiteSpace(PrescriberContactRaw);

    public string StatusLabel
    {
        get
        {
            return Participant.AttendanceStatus switch
            {
                "present" => "Présent",
                "absent" => "Absent",
                _ => "Non pointé",
            };
        }
    }

    public void NotifyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusLabel)));
    }
}
