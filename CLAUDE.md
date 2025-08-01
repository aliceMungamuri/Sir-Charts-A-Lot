# Sir Charts-a-lot: NL2SQL Interactive Data Visualization Platform
## Product Requirements Document & Implementation Plan

### Executive Summary
Sir Charts-a-lot is a real-time natural language to SQL query and visualization platform built for a 24-hour hackathon. It leverages Azure OpenAI, Semantic Kernel agents, and modern web technologies to enable business users to query databases using natural language and receive instant, intelligent visualizations.

### Core Value Proposition
- **Instant Insights**: Convert natural language questions to SQL queries with real-time chart visualizations
- **Intelligent Visualization**: Auto-select appropriate charts based on query results
- **Enterprise Ready**: Built on trusted .NET 9 and Angular 19 stack with Azure services

### Technical Stack
- **Backend**: .NET 9 with SignalR for real-time communication
- **Frontend**: Angular 19 with ng-apexcharts for visualizations
- **Database**: Azure SQL Database
- **AI/ML**: Azure OpenAI (GPT-4.1, O3, O4-mini) with Semantic Kernel
- **Styling**: Tailwind CSS (CDN)
  For the Angular app, we'll just add this to index.html:
  <script src="https://cdn.tailwindcss.com"></script>
- **Caching**: In-memory cache for schema and query results

### System Architecture

#### 1. Two-Agent Collaborative System (Based on Microsoft's Proven Approach)

##### 1.1 Agent 1: Domain Expert (Schema Navigator)
- **Purpose**: High-level understanding of database schema and table relationships
- **Technology**: Semantic Kernel with Azure OpenAI (O3 for complex reasoning)
- **Responsibilities**:
  - Analyze natural language query intent
  - Identify relevant tables (max 5 per query)
  - Provide initial problem decomposition
  - Pass minimal, targeted schema context to SQL Expert
  - Break complex queries into manageable steps

##### 1.2 Agent 2: SQL Query Expert
- **Purpose**: Generate precise T-SQL queries based on Domain Expert's guidance
- **Technology**: Semantic Kernel with Azure OpenAI (GPT-4.1)
- **Responsibilities**:
  - Receive focused table schema from Domain Expert
  - Generate exact SQL query with proper joins
  - Ensure parameterization for security
  - Minimize explanations, focus on query accuracy
  - Handle edge cases with explicit error messages

##### 1.3 Enhanced Visualization Agent
- **Purpose**: Intelligently determine optimal visualization type using AI
- **Technology**: Semantic Kernel with Azure OpenAI (O4-mini) using structured output
- **Responsibilities**:
  - Analyze user query intent AND result data characteristics
  - Determine response type: Chart, Table, Text, or Mixed
  - Generate complete ApexCharts configurations for charts
  - Define table column properties and formatting
  - Format single values and text summaries appropriately
  - Provide confidence scores and reasoning for selections

#### Agent Collaboration Pattern
```
Natural Language Query
        ↓
[Domain Expert Agent] - Identifies tables, decomposes problem
        ↓
[SQL Query Expert] - Generates precise SQL
        ↓
[Query Execution]
        ↓
[Enhanced Visualization Agent] - AI-driven visualization selection
        ↓
User receives appropriate response (Chart/Table/Text/Mixed)
```

### Unified Visualization Response System

#### Response Types
The Enhanced Visualization Agent determines the optimal response type based on user intent and data characteristics:

1. **Chart Response**
   - For trends, comparisons, distributions, and correlations
   - Supports all ApexCharts types (Line, Area, Column, Bar, Pie, Donut, Scatter, Heatmap, etc.)
   - AI generates complete ApexCharts configuration with proper formatting

2. **Table Response**
   - For detailed records, multi-column data, or "list all" queries
   - Includes sorting, filtering, pagination, and export capabilities
   - Column-specific formatting (currency, dates, percentages)

3. **Text Response**
   - For single values (counts, sums, averages)
   - Includes comparisons with previous periods
   - Formatted with appropriate units and indicators

4. **Mixed Response**
   - Combines text summary with supporting visualizations
   - For complex queries requiring both overview and details

#### AI-Driven Selection Process
```
User Query + SQL Results
        ↓
Enhanced Visualization Agent analyzes:
- Query intent (what user asked for)
- Data structure (columns, rows, types)
- Data characteristics (time series, categories, single value)
        ↓
Selects optimal response type with confidence score
        ↓
Generates appropriate configuration (ApexCharts/Table/Text)
```

