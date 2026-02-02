// Models/ApiResponse.cs
using System.Text.Json.Serialization;

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T Data { get; set; }

    [JsonPropertyName("error")]
    public ApiError Error { get; set; }
}

public class ApiError
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }
}

// Models/Actualite.cs
public class Actualite
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("titre")]
    public string Titre { get; set; }

    [JsonPropertyName("contenu")]
    public string Contenu { get; set; }

    [JsonPropertyName("date_publication")]
    public string DatePublication { get; set; }

    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; }
}

// Models/LoginResponse.cs
public class LoginResponse
{
    [JsonPropertyName("token")]
    public TokenInfo Token { get; set; }

    [JsonPropertyName("user")]
    public UserInfo User { get; set; }
}

public class TokenInfo
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; set; }
}

public class UserInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }
}