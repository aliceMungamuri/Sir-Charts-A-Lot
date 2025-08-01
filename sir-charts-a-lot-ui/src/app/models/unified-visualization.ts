// Unified Visualization Response Models

export enum ResponseType {
  Chart = 'Chart',
  Table = 'Table',
  Text = 'Text',
  Mixed = 'Mixed'
}

export interface UnifiedVisualizationResponse {
  responseType: ResponseType;
  confidence: number;
  selectionReasoning: string;
  chartConfig?: ChartVisualization;
  tableConfig?: TableVisualization;
  textConfig?: TextVisualization;
  secondaryVisualizations?: SecondaryVisualization[];
}

export enum ChartType {
  Line = 'Line',
  Area = 'Area',
  Column = 'Column',
  Bar = 'Bar',
  Pie = 'Pie',
  Donut = 'Donut',
  RadialBar = 'RadialBar',
  Scatter = 'Scatter',
  Bubble = 'Bubble',
  Heatmap = 'Heatmap',
  Treemap = 'Treemap',
  Candlestick = 'Candlestick',
  BoxPlot = 'BoxPlot',
  Radar = 'Radar',
  PolarArea = 'PolarArea',
  RangeBar = 'RangeBar',
  Funnel = 'Funnel'
}

export interface ChartVisualization {
  chartType: ChartType;
  apexChartOptions: any; // ApexCharts options
  dataTransformation?: DataTransformation;
  fallbackChartType?: ChartType;
}

export interface TableVisualization {
  columns: TableColumn[];
  enablePagination: boolean;
  pageSize: number;
  enableSorting: boolean;
  enableFiltering: boolean;
  enableExport: boolean;
  conditionalFormats?: ConditionalFormat[];
}

export interface TableColumn {
  key: string;
  displayName: string;
  dataType: ColumnDataType;
  format?: string;
  width?: string;
  sortable: boolean;
  showSparkline?: boolean;
}

export enum ColumnDataType {
  String = 'String',
  Number = 'Number',
  Currency = 'Currency',
  Percentage = 'Percentage',
  Date = 'Date',
  DateTime = 'DateTime',
  Boolean = 'Boolean',
  Link = 'Link'
}

export interface TextVisualization {
  content: string;
  formatType: TextFormatType;
  isSingleValue: boolean;
  singleValueMetadata?: SingleValueMetadata;
  useMarkdown: boolean;
  highlights?: string[];
}

export enum TextFormatType {
  Plain = 'Plain',
  Number = 'Number',
  Currency = 'Currency',
  Percentage = 'Percentage',
  Markdown = 'Markdown',
  Summary = 'Summary'
}

export interface SingleValueMetadata {
  numericValue?: number;
  unit?: string;
  comparison?: ComparisonData;
  indicator?: string;
}

export interface ComparisonData {
  previousValue: number;
  changeAmount: number;
  changePercentage: number;
  direction: 'up' | 'down' | 'stable';
}

export interface DataTransformation {
  type: TransformationType;
  config?: Record<string, any>;
}

export enum TransformationType {
  None = 'None',
  Pivot = 'Pivot',
  Aggregate = 'Aggregate',
  Hierarchical = 'Hierarchical',
  Matrix = 'Matrix',
  TimeSeries = 'TimeSeries',
  OHLC = 'OHLC'
}

export interface SecondaryVisualization {
  order: number;
  type: ResponseType;
  configuration: any;
}

export interface ConditionalFormat {
  columnKey: string;
  condition: ConditionType;
  value: any;
  style: string;
}

export enum ConditionType {
  Equals = 'Equals',
  NotEquals = 'NotEquals',
  GreaterThan = 'GreaterThan',
  LessThan = 'LessThan',
  GreaterThanOrEqual = 'GreaterThanOrEqual',
  LessThanOrEqual = 'LessThanOrEqual',
  Contains = 'Contains',
  StartsWith = 'StartsWith',
  EndsWith = 'EndsWith'
}

// Enhanced message types for SignalR
export interface UnifiedVisualizationMessage {
  responseType: ResponseType;
  title: string;
  confidence: number;
  selectionReasoning: string;
  serializedConfig: string; // JSON serialized config
  columns?: string[];
  totalRowCount?: number;
}

export interface TableDataMessage {
  rows: any[];
  columnDefinitions: TableColumn[];
  isComplete: boolean;
  totalRowCount: number;
  currentPage: number;
  pageSize: number;
}

export interface TextResponseMessage {
  content: string;
  formatType: TextFormatType;
  isSingleValue: boolean;
  metadata?: SingleValueMetadata;
  highlights?: string[];
}