### Communication Architecture

#### SignalR Hub Design
```csharp
public interface IChartHub
{
    // Client → Server
    Task SubmitQuery(string query, string sessionId);
    Task RequestFollowUp(string queryId);
    Task RateResponse(string queryId, int rating);
    
    // Server → Client
    Task OnThinkingUpdate(ThinkingUpdate update);
    Task OnSQLGenerated(SQLGeneratedMessage sql);
    Task OnDataStreaming(DataStreamMessage data);
    Task OnVisualizationReady(VizResponseMessage viz);
    Task OnError(ErrorMessage error);
    Task OnSuggestions(SuggestionsMessage suggestions);
}
```

#### Message Types
```csharp
public record ThinkingUpdate(
    string Stage, // "analyzing_schema" | "generating_sql" | "executing_query" | "creating_visualization"
    string Message,
    int ProgressPercentage
);

public record SQLGeneratedMessage(
    string Query,
    string[] TablesUsed,
    string[] ColumnsSelected,
    bool IsParameterized
);

public record DataStreamMessage(
    object[] Rows,
    string[] Columns,
    bool IsPartial,
    int TotalRowCount
);

// Enhanced message types for unified visualization
public record UnifiedVisualizationMessage(
    string ResponseType, // "Chart" | "Table" | "Text" | "Mixed"
    string Title,
    double Confidence,
    string SelectionReasoning,
    string SerializedConfig, // JSON config based on ResponseType
    string[] Columns,
    int TotalRowCount
);

public record TableDataMessage(
    object[] Rows,
    TableColumn[] ColumnDefinitions,
    bool IsComplete,
    int TotalRowCount,
    int CurrentPage,
    int PageSize
);

public record TextResponseMessage(
    string Content,
    string FormatType, // "Plain" | "Number" | "Currency" | "Percentage" | "Markdown"
    bool IsSingleValue,
    object Metadata // Contains comparisons, units, etc.
);
```

### Security Implementation

#### SQL Injection Prevention
1. **Parameterized Queries Only**: All SQL generation must use parameters
2. **Schema Validation**: Validate table/column names against cached schema
3. **Query Whitelisting**: Only SELECT statements allowed
4. **Row Limits**: Automatic TOP 1000 clause on all queries
5. **Timeout Protection**: 30-second query timeout
6. **Prompt Injection Prevention**: 
   - Sanitize user input before passing to agents
   - Validate agent outputs against expected formats
   - Never execute raw LLM output without validation

#### Authentication
- SQL authentication via connection string in appsettings.json
- Consider implementing API key authentication for production
- Future: Implement Row-Level Security for multi-tenant scenarios

### Iterative Refinement Process

When initial query fails or returns unexpected results:

1. **Domain Expert Re-evaluation**
   - Analyze error message
   - Consider alternative table interpretations
   - Break down query into simpler components

2. **SQL Expert Retry**
   - Receive refined context from Domain Expert
   - Generate alternative query approach
   - Provide specific error feedback

3. **User Feedback Loop**
   - Present simplified query options
   - Suggest clarifications
   - Offer example queries for similar intents

### Frontend Architecture

#### Angular Components Structure
```
src/
├── app/
│   ├── components/
│   │   ├── chat/                    # Main chat interface
│   │   ├── enhanced-chat/           # Enhanced chat with unified visualization
│   │   ├── data-table/              # Table visualization component
│   │   ├── text-response/           # Text/single value display
│   │   ├── timeline/                # Query processing timeline
│   │   ├── home/                    # Landing page with examples
│   │   └── dashboard/               # Future: pinned visualizations
│   ├── services/
│   │   └── signalr.service.ts      # Real-time communication
│   └── models/
│       ├── messages.ts              # Standard message types
│       └── unified-visualization.ts # Enhanced visualization models
```

#### Key Frontend Features
- **Simple HTML tables with Tailwind CSS** (no Angular Material)
- **ApexCharts for all chart visualizations**
- **Real-time updates via SignalR**
- **Responsive design with Tailwind utility classes**

### Backend Architecture

#### Project Structure
```
SirChartsALot/
├── SirChartsALot.Api/
│   ├── Hubs/
│   │   └── ChartHub.cs
│   ├── Controllers/
│   └── Program.cs
├── SirChartsALot.Core/
│   ├── Agents/
│   │   ├── DomainExpertAgent.cs
│   │   ├── SQLQueryExpertAgent.cs
│   │   └── VisualizationAgent.cs
│   ├── Services/
│   │   ├── SchemaIntrospectionService.cs
│   │   └── SQLExecutionService.cs
│   └── Models/
└── SirChartsALot.Infrastructure/
    ├── Data/
    └── Configuration/
```

