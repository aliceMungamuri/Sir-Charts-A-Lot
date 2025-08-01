using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SirChartsALot.Core.Models;

public class VisualizationData
{

    [Description(Examples)]
    public required ApexOptions ApexChartOptions { get; set; }
    

    public const string Examples = """
                                    Data is arranged based on chart type. Here are some examples:
                                    
                                    ### 1. LINE CHART (Time Series)
                                    For data with date/time columns showing trends:
                                    {"chart":{"type":"line","height":350,"toolbar":{"show":false}},"series":[{"name":"Revenue","data":[30000,40000,35000,50000,49000,60000,70000]}],"xaxis":{"categories":["Jan","Feb","Mar","Apr","May","Jun","Jul"],"title":{"text":"Month"}},"yaxis":{"title":{"text":"Revenue ($)"},"labels":{"formatter":"function(val) { return '$' + val.toLocaleString(); }"}},"title":{"text":"Monthly Revenue Trend","align":"center"},"stroke":{"curve":"smooth","width":3},"dataLabels":{"enabled":false}}
                                    
                                    ### 2. COLUMN/BAR CHART (Comparisons)
                                    For comparing values across categories:
                                    {"chart":{"type":"bar","height":350},"plotOptions":{"bar":{"horizontal":false,"columnWidth":"55%","endingShape":"rounded"}},"series":[{"name":"User Count","data":[44,55,57,56,61,58]}],"xaxis":{"categories":["Single","Married","Divorced","Widowed","Separated","Unknown"]},"title":{"text":"Users by Filing Status","align":"center"},"dataLabels":{"enabled":false}}
                                    
                                    ### 3. PIE/DONUT CHART (Parts of Whole)
                                    For showing proportions (max 10 slices):
                                    {"chart":{"type":"pie","height":350},"series":[44,55,13,43,22],"labels":["Team A","Team B","Team C","Team D","Team E"],"title":{"text":"Team Distribution","align":"center"},"legend":{"position":"bottom"},"dataLabels":{"enabled":true,"formatter":"function(val) { return val.toFixed(1) + '%' }"}}
                                    
                                    ### 4. AREA CHART (Cumulative Trends)
                                    For showing volume/accumulation over time:
                                    {"chart":{"type":"area","height":350,"stacked":false},"series":[{"name":"Total Users","data":[31,40,28,51,42,109,100]}],"xaxis":{"type":"datetime","categories":["2024-01-01","2024-02-01","2024-03-01","2024-04-01","2024-05-01","2024-06-01","2024-07-01"]},"stroke":{"curve":"smooth"},"fill":{"type":"gradient","gradient":{"shadeIntensity":1,"opacityFrom":0.7,"opacityTo":0.9}},"title":{"text":"User Growth Over Time","align":"center"}}
                                    
                                    ### 5. SCATTER PLOT (Correlations)
                                    For showing relationships between two numeric variables:
                                    {"chart":{"type":"scatter","height":350,"zoom":{"enabled":true,"type":"xy"}},"series":[{"name":"Income vs Tax","data":[[16.4,5.4],[21.7,2],[25.4,3],[19,2],[10.9,1],[13.6,3.2]]}],"xaxis":{"title":{"text":"Income (thousands)"},"tickAmount":10},"yaxis":{"title":{"text":"Tax Paid (thousands)"},"tickAmount":7},"title":{"text":"Income vs Tax Correlation","align":"center"}}
                                    
                                    ### 6. HEATMAP (Matrix Data)
                                    For dense numerical data in grid format:
                                    {"chart":{"type":"heatmap","height":350},"series":[{"name":"Q1","data":[{"x":"W1","y":22},{"x":"W2","y":29},{"x":"W3","y":13}]},{"name":"Q2","data":[{"x":"W1","y":43},{"x":"W2","y":43},{"x":"W3","y":33}]}],"dataLabels":{"enabled":false},"colors":["#008FFB"],"title":{"text":"Weekly Performance Heatmap"}}
                                    
                                    ### 7. RADAR CHART (Multi-dimensional Comparison)
                                    For comparing multiple attributes:
                                    {"chart":{"type":"radar","height":350},"series":[{"name":"Product A","data":[80,50,30,40,100,20]}],"xaxis":{"categories":["Speed","Reliability","Comfort","Safety","Efficiency","Cost"]},"title":{"text":"Product Comparison","align":"center"}}
                                    
                                    ### 8. MULTIPLE SERIES (Stacked/Grouped)
                                    For comparing multiple data series:
                                    {"chart":{"type":"bar","height":350,"stacked":true},"series":[{"name":"Product A","data":[44,55,41,67,22]},{"name":"Product B","data":[13,23,20,8,13]},{"name":"Product C","data":[11,17,15,15,21]}],"xaxis":{"categories":["Q1","Q2","Q3","Q4","Q5"]},"title":{"text":"Quarterly Sales by Product","align":"center"},"legend":{"position":"right"}}
                                    
                                            
                                    """;

}
public class ApexOptions
{
    private const string Examples = """
                                    Series type depends on chart type. Here are some examples:
                                    **Bar Chart Single**
                                    {
                                      "series": [
                                        {
                                          "data": [
                                            {
                                              "x": "category A",
                                              "y": 10
                                            },
                                            {
                                              "x": "category B",
                                              "y": 18
                                            },
                                            {
                                              "x": "category C",
                                              "y": 13
                                            }
                                          ]
                                        }
                                      ]
                                    }
                                    **Bar Chart Multiple**
                                    {"series":[{"name":"OPP","data":[116,62,178,40,19,138]},{"name":"TCD","data":[5,30,84,31,18,90]},{"name":"TCF","data":[15,0,6,0,0,21]},{"name":"TCL","data":[1,0,2,0,0,6]},{"name":"SEP","data":[0,36,72,15,9,43]},{"name":"OAP","data":[0,0,0,13,0,0]}]}
                                    
                                    **Pie Chart**
                                    {
                                      "series": [10, 18, 13]
                                    }
                                    """;
    [JsonPropertyName("chart")]
    public required Chart Chart { get; set; }

    [JsonPropertyName("dataLabels")]
    public required DataLabels DataLabels { get; set; }

    [JsonPropertyName("title")]
    public required Title Title { get; set; }

    [JsonPropertyName("grid")]
    public required Grid Grid { get; set; }

    [JsonPropertyName("series")]
    [Description("series data as a json object")]
    public required string Series { get; set; }

    [JsonPropertyName("labels")]
    public required List<string> Labels { get; set; }

    [JsonPropertyName("legend")]
    public required Legend Legend { get; set; }

    [JsonPropertyName("xaxis")]
    public XAxis? XAxis { get; set; }

    [JsonPropertyName("yaxis")]
    public YAxis? YAxis { get; set; }

    public object? GetSeriesObject()
    {
        if (string.IsNullOrWhiteSpace(Series)) return null;
        return JsonSerializer.Deserialize<object>(Series);
    }
}

public class Chart
{
    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("animations")]
    public Animations? Animations { get; set; }

    [JsonPropertyName("toolbar")]
    public Toolbar? Toolbar { get; set; }

    [JsonPropertyName("zoom")]
    public DataLabels? Zoom { get; set; }

    [JsonPropertyName("selection")]
    public DataLabels? Selection { get; set; }
}

public class Animations
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("easing")]
    public string Easing { get; set; } = "linear";

    [JsonPropertyName("speed")]
    public int Speed { get; set; }

    //[JsonPropertyName("animateGradually")]
    //public AnimateGradually AnimateGradually { get; set; }
}

public class AnimateGradually
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("delay")]
    public int Delay { get; set; }
}

public class DataLabels
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

public class Toolbar
{
    [JsonPropertyName("show")]
    public bool Show { get; set; }
}

public class Grid
{
    [JsonPropertyName("borderColor")]
    public string BorderColor { get; set; } = "#e7e7e7";

    [JsonPropertyName("strokeDashArray")]
    public int StrokeDashArray { get; set; }
}

public class Legend
{
    [JsonPropertyName("position")]
    public string Position { get; set; } = "bottom";
}

public class Title
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("align")]
    public string Align { get; set; } = "center";

    [JsonPropertyName("style")]
    public Style? Style { get; set; }
}

public class Style
{
    [JsonPropertyName("fontSize")]
    public string FontSize { get; set; } = "16px";

    [JsonPropertyName("fontWeight")]
    public int FontWeight { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#263238";
}

public class XAxis
{
    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }

    [JsonPropertyName("title")]
    public AxisTitle? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class YAxis
{
    [JsonPropertyName("title")]
    public AxisTitle? Title { get; set; }

    [JsonPropertyName("min")]
    public double? Min { get; set; }

    [JsonPropertyName("max")]
    public double? Max { get; set; }
}

public class AxisTitle
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}