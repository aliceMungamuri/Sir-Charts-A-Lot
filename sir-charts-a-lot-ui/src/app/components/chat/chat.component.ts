import { Component, OnInit, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { NgApexchartsModule } from 'ng-apexcharts';
import { TimelineComponent, TimelineEvent } from '../timeline/timeline.component';
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
  ApexGrid
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
};

interface Message {
  id: string;
  text: string;
  sender: 'user' | 'bot';
  timestamp: Date;
  type?: 'text' | 'sql' | 'thinking' | 'visualization';
  isCode?: boolean;
  chart?: any;
  chartOptions?: Partial<ChartOptions>;
  isThinking?: boolean;
  timelineEvents?: TimelineEvent[];
  thinkingDuration?: number;
  parentMessageId?: string;  // Links related messages to the main thinking message
  childMessages?: Message[]; // Contains SQL, visualization, and summary messages
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, NgApexchartsModule, RouterLink, TimelineComponent],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.css']
})
export class ChatComponent implements OnInit, AfterViewChecked {
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;
  
  messages: Message[] = [];
  queryText = '';
  copiedMessageId: string | null = null;
  private hubConnection!: signalR.HubConnection;
  private shouldScroll = false;
  private accumulatedData: any[] = [];
  private currentThinkingMessageId: string | null = null;
  
  // Timeline properties
  timelineEvents: TimelineEvent[] = [];
  isThinking = false;
  thinkingDuration = 0;
  currentMessageId: string | null = null;
  elapsedTime = 0;

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
      const checkInterval = 100; // Check every 100ms
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
      // Add a small delay to ensure DOM is fully rendered
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
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/charthub')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('Connection started'))
      .catch(err => console.log('Error while starting connection: ' + err));

    // Handle connection state changes
    this.hubConnection.onreconnecting(() => {
      console.log('Attempting to reconnect...');
    });

    this.hubConnection.onreconnected(() => {
      console.log('Reconnected successfully');
    });

    this.hubConnection.onclose((error) => {
      console.error('Connection closed:', error);
    });