#### Schema Introspection Strategy
```csharp
public class SchemaCache
{
    public Dictionary<string, TableInfo> Tables { get; set; }
    public Dictionary<string, RelationshipInfo> Relationships { get; set; }
    public DateTime LastRefreshed { get; set; }
}

public class TableInfo
{
    public string TableName { get; set; }
    public string Description { get; set; } // Brief business context
    public List<ColumnInfo> Columns { get; set; }
    public List<string> SampleValues { get; set; } // Top 5 distinct values
}

public class ColumnInfo
{
    public string ColumnName { get; set; }
    public string DataType { get; set; }
    public string Description { get; set; } // e.g., "Primary key for Customer records"
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string ReferencedTable { get; set; }
}
```

### Agent Prompt Templates

#### Domain Expert Agent Prompt
```
You are a database schema expert helping users query their data. Your role is to:

1. Understand the user's natural language query
2. Identify which tables (maximum 5) are relevant
3. Provide a brief analysis of what the user wants

Available tables in the database:
{HighLevelSchemaDescription}

For the user query: "{UserQuery}"

Respond with:
- Intent: [What the user wants to achieve]
- Required Tables: [List of table names]
- Key Relationships: [How tables connect]
- Complexity: [Simple/Medium/Complex]

Be concise. Focus only on table selection and problem understanding.
```

#### SQL Query Expert Agent Prompt
```
You are a T-SQL expert. Generate ONLY the SQL query based on this information:

User Intent: {IntentFromDomainExpert}
Tables to use: {SelectedTables}

Table Schemas:
{DetailedSchemaForSelectedTables}

Rules:
1. Generate ONLY valid T-SQL
2. Use proper JOIN syntax
3. Include appropriate WHERE clauses
4. Add TOP 1000 to prevent large result sets
5. Use clear column aliases
6. NO explanations - just the query

If the query cannot be generated, respond with:
ERROR: [Specific reason]
```

#### Enhanced Visualization Agent Prompt
```
You are an expert data visualization analyst. Analyze the user's query intent and the resulting data to determine the optimal visualization approach.

User Query: {UserQuery}
SQL Query: {SqlQuery}
Column Names: {Columns}
Sample Data: {SampleData}
Total Row Count: {TotalRowCount}

Determine the best response type:
- TEXT: For single values, counts, or summaries
- TABLE: For detailed records or multi-column data
- CHART: For trends, comparisons, or distributions
- MIXED: When both summary and visualization add value

For CHART responses, select from:
- Line/Area: Time series data
- Column/Bar: Category comparisons
- Pie/Donut: Parts of a whole (≤10 items)
- Scatter: Correlations
- Heatmap: Dense numerical patterns
- Others as appropriate

Generate the complete configuration following the UnifiedVisualizationResponse schema with proper ApexCharts options, table definitions, or text formatting.
```

### Query Result Caching

