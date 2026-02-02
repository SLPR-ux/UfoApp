// Services/ApiService.cs
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://ufolep84.fr/API";

    // Stockage du token (utilisez SecureStorage pour plus de sécurité)
    private string _accessToken;

    public ApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    // ===== AUTHENTIFICATION =====
    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var loginData = new
            {
                username,
                password
            };

            var json = JsonSerializer.Serialize(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/auth/login", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(jsonResponse);

                if (apiResponse?.Success == true)
                {
                    _accessToken = apiResponse.Data.Token.AccessToken;
                    await SecureStorage.SetAsync("access_token", _accessToken);

                    // Stocker les infos utilisateur
                    var userJson = JsonSerializer.Serialize(apiResponse.Data.User);
                    await SecureStorage.SetAsync("user_info", userJson);

                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            return false;
        }
    }

    // ===== DONNÉES PUBLIQUES (sans auth) =====
    public async Task<List<Actualite>> GetActualitesAsync(int limit = 30)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/public/actualites?limit={limit}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ActualitesListResponse>>(json);

                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data.Items;
                }
            }

            return new List<Actualite>();
        }
        catch
        {
            return new List<Actualite>();
        }
    }

    public async Task<List<Evenement>> GetEvenementsAsync(string from = "", string to = "", int limit = 100)
    {
        try
        {
            var url = $"{BaseUrl}/public/evenements?limit={limit}";
            if (!string.IsNullOrEmpty(from)) url += $"&from={from}";
            if (!string.IsNullOrEmpty(to)) url += $"&to={to}";

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<EvenementsListResponse>>(json);

                return apiResponse?.Success == true ? apiResponse.Data.Items : new List<Evenement>();
            }
        }
        catch { }

        return new List<Evenement>();
    }

    // ===== DONNÉES PROTÉGÉES (avec token) =====
    private void SetAuthHeader()
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    public async Task<List<Actualite>> GetAdminActualitesAsync(int limit = 100)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync($"{BaseUrl}/admin/actualites?limit={limit}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ActualitesListResponse>>(json);

                return apiResponse?.Success == true ? apiResponse.Data.Items : new List<Actualite>();
            }

            return new List<Actualite>();
        }
        catch
        {
            return new List<Actualite>();
        }
    }

    // ===== RESTAURER LA SESSION =====
    public async Task<bool> RestoreSessionAsync()
    {
        _accessToken = await SecureStorage.GetAsync("access_token");
        return !string.IsNullOrEmpty(_accessToken);
    }

    public async Task<UserInfo> GetCurrentUserAsync()
    {
        var userJson = await SecureStorage.GetAsync("user_info");
        if (!string.IsNullOrEmpty(userJson))
        {
            return JsonSerializer.Deserialize<UserInfo>(userJson);
        }
        return null;
    }
}

// Classes supplémentaires pour les réponses listées
public class ActualitesListResponse
{
    [JsonPropertyName("items")]
    public List<Actualite> Items { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class Evenement
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("titre")]
    public string Titre { get; set; }

    [JsonPropertyName("date_event")]
    public string DateEvent { get; set; }

    [JsonPropertyName("association_nom")]
    public string AssociationNom { get; set; }
}

public class EvenementsListResponse
{
    [JsonPropertyName("items")]
    public List<Evenement> Items { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}