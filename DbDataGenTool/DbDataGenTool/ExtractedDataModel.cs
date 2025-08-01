using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DbDataGenTool;

public class ExtractedDataModel
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; }
    [JsonPropertyName("contactInformation")]
    public ContactInformation ContactInformation { get; set; }
    [JsonPropertyName("filingStatus")]
    public string FilingStatus { get; set; }

    [JsonPropertyName("incomeForms")]
    public List<IncomeForm> IncomeForms { get; set; }
    [JsonPropertyName("adjustedGrossIncome")]
    public int AdjustedGrossIncome { get; set; }

    [JsonPropertyName("signed7216")]
    public bool Signed7216 { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }
    [JsonPropertyName("taxType")]
    public string ProductSku { get; set; }
    [JsonPropertyName("hasPaid")]
    public bool HasPaid { get; set; }
}

public class ContactInformation
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; }
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string LastName { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("phone")]
    public string Phone { get; set; }
}

public class IncomeForm
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; }
    [JsonPropertyName("formType")]
    public string FormType { get; set; }

    [JsonPropertyName("incomeSource")]
    public string IncomeSource { get; set; }

    [JsonPropertyName("incomeAmount")]
    public double IncomeAmount { get; set; }
}