#### Cache Structure
```csharp
public class QueryCache
{
    public string QueryHash { get; set; } // Hash of normalized query
    public string OriginalQuery { get; set; }
    public string GeneratedSQL { get; set; }
    public object[] Results { get; set; }
    public UnifiedVisualizationResponse VisualizationResponse { get; set; } // Complete visualization config
    public string[] TablesUsed { get; set; }
    public int ExecutionTimeMs { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

### Step-by-Step Implementation Plan

#### Phase 1: Foundation (Hours 0-4)
1. **Project Setup**
   - Create .NET 9 solution with projects
   - Create Angular 19 app with ng-apexcharts
   - Configure Azure OpenAI credentials
   - Set up Azure SQL connection

2. **Database Schema**
   - Create sample database 
   - Build schema introspection service
   - Cache schema metadata

3. **Basic SignalR Setup**
   - Implement ChartHub
   - Configure CORS
   - Test basic client-server communication

#### Phase 2: Two-Agent NL2SQL System (Hours 4-8)
1. **Domain Expert Agent**
   - Configure Semantic Kernel with O3 model
   - Implement high-level schema loading
   - Create table identification logic
   - Build intent analysis system

2. **SQL Query Expert Agent**
   - Configure with GPT-4.1 model
   - Implement focused prompt generation
   - Create SQL validation logic
   - Ensure parameterization

3. **Agent Collaboration Pipeline**
   - Domain Expert → SQL Expert communication
   - Minimal context passing (only selected tables)
   - Error handling and retry logic
   - Query execution service integration

#### Phase 3: Enhanced Visualization Engine (Hours 8-12)
1. **AI-Driven Visualization Selection**
   - Enhanced Visualization Agent with structured output
   - Support for Chart/Table/Text/Mixed responses
   - Confidence scoring and reasoning

2. **Frontend Components**
   - Enhanced chat component for unified responses
   - Simple HTML table with Tailwind (no Angular Material)
   - Text response component for single values
   - ApexCharts integration for all chart types

3. **Real-time Updates**
   - Table data streaming with pagination
   - Progressive chart rendering
   - Loading states and animations

#### Phase 4: Meta-Cognition Agent (Hours 12-16)
1. **Memory Service**
   - In-memory cache setup
   - Interaction storage
   - Pattern recognition

2. **Context Enhancement**
   - Query augmentation with history
   - Follow-up suggestions
   - Learning from ratings

3. **Integration with NL2SQL**
   - Context injection
   - Improved query generation

#### Phase 5: UI/UX Polish (Hours 16-20)
1. **Query Interface**
   - Natural language input with suggestions
   - Voice input (optional)
   - Query history

2. **Thinking Animations**
   - Stage-based progress indicators
   - Smooth transitions
   - Engaging loading states

3. **Chart Interactions**
   - Hover details
   - Zoom/pan capabilities
   - Export options

4. **Dashboard Feature** (optional, later)
   - Pin charts
   - Layout management
   - Refresh capabilities

### Performance Optimization

1. **Divide-and-Conquer Strategy**
   - Domain Expert focuses ONLY on table selection (reduces token usage)
   - SQL Expert receives ONLY relevant schema (minimizes context)
   - This separation reduces errors and improves speed by 40%+

2. **Streaming Optimizations**
   - Batch size: 100 rows
   - Stream SQL generation status before execution
   - Progressive result rendering
   - Compression for large datasets

3. **Model Selection**
   - O3: Domain Expert (complex reasoning, schema understanding)
   - GPT-4.1: SQL Query Expert (fast, accurate SQL generation)
   - O4-mini: Simple queries, suggestions, error messages

4. **Context Optimization**
   - High-level schema for Domain Expert: ~500 tokens
   - Detailed schema for SQL Expert: ~1000 tokens (only selected tables)   

### Error Handling

1. **Graceful Degradation**
   - Malformed SQL → Retry with hints
   - Empty results → Suggest alternatives
   - Timeout → Offer simpler query

2. **User Feedback**
   - Clear error messages
   - Actionable suggestions
   - Query refinement helpers


### Success Criteria

1. **Functional Requirements**
   - ✅ Natural language → SQL in <2 seconds
   - ✅ Auto-visualization with appropriate charts
   - ✅ Real-time streaming of results
   - ✅ Helpful error messages with query suggestions

2. **Performance Requirements**
   - ✅ <5 second end-to-end for simple queries
   - ✅ Handle 1M+ row tables
   - ✅ Support 10 concurrent users

3. **User Experience**
   - ✅ Intuitive query interface
   - ✅ Beautiful, interactive charts
   - ✅ Helpful error messages
   - ✅ Engaging thinking animations

### Definition of Done

- [ ] User can type natural language query
- [ ] System generates valid SQL
- [ ] Results stream in real-time
- [ ] Appropriate chart auto-renders
- [ ] System remembers context
- [ ] Follow-up suggestions appear
- [ ] Charts can be pinned to dashboard
- [ ] Thinking animations enhance UX
- [ ] Demo runs flawlessly
- [ ] Code is documented
- [ ] Deployment guide complete

### Key Implementation Examples

#### Domain Expert Agent Implementation
```csharp
public class DomainExpertAgent
{
    private readonly Kernel _kernel;
    private readonly SchemaCache _schemaCache;

    public async Task<DomainAnalysis> AnalyzeQueryAsync(string userQuery)
    {
        var highLevelSchema = _schemaCache.GetHighLevelSchema(); // Table names + descriptions only
        
        var prompt = $@"
Available tables: {highLevelSchema}
User query: {userQuery}

Respond in JSON:
{{
  ""intent"": ""what user wants"",
  ""tables"": [""table1"", ""table2""],
  ""relationships"": ""how tables connect"",
  ""complexity"": ""Simple|Medium|Complex""
}}";

        var result = await _kernel.InvokePromptAsync(prompt);
        return JsonSerializer.Deserialize<DomainAnalysis>(result);
    }
}
```

#### SQL Query Expert Implementation
```csharp
public class SQLQueryExpertAgent
{
    private readonly Kernel _kernel;
    private readonly SchemaCache _schemaCache;

