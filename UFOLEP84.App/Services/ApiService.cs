// Services/ApiService.cs
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UFOLEP84.App.Models;
using Microsoft.Maui.Storage;
using System.Net;
using System.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _httpGate = new(1, 1);
    private DateTimeOffset? _rateLimitUntilUtc;

    private readonly object _cacheLock = new();
    private (DateTimeOffset at, List<PscSession> items)? _pscSessionsCache;
    private (DateTimeOffset at, List<GqsSession> items)? _gqsSessionsCache;
    private readonly ConcurrentDictionary<string, (DateTimeOffset at, List<PscParticipant> items)> _pscParticipantsCache = new();
    private readonly ConcurrentDictionary<string, (DateTimeOffset at, List<GqsParticipant> items)> _gqsParticipantsCache = new();

    // Cache plus long pour éviter les rafales (navigation entre pages) qui déclenchent du 429 côté hébergeur/WAF.
    private static readonly TimeSpan SessionsCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ParticipantsCacheTtl = TimeSpan.FromMinutes(1);
    private const string DefaultBaseUrl = "https://ufolep84.fr/API";
    private const string ApiBaseUrlKey = "api_base_url";
    private string _baseUrl;

    public string BaseUrl => _baseUrl;
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public string? LastRequestUrl { get; private set; }
    public int? LastHttpStatusCode { get; private set; }
    public bool LastResultWasCache { get; private set; }
    public TimeSpan? LastCacheAge { get; private set; }

    private static readonly string[] AllowedUsernames = ["admin_ufo", "superadmin"]; // usernames
    private static readonly string[] AllowedRoles = ["admin_ufolep", "admin_ufo", "superadmin"];  // roles côté site/API

    // Stockage du token (utilisez SecureStorage pour plus de sécurité)
    private string? _accessToken;

    private const string AccessTokenKey = "access_token";
    private const string UserInfoKey = "user_info";

    public ApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        // Certains WAF/rate-limiters sont plus stricts quand l'User-Agent est vide.
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UFOLEP84App", "1.0"));
        }
        catch
        {
            // best effort
        }
        // Évite les écrans qui restent bloqués sur "Chargement..." en cas d'API lente/injoignable.
        _httpClient.Timeout = TimeSpan.FromSeconds(20);

        _baseUrl = NormalizeBaseUrl(Preferences.Get(ApiBaseUrlKey, DefaultBaseUrl));
        TrySetBaseAddress(_baseUrl);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> createRequest, int maxAttempts = 3)
    {
        await _httpGate.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_rateLimitUntilUtc is DateTimeOffset until && until > now)
            {
                await Task.Delay(until - now);
            }

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = createRequest();
                LastRequestUrl = request.RequestUri?.ToString();
                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode != HttpStatusCode.TooManyRequests)
                    return response;

                // 429: si c'est le dernier essai, on renvoie la réponse pour que l'appelant affiche l'erreur
                if (attempt >= maxAttempts)
                    return response;

                // 429: attendre avant de retenter
                var delay = TimeSpan.FromSeconds(Math.Min(60, 2 * Math.Pow(2, attempt - 1))); // 2s, 4s, 8s, 16s, 32s, 60s
                if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
                {
                    if (delta < TimeSpan.FromSeconds(1)) delta = TimeSpan.FromSeconds(1);
                    if (delta > TimeSpan.FromSeconds(60)) delta = TimeSpan.FromSeconds(60);
                    delay = delta;
                }

                _rateLimitUntilUtc = DateTimeOffset.UtcNow + delay;
                response.Dispose();
                await Task.Delay(delay);
            }

            // Défensif: ne devrait pas arriver
            using var lastRequest = createRequest();
            LastRequestUrl = lastRequest.RequestUri?.ToString();
            return await _httpClient.SendAsync(lastRequest);
        }
        finally
        {
            _httpGate.Release();
        }
    }

    private Task<HttpResponseMessage> GetWithRetryAsync(string url, int maxAttempts = 3)
        => SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, url), maxAttempts);

    public void SetBaseUrl(string baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);
        _baseUrl = normalized;
        Preferences.Set(ApiBaseUrlKey, normalized);
        TrySetBaseAddress(normalized);
    }

    private void TrySetBaseAddress(string baseUrl)
    {
        // Délibérément "best effort" : si URI invalide, on garde les appels en absolu via _baseUrl.
        try
        {
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }
        catch
        {
            // ignore
        }
    }

    private static string NormalizeBaseUrl(string raw)
    {
        var url = (raw ?? string.Empty).Trim();
        if (url.Length == 0)
            return DefaultBaseUrl;

        // Correction d'une faute fréquente: "https://domaine.tld:API" au lieu de ".../API"
        // (un port doit être numérique, donc ":API" rend l'URI invalide côté .NET)
        url = url.Replace(":API", "/API", StringComparison.OrdinalIgnoreCase);
        url = url.Replace(":/API", "/API", StringComparison.OrdinalIgnoreCase);

        // Enlever le trailing slash
        url = url.TrimEnd('/');

        // L’app utilise des routes relatives à /API
        if (!url.EndsWith("/API", StringComparison.OrdinalIgnoreCase))
            url += "/API";

        return url;
    }

    private void ResetLastError()
    {
        LastErrorCode = null;
        LastErrorMessage = null;
        LastRequestUrl = null;
        LastHttpStatusCode = null;
        LastResultWasCache = false;
        LastCacheAge = null;
    }

    private static string CachePath(string key)
    {
        var safe = string.Concat(key.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        return Path.Combine(FileSystem.AppDataDirectory, $"cache_{safe}.json");
    }

    private static async Task WriteCacheAsync(string key, string json)
    {
        try
        {
            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            var payload = JsonSerializer.Serialize(new CacheEnvelope
            {
                SavedAtUtc = DateTimeOffset.UtcNow,
                Json = json,
            });
            await File.WriteAllTextAsync(CachePath(key), payload, Encoding.UTF8);
        }
        catch
        {
            // best effort
        }
    }

    private static async Task<CacheEnvelope?> ReadCacheAsync(string key)
    {
        try
        {
            var path = CachePath(key);
            if (!File.Exists(path))
                return null;
            var payload = await File.ReadAllTextAsync(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<CacheEnvelope>(payload);
        }
        catch
        {
            return null;
        }
    }

    private void MarkFromCache(DateTimeOffset savedAtUtc)
    {
        LastResultWasCache = true;
        LastCacheAge = DateTimeOffset.UtcNow - savedAtUtc;
    }

    private async Task CaptureErrorAsync(HttpResponseMessage response)
    {
        try
        {
            LastHttpStatusCode = (int)response.StatusCode;

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                LastErrorCode = "rate_limited";
                var wait = response.Headers.RetryAfter?.Delta;
                LastErrorMessage = wait is null ? "HTTP 429" : $"HTTP 429 (attendre {Math.Ceiling(wait.Value.TotalSeconds)}s)";
                return;
            }

            var body = await response.Content.ReadAsStringAsync();

            ApiErrorResponse? apiErr = null;
            try
            {
                apiErr = JsonSerializer.Deserialize<ApiErrorResponse>(body);
            }
            catch
            {
                // ignore parse errors
            }

            LastErrorCode = apiErr?.Error?.Code;
            LastErrorMessage = apiErr?.Error?.Message;

            // Si l'API est en ENV=development, elle peut renvoyer `error.details`
            // Ex: {"exception":"ErrorException","message":"..."}
            if (apiErr?.Error is ApiError err && err.Details.ValueKind == JsonValueKind.Object)
            {
                string? exClass = null;
                string? exMsg = null;
                try
                {
                    if (err.Details.TryGetProperty("exception", out var exEl) && exEl.ValueKind == JsonValueKind.String)
                        exClass = exEl.GetString();
                    if (err.Details.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                        exMsg = msgEl.GetString();
                }
                catch
                {
                    // ignore
                }

                if (!string.IsNullOrWhiteSpace(exClass) || !string.IsNullOrWhiteSpace(exMsg))
                {
                    var suffix = "";
                    if (!string.IsNullOrWhiteSpace(exClass) && !string.IsNullOrWhiteSpace(exMsg))
                        suffix = $" — {exClass}: {exMsg}";
                    else if (!string.IsNullOrWhiteSpace(exClass))
                        suffix = $" — {exClass}";
                    else
                        suffix = $" — {exMsg}";

                    LastErrorMessage = (LastErrorMessage ?? $"HTTP {(int)response.StatusCode}") + suffix;
                }
            }

            // Fallback si on n'a pas réussi à parser l'erreur JSON
            if (string.IsNullOrWhiteSpace(LastErrorMessage))
            {
                var snippet = (body ?? string.Empty).Trim();
                if (snippet.Length > 200) snippet = snippet.Substring(0, 200) + "…";
                LastErrorCode = string.IsNullOrWhiteSpace(LastErrorCode) ? "http_error" : LastErrorCode;
                LastErrorMessage = snippet.Length > 0
                    ? $"HTTP {(int)response.StatusCode} — {snippet}"
                    : $"HTTP {(int)response.StatusCode}";
            }
        }
        catch
        {
            LastErrorCode = "http_error";
            LastErrorMessage = $"HTTP {(int)response.StatusCode}";
        }
    }

    // ===== AUTHENTIFICATION =====
    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            ResetLastError();
            var loginData = new
            {
                username,
                password
            };

            var loginJson = JsonSerializer.Serialize(loginData);

            var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/auth/login");
                request.Content = new StringContent(loginJson, Encoding.UTF8, "application/json");
                return request;
            }, maxAttempts: 2);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(jsonResponse);

                if (apiResponse?.Success == true)
                {
                    _accessToken = apiResponse.Data.Token.AccessToken;
                    await SetStoredStringAsync(AccessTokenKey, _accessToken);

                    // Stocker les infos utilisateur
                    var userJson = JsonSerializer.Serialize(apiResponse.Data.User);
                    await SetStoredStringAsync(UserInfoKey, userJson);

                    // Sécurité : verrouiller l'accès à certains comptes
                    if (!IsUserAllowed(apiResponse.Data.User))
                    {
                        await LogoutAsync();
                        return false;
                    }

                    return true;
                }
            }

            await CaptureErrorAsync(response);

            return false;
        }
        catch (Exception ex)
        {
            LastErrorCode = "exception";
            LastErrorMessage = ex.Message;
            return false;
        }
    }

    public async Task<bool> TestHealthAsync()
    {
        try
        {
            ResetLastError();
            var response = await GetWithRetryAsync($"{_baseUrl}/health", maxAttempts: 2);
            if (!response.IsSuccessStatusCode)
            {
                await CaptureErrorAsync(response);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LastErrorCode = "exception";
            LastErrorMessage = ex.Message;
            return false;
        }
    }

    // ===== DONNÉES PUBLIQUES (sans auth) =====
    public async Task<List<Actualite>> GetActualitesAsync(int limit = 30)
    {
        try
        {
            var response = await GetWithRetryAsync($"{_baseUrl}/public/actualites?limit={limit}");

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
            var url = $"{_baseUrl}/public/evenements?limit={limit}";
            if (!string.IsNullOrEmpty(from)) url += $"&from={from}";
            if (!string.IsNullOrEmpty(to)) url += $"&to={to}";

            var response = await GetWithRetryAsync(url);

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
            var response = await GetWithRetryAsync($"{_baseUrl}/admin/actualites?limit={limit}");

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

    // ===== FORMATIONS (admin) =====
    public async Task<List<Formation>> GetAdminFormationsAsync(int limit = 200)
    {
        try
        {
            ResetLastError();
            SetAuthHeader();
            var response = await GetWithRetryAsync($"{_baseUrl}/admin/formations?limit={limit}");
            if (!response.IsSuccessStatusCode)
            {
                await CaptureErrorAsync(response);
                var cached = await ReadCacheAsync("admin_formations");
                if (cached?.Json is string cj && cj.Length > 0)
                {
                    var apiCached = JsonSerializer.Deserialize<ApiResponse<FormationsListResponse>>(cj);
                    var itemsCached = apiCached?.Success == true ? (apiCached.Data.Items ?? new List<Formation>()) : new List<Formation>();
                    if (itemsCached.Count > 0)
                    {
                        MarkFromCache(cached.SavedAtUtc);
                        return itemsCached;
                    }
                }
                return new List<Formation>();
            }

            var json = await response.Content.ReadAsStringAsync();
            await WriteCacheAsync("admin_formations", json);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<FormationsListResponse>>(json);
            return apiResponse?.Success == true ? apiResponse.Data.Items : new List<Formation>();
        }
        catch (Exception ex)
        {
            LastErrorCode = "exception";
            LastErrorMessage = ex.Message;

            var cached = await ReadCacheAsync("admin_formations");
            if (cached?.Json is string cj && cj.Length > 0)
            {
                var apiCached = JsonSerializer.Deserialize<ApiResponse<FormationsListResponse>>(cj);
                var itemsCached = apiCached?.Success == true ? (apiCached.Data.Items ?? new List<Formation>()) : new List<Formation>();
                if (itemsCached.Count > 0)
                {
                    MarkFromCache(cached.SavedAtUtc);
                    return itemsCached;
                }
            }
            return new List<Formation>();
        }
    }

    // ===== RESTAURER LA SESSION =====
    public async Task<bool> RestoreSessionAsync()
    {
        try
        {
            _accessToken = await GetStoredStringAsync(AccessTokenKey);
            return !string.IsNullOrEmpty(_accessToken);
        }
        catch
        {
            _accessToken = null;
            return false;
        }
    }

    public static bool IsUserAllowed(UserInfo? user)
    {
        if (user is null)
            return false;

        var usernameOk = AllowedUsernames.Any(u => string.Equals(u, user.Username, StringComparison.OrdinalIgnoreCase));
        var roleOk = AllowedRoles.Any(r => string.Equals(r, user.Role, StringComparison.OrdinalIgnoreCase));
        return usernameOk || roleOk;
    }

    public async Task<bool> EnsureAuthorizedAsync()
    {
        var hasSession = await RestoreSessionAsync();
        if (!hasSession)
            return false;

        var user = await GetCurrentUserAsync();
        if (!IsUserAllowed(user))
        {
            await LogoutAsync();
            return false;
        }

        return true;
    }

    public async Task LogoutAsync()
    {
        _accessToken = null;
        await SetStoredStringAsync(AccessTokenKey, "");
        await SetStoredStringAsync(UserInfoKey, "");

        lock (_cacheLock)
        {
            _pscSessionsCache = null;
            _gqsSessionsCache = null;
        }
        _pscParticipantsCache.Clear();
        _gqsParticipantsCache.Clear();
        _rateLimitUntilUtc = null;
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        var userJson = await GetStoredStringAsync(UserInfoKey);
        if (!string.IsNullOrEmpty(userJson))
        {
            return JsonSerializer.Deserialize<UserInfo>(userJson);
        }
        return null;
    }

    private static async Task SetStoredStringAsync(string key, string value)
    {
        // SecureStorage peut échouer sur certaines configs Windows (non packagé).
        try
        {
            await SecureStorage.SetAsync(key, value);
        }
        catch
        {
            Preferences.Set(key, value);
        }
    }

    private static async Task<string?> GetStoredStringAsync(string key)
    {
        try
        {
            return await SecureStorage.GetAsync(key);
        }
        catch
        {
            return Preferences.Get(key, null);
        }
    }

    // ===== PSC (auth) =====
    public async Task<List<PscSession>> GetPscSessionsAsync(int limit = 200)
    {
        try
        {
            ResetLastError();

            lock (_cacheLock)
            {
                if (_pscSessionsCache is { } cache && (DateTimeOffset.UtcNow - cache.at) <= SessionsCacheTtl)
                {
                    return cache.items;
                }
            }

            SetAuthHeader();
            var response = await GetWithRetryAsync($"{_baseUrl}/psc/sessions?limit={limit}");
            if (!response.IsSuccessStatusCode)
            {
                await CaptureErrorAsync(response);
                var cached = await ReadCacheAsync("psc_sessions");
                if (cached?.Json is string cj && cj.Length > 0)
                {
                    var apiCached = JsonSerializer.Deserialize<ApiResponse<PscSessionsListResponse>>(cj);
                    var itemsCached = apiCached?.Success == true ? (apiCached.Data.Items ?? new List<PscSession>()) : new List<PscSession>();
                    if (itemsCached.Count > 0)
                    {
                        MarkFromCache(cached.SavedAtUtc);
                        return itemsCached;
                    }
                }
                return new List<PscSession>();
            }

            var json = await response.Content.ReadAsStringAsync();
            await WriteCacheAsync("psc_sessions", json);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<PscSessionsListResponse>>(json);
            var items = apiResponse?.Success == true ? (apiResponse.Data.Items ?? new List<PscSession>()) : new List<PscSession>();
            if (items.Count > 0)
            {
                lock (_cacheLock)
                {
                    _pscSessionsCache = (DateTimeOffset.UtcNow, items);
                }
            }
            return items;
        }
        catch (Exception ex)
        {
            LastErrorCode = "exception";
            LastErrorMessage = ex.Message;

            var cached = await ReadCacheAsync("psc_sessions");
            if (cached?.Json is string cj && cj.Length > 0)
            {
                var apiCached = JsonSerializer.Deserialize<ApiResponse<PscSessionsListResponse>>(cj);
                var itemsCached = apiCached?.Success == true ? (apiCached.Data.Items ?? new List<PscSession>()) : new List<PscSession>();
                if (itemsCached.Count > 0)
                {
                    MarkFromCache(cached.SavedAtUtc);
                    return itemsCached;
                }
            }
            return new List<PscSession>();
        }
    }

    public async Task<List<PscParticipant>> GetPscParticipantsAsync(int sessionId, bool includeExtracted = true)
    {
        try
        {
            ResetLastError();

            var cacheKey = sessionId + "|" + (includeExtracted ? "1" : "0");
            if (_pscParticipantsCache.TryGetValue(cacheKey, out var cache)
                && (DateTimeOffset.UtcNow - cache.at) <= ParticipantsCacheTtl)
            {
                return cache.items;
            }

            SetAuthHeader();
            var inc = includeExtracted ? "1" : "0";
            var response = await GetWithRetryAsync($"{_baseUrl}/psc/sessions/{sessionId}/participants?include_extracted={inc}");
            if (!response.IsSuccessStatusCode)
            {
                await CaptureErrorAsync(response);
                return new List<PscParticipant>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<PscParticipantsListResponse>>(json);
            var items = apiResponse?.Success == true ? (apiResponse.Data.Items ?? new List<PscParticipant>()) : new List<PscParticipant>();
            if (items.Count > 0)
            {
                _pscParticipantsCache[cacheKey] = (DateTimeOffset.UtcNow, items);
            }
            return items;
        }
        catch (Exception ex)
        {
            LastErrorCode = "exception";
            LastErrorMessage = ex.Message;
            return new List<PscParticipant>();
        }
    }

    public async Task<bool> SetPscAttendanceAsync(int participantId, string status, string? note)
    {
        try
        {
            ResetLastError();
            SetAuthHeader();
            var payload = new { status, note };
            var payloadJson = JsonSerializer.Serialize(payload);

            var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}/psc/participants/{participantId}/attendance");
                request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                return request;
            }, maxAttempts: 2);
            if (!response.IsSuccessStatusCode)
            {
                await CaptureErrorAsync(response);
                return false;
            }

            var body = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<PscAttendanceUpdateResult>>(body);
            return apiResponse?.Success == true;
        }
        catch
        {
            LastErrorCode = "exception";
            return false;
        }
    }

    // ===== GQS (auth) =====
    public async Task<List<GqsSession>> GetGqsSessionsAsync(int limit = 200)
    {
        try
        {
            ResetLastError();

            lock (_cacheLock)
            {
                if (_gqsSessionsCache is { } cache && (DateTimeOffset.UtcNow - cache.at) <= SessionsCacheTtl)
                {
                    return cache.items;
                }
            }

            SetAuthHeader();
            var response = await GetWithRetryAsync($"{_baseUrl}/gqs/sessions?limit={limit}");
            if (!response.IsSuccessStatusCode)
            {
                await CaptureErrorAsync(response);
                var cached = await ReadCacheAsync("gqs_sessions");
                if (cached?.Json is string cj && cj.Length > 0)
                {
                    var apiCached = JsonSerializer.Deserialize<ApiResponse<GqsSessionsListResponse>>(cj);
                    var itemsCached = apiCached?.Success == true ? (apiCached.Data.Items ?? new List<GqsSession>()) : new List<GqsSession>();
                    if (itemsCached.Count > 0)
                    {
                        MarkFromCache(cached.SavedAtUtc);
                        return itemsCached;
                    }
                }
                return new List<GqsSession>();
            }

            var json = await response.Content.ReadAsStringAsync();
            await WriteCacheAsync("gqs_sessions", json);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<GqsSessionsListResponse>>(json);
            var items = apiResponse?.Success == true ? (apiResponse.Data.Items ?? new List<GqsSession>()) : new List<GqsSession>();
            if (items.Count > 0)
            {
                lock (_cacheLock)
                {
                    _gqsSessionsCache = (DateTimeOffset.UtcNow, items);
                }
            }
            return items;
        }
        catch (Exception ex)
        {
            LastErrorCode = "exception";
            LastErrorMessage = ex.Message;

            var cached = await ReadCacheAsync("gqs_sessions");
            if (cached?.Json is string cj && cj.Length > 0)
            {
                var apiCached = JsonSerializer.Deserialize<ApiResponse<GqsSessionsListResponse>>(cj);
                var itemsCached = apiCached?.Success == true ? (apiCached.Data.Items ?? new List<GqsSession>()) : new List<GqsSession>();
                if (itemsCached.Count > 0)
                {
                    MarkFromCache(cached.SavedAtUtc);
                    return itemsCached;
                }
            }
            return new List<GqsSession>();
        }
    }

    private class CacheEnvelope
    {
        public DateTimeOffset SavedAtUtc { get; set; }
        public string Json { get; set; } = string.Empty;
    }

    public async Task<List<GqsParticipant>> GetGqsParticipantsAsync(int sessionId, bool includeExtracted = true)
    {
        try
        {
            ResetLastError();

            var cacheKey = sessionId + "|" + (includeExtracted ? "1" : "0");
            if (_gqsParticipantsCache.TryGetValue(cacheKey, out var cache)
                && (DateTimeOffset.UtcNow - cache.at) <= ParticipantsCacheTtl)
            {
                return cache.items;
            }

            SetAuthHeader();
            var inc = includeExtracted ? "1" : "0";
            var response = await GetWithRetryAsync($"{_baseUrl}/gqs/sessions/{sessionId}/participants?include_extracted={inc}");
            if (!response.IsSuccessStatusCode)
            {
                await CaptureErrorAsync(response);
                return new List<GqsParticipant>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<GqsParticipantsListResponse>>(json);
            var items = apiResponse?.Success == true ? (apiResponse.Data.Items ?? new List<GqsParticipant>()) : new List<GqsParticipant>();
            if (items.Count > 0)
            {
                _gqsParticipantsCache[cacheKey] = (DateTimeOffset.UtcNow, items);
            }
            return items;
        }
        catch (Exception ex)
        {
            LastErrorCode = "exception";
            LastErrorMessage = ex.Message;
            return new List<GqsParticipant>();
        }
    }

    private sealed class ApiErrorResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public ApiError? Error { get; set; }
    }

    private sealed class ApiError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("details")]
        public JsonElement Details { get; set; }
    }
}

// Classes supplémentaires pour les réponses listées
public class ActualitesListResponse
{
    [JsonPropertyName("items")]
    public List<Actualite> Items { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class Evenement
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("titre")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("date_event")]
    public string DateEvent { get; set; } = string.Empty;

    [JsonPropertyName("association_nom")]
    public string AssociationNom { get; set; } = string.Empty;
}

public class EvenementsListResponse
{
    [JsonPropertyName("items")]
    public List<Evenement> Items { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }
}