    this.hubConnection.on('ReceiveMessage', (message: string) => {
      if (this.currentThinkingMessageId) {
        // Find the thinking message and add summary as a child message
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.childMessages) {
            thinkingMessage.childMessages = [];
          }
          
          const summaryMessage: Message = {
            id: this.generateId(),
            text: message,
            sender: 'bot',
            timestamp: new Date(),
            type: 'text',
            parentMessageId: this.currentThinkingMessageId
          };
          
          thinkingMessage.childMessages.push(summaryMessage);
          // Keep the timeline events for the "Thought for X seconds" header
        }
        this.currentThinkingMessageId = null;
      } else {
        // If no thinking message exists, create a regular message
        const newMessage: Message = {
          id: this.generateId(),
          text: message,
          sender: 'bot',
          timestamp: new Date(),
          type: 'text'
        };
        
        this.messages.push(newMessage);
      }
      this.shouldScroll = true;
    });

    // Handle thinking updates
    this.hubConnection.on('ThinkingUpdate', (data: any) => {
      if (this.currentThinkingMessageId) {
        // Update existing thinking message instead of removing it
        const existingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (existingMessage) {
          existingMessage.text = data.message;
          return;
        }
      }
      
      const thinkingMessage: Message = {
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

    // Handle SQL display
    this.hubConnection.on('SqlGenerated', (data: any) => {
      if (this.currentThinkingMessageId) {
        // Find the thinking message and add SQL as a child message
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.childMessages) {
            thinkingMessage.childMessages = [];
          }
          
          const sqlMessage: Message = {
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
      } else {
        // Fallback: create a standalone message if no thinking message exists
        const sqlMessage: Message = {
          id: this.generateId(),
          text: data.query,
          sender: 'bot',
          timestamp: new Date(),
          type: 'sql',
          isCode: true
        };
        
        this.messages.push(sqlMessage);
        this.shouldScroll = true;
      }
    });    // Handle data streaming
    this.hubConnection.on('DataStream', (data: any) => {
      // Validate data before processing
      if (data && data.data && Array.isArray(data.data)) {
        // Accumulate data
        this.accumulatedData = [...this.accumulatedData, ...data.data];
      }
      
      if (data && data.isComplete) {
        // Data stream complete - don't remove thinking message, it will contain all responses
      }
    });// Handle visualization
    this.hubConnection.on('VisualizationReady', (data: any) => {
      // Validate data before processing
      if (!data) {
        console.error('VisualizationReady received null or undefined data');
        return;
      }

      try {
        console.log('Raw visualization data received:', JSON.stringify(data, null, 2));
        
        // Update thinking message if it exists
        /* if (this.currentThinkingMessageId) {
          const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
          if (thinkingMessage) {
            thinkingMessage.isThinking = false;
            thinkingMessage.type = 'visualization';
            thinkingMessage.chartOptions = this.createChartOptions(data);
            // Keep the timeline events
          }
          this.currentThinkingMessageId = null;
          this.shouldScroll = true;
          this.accumulatedData = [];
          console.log('Visualization updated in thinking message');
          return;
        } */
        console.log(`Creating new visualization message`);
          // Create chart options with error handling
        let chartOptions: Partial<ChartOptions>;
        try {
          chartOptions = this.createChartOptions(data);
          console.log('Generated chart options:', JSON.stringify(chartOptions, null, 2));
          console.log('Provided chart options:', JSON.stringify(data.vizTag, null, 2));
          // Final validation of chart options
          if (!chartOptions.series || (Array.isArray(chartOptions.series) && chartOptions.series.length === 0)) {
            throw new Error('Chart series is empty or undefined');
          }
          
        } catch (optionsError) {
          console.error('Error creating chart options:', optionsError);          // Create a safe fallback chart
          chartOptions = {
            chart: { height: 350, type: 'bar' as any },
            series: [{ name: 'No Data', data: [{ x: 'No Data', y: 0 }] }],
            title: { text: 'Error Loading Chart' },
            dataLabels: { enabled: false }
          };
        }
        
        if (this.currentThinkingMessageId) {
          // Find the thinking message and add visualization as a child message
          const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
          if (thinkingMessage) {
            thinkingMessage.isThinking = false; // Stop the thinking animation
            
            if (!thinkingMessage.childMessages) {
              thinkingMessage.childMessages = [];
            }
            
            const vizMessage: Message = {
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
        } else {
          // Fallback: create a standalone message if no thinking message exists
          const vizMessage: Message = {
            id: this.generateId(),
            text: '',
            sender: 'bot',
            timestamp: new Date(),
            type: 'visualization',
            chartOptions: JSON.parse(data.vizTag)
          };
          
          this.messages.push(vizMessage);
          this.shouldScroll = true;
        }
        
        // Reset accumulated data for next query
        this.accumulatedData = [];
      } catch (error) {
        console.error('Error processing visualization data:', error, error.stack);
        // Create an error message instead
        const errorMessage: Message = {
          id: this.generateId(),
          text: `Error creating visualization: ${error.message}. Please try again.`,
          sender: 'bot',
          timestamp: new Date(),
          type: 'text'
        };
        this.messages.push(errorMessage);
        this.shouldScroll = true;
      }
    });

    // Handle timeline events
    this.hubConnection.on('TimelineEvent', (event: any) => {
      const timelineEvent: TimelineEvent = {
        id: event.id,
        agent: event.agent,
        stage: event.stage,
        message: event.message,
        timestamp: new Date(),
        status: event.status,
        details: event.details
      };
      
      this.timelineEvents.push(timelineEvent);
      
      // Create or update thinking message
      if (!this.currentThinkingMessageId) {
        const thinkingMessage: Message = {
          id: this.generateId(),
          text: 'Thinking...',
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
        // Add event to existing thinking message
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          if (!thinkingMessage.timelineEvents) {
            thinkingMessage.timelineEvents = [];
          }
          thinkingMessage.timelineEvents.push(timelineEvent);
        }
      }
    });

    // Handle thinking complete
    this.hubConnection.on('ThinkingComplete', (data: any) => {
      this.isThinking = false;
      this.thinkingDuration = data.duration || this.elapsedTime;
      
      // Update thinking message duration
      if (this.currentThinkingMessageId) {
        const thinkingMessage = this.messages.find(m => m.id === this.currentThinkingMessageId);
        if (thinkingMessage) {
          thinkingMessage.thinkingDuration = this.thinkingDuration;
        }
      }
    });
  }

  sendQuery() {
    if (this.queryText.trim()) {
      // Check if connection is established
      if (this.hubConnection.state !== signalR.HubConnectionState.Connected) {
        console.error('SignalR connection is not established. Current state:', this.hubConnection.state);
        // Try to reconnect
        this.hubConnection.start()
          .then(() => {
            console.log('Reconnected, sending query...');
            this.executeQuery();
          })
          .catch(err => {
            console.error('Failed to reconnect:', err);
            // Show error message to user
            const errorMessage: Message = {
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
    // Reset timeline for new query
    this.timelineEvents = [];
    this.isThinking = false;
    this.thinkingDuration = 0;
    
    const userMessage: Message = {
      id: this.generateId(),
      text: this.queryText,
      sender: 'user',
      timestamp: new Date()
    };
    
    this.messages.push(userMessage);
    this.shouldScroll = true;
    this.currentMessageId = userMessage.id;
    
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
    return new Date(date).toLocaleTimeString('en-US', { 
      hour: 'numeric', 
      minute: '2-digit', 
      hour12: true 
    });
  }

  private generateId(): string {
    return Math.random().toString(36).substr(2, 9);
  }  
  
  private createChartOptions(data: any): Partial<ChartOptions> {
    console.log('createChartOptions called with data:', JSON.stringify(data, null, 2));
    
    // Parse VIZ tag if present
    let chartData: any[] = [];
    let chartType = data?.type || 'column';
    
    console.log('Creating chart options with data:', data);
    
    if (data.vizTag) {
      const vizRegex = /\[VIZ:(\w+):([^\]]+)\]\s*([^[]*)\[\/VIZ\]/s;
      const match = data.vizTag.match(vizRegex);
      
      console.log('VIZ tag match:', match);
      
      if (match) {
        chartType = match[1];
        const dataContent = match[3].trim();
        
        console.log('Chart type:', chartType);
        console.log('Data content:', dataContent);
        
        if (dataContent) {
          // Parse the data based on chart type
          if (chartType === 'pie') {
            // For pie charts: parse label:value pairs
            const pairs = dataContent.split(',');
            chartData = pairs.map(pair => {
              const colonIndex = pair.lastIndexOf(':');
              if (colonIndex === -1) return null;
              const label = pair.substring(0, colonIndex).trim();
              const value = pair.substring(colonIndex + 1).trim();
              return { x: label || 'Unknown', y: parseFloat(value) || 0 };
            }).filter(item => item !== null && !isNaN(item.y));
          } else if (chartType === 'scatter') {
            // For scatter plots: parse x|y pairs
            const pairs = dataContent.split(',');
            chartData = pairs.map(pair => {
              const [x, y] = pair.split('|');
              return { x: parseFloat(x) || 0, y: parseFloat(y) || 0 };
            }).filter(item => !isNaN(item.x) && !isNaN(item.y));
          } else {
            // For line/column/area charts: parse label:value pairs
            const pairs = dataContent.split(',');
            chartData = pairs.map(pair => {
              const colonIndex = pair.lastIndexOf(':');
              if (colonIndex === -1) return null;
              const label = pair.substring(0, colonIndex).trim();
              const value = pair.substring(colonIndex + 1).trim();
              return { x: label || 'Unknown', y: parseFloat(value) || 0 };
            }).filter(item => item !== null && !isNaN(item.y));
          }
          
          console.log('Parsed chart data:', chartData);
        }
      }
    }
    
    // If no VIZ tag data, fall back to accumulated data
    if (chartData.length === 0 && this.accumulatedData && this.accumulatedData.length > 0) {
      try {
        // Use the first two columns of accumulated data
        chartData = this.accumulatedData.map((item: any) => {
          if (item && typeof item === 'object') {
            const values = Object.values(item);
            return {
              x: values[0] || 'Unknown',
              y: parseFloat(values[1] as string) || 0
            };
          }
          return { x: 'Unknown', y: 0 };
        }).filter(item => item.x !== 'Unknown');
      } catch (error) {
        console.error('Error processing accumulated data:', error);
        chartData = [];
      }
    }

    // Ensure we have valid data - provide default empty data if none exists
    if (!chartData || chartData.length === 0) {
      chartData = [{ x: 'No Data', y: 0 }];
    }

    console.log('Processed chartData:', JSON.stringify(chartData, null, 2));
    console.log('Chart type:', chartType);

    // Build chart configuration based on type - ensure all properties are safely accessed
    const baseConfig: Partial<ChartOptions> = {
      chart: {
        height: 350,
        type: chartType as any,
        animations: {
          enabled: true,
          easing: 'easeinout',
          speed: 800,
          animateGradually: {
            enabled: true,
            delay: 150
          }
        },
        toolbar: {
          show: false
        },
        zoom: {
          enabled: false
        },
        selection: {
          enabled: false
        }
      },
      dataLabels: {
        enabled: false
      },
      title: {
        text: (data && data.title) ? String(data.title) : 'Chart',
        align: 'left',
        style: {
          fontSize: '16px',
          fontWeight: 600,
          color: '#1f2937'
        }
      },
      grid: {
        borderColor: '#e5e7eb',
        strokeDashArray: 4
      }
    };

    // Get column names from data or accumulated data
    let xAxisLabel = 'Category';
    let yAxisLabel = 'Value';
    let seriesName = 'Values';
    
    if (data.columns && data.columns.length >= 2) {
      xAxisLabel = data.columns[0];
      yAxisLabel = data.columns[1];
      seriesName = data.columns[1];
    } else if (this.accumulatedData.length > 0) {
      const columns = Object.keys(this.accumulatedData[0]);
      if (columns.length >= 2) {
        xAxisLabel = columns[0];
        yAxisLabel = columns[1];
        seriesName = columns[1];
      }
    }
    
    // Configure based on chart type
    if (chartType === 'pie') {
      return {
        ...baseConfig,
        // series: seriesData.length > 0 ? seriesData : [0],
        // labels: labelsData.length > 0 ? labelsData : ['No Data'],
        series: chartData.map(d => d.y),
        labels: chartData.map(d => String(d.x)),
        legend: {
          position: 'bottom'
        }
      };
    } else {
      // Ensure we have valid series data for other chart types
      const validChartData = Array.isArray(chartData) ? chartData : [{ x: 'No Data', y: 0 }];
      const seriesData = validChartData.length > 0 ? validChartData.map(item => ({
        x: (item && item.x !== undefined) ? item.x : 'Unknown',
        y: (item && typeof item.y === 'number') ? item.y : 0
      })) : [{ x: 'No Data', y: 0 }];
      
      console.log('Other chart seriesData:', JSON.stringify(seriesData, null, 2));
        const chartConfig: Partial<ChartOptions> = {
        ...baseConfig,
        series: [{
          name: seriesName,
          data: chartData
        }],
        xaxis: {
          type: 'category',
          title: {
            text: xAxisLabel,
            style: {
              color: '#4b5563',
              fontSize: '12px',
              fontWeight: 600
            }
          },
          labels: {
            style: {
              colors: '#6b7280'
            },
            rotate: -45,
            rotateAlways: chartData.length > 5,
            trim: true,
            maxHeight: 100
          }
        },
        yaxis: {
          title: {
            text: yAxisLabel,
            style: {
              color: '#4b5563',
              fontSize: '12px',
              fontWeight: 600
            }
          },
          labels: {
            style: {
              colors: '#6b7280'
            },
            formatter: (value: number) => {
              // Format based on the column name or value type
              if (yAxisLabel.toLowerCase().includes('count') || 
                  yAxisLabel.toLowerCase().includes('quantity') ||
                  yAxisLabel.toLowerCase().includes('number')) {
                return Math.round(value).toLocaleString();
              } else if (yAxisLabel.toLowerCase().includes('amount') || 
                         yAxisLabel.toLowerCase().includes('revenue') ||
                         yAxisLabel.toLowerCase().includes('price') ||
                         yAxisLabel.toLowerCase().includes('cost') ||
                         yAxisLabel.toLowerCase().includes('salary')) {
                return '$' + value.toLocaleString();
              } else if (yAxisLabel.toLowerCase().includes('percent') || 
                         yAxisLabel.toLowerCase().includes('rate')) {
                return value.toFixed(1) + '%';
              }
              return value.toLocaleString();
            }
          }
        },
        stroke: chartType === 'line' || chartType === 'area' ? {
          curve: 'smooth',
          width: 3,
          colors: ['#22805a']
        } : undefined,
        tooltip: {
          y: {
            formatter: (value: number) => {
              // Use same formatting as Y axis
              if (yAxisLabel.toLowerCase().includes('count') || 
                  yAxisLabel.toLowerCase().includes('quantity') ||
                  yAxisLabel.toLowerCase().includes('number')) {
                return Math.round(value).toLocaleString();
              } else if (yAxisLabel.toLowerCase().includes('amount') || 
                         yAxisLabel.toLowerCase().includes('revenue') ||
                         yAxisLabel.toLowerCase().includes('price') ||
                         yAxisLabel.toLowerCase().includes('cost') ||
                         yAxisLabel.toLowerCase().includes('salary')) {
                return '$' + value.toLocaleString();
              } else if (yAxisLabel.toLowerCase().includes('percent') || 
                         yAxisLabel.toLowerCase().includes('rate')) {
                return value.toFixed(1) + '%';
              }
              return value.toLocaleString();
            }
          }
        }
      };
      
      console.log('Final chart config:', JSON.stringify(chartConfig, null, 2));
      return chartConfig;
    }
  }
}