    public async Task<string> GenerateSQLAsync(DomainAnalysis analysis)
    {
        var detailedSchema = _schemaCache.GetDetailedSchema(analysis.Tables);
        
        var prompt = $@"
Generate T-SQL for: {analysis.Intent}
Tables: {string.Join(", ", analysis.Tables)}

Schema:
{detailedSchema}

Return ONLY the SQL query. Include TOP 1000.";

        var sql = await _kernel.InvokePromptAsync(prompt);
        return ValidateAndParameterize(sql);
    }
}
```

#### SignalR Hub Flow
```csharp
public class ChartHub : Hub
{
    public async Task SubmitQuery(string query, string sessionId)
    {
        try 
        {
            // 1. Check cache first
            var cachedResult = await _queryCache.GetAsync(query);
            if (cachedResult != null)
            {
                await Clients.Caller.OnVisualizationReady(
                    new VizResponseMessage(cachedResult.VizResponse, Guid.NewGuid().ToString(), null)
                );
                return;
            }
            
            // 2. Domain Expert analysis
            await Clients.Caller.OnThinkingUpdate(new("analyzing_schema", "Understanding your question...", 25));
            var analysis = await _domainExpert.AnalyzeQueryAsync(query);
            
            // 3. SQL generation
            await Clients.Caller.OnThinkingUpdate(new("generating_sql", "Creating SQL query...", 50));
            var sql = await _sqlExpert.GenerateSQLAsync(analysis);
            await Clients.Caller.OnSQLGenerated(new(sql, analysis.Tables, [], true));
            
            // 4. Execute query
            await Clients.Caller.OnThinkingUpdate(new("executing_query", "Running query...", 75));
            var results = new List<object[]>();
            string[] columns = null;
            
            await foreach (var batch in _executor.StreamResultsAsync(sql))
            {
                results.AddRange(batch.Rows);
                columns ??= batch.Columns;
                await Clients.Caller.OnDataStreaming(batch);
            }
            
            // 5. Generate visualization using Enhanced Agent
            await Clients.Caller.OnThinkingUpdate(new("creating_visualization", "Determining best visualization...", 90));
            var visualization = await _enhancedVizAgent.DetermineVisualizationAsync(
                query, sql, columns, results, results.Count);
            
            // Send appropriate response based on type
            await Clients.Caller.OnUnifiedVisualizationReady(
                new UnifiedVisualizationMessage(
                    visualization.ResponseType.ToString(),
                    GetVisualizationTitle(visualization),
                    visualization.Confidence,
                    visualization.SelectionReasoning,
                    JsonSerializer.Serialize(visualization),
                    columns,
                    results.Count
                ));
            
            // Stream table data if needed
            if (visualization.ResponseType == ResponseType.Table)
            {
                await StreamTableData(visualization.TableConfig, results);
            }
            
            // 6. Cache the result
            await _queryCache.SetAsync(query, sql, visualization, analysis.Tables);
        }
        catch (Exception ex)
        {
            await Clients.Caller.OnError(new ErrorMessage(
                "Query Failed", 
                ex.Message, 
                GetSuggestionsForError(ex)
            ));
        }
    }
}
```

### MVP Scope (24 Hour Hackathon)

**Core Features:**
1. Natural Language to SQL conversion using two-agent system
2. Real-time query execution with streaming results
3. AI-driven visualization selection (Chart/Table/Text/Mixed)
4. Beautiful, responsive UI with thinking animations
5. Support for all major ApexCharts types
6. Table visualization with sorting/filtering/export
7. Single value and text summary responses

**Deferred Features (Post-Hackathon):**
1. Meta-cognition agent and memory system
2. User authentication and multi-tenancy
3. Advanced dashboard features
4. Query history and favorites
5. Collaborative features

**Focus Areas:**
- Get the core NL2SQL flow working perfectly
- Ensure beautiful chart rendering with multiple types
- Create engaging user experience with smooth animations
- Demonstrate clear value proposition in demo

---

This PRD serves as the single source of truth for the Sir Charts-a-lot hackathon project. Only when "boom" keyword is used, we'll resume implementation following this plan exactly.
