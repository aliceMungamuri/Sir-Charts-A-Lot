Query Processing Pipeline Flow

  Here's an easy-to-understand representation of how a user query flows through Sir-Chart-a-lot  system:

  🧑 USER QUERY
       ↓
  [1] 🌐 ANGULAR UI (enhanced-chat.component.ts)
       • User types natural language query
       • SignalR connection sends to backend
       ↓
  [2] 📡 SIGNALR HUB (DataInsightHub.cs)
       • Receives query via SubmitQuery()
       • Coordinates the entire pipeline
       ↓
  [3] 🧠 DOMAIN EXPERT AGENT
       • Analyzes query intent
       • Identifies relevant database tables (max 5)
       • Returns: tables, intent, complexity, relationships
       ↓
  [4] 💾 SCHEMA SERVICE
       • Provides detailed schema for selected tables only
       • Minimizes context for efficiency
       ↓
  [5] 🔧 SQL EXPERT AGENT
       • Receives focused table info from Domain Expert
       • Generates precise T-SQL query
       • Ensures security (parameterization, SELECT only)
       ↓
  [6] ⚡ SQL EXECUTION SERVICE
       • Executes query against database
       • Streams results in batches
       • Returns: data rows + column info
       ↓
  [7] 📊 VISUALIZATION SERVICE
       • AI analyzes query intent + data characteristics
       • Determines best response type:
         - 📈 Chart (line, bar, pie, etc.)
         - 📋 Table (sortable, filterable)
         - 📝 Text (single values, summaries)
         - 🎭 Mixed (combination)
       • Generates complete visualization config
       ↓
  [8] 📡 SIGNALR RESPONSES
       • Sends real-time updates throughout process
       • Timeline events show progress
       • Final visualization sent to UI
       ↓
  [9] 🌐 ANGULAR UI RENDERING
       • Displays appropriate component based on type
       • Charts use ApexCharts
       • Tables use custom data-table component
       • Text shows formatted responses

  Key Design Principles:

  1. 3-Agent Architecture: Domain Expert focuses on "what tables", SQL Expert focuses on "how to query", and Visualization Agent
  2. Minimal Context: Each agent gets only the information it needs
  3. Reasoning Transparency via Timeline component to offer real-time Feedback: Users see progress at each step
  4. AI-Driven Visualization: System intelligently chooses how to display results
  5. Streaming: Large results stream progressively for better UX