import { Component, OnInit, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { NgApexchartsModule } from 'ng-apexcharts';
import { TimelineComponent, TimelineEvent } from '../timeline/timeline.component';
import { DataTableComponent } from '../data-table/data-table.component';
import { TextResponseComponent } from '../text-response/text-response.component';
import {
  UnifiedVisualizationResponse,
  UnifiedVisualizationMessage,
  TableDataMessage,
  TextResponseMessage,
  ResponseType,
  TableVisualization,
  TextVisualization
} from '../../models/unified-visualization';
import {
  ApexAxisChartSeries,
  ApexChart,
  ApexXAxis,
  ApexDataLabels,
  ApexStroke,
  ApexYAxis,
  ApexTitleSubtitle,
  ApexLegend,
  ApexTooltip,
  ApexGrid,
  ApexPlotOptions
} from 'ng-apexcharts';

export type ChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  xaxis: ApexXAxis;
  stroke: ApexStroke;
  dataLabels: ApexDataLabels;
  yaxis: ApexYAxis;
  title: ApexTitleSubtitle;
  labels: string[];
  legend: ApexLegend;
  subtitle: ApexTitleSubtitle;
  tooltip: ApexTooltip;
  grid: ApexGrid;
  plotOptions: ApexPlotOptions;
};

interface EnhancedMessage {
  id: string;
  text: string;
  sender: 'user' | 'bot';
  timestamp: Date;
  type?: 'text' | 'sql' | 'thinking' | 'visualization' | 'table' | 'textResponse' | 'mixed' | 'domainAnalysis';
  isCode?: boolean;
  chart?: any;
  chartOptions?: Partial<ChartOptions>;
  isThinking?: boolean;
  timelineEvents?: TimelineEvent[];
  thinkingDuration?: number;
  parentMessageId?: string;
  childMessages?: EnhancedMessage[];
  
  // New fields for unified visualization
  unifiedVisualization?: UnifiedVisualizationResponse;
  tableConfig?: TableVisualization;
  tableData?: any[];
  textConfig?: TextVisualization;
}

@Component({
  selector: 'app-enhanced-chat',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    NgApexchartsModule, 
    RouterLink, 
    TimelineComponent,
    DataTableComponent,
    TextResponseComponent
  ],
  templateUrl: './enhanced-chat.component.html',
  styleUrls: ['./enhanced-chat.component.css']
})
export class EnhancedChatComponent implements OnInit, AfterViewChecked {
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;
  @ViewChild('dataTable') private dataTable?: DataTableComponent;
  
  messages: EnhancedMessage[] = [];
  queryText = '';
  copiedMessageId: string | null = null;
  private hubConnection!: signalR.HubConnection;
  private shouldScroll = false;
  private accumulatedData: any[] = [];
  private currentThinkingMessageId: string | null = null;
  private pendingTableData: Map<string, any[]> = new Map();
  private currentUserQuery: string = '';
  
  // Timeline properties
  timelineEvents: TimelineEvent[] = [];
  isThinking = false;
  thinkingDuration = 0;
  currentMessageId: string | null = null;
  elapsedTime = 0;
  
  ResponseType = ResponseType;

  constructor(private route: ActivatedRoute) {}

  ngOnInit() {
    this.startConnection();
    
    // Check if there's an initial query from navigation
    this.route.queryParams.subscribe(params => {
      if (params['q']) {
        this.queryText = params['q'];
        // Auto-submit the query after connection is established
        this.waitForConnection().then(() => {
          this.sendQuery();
        });
      }
    });
  }

  private waitForConnection(timeout = 5000): Promise<void> {
    return new Promise((resolve, reject) => {
      const checkInterval = 100;
      let elapsed = 0;
      
      const checkConnection = () => {
        if (this.hubConnection.state === signalR.HubConnectionState.Connected) {
          resolve();
        } else if (elapsed >= timeout) {
          reject(new Error('Connection timeout'));
        } else {
          elapsed += checkInterval;
          setTimeout(checkConnection, checkInterval);
        }
      };
      
      checkConnection();
    });
  }

  ngAfterViewChecked() {
    if (this.shouldScroll) {
      setTimeout(() => {
        this.scrollToBottom();
        this.shouldScroll = false;
      }, 100);
    }
  }

  private scrollToBottom(): void {
    try {
      if (this.scrollContainer && this.scrollContainer.nativeElement) {
        const element = this.scrollContainer.nativeElement;
        element.scrollTo({
          top: element.scrollHeight,
          behavior: 'smooth'
        });
      }
    } catch(err) { }
  }

  private startConnection() {
    // Use the data insight hub endpoint
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/datainsighthub')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('Connection started to data insight hub'))
      .catch(err => console.log('Error while starting connection: ' + err));

    this.setupStandardHandlers();
    this.setupEnhancedHandlers();
  }

  private setupStandardHandlers() {
    // Standard handlers from original chat component
    this.hubConnection.on('ReceiveMessage', (message: string) => {
      const botMessage: EnhancedMessage = {
        id: this.generateId(),
        text: message,
        sender: 'bot',
        timestamp: new Date(),
        type: 'text'
      };
      
      if (this.currentThinkingMessageId) {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.childMessages) {
            thinkingMessage.childMessages = [];
          }
          thinkingMessage.childMessages.push(botMessage);
        }
      } else {
        this.messages.push(botMessage);
      }
      
      this.shouldScroll = true;
    });

    this.hubConnection.on('ThinkingUpdate', (data: any) => {
      if (this.currentThinkingMessageId) {
        const existingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (existingMessage) {
          existingMessage.text = data.message;
          return;
        }
      }
      
      const thinkingMessage: EnhancedMessage = {
        id: this.generateId(),
        text: data.message,
        sender: 'bot',
        timestamp: new Date(),
        type: 'thinking',
        isThinking: true,
        timelineEvents: []
      };
      
      this.currentThinkingMessageId = thinkingMessage.id;
      this.messages.push(thinkingMessage);
      this.shouldScroll = true;
    });

    this.hubConnection.on('SqlGenerated', (data: any) => {
      if (this.currentThinkingMessageId) {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.childMessages) {
            thinkingMessage.childMessages = [];
          }
          
          const sqlMessage: EnhancedMessage = {
            id: this.generateId(),
            text: data.query,
            sender: 'bot',
            timestamp: new Date(),
            type: 'sql',
            isCode: true,
            parentMessageId: this.currentThinkingMessageId
          };
          
          thinkingMessage.childMessages.push(sqlMessage);
          this.shouldScroll = true;
        }
      }
    });

    this.hubConnection.on('TimelineEvent', (timelineEvent: TimelineEvent) => {
      if (!this.currentThinkingMessageId) {
        const thinkingMessage: EnhancedMessage = {
          id: this.generateId(),
          text: 'Processing your request...',
          sender: 'bot',
          timestamp: new Date(),
          type: 'thinking',
          isThinking: true,
          timelineEvents: [timelineEvent]
        };
        
        this.currentThinkingMessageId = thinkingMessage.id;
        this.messages.push(thinkingMessage);
        this.shouldScroll = true;
        this.isThinking = true;
      } else {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.timelineEvents) {
            thinkingMessage.timelineEvents = [];
          }
          thinkingMessage.timelineEvents.push(timelineEvent);
        }
      }
    });

    this.hubConnection.on('ThinkingComplete', (data: any) => {
      this.isThinking = false;
      this.thinkingDuration = data.duration || this.elapsedTime;
      
      if (this.currentThinkingMessageId) {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          thinkingMessage.thinkingDuration = this.thinkingDuration;
          thinkingMessage.isThinking = false;
        }
      }
    });

    // Domain Expert Analysis handler
    this.hubConnection.on('DomainAnalysis', (data: any) => {
      console.log('Received domain analysis:', data);
      
      if (this.currentThinkingMessageId) {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage && thinkingMessage.timelineEvents) {
          // Find the domain-expert completed event and add details to it
          const domainExpertEvent = thinkingMessage.timelineEvents.find(
            event => event.agent === 'domain-expert' && event.status === 'completed'
          );
          
          if (domainExpertEvent) {
            // Add the domain analysis details to the existing timeline event
            domainExpertEvent.details = [
              `Tables: ${data.tables.join(', ')}`,
              `Intent: ${data.intent}`,
              `Complexity: ${data.complexity}`,
              `Relationships: ${data.relationships}`
            ];
          }
          
          this.shouldScroll = true;
        }
      }
    });
  }

  private setupEnhancedHandlers() {
    // Enhanced unified visualization handler
    this.hubConnection.on('UnifiedVisualizationReady', (data: UnifiedVisualizationMessage) => {
      console.log('Received unified visualization:', data);
      console.log('Raw serialized config:', data.serializedConfig);
      
      try {
        const visualization: UnifiedVisualizationResponse = JSON.parse(data.serializedConfig);
        console.log('Parsed visualization:', visualization);
        
        if (this.currentThinkingMessageId) {
          const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
          if (thinkingMessage) {
            if (!thinkingMessage.childMessages) {
              thinkingMessage.childMessages = [];
            }
            
            // Create appropriate child message based on response type
            switch (visualization.responseType) {
              case ResponseType.Chart:
                this.addChartMessage(thinkingMessage, visualization);
                break;
              case ResponseType.Table:
                this.addTableMessage(thinkingMessage, visualization);
                break;
              case ResponseType.Text:
                this.addTextResponseMessage(thinkingMessage, visualization);
                break;
              case ResponseType.Mixed:
                this.addMixedResponseMessage(thinkingMessage, visualization);
                break;
            }
            
            this.shouldScroll = true;
          }
        }
      } catch (error) {
        console.error('Error parsing unified visualization:', error);
      }
    });

    // Table data streaming handler
    this.hubConnection.on('TableData', (data: TableDataMessage) => {
      console.log('Received table data:', data);
      
      if (this.currentThinkingMessageId) {
        const messageId = this.currentThinkingMessageId;
        
        // Accumulate table data
        if (!this.pendingTableData.has(messageId)) {
          this.pendingTableData.set(messageId, []);
        }
        
        const currentData = this.pendingTableData.get(messageId)!;
        currentData.push(...data.rows);
        
        // Update table component if it exists
        const thinkingMessage = this.messages.find(m => m.id === messageId);
        if (thinkingMessage && thinkingMessage.childMessages) {
          const tableMessage = thinkingMessage.childMessages.find(m => m.type === 'table');
          if (tableMessage) {
            tableMessage.tableData = [...currentData];
          }
        }
        
        if (data.isComplete) {
          this.pendingTableData.delete(messageId);
        }
      }
    });

    // Text response handler (for backward compatibility)
    this.hubConnection.on('TextResponse', (data: TextResponseMessage) => {
      console.log('Received text response:', data);
      
      if (this.currentThinkingMessageId) {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.childMessages) {
            thinkingMessage.childMessages = [];
          }
          
          // Check if we already have a text response (from UnifiedVisualizationReady)
          const existingTextResponse = thinkingMessage.childMessages.find(
            child => child.type === 'textResponse'
          );
          
          if (existingTextResponse) {
            console.log('Skipping duplicate text response - already handled by UnifiedVisualizationReady');
            return;
          }
          
          const textConfig: TextVisualization = {
            content: data.content,
            formatType: data.formatType,
            isSingleValue: data.isSingleValue,
            singleValueMetadata: data.metadata,
            useMarkdown: data.formatType === 'Markdown' || data.formatType === 'Summary',
            highlights: data.highlights
          };
          
          const textMessage: EnhancedMessage = {
            id: this.generateId(),
            text: data.content,
            sender: 'bot',
            timestamp: new Date(),
            type: 'textResponse',
            parentMessageId: this.currentThinkingMessageId,
            textConfig: textConfig
          };
          
          thinkingMessage.childMessages.push(textMessage);
          this.shouldScroll = true;
        }
      }
    });

    // Backward compatibility: standard visualization handler
    this.hubConnection.on('VisualizationReady', (data: any) => {
      console.log('Received standard visualization:', data);
      
      if (this.currentThinkingMessageId) {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.childMessages) {
            thinkingMessage.childMessages = [];
          }
          
          const vizMessage: EnhancedMessage = {
            id: this.generateId(),
            text: '',
            sender: 'bot',
            timestamp: new Date(),
            type: 'visualization',
            chartOptions: JSON.parse(data.vizTag),
            parentMessageId: this.currentThinkingMessageId
          };
          
          thinkingMessage.childMessages.push(vizMessage);
          this.shouldScroll = true;
        }
      }
    });
  }

  private addChartMessage(parentMessage: EnhancedMessage, visualization: UnifiedVisualizationResponse) {
    console.log('Adding chart message with visualization:', visualization);
    
    if (visualization.chartConfig) {
      console.log('Chart config before processing:', visualization.chartConfig.apexChartOptions);
      
      // Parse the series string to convert it from JSON string to array
      if (typeof visualization.chartConfig.apexChartOptions.series === 'string') {
        try {
          visualization.chartConfig.apexChartOptions.series = JSON.parse(visualization.chartConfig.apexChartOptions.series);
          console.log('Parsed series:', visualization.chartConfig.apexChartOptions.series);
          
          // Fix series data structure issues
          const chartType = visualization.chartConfig.apexChartOptions.chart?.type;
          
          // For column/bar charts, ensure proper series structure
          if (chartType === 'bar' || chartType === 'column' || chartType === 'line' || chartType === 'area') {
            // Validate each series has proper structure
            visualization.chartConfig.apexChartOptions.series = visualization.chartConfig.apexChartOptions.series.filter(s => 
              s && s.data && Array.isArray(s.data) && s.data.length > 0
            );
            
            // If multiple series but query asks for single metric, use only first
            if (visualization.chartConfig.apexChartOptions.series.length > 1) {
              const title = visualization.chartConfig.apexChartOptions.title?.text?.toLowerCase() || '';
              const reasoning = visualization.selectionReasoning?.toLowerCase() || '';
              const originalQuery = this.currentUserQuery?.toLowerCase() || '';
              
              // Check if this is a user count query but showing financial metrics
              const isUserCountQuery = originalQuery.includes('users who') || originalQuery.includes('number of users') ||
                                     originalQuery.includes('count of users') || originalQuery.includes('how many users');
              const isShowingFinancialMetrics = title.includes('financial metrics') || 
                                               visualization.chartConfig.apexChartOptions.series.some(s => 
                                                 s.name && (s.name.includes('Income') || s.name.includes('Tax') || s.name.includes('Refund')));
              
              if ((title.includes('users by') || title.includes('count by') || 
                  reasoning.includes('number of users') || reasoning.includes('user count') ||
                  reasoning.includes('single metric')) ||
                  (isUserCountQuery && isShowingFinancialMetrics)) {
                console.log('Detected single-metric chart or user count query with financial data, using only first series');
                console.log('Original query:', originalQuery);
                console.log('Title:', title);
                console.log('Series names:', visualization.chartConfig.apexChartOptions.series.map(s => s.name));
                
                // For user count queries showing financial data, we might want to rename the series
                if (isUserCountQuery && isShowingFinancialMetrics) {
                  visualization.chartConfig.apexChartOptions.series = [{
                    ...visualization.chartConfig.apexChartOptions.series[0],
                    name: 'User Count'
                  }];
                  // Also update the title if it says "Financial Metrics"
                  if (visualization.chartConfig.apexChartOptions.title && 
                      visualization.chartConfig.apexChartOptions.title.text.includes('Financial Metrics')) {
                    visualization.chartConfig.apexChartOptions.title.text = 
                      visualization.chartConfig.apexChartOptions.title.text.replace('Financial Metrics', 'User Count');
                  }
                } else {
                  visualization.chartConfig.apexChartOptions.series = [visualization.chartConfig.apexChartOptions.series[0]];
                }
              }
            }
            
            // Ensure data values are numbers, not strings
            visualization.chartConfig.apexChartOptions.series = visualization.chartConfig.apexChartOptions.series.map(series => ({
              ...series,
              data: series.data.map(d => {
                if (typeof d === 'string' && !isNaN(parseFloat(d))) {
                  return parseFloat(d);
                }
                return d;
              })
            }));
          }
        } catch (e) {
          console.error('Error parsing series:', e);
          visualization.chartConfig.apexChartOptions.series = [];
        }
      }
      
      // Ensure all required properties are present with defaults
      const chartOptions = visualization.chartConfig.apexChartOptions;
      console.log('Chart options after series parsing:', chartOptions);
      
      // Ensure series is an array
      if (!chartOptions.series) {
        chartOptions.series = [];
      }
      
      // Handle both xaxis/yaxis and XAxis/YAxis from backend (C# sends PascalCase)
      if (!chartOptions.xaxis && chartOptions.XAxis) {
        chartOptions.xaxis = chartOptions.XAxis;
        delete chartOptions.XAxis;
      }
      if (!chartOptions.yaxis && chartOptions.YAxis) {
        chartOptions.yaxis = chartOptions.YAxis;
        delete chartOptions.YAxis;
      }
      
      // IMPORTANT: The backend model might not include xaxis property, so we need to add it
      if (!chartOptions.xaxis) {
        chartOptions.xaxis = {};
      }
      if (!chartOptions.xaxis.categories) {
        chartOptions.xaxis.categories = [];
      }
      
      // Ensure chart object exists with type and height
      if (!chartOptions.chart) {
        chartOptions.chart = { type: 'bar', height: 350 };
      } else {
        if (!chartOptions.chart.type) chartOptions.chart.type = 'bar';
        if (!chartOptions.chart.height) chartOptions.chart.height = 350;
      }
      
      // For category-based charts, ensure xaxis exists with categories
      const categoryCharts = ['bar', 'column', 'line', 'area'];
      if (categoryCharts.includes(chartOptions.chart.type)) {
        if (!chartOptions.xaxis) {
          chartOptions.xaxis = { categories: [] };
        }
        
        // Try to extract categories from series data if not provided
        if ((!chartOptions.xaxis.categories || chartOptions.xaxis.categories.length === 0) && 
            chartOptions.series && chartOptions.series.length > 0) {
          
          // Check if series has data with x property
          const firstSeries = chartOptions.series[0];
          if (firstSeries.data && Array.isArray(firstSeries.data) && firstSeries.data.length > 0) {
            if (typeof firstSeries.data[0] === 'object' && 'x' in firstSeries.data[0]) {
              // Extract x values as categories
              chartOptions.xaxis.categories = firstSeries.data.map((d: any) => d.x);
            } else if (chartOptions.labels && chartOptions.labels.length > 0) {
              // Use labels array if available
              chartOptions.xaxis.categories = [...chartOptions.labels];
            } else {
              // For simple numeric arrays, generate categories based on data length
              chartOptions.xaxis.categories = Array.from(
                {length: firstSeries.data.length}, 
                (_, i) => `Item ${i + 1}`
              );
            }
          }
        }
      }
      
      // Ensure yaxis exists
      if (!chartOptions.yaxis) {
        chartOptions.yaxis = {};
      }
      
      // For pie/donut charts, ensure labels exist
      const pieCharts = ['pie', 'donut', 'radialBar', 'polarArea'];
      if (pieCharts.includes(chartOptions.chart.type) && !chartOptions.labels) {
        chartOptions.labels = [];
      }
      
      // Clean up empty labels
      if (chartOptions.labels) {
        chartOptions.labels = chartOptions.labels.map(label => label || 'Unknown');
        
        // For pie/donut charts, ensure labels match series data length
        const pieCharts = ['pie', 'donut', 'radialBar', 'polarArea'];
        if (pieCharts.includes(chartOptions.chart.type) && chartOptions.series && Array.isArray(chartOptions.series)) {
          // If series is an array of numbers (not objects with data property)
          if (chartOptions.series.length > 0 && typeof chartOptions.series[0] === 'number') {
            // Ensure labels array matches series length
            if (chartOptions.labels.length !== chartOptions.series.length) {
              console.warn(`Label count (${chartOptions.labels.length}) doesn't match series count (${chartOptions.series.length})`);
              // Trim or pad labels to match
              if (chartOptions.labels.length > chartOptions.series.length) {
                chartOptions.labels = chartOptions.labels.slice(0, chartOptions.series.length);
              } else {
                while (chartOptions.labels.length < chartOptions.series.length) {
                  chartOptions.labels.push(`Item ${chartOptions.labels.length + 1}`);
                }
              }
            }
          }
        }
      }
      
      // Clean up empty categories in xaxis
      if (chartOptions.xaxis && chartOptions.xaxis.categories) {
        chartOptions.xaxis.categories = chartOptions.xaxis.categories.map(cat => cat || 'Unknown');
        
        // For category-based charts, ensure categories match data length
        const categoryCharts = ['bar', 'column', 'line', 'area'];
        if (categoryCharts.includes(chartOptions.chart.type) && chartOptions.series && chartOptions.series.length > 0) {
          const dataLength = chartOptions.series[0].data ? chartOptions.series[0].data.length : 0;
          if (dataLength > 0 && chartOptions.xaxis.categories.length !== dataLength) {
            console.warn(`Category count (${chartOptions.xaxis.categories.length}) doesn't match data length (${dataLength})`);
            // Trim or pad categories to match
            if (chartOptions.xaxis.categories.length > dataLength) {
              chartOptions.xaxis.categories = chartOptions.xaxis.categories.slice(0, dataLength);
            } else {
              while (chartOptions.xaxis.categories.length < dataLength) {
                chartOptions.xaxis.categories.push(`Category ${chartOptions.xaxis.categories.length + 1}`);
              }
            }
          }
        }
      }
      
      // For bar/column charts, if labels are empty but we have xaxis categories, use those
      if (categoryCharts.includes(chartOptions.chart.type)) {
        if (!chartOptions.labels || chartOptions.labels.length === 0) {
          // If we have xaxis categories, use them as labels
          if (chartOptions.xaxis && chartOptions.xaxis.categories && chartOptions.xaxis.categories.length > 0) {
            chartOptions.labels = chartOptions.xaxis.categories;
          } else if (chartOptions.series && chartOptions.series.length > 0 && chartOptions.series[0].data) {
            // Try to generate labels based on data length
            const dataLength = chartOptions.series[0].data.length;
            chartOptions.labels = Array.from({length: dataLength}, (_, i) => `Category ${i + 1}`);
          }
        }
      }
      
      // Ensure other common properties have defaults
      if (!chartOptions.dataLabels) {
        chartOptions.dataLabels = { enabled: false };
      }
      
      if (!chartOptions.grid) {
        chartOptions.grid = { show: true };
      }
      
      if (!chartOptions.tooltip) {
        chartOptions.tooltip = { enabled: true };
      }
      
      // Ensure stroke property exists for line/area charts
      if (!chartOptions.stroke) {
        chartOptions.stroke = { curve: 'straight' };
      }
      
      // Ensure legend exists
      if (!chartOptions.legend) {
        chartOptions.legend = { show: true };
      }
      
      // Ensure title exists
      if (!chartOptions.title) {
        chartOptions.title = { text: 'Chart', align: 'center' };
      }
      
      // Ensure fill property for area charts
      if (chartOptions.chart.type === 'area' && !chartOptions.fill) {
        chartOptions.fill = { opacity: 0.5 };
      }
      
      // Ensure markers for line/area charts
      if ((chartOptions.chart.type === 'line' || chartOptions.chart.type === 'area') && !chartOptions.markers) {
        chartOptions.markers = { size: 0 };
      }
      
      // Ensure responsive array
      if (!chartOptions.responsive) {
        chartOptions.responsive = [];
      }
      
      // Ensure annotations
      if (!chartOptions.annotations) {
        chartOptions.annotations = { };
      }
      
      // Deep property checks - ensure nested properties exist
      // Chart toolbar
      if (chartOptions.chart && !chartOptions.chart.toolbar) {
        chartOptions.chart.toolbar = { show: true };
      }
      
      // Chart animations
      if (chartOptions.chart && !chartOptions.chart.animations) {
        chartOptions.chart.animations = { enabled: true };
      }
      
      // DataLabels style
      if (chartOptions.dataLabels && !chartOptions.dataLabels.style) {
        chartOptions.dataLabels.style = { fontSize: '12px' };
      }
      
      // Grid padding
      if (chartOptions.grid && !chartOptions.grid.padding) {
        chartOptions.grid.padding = { top: 0, right: 0, bottom: 0, left: 0 };
      }
      
      // Tooltip fixed
      if (chartOptions.tooltip) {
        if (chartOptions.tooltip.fixed === null || chartOptions.tooltip.fixed === undefined) {
          chartOptions.tooltip.fixed = { enabled: false };
        }
        // Ensure tooltip has all required properties
        if (chartOptions.tooltip.shared === null || chartOptions.tooltip.shared === undefined) {
          chartOptions.tooltip.shared = true;
        }
        if (chartOptions.tooltip.intersect === null || chartOptions.tooltip.intersect === undefined) {
          chartOptions.tooltip.intersect = false;
        }
      }
      
      // Chart zoom
      if (chartOptions.chart && !chartOptions.chart.zoom) {
        chartOptions.chart.zoom = { enabled: false };
      }
      
      // Chart selection
      if (chartOptions.chart && chartOptions.chart.selection === null) {
        chartOptions.chart.selection = { enabled: false };
      }
      
      // XAxis labels
      if (chartOptions.xaxis && !chartOptions.xaxis.labels) {
        chartOptions.xaxis.labels = { show: true };
      }
      
      // YAxis labels
      if (chartOptions.yaxis && !chartOptions.yaxis.labels) {
        chartOptions.yaxis.labels = { show: true };
      }
      
      // States
      if (!chartOptions.states) {
        chartOptions.states = {
          hover: { filter: { type: 'lighten', value: 0.15 } },
          active: { filter: { type: 'darken', value: 0.35 } }
        };
      }
      
      // Add plotOptions for bar/column charts
      if ((chartOptions.chart.type === 'bar' || chartOptions.chart.type === 'column') && !chartOptions.plotOptions) {
        chartOptions.plotOptions = {
          bar: {
            horizontal: chartOptions.chart.type === 'bar',
            columnWidth: '55%',
            endingShape: 'rounded'
          }
        };
      }
      
      // Final validation before rendering
      if (chartOptions.series && Array.isArray(chartOptions.series)) {
        // For category charts, ensure all series have valid data arrays
        const categoryCharts = ['bar', 'column', 'line', 'area'];
        if (categoryCharts.includes(chartOptions.chart.type)) {
          const validSeries = chartOptions.series.every(s => 
            s && s.data && Array.isArray(s.data) && s.data.length > 0
          );
          
          if (!validSeries) {
            console.error('Invalid series structure detected, creating fallback');
            chartOptions.series = [{
              name: 'Data',
              data: [0] // Fallback to prevent crash
            }];
            if (chartOptions.xaxis) {
              chartOptions.xaxis.categories = ['No Data'];
            }
          }
        }
        
        // For pie charts, ensure series is array of numbers
        const pieCharts = ['pie', 'donut', 'radialBar', 'polarArea'];
        if (pieCharts.includes(chartOptions.chart.type)) {
          // If series has objects with data property, flatten it
          if (chartOptions.series.length > 0 && chartOptions.series[0].data) {
            console.log('Converting series format for pie chart');
            chartOptions.series = chartOptions.series[0].data;
          }
        }
      }
      
      console.log('Final processed chart options:', chartOptions);
      console.log('Series data:', JSON.stringify(chartOptions.series));
      console.log('Labels:', chartOptions.labels);
      console.log('XAxis categories:', chartOptions.xaxis?.categories);
    }
    
    // Create a safe default chart configuration - use the processed chartOptions
    const processedOptions = visualization.chartConfig?.apexChartOptions || {};
    
    // Debug log to see what we're working with
    console.log('ProcessedOptions before sanitization:', processedOptions);
    console.log('Chart type:', processedOptions.chart?.type);
    console.log('Series type:', typeof processedOptions.series);
    
    // Ensure series is properly formatted
    let safeSeries = [];
    if (processedOptions.series) {
      if (typeof processedOptions.series === 'string') {
        try {
          safeSeries = JSON.parse(processedOptions.series);
        } catch (e) {
          console.error('Failed to parse series string:', e);
          safeSeries = [];
        }
      } else {
        safeSeries = processedOptions.series;
      }
    }
    
    // Fix chart type - ApexCharts expects lowercase and specific types
    let chartType = processedOptions.chart?.type || 'bar';
    // Convert PascalCase to lowercase first
    chartType = chartType.toLowerCase();
    
    // ApexCharts doesn't have 'column' type - it's 'bar' with horizontal: false
    if (chartType === 'column') {
      chartType = 'bar';
    }
    
    // Ensure we have a valid ApexCharts type
    const validTypes = ['line', 'area', 'bar', 'pie', 'donut', 'radialBar', 'scatter', 'bubble', 
                       'heatmap', 'treemap', 'candlestick', 'boxPlot', 'radar', 'polarArea', 'rangeBar'];
    if (!validTypes.includes(chartType)) {
      console.warn(`Invalid chart type: ${chartType}, defaulting to bar`);
      chartType = 'bar';
    }
    
    // Ensure absolutely no null/undefined values - deep clone and sanitize
    const safeChartOptions = JSON.parse(JSON.stringify({
      series: safeSeries,
      chart: {
        type: chartType,
        height: processedOptions.chart?.height || 350,
        toolbar: { show: true },
        animations: { enabled: true },
        zoom: { enabled: false },
        selection: { enabled: false },
        background: 'transparent'
      },
      xaxis: {
        categories: processedOptions.xaxis?.categories || [],
        labels: { show: true },
        title: processedOptions.xaxis?.title || {}
      },
      yaxis: {
        labels: { show: true },
        title: processedOptions.yaxis?.title || {}
      },
      title: processedOptions.title || { text: 'Chart', align: 'center' },
      dataLabels: { enabled: false },
      stroke: { curve: 'straight' },
      grid: { 
        show: true,
        borderColor: '#e7e7e7',
        padding: { top: 0, right: 0, bottom: 0, left: 0 }
      },
      tooltip: { 
        enabled: true,
        shared: true,
        intersect: false,
        fixed: { enabled: false }
      },
      labels: processedOptions.labels || [],
      legend: { show: true, position: 'bottom' },
      plotOptions: processedOptions.plotOptions || {
        bar: {
          horizontal: false,
          columnWidth: '55%',
          endingShape: 'rounded'
        }
      },
      fill: { opacity: 1 },
      markers: { size: 0 },
      responsive: [],
      annotations: {},
      states: {
        hover: { filter: { type: 'lighten', value: 0.15 } },
        active: { filter: { type: 'darken', value: 0.35 } }
      },
      colors: processedOptions.colors,
      theme: { mode: 'light' },
      noData: { text: 'No data available' }
    }, (key, value) => {
      // Replace any null or undefined with appropriate defaults
      if (value === null || value === undefined) {
        if (key === 'enabled' || key === 'show') return false;
        if (key === 'categories' || key === 'labels' || key === 'data') return [];
        if (typeof key === 'string' && key.includes('color')) return '#000000';
        return {};
      }
      return value;
    }));
    
    // Final validation log
    console.log('Safe chart options created:', safeChartOptions);
    console.log('Safe series:', JSON.stringify(safeChartOptions.series));
    console.log('Safe chart type:', safeChartOptions.chart.type);
    
    // Validate series structure for column/bar charts
    if ((safeChartOptions.chart.type === 'column' || safeChartOptions.chart.type === 'bar') && 
        safeChartOptions.series && safeChartOptions.series.length > 0) {
      const firstSeries = safeChartOptions.series[0];
      if (!firstSeries.name || !firstSeries.data || !Array.isArray(firstSeries.data)) {
        console.error('Invalid series structure for column/bar chart:', firstSeries);
        // Force a valid structure
        safeChartOptions.series = [{
          name: 'Data',
          data: [0]
        }];
        safeChartOptions.xaxis.categories = ['No Data'];
      } else {
        // Ensure all data values are numbers
        safeChartOptions.series = safeChartOptions.series.map(series => ({
          ...series,
          name: series.name || 'Series',
          data: series.data.map(d => {
            const num = Number(d);
            return isNaN(num) ? 0 : num;
          })
        }));
      }
    }
    
    // One more safety check - ensure xaxis categories is an array
    if (!Array.isArray(safeChartOptions.xaxis.categories)) {
      console.warn('xaxis.categories is not an array, fixing...');
      safeChartOptions.xaxis.categories = [];
    }
    
    const chartMessage: EnhancedMessage = {
      id: this.generateId(),
      text: '',
      sender: 'bot',
      timestamp: new Date(),
      type: 'visualization',
      chartOptions: safeChartOptions,
      parentMessageId: parentMessage.id,
      unifiedVisualization: visualization
    };
    
    parentMessage.childMessages!.push(chartMessage);
  }

  private addTableMessage(parentMessage: EnhancedMessage, visualization: UnifiedVisualizationResponse) {
    const tableMessage: EnhancedMessage = {
      id: this.generateId(),
      text: '',
      sender: 'bot',
      timestamp: new Date(),
      type: 'table',
      parentMessageId: parentMessage.id,
      tableConfig: visualization.tableConfig,
      tableData: this.pendingTableData.get(parentMessage.id) || [],
      unifiedVisualization: visualization
    };
    
    parentMessage.childMessages!.push(tableMessage);
  }

  private addTextResponseMessage(parentMessage: EnhancedMessage, visualization: UnifiedVisualizationResponse) {
    const textMessage: EnhancedMessage = {
      id: this.generateId(),
      text: visualization.textConfig?.content || '',
      sender: 'bot',
      timestamp: new Date(),
      type: 'textResponse',
      parentMessageId: parentMessage.id,
      textConfig: visualization.textConfig,
      unifiedVisualization: visualization
    };
    
    parentMessage.childMessages!.push(textMessage);
  }

  private addMixedResponseMessage(parentMessage: EnhancedMessage, visualization: UnifiedVisualizationResponse) {
    // Add text part first if it exists
    if (visualization.textConfig) {
      this.addTextResponseMessage(parentMessage, visualization);
    }
    
    // Add secondary visualizations
    if (visualization.secondaryVisualizations) {
      visualization.secondaryVisualizations
        .sort((a, b) => a.order - b.order)
        .forEach(secondary => {
          // Create a new visualization response for each secondary
          const secondaryViz: UnifiedVisualizationResponse = {
            responseType: secondary.type,
            confidence: visualization.confidence,
            selectionReasoning: 'Secondary visualization',
            ...secondary.configuration
          };
          
          switch (secondary.type) {
            case ResponseType.Chart:
              this.addChartMessage(parentMessage, secondaryViz);
              break;
            case ResponseType.Table:
              this.addTableMessage(parentMessage, secondaryViz);
              break;
          }
        });
    }
  }

  sendQuery() {
    if (this.queryText.trim()) {
      if (this.hubConnection.state !== signalR.HubConnectionState.Connected) {
        console.error('SignalR connection is not established. Current state:', this.hubConnection.state);
        this.hubConnection.start()
          .then(() => {
            console.log('Reconnected, sending query...');
            this.executeQuery();
          })
          .catch(err => {
            console.error('Failed to reconnect:', err);
            const errorMessage: EnhancedMessage = {
              id: this.generateId(),
              text: 'Connection error. Please refresh the page and try again.',
              sender: 'bot',
              timestamp: new Date(),
              type: 'text'
            };
            this.messages.push(errorMessage);
            this.shouldScroll = true;
          });
        return;
      }
      
      this.executeQuery();
    }
  }

  private executeQuery() {
    this.timelineEvents = [];
    this.isThinking = false;
    this.thinkingDuration = 0;
    this.currentThinkingMessageId = null;
    
    const userMessage: EnhancedMessage = {
      id: this.generateId(),
      text: this.queryText,
      sender: 'user',
      timestamp: new Date()
    };
    
    this.messages.push(userMessage);
    this.shouldScroll = true;
    this.currentMessageId = userMessage.id;
    this.currentUserQuery = this.queryText; // Store the current query
    
    this.hubConnection
      .invoke('SubmitQuery', this.queryText, 'session-123')
      .catch(err => console.error(err));
    
    this.queryText = '';
  }

  copyToClipboard(text: string) {
    navigator.clipboard.writeText(text).then(() => {
      const message = this.messages.find(m => m.text === text);
      if (message) {
        this.copiedMessageId = message.id;
        setTimeout(() => {
          this.copiedMessageId = null;
        }, 2000);
      }
    });
  }

  formatTime(date: Date): string {
    return date.toLocaleTimeString('en-US', { 
      hour: 'numeric', 
      minute: '2-digit',
      hour12: true 
    });
  }

  private generateId(): string {
    return Math.random().toString(36).substring(2, 11);
  }

  private formatDomainAnalysis(data: any): string {
    const lines: string[] = [];
    
    if (data.tables && data.tables.length > 0) {
      lines.push(`**Domain Expert ✓**`);
      lines.push(`Identified relevant tables: ${data.tables.join(', ')}`);
    }
    
    if (data.intent) {
      lines.push(`Intent: ${data.intent}`);
    }
    
    if (data.complexity) {
      lines.push(`Complexity: ${data.complexity}`);
    }
    
    if (data.relationships) {
      lines.push(`Relationships: ${data.relationships}`);
    }
    
    return lines.join('\n');
  }

  formatMarkdown(text: string): string {
    // Simple markdown formatting
    return text
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/\n/g, '<br>');
  }
}