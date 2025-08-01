// See https://aka.ms/new-console-template for more information

using DbDataGenTool;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

// Add code to use UserSecrets as app configuration
IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
    .Build();
Console.WriteLine("Select Service from options: 'Extract', 'AddToDb', or 'fix'");
var reply = Console.ReadLine()?.Trim().ToLower();
switch (reply)
{
    case "extract":
        {
            Console.WriteLine("Extracting data...");
            var apiKey = config["AzureOpenAI:ApiKey"];
            var endpoint = config["AzureOpenAI:Endpoint"];
            var path = @"Input\Query 2 (1).json"; // adjust the path as needed
            await GenerateTablesFromTxo.GenerateOutputJson(apiKey, endpoint, path);

            Console.WriteLine("Data extraction completed.");
            break;
        }
    case "addtodb":
        {
            var connectionString = config["Db:ConnectionString"] ?? "Data Source=(localdb)\\ProjectModels;Database=OutputDb3";

            // Set up DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<OutputDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            // Read and deserialize OutputModels.Json
            var jsonPath = Path.Combine("Data", "OutputModelsFull.Json");
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"File not found: {jsonPath}");
                return;
            }
            var json = File.ReadAllText(jsonPath);
            var models = JsonSerializer.Deserialize<List<ExtractedDataModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (models == null)
            {
                Console.WriteLine("No models found in JSON.");
                return;
            }

            await using (var db = new OutputDbContext(optionsBuilder.Options))
            {
                db.Database.EnsureCreated();
                foreach (var model in models)
                {
                    model.ContactInformation.UserId = model.UserId; // Ensure ContactInformation has UserId set
                    if (model.IncomeForms?.Any() == true)
                    {
                        foreach (var form in model.IncomeForms)
                        {
                            form.UserId = model.UserId; // Ensure IncomeForm has UserId set
                        }
                    }
                    try
                    {
                        // Deduplicate IncomeForms for this model
                        if (model.IncomeForms != null)
                        {
                            model.IncomeForms = model.IncomeForms
                                .GroupBy(f => new { f.UserId, f.FormType, f.IncomeSource, f.IncomeAmount })
                                .Select(g => g.First())
                                .ToList();

                            // Add IncomeForms individually if not already tracked
                            foreach (var form in model.IncomeForms)
                            {
                                if (!db.IncomeForms.Any(f =>
                                        f.UserId == form.UserId && f.FormType == form.FormType &&
                                        f.IncomeSource == form.IncomeSource &&
                                        Math.Abs(f.IncomeAmount - form.IncomeAmount) < 0.001))
                                {
                                    db.IncomeForms.Add(form);
                                }
                            }
                        }

                        // Add ContactInformation if not already tracked
                        if (model.ContactInformation != null &&
                            !db.ContactInformation.Any(c => c.UserId == model.ContactInformation.UserId))
                        {
                            db.ContactInformation.Add(model.ContactInformation);
                        }

                        // Avoid duplicates if rerun
                        if (!db.ExtractedDataModels.Any(x => x.UserId == model.UserId))
                        {
                            // Set navigation properties to null to avoid EF Core tracking issues
                            var tempModel = new ExtractedDataModel
                            {
                                UserId = model.UserId,
                                FilingStatus = model.FilingStatus,
                                AdjustedGrossIncome = model.AdjustedGrossIncome,
                                Signed7216 = model.Signed7216,
                                Summary = model.Summary,
                                ProductSku = model.ProductSku,
                                HasPaid = model.HasPaid,
                                ContactInformation = null, // Navigation property
                                IncomeForms = null // Navigation property
                            };
                            db.ExtractedDataModels.Add(tempModel);
                        }
                        Console.WriteLine($"Model {model.UserId} added");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing model for UserId {model.UserId}: {ex.Message}");
                    }
                }
                db.SaveChanges();
            }
            Console.WriteLine($"Inserted {models.Count} records from OutputModels.Json into the database.");
            break;
        }
    case "fix":
        {
            var connectionString = config["Db:ConnectionString"] ?? "Data Source=(localdb)\\ProjectModels;Database=OutputDb3";
            var optionsBuilder = new DbContextOptionsBuilder<OutputDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            await using (var db = new OutputDbContext(optionsBuilder.Options))
            {
                var models = db.ExtractedDataModels.ToList();

                foreach (var model in models)
                {
                    // Remove spaces from FilingStatus
                    if (!string.IsNullOrEmpty(model.FilingStatus))
                    {
                        var status = model.FilingStatus.Replace(" ", "");

                        // Normalize to "MarriedFilingJointly" if needed
                        if (status.Contains("Joint", StringComparison.OrdinalIgnoreCase) ||
                            status.Equals("MarriedFilingJoint", StringComparison.OrdinalIgnoreCase))
                        {
                            status = "MarriedFilingJointly";
                        }

                        if (model.FilingStatus != status)
                        {
                            model.FilingStatus = status;
                            db.Entry(model).State = EntityState.Modified;
                        }
                    }
                }

                await db.SaveChangesAsync();
                Console.WriteLine("FilingStatus values have been normalized.");
            }
            break;
        }
    default:
        // Get connection string from user secrets or fallback

        Console.WriteLine("Goodbye, World!");
        break;
}