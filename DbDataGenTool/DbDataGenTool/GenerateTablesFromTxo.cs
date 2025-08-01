using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace DbDataGenTool;

internal class GenerateTablesFromTxo
{
    public static string DecryptAndExtract(string base64Gzipped)
    {
        // 1. Decode base64
        var gzippedData = Convert.FromBase64String(base64Gzipped);

        // 2. Decompress gzip
        using var compressedStream = new MemoryStream(gzippedData);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream, Encoding.UTF8);
        var result = reader.ReadToEnd();
        // 3. Return the decompressed string
        return result;

    }

    public static async Task GenerateOutputJson(string apiKey, string endpoint, string filePath)
    {
        var inputModels = JsonSerializer.Deserialize<List<InputModel>>(File.ReadAllText(filePath));
        var inputChunks = SplitList(inputModels.ToList(), 3);
        var kernel = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion("gpt-4.1", endpoint, apiKey).Build();
        List<ExtractedDataModel> outputModels = [];
        foreach (var inputChunk in inputChunks)
        {
            try
            {
                var tasks = inputChunk.Select(inputModel => OutputGenTask(kernel, inputModel)).ToList();
                var results = await Task.WhenAll(tasks);
                outputModels.AddRange(results);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing chunk: {ex.Message}");
            }
            Console.WriteLine($"{outputModels.Count}/{inputModels.Count}");
            if (outputModels.Count >= 100) break;
        }
        // Change to your desired output directory
        var outputDir = @"C:\Users\A876302\source\repos\CacheMeIfYouCan\DbDataGenTool\DbDataGenTool\Data\";
       
        File.WriteAllText(Path.Combine(outputDir,"OutputModelsFull.Json"), JsonSerializer.Serialize(outputModels, new JsonSerializerOptions() {WriteIndented = true}));
    }

    private static async Task<ExtractedDataModel> OutputGenTask(Kernel kernel, InputModel inputModel)
    {
        Console.WriteLine($"Starting Data Extraction for user: {inputModel.UserId}");
        var settings = new AzureOpenAIPromptExecutionSettings() { ResponseFormat = typeof(ExtractedDataModel) };
        var tom = DecryptAndExtract(inputModel.TaxData);
        var userData = DecryptAndExtract(inputModel.UserData);
        var args = new KernelArguments(settings) { ["tom"] = tom, ["userData"] = userData };
        var response = await kernel.InvokePromptAsync<string>(Prompt, args);
        var outputModel = JsonSerializer.Deserialize<ExtractedDataModel>(response);
        //outputModel.UserId = inputModel.UserId;
        Console.WriteLine($"Data Extraction Completed for user: {inputModel.UserId}");
        return outputModel;
    }
    // Method to split a list into smaller chunks
    public static List<List<T>> SplitList<T>(List<T> list, int chunkSize)
    {
        var chunks = new List<List<T>>();
        for (int i = 0; i < list.Count; i += chunkSize)
        {
            chunks.Add(list.Skip(i).Take(chunkSize).ToList());
        }
        return chunks;
    }

    private const string Prompt = """
                                  ## Instructions

                                  You are a Tax Object Model and User Data Analyzer. Extract relevant data from a tax object model json object and user data xml. 

                                  ### Relevant Data from Tax Object Model:

                                  - User ID (TaxReturnGuid)
                                  
                                  - Filer Contact Information

                                  - Filer Income Forms. Details are income amount, Income source.

                                  1. W-2s
                                  2. K1s
                                  3. Schedule C
                                  4. 1099-nec/int/div/misc

                                  - Whether the user signed 7162.
                                  - AGI (Adjusted Gross Income)
                                  - 1 paragraph summary of the user details

                                  ### Relevant Data from User Data XML:
                                  <HASPAIDFORPRODUCT>1</HASPAIDFORPRODUCT> mean user has paid for the product, otherwise 0.
                                  TAXTYPE="TCD" is the tax type or product sku.
                                  
                                  ## Output Format

                                  Output a json object using the provided schema.
                                  
                                  ## Input Tax Object Model
                                  
                                  ```
                                  {{ $tom }}
                                  ```
                                  
                                  ## Input User Data XML
                                  ```xml
                                  {{ $userData }}
                                  ```
                                  """;
}