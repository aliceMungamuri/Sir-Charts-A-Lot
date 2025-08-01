namespace SirChartsALot.Core.Agents;

/// <summary>
/// ApexCharts configuration examples for the AI visualization agent
/// </summary>
public static class ChartExamples
{
    public const string Examples = """
        ## ApexCharts Configuration Examples (Structure Only - Data Will Be Populated by System):
        
        ### 1. LINE CHART (Time Series)
        For data with date/time columns showing trends:
        ```json
        {
          "chartType": "Line",
          "apexChartOptions": {
            "chart": { "type": "line", "height": 350, "toolbar": { "show": false } },
            "series": null,
            "xaxis": {
              "title": { "text": "Date" }
            },
            "yaxis": {
              "title": { "text": "Value" },
              "labels": { "formatter": "function(val) { return val.toLocaleString(); }" }
            },
            "title": { "text": "Trend Over Time", "align": "center" },
            "stroke": { "curve": "smooth", "width": 3 },
            "dataLabels": { "enabled": false }
          }
        }
        ```
        
        ### 2. COLUMN/BAR CHART (Comparisons)
        For comparing values across categories:
        ```json
        {
          "chartType": "Column",
          "apexChartOptions": {
            "chart": { "type": "bar", "height": 350 },
            "plotOptions": {
              "bar": { "horizontal": false, "columnWidth": "55%", "endingShape": "rounded" }
            },
            "series": null,
            "xaxis": null,
            "title": { "text": "Category Comparison", "align": "center" },
            "dataLabels": { "enabled": false }
          }
        }
        ```
        
        ### 3. PIE/DONUT CHART (Parts of Whole)
        For showing proportions (max 10 slices):
        ```json
        {
          "chartType": "Pie",
          "apexChartOptions": {
            "chart": { "type": "pie", "height": 350 },
            "series": null,
            "labels": null,
            "title": { "text": "Distribution", "align": "center" },
            "legend": { "position": "bottom" },
            "dataLabels": {
              "enabled": true,
              "formatter": "function(val) { return val.toFixed(1) + '%' }"
            }
          }
        }
        ```
        
        ### 4. AREA CHART (Cumulative Trends)
        For showing volume/accumulation over time:
        ```json
        {
          "chartType": "Area",
          "apexChartOptions": {
            "chart": { "type": "area", "height": 350, "stacked": false },
            "series": null,
            "xaxis": { "type": "datetime" },
            "stroke": { "curve": "smooth" },
            "fill": { "type": "gradient", "gradient": { "shadeIntensity": 1, "opacityFrom": 0.7, "opacityTo": 0.9 } },
            "title": { "text": "Cumulative Growth", "align": "center" }
          }
        }
        ```
        
        ### 5. SCATTER PLOT (Correlations)
        For showing relationships between two numeric variables:
        ```json
        {
          "chartType": "Scatter",
          "apexChartOptions": {
            "chart": { "type": "scatter", "height": 350, "zoom": { "enabled": true, "type": "xy" } },
            "series": null,
            "xaxis": { "title": { "text": "X Value" }, "tickAmount": 10 },
            "yaxis": { "title": { "text": "Y Value" }, "tickAmount": 7 },
            "title": { "text": "Correlation Analysis", "align": "center" }
          }
        }
        ```
        
        ### 6. HEATMAP (Matrix Data)
        For dense numerical data in grid format:
        ```json
        {
          "chartType": "Heatmap",
          "apexChartOptions": {
            "chart": { "type": "heatmap", "height": 350 },
            "series": null,
            "dataLabels": { "enabled": false },
            "colors": ["#008FFB"],
            "title": { "text": "Data Heatmap" }
          }
        }
        ```
        
        ### 7. RADAR CHART (Multi-dimensional Comparison)
        For comparing multiple attributes:
        ```json
        {
          "chartType": "Radar",
          "apexChartOptions": {
            "chart": { "type": "radar", "height": 350 },
            "series": null,
            "xaxis": null,
            "title": { "text": "Multi-Attribute Comparison", "align": "center" }
          }
        }
        ```
        
        ### 8. MULTIPLE SERIES (Stacked/Grouped)
        For comparing multiple data series:
        ```json
        {
          "chartType": "Column",
          "apexChartOptions": {
            "chart": { "type": "bar", "height": 350, "stacked": true },
            "series": null,
            "xaxis": null,
            "title": { "text": "Stacked Comparison", "align": "center" },
            "legend": { "position": "right" }
          }
        }
        ```
        
        ## Important Guidelines:
        1. Always set chart.height to 350 for consistency
        2. Include descriptive titles that explain what the chart shows
        3. Format numbers appropriately (currency, percentages, etc.)
        4. Use categories for discrete x-axis values
        5. Use datetime type for time-based x-axis
        6. Disable dataLabels for cleaner look unless specifically needed
        7. NEVER include actual data values - set series and labels to null
        8. The system will populate all data arrays automatically
        7. Position legends appropriately (bottom for pie, right for multi-series)
        8. For horizontal bars, set plotOptions.bar.horizontal = true
        
        ## TEXT Response Examples:
        
        ### Single Value Response:
        ```json
        {
          "responseType": "Text",
          "textConfig": {
            "content": "1234567.89",
            "formatType": "Currency",
            "isSingleValue": true,
            "singleValueMetadata": {
              "numericValue": 1234567.89,
              "unit": "USD",
              "comparison": {
                "previousValue": 1000000,
                "changeAmount": 234567.89,
                "changePercentage": 23.46,
                "direction": "up"
              }
            },
            "useMarkdown": false
          }
        }
        ```
        
        ### Summary Response:
        ```json
        {
          "responseType": "Text",
          "textConfig": {
            "content": "## Revenue Analysis\n\nTotal revenue for Q4 2023 was **$1,234,567**, representing a **23.5% increase** from Q3.",
            "formatType": "Summary",
            "isSingleValue": false,
            "useMarkdown": true,
            "highlights": [
              "Revenue increased by 23.5% quarter-over-quarter",
              "December was the strongest month with $456,789",
              "Product A contributed 45% of total revenue"
            ]
          }
        }
        ```
        
        ## TABLE Response Example:
        ```json
        {
          "responseType": "Table",
          "tableConfig": {
            "columns": [
              {
                "key": "userName",
                "displayName": "User Name",
                "dataType": "String",
                "sortable": true,
                "width": "200px"
              },
              {
                "key": "filingStatus",
                "displayName": "Filing Status",
                "dataType": "String",
                "sortable": true
              },
              {
                "key": "income",
                "displayName": "Income",
                "dataType": "Currency",
                "sortable": true,
                "format": "currency"
              },
              {
                "key": "taxPaid",
                "displayName": "Tax Paid",
                "dataType": "Currency",
                "sortable": true,
                "format": "currency"
              }
            ],
            "enablePagination": true,
            "pageSize": 20,
            "enableSorting": true,
            "enableFiltering": true,
            "enableExport": true
          }
        }
        ```
        """;
}