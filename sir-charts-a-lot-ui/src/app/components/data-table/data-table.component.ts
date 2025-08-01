import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableColumn, ColumnDataType, ConditionalFormat, ConditionType } from '../../models/unified-visualization';

interface SortConfig {
  column: string;
  direction: 'asc' | 'desc';
}

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './data-table.component.html',
  styleUrls: ['./data-table.component.css']
})
export class DataTableComponent implements OnInit, OnChanges {
  @Input() columns: TableColumn[] = [];
  @Input() data: any[] = [];
  @Input() enablePagination: boolean = true;
  @Input() pageSize: number = 10;
  @Input() enableSorting: boolean = true;
  @Input() enableFiltering: boolean = true;
  @Input() enableExport: boolean = true;
  @Input() conditionalFormats?: ConditionalFormat[];

  // Internal state
  displayedData: any[] = [];
  filteredData: any[] = [];
  filterValue: string = '';
  currentPage: number = 1;
  totalPages: number = 1;
  sortConfig: SortConfig | null = null;
  
  // Pagination options
  pageSizeOptions: number[] = [5, 10, 25, 50, 100];
  
  ColumnDataType = ColumnDataType;
  Math = Math; // Expose Math to template

  ngOnInit() {
    this.initializeData();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['data'] || changes['columns']) {
      this.initializeData();
    }
  }

  private initializeData() {
    this.filteredData = [...this.data];
    this.updatePagination();
    this.updateDisplayedData();
  }

  // Filtering
  applyFilter() {
    if (!this.filterValue.trim()) {
      this.filteredData = [...this.data];
    } else {
      const searchTerm = this.filterValue.toLowerCase();
      this.filteredData = this.data.filter(row => {
        return this.columns.some(col => {
          const value = row[col.key];
          if (value === null || value === undefined) return false;
          return value.toString().toLowerCase().includes(searchTerm);
        });
      });
    }
    
    this.currentPage = 1;
    this.updatePagination();
    this.updateDisplayedData();
  }

  // Sorting
  sort(column: TableColumn) {
    if (!this.enableSorting || !column.sortable) return;

    if (this.sortConfig?.column === column.key) {
      // Toggle direction
      this.sortConfig.direction = this.sortConfig.direction === 'asc' ? 'desc' : 'asc';
    } else {
      // New column
      this.sortConfig = { column: column.key, direction: 'asc' };
    }

    this.filteredData.sort((a, b) => {
      const aVal = a[column.key];
      const bVal = b[column.key];

      if (aVal === null || aVal === undefined) return 1;
      if (bVal === null || bVal === undefined) return -1;

      let comparison = 0;
      if (aVal < bVal) comparison = -1;
      if (aVal > bVal) comparison = 1;

      return this.sortConfig!.direction === 'asc' ? comparison : -comparison;
    });

    this.updateDisplayedData();
  }

  getSortIcon(column: TableColumn): string {
    if (!this.sortConfig || this.sortConfig.column !== column.key) {
      return '↕️'; // Both arrows
    }
    return this.sortConfig.direction === 'asc' ? '↑' : '↓';
  }

  // Pagination
  updatePagination() {
    this.totalPages = Math.ceil(this.filteredData.length / this.pageSize);
    if (this.currentPage > this.totalPages) {
      this.currentPage = Math.max(1, this.totalPages);
    }
  }

  updateDisplayedData() {
    if (!this.enablePagination) {
      this.displayedData = this.filteredData;
    } else {
      const start = (this.currentPage - 1) * this.pageSize;
      const end = start + this.pageSize;
      this.displayedData = this.filteredData.slice(start, end);
    }
  }

  changePage(page: number) {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.updateDisplayedData();
    }
  }

  changePageSize() {
    this.currentPage = 1;
    this.updatePagination();
    this.updateDisplayedData();
  }

  getPageNumbers(): number[] {
    const pages: number[] = [];
    const maxButtons = 5;
    
    let start = Math.max(1, this.currentPage - Math.floor(maxButtons / 2));
    let end = Math.min(this.totalPages, start + maxButtons - 1);
    
    if (end - start + 1 < maxButtons) {
      start = Math.max(1, end - maxButtons + 1);
    }
    
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    
    return pages;
  }

  // Formatting
  formatCell(value: any, column: TableColumn): string {
    if (value === null || value === undefined) {
      return '';
    }

    switch (column.dataType) {
      case ColumnDataType.Currency:
        return this.formatCurrency(value);
      case ColumnDataType.Percentage:
        return this.formatPercentage(value);
      case ColumnDataType.Date:
        return this.formatDate(value);
      case ColumnDataType.DateTime:
        return this.formatDateTime(value);
      case ColumnDataType.Number:
        return this.formatNumber(value);
      case ColumnDataType.Boolean:
        return value ? '✓' : '✗';
      default:
        return value.toString();
    }
  }

  private formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD'
    }).format(value);
  }

  private formatPercentage(value: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'percent',
      minimumFractionDigits: 1,
      maximumFractionDigits: 1
    }).format(value / 100);
  }

  private formatDate(value: string | Date): string {
    const date = typeof value === 'string' ? new Date(value) : value;
    return new Intl.DateTimeFormat('en-US').format(date);
  }

  private formatDateTime(value: string | Date): string {
    const date = typeof value === 'string' ? new Date(value) : value;
    return new Intl.DateTimeFormat('en-US', {
      dateStyle: 'short',
      timeStyle: 'short'
    }).format(date);
  }

  private formatNumber(value: number): string {
    return new Intl.NumberFormat('en-US').format(value);
  }

  // Conditional formatting
  getCellClass(value: any, column: TableColumn): string {
    const baseClasses = 'px-6 py-4 whitespace-nowrap text-sm';
    let conditionalClasses = '';

    if (this.conditionalFormats) {
      const formats = this.conditionalFormats.filter(f => f.columnKey === column.key);
      for (const format of formats) {
        if (this.evaluateCondition(value, format.condition, format.value)) {
          conditionalClasses = format.style;
          break;
        }
      }
    }

    return `${baseClasses} ${conditionalClasses}`;
  }

  private evaluateCondition(value: any, condition: ConditionType, compareValue: any): boolean {
    switch (condition) {
      case ConditionType.Equals:
        return value === compareValue;
      case ConditionType.NotEquals:
        return value !== compareValue;
      case ConditionType.GreaterThan:
        return value > compareValue;
      case ConditionType.LessThan:
        return value < compareValue;
      case ConditionType.GreaterThanOrEqual:
        return value >= compareValue;
      case ConditionType.LessThanOrEqual:
        return value <= compareValue;
      case ConditionType.Contains:
        return value?.toString().includes(compareValue);
      case ConditionType.StartsWith:
        return value?.toString().startsWith(compareValue);
      case ConditionType.EndsWith:
        return value?.toString().endsWith(compareValue);
      default:
        return false;
    }
  }

  // Export
  exportToCSV() {
    const headers = this.columns.map(col => col.displayName).join(',');
    const rows = this.filteredData.map(row => 
      this.columns.map(col => {
        const value = row[col.key];
        const formatted = this.formatCell(value, col);
        // Escape quotes and wrap in quotes if contains comma
        return formatted.includes(',') || formatted.includes('"') 
          ? `"${formatted.replace(/"/g, '""')}"` 
          : formatted;
      }).join(',')
    );

    const csv = [headers, ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `data-export-${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  // For streaming data
  addData(newRows: any[]) {
    this.data = [...this.data, ...newRows];
    this.initializeData();
  }
}