namespace UFOLEP84.App
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnAgendaButtonClicked(object sender, EventArgs e)
        {
            // Affiche une alerte simple pour l'instant
            await DisplayAlert("Agenda", "Page de l'agenda à venir !", "OK");
        }

        private async void OnPscButtonClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(Pages.PscSessionsPage));
        }
    }

}
