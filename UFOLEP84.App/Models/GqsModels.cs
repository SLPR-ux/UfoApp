using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UFOLEP84.App.Models;

public class GqsSession
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("date_session")] public string DateSession { get; set; } = "";
    [JsonPropertyName("heure_debut")] public string? HeureDebut { get; set; }
    [JsonPropertyName("heure_fin")] public string? HeureFin { get; set; }
    [JsonPropertyName("lieu")] public string? Lieu { get; set; }
    [JsonPropertyName("formateur")] public string Formateur { get; set; } = "";
    [JsonPropertyName("places_max")] public int PlacesMax { get; set; }
    [JsonPropertyName("is_published")] public int IsPublished { get; set; }

    [JsonPropertyName("participants_count")] public int ParticipantsCount { get; set; }
    [JsonPropertyName("places_restantes")] public int PlacesRestantes { get; set; }
}

public class GqsSessionsListResponse
{
    [JsonPropertyName("items")] public List<GqsSession> Items { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
}

public class GqsParticipant
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("session_id")] public int SessionId { get; set; }

    [JsonPropertyName("civilite")] public string? Civilite { get; set; }
    [JsonPropertyName("nom")] public string Nom { get; set; } = "";
    [JsonPropertyName("prenom")] public string Prenom { get; set; } = "";
    [JsonPropertyName("naissance")] public string? Naissance { get; set; }

    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("telephone")] public string? Telephone { get; set; }
    [JsonPropertyName("portable")] public string? Portable { get; set; }

    [JsonPropertyName("is_licencie_ufolep")] public int? IsLicencieUfolep { get; set; }
    [JsonPropertyName("numero_licence")] public string? NumeroLicence { get; set; }
    [JsonPropertyName("tarif_cents")] public int? TarifCents { get; set; }

    [JsonPropertyName("date_inscription")] public string? DateInscription { get; set; }
    [JsonPropertyName("extracted_at")] public string? ExtractedAt { get; set; }

    // Champs session (jointure)
    [JsonPropertyName("date_session")] public string? SessionDate { get; set; }
    [JsonPropertyName("formateur")] public string? SessionFormateur { get; set; }
}

public class GqsParticipantsListResponse
{
    [JsonPropertyName("items")] public List<GqsParticipant> Items { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
}
