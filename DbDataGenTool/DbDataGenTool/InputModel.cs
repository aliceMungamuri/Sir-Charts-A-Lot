using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DbDataGenTool;

public class InputModel
{
    [JsonPropertyName("UserID")]
    public string UserId { get; set; }

    [JsonPropertyName("SessionID")]
    public string SessionId { get; set; }

    [JsonPropertyName("SessionData")]
    public string SessionData { get; set; }

    [JsonPropertyName("UserData")]
    public string UserData { get; set; }

    [JsonPropertyName("TaxData")]
    public string TaxData { get; set; }

    [JsonPropertyName("HistoryData")]
    public string HistoryData { get; set; }

    [JsonPropertyName("CreatedOn")]
    public DateTimeOffset CreatedOn { get; set; }

    [JsonPropertyName("CreatedBY")]
    public string CreatedBy { get; set; }

    [JsonPropertyName("ModifiedOn")]
    public DateTimeOffset ModifiedOn { get; set; }

    [JsonPropertyName("ModifiedBY")]
    public string ModifiedBy { get; set; }

    [JsonPropertyName("ThrottleFlag")]
    public string ThrottleFlag { get; set; }

    [JsonPropertyName("SessionTerminated")]
    public string SessionTerminated { get; set; }

    [JsonPropertyName("LenSessionData")]
    public string LenSessionData { get; set; }

    [JsonPropertyName("LenUserData")]
    public string LenUserData { get; set; }

    [JsonPropertyName("LenTaxData")]
    public string LenTaxData { get; set; }

    [JsonPropertyName("LenHistoryData")]
    public string LenHistoryData { get; set; }

    [JsonPropertyName("NotesData")]
    public string NotesData { get; set; }

    [JsonPropertyName("LenNotesData")]
    public string LenNotesData { get; set; }

    [JsonPropertyName("TotalSessionTimeSecs")]
    public string TotalSessionTimeSecs { get; set; }
}