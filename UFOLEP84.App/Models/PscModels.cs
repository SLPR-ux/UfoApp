using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UFOLEP84.App.Models;

public class PscSession
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("date_session")] public string DateSession { get; set; } = "";
    [JsonPropertyName("formateur")] public string Formateur { get; set; } = "";
    [JsonPropertyName("places_max")] public int PlacesMax { get; set; }

    [JsonPropertyName("participants_count")] public int ParticipantsCount { get; set; }
    [JsonPropertyName("present_count")] public int PresentCount { get; set; }
    [JsonPropertyName("absent_count")] public int AbsentCount { get; set; }
    [JsonPropertyName("unknown_count")] public int UnknownCount { get; set; }
}

public class PscSessionsListResponse
{
    [JsonPropertyName("items")] public List<PscSession> Items { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
}

public class PscParticipant
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("session_id")] public int SessionId { get; set; }

    [JsonPropertyName("civilite")] public string Civilite { get; set; } = "";
    [JsonPropertyName("nom")] public string Nom { get; set; } = "";
    [JsonPropertyName("prenom")] public string Prenom { get; set; } = "";
    [JsonPropertyName("naissance")] public string? Naissance { get; set; }

    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("telephone")] public string? Telephone { get; set; }
    [JsonPropertyName("numero_licence")] public string? NumeroLicence { get; set; }

    [JsonPropertyName("prescripteur")] public string? Prescripteur { get; set; }
    [JsonPropertyName("nom_prescripteur")] public string? NomPrescripteur { get; set; }

    [JsonPropertyName("date_inscription")] public string? DateInscription { get; set; }
    [JsonPropertyName("extracted_at")] public string? ExtractedAt { get; set; }

    [JsonPropertyName("attendance_status")] public string AttendanceStatus { get; set; } = "unknown";
    [JsonPropertyName("attendance_note")] public string? AttendanceNote { get; set; }
    [JsonPropertyName("attendance_updated_at")] public string? AttendanceUpdatedAt { get; set; }
}

public class PscParticipantsListResponse
{
    [JsonPropertyName("items")] public List<PscParticipant> Items { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
}

public class PscAttendanceUpdateResult
{
    [JsonPropertyName("participant_id")] public int ParticipantId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "unknown";
    [JsonPropertyName("note")] public string? Note { get; set; }
}
