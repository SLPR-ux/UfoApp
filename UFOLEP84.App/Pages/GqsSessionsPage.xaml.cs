using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using UFOLEP84.App.Models;
using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

public partial class GqsSessionsPage : ContentPage
{
    private readonly ObservableCollection<GqsSessionRow> _items = new();
    private bool _isLoading;

    public GqsSessionsPage()
    {
        InitializeComponent();
        SessionsView.ItemsSource = _items;
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
        InfoLabel.Text = "Chargement...";

        var api = ServiceLocator.Get<ApiService>();
        var sessions = await api.GetGqsSessionsAsync();

        var rows = BuildRows(sessions);
        _items.Clear();
        foreach (var r in rows)
            _items.Add(r);

        if (_items.Count == 0 && !string.IsNullOrWhiteSpace(api.LastErrorCode))
        {
            InfoLabel.Text = api.LastErrorCode == "auth_disabled"
                ? "API: authentification désactivée (secret manquant côté serveur)"
                : $"API: {api.LastErrorMessage ?? api.LastErrorCode} (URL: {api.LastRequestUrl ?? api.BaseUrl})";
        }
        else
        {
            InfoLabel.Text = _items.Count == 0
                ? $"Aucune session (URL: {api.LastRequestUrl ?? api.BaseUrl})"
                : api.LastResultWasCache
                    ? $"{_items.Count} session(s) — cache ({Math.Max(1, (int)Math.Ceiling((api.LastCacheAge ?? TimeSpan.FromMinutes(1)).TotalMinutes))} min)"
                    : $"{_items.Count} session(s)";
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await EnsureAuthAndLoadAsync();
    }

    private async void OnSessionTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not GqsSessionRow row)
            return;

        await NavigateToSessionAsync(row);
    }

    private async void OnOpenInvoked(object sender, EventArgs e)
    {
        if (((SwipeItem)sender).CommandParameter is not GqsSessionRow row)
            return;

        await NavigateToSessionAsync(row);
    }

    private static List<GqsSessionRow> BuildRows(List<GqsSession> sessions)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var min = today.AddDays(-5);

        var rows = new List<GqsSessionRow>();
        foreach (var s in sessions)
        {
            var date = TryParseDateOnly(s.DateSession, out var d) ? d : (DateOnly?)null;
            if (date is { } dd && dd < min)
                continue;

            rows.Add(new GqsSessionRow(s, date));
        }

        rows.Sort((a, b) => Nullable.Compare(a.Date, b.Date));

        var next = rows
            .Where(r => r.Date is { } d && d >= today)
            .OrderBy(r => r.Date)
            .FirstOrDefault();
        if (next != null)
            next.IsNextUpcoming = true;

        foreach (var r in rows)
            r.IsToday = r.Date == today;

        return rows;
    }

    private static bool TryParseDateOnly(string input, out DateOnly date)
    {
        var s = (input ?? string.Empty).Trim();
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        if (DateOnly.TryParseExact(s, "dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out date))
            return true;
        if (DateOnly.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            return true;
        date = default;
        return false;
    }

    private static async Task NavigateToSessionAsync(GqsSessionRow row)
    {
        var s = row.Session;
        var qs = $"?sessionId={s.Id}" +
                 $"&date={Uri.EscapeDataString(s.DateSession ?? string.Empty)}" +
                 $"&formateur={Uri.EscapeDataString(s.Formateur ?? string.Empty)}" +
                 $"&lieu={Uri.EscapeDataString(s.Lieu ?? string.Empty)}" +
                 $"&placesMax={s.PlacesMax}" +
                 $"&participantsCount={s.ParticipantsCount}" +
                 $"&placesRestantes={s.PlacesRestantes}";
        await Shell.Current.GoToAsync(nameof(GqsParticipantsPage) + qs);
    }
}

public sealed class GqsSessionRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public GqsSession Session { get; }
    public DateOnly? Date { get; }

    private bool _isToday;
    public bool IsToday
    {
        get => _isToday;
        set { _isToday = value; NotifyChanged(); NotifyChanged(nameof(BorderColor)); NotifyChanged(nameof(BadgeText)); NotifyChanged(nameof(HasBadge)); NotifyChanged(nameof(BadgeBackground)); }
    }

    private bool _isNextUpcoming;
    public bool IsNextUpcoming
    {
        get => _isNextUpcoming;
        set { _isNextUpcoming = value; NotifyChanged(); NotifyChanged(nameof(BorderColor)); NotifyChanged(nameof(BadgeText)); NotifyChanged(nameof(HasBadge)); NotifyChanged(nameof(BadgeBackground)); }
    }

    public GqsSessionRow(GqsSession session, DateOnly? date)
    {
        Session = session;
        Date = date;
    }

    public string DisplayDate
    {
        get
        {
            if (Date is not { } d)
                return Session.DateSession;

            var dt = d.ToDateTime(TimeOnly.MinValue);
            return dt.ToString("dddd d MMMM", CultureInfo.GetCultureInfo("fr-FR"));
        }
    }

    public Color BorderColor
    {
        get
        {
            if (IsToday) return Color.FromArgb("#FCA5A5");
            if (IsNextUpcoming) return Colors.White;
            return Color.FromArgb("#E5E7EB");
        }
    }

    public string BadgeText => IsToday ? "Aujourd'hui" : IsNextUpcoming ? "Prochaine" : "";
    public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);

    public Color BadgeBackground
    {
        get
        {
            if (IsToday) return Color.FromArgb("#EF4444");
            if (IsNextUpcoming) return Color.FromArgb("#111827");
            return Color.FromArgb("#9CA3AF");
        }
    }

    private void NotifyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
