using UFOLEP84.App.Services;

namespace UFOLEP84.App.Pages;

public partial class UfoStreetPage : ContentPage
{
    public UfoStreetPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var api = ServiceLocator.Get<ApiService>();
        if (!await api.EnsureAuthorizedAsync())
            await Shell.Current.GoToAsync("//login");
    }
}
