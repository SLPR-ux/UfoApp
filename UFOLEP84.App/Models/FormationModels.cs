using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UFOLEP84.App.Models;

public class Formation
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("titre")] public string Titre { get; set; } = "";
    [JsonPropertyName("date_formation")] public string? DateFormation { get; set; }
    [JsonPropertyName("lieu")] public string? Lieu { get; set; }
    [JsonPropertyName("est_publie")] public bool EstPublie { get; set; }
}

public class FormationsListResponse
{
    [JsonPropertyName("items")] public List<Formation> Items { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
}
