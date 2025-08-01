import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TextVisualization, TextFormatType, SingleValueMetadata } from '../../models/unified-visualization';

@Component({
  selector: 'app-text-response',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './text-response.component.html',
  styleUrls: ['./text-response.component.css']
})
export class TextResponseComponent implements OnInit {
  @Input() textConfig!: TextVisualization;
  
  formattedContent: string = '';
  TextFormatType = TextFormatType;

  ngOnInit() {
    this.formatContent();
  }

  get shouldShowUnit(): boolean {
    if (!this.textConfig.singleValueMetadata?.unit) {
      return false;
    }
    
    const unit = this.textConfig.singleValueMetadata.unit;
    
    // Don't show unit if it's already included in the formatted content
    // This handles cases like "%" when the content is "20.1% of users..."
    return !this.formattedContent.includes(unit);
  }

  private formatContent() {
    switch (this.textConfig.formatType) {
      case TextFormatType.Number:
        this.formattedContent = this.formatNumber(this.textConfig.content);
        break;
      case TextFormatType.Currency:
        this.formattedContent = this.formatCurrency(this.textConfig.content);
        break;
      case TextFormatType.Percentage:
        this.formattedContent = this.formatPercentage(this.textConfig.content);
        break;
      case TextFormatType.Markdown:
      case TextFormatType.Summary:
        this.formattedContent = this.textConfig.content;
        break;
      default:
        this.formattedContent = this.textConfig.content;
    }
  }

  private formatNumber(value: string): string {
    const num = parseFloat(value);
    if (isNaN(num)) return value;
    
    return new Intl.NumberFormat('en-US', {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2
    }).format(num);
  }

  private formatCurrency(value: string): string {
    const num = parseFloat(value);
    if (isNaN(num)) return value;
    
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD'
    }).format(num);
  }

  private formatPercentage(value: string): string {
    // For percentage format, the backend might send:
    // 1. Just a number like "21.23" 
    // 2. A number with % like "21.23%"
    // 3. A full sentence with % like "21.23% of users..."
    
    // If it's already a formatted string with text, return as-is
    if (value.includes('%')) {
      return value;
    }
    
    // Try to parse as a number
    const num = parseFloat(value);
    if (isNaN(num)) {
      // If it's not a number, return the original string
      return value;
    }
    
    // Format the number as a percentage
    // If the number is greater than 1, assume it's already in percentage form (e.g., 21.23)
    // If it's between 0 and 1, it needs to be multiplied by 100
    if (num > 1 || num < -1) {
      return `${num.toFixed(2)}%`;
    } else {
      return `${(num * 100).toFixed(2)}%`;
    }
  }

  getComparisonIcon(): string {
    if (!this.textConfig.singleValueMetadata?.comparison) {
      return '';
    }
    
    const direction = this.textConfig.singleValueMetadata.comparison.direction;
    switch (direction) {
      case 'up':
        return '↑';
      case 'down':
        return '↓';
      case 'stable':
        return '→';
      default:
        return '';
    }
  }

  getComparisonClass(): string {
    if (!this.textConfig.singleValueMetadata?.comparison) {
      return '';
    }
    
    const direction = this.textConfig.singleValueMetadata.comparison.direction;
    const changePercentage = this.textConfig.singleValueMetadata.comparison.changePercentage;
    
    // For positive metrics, up is good
    if (direction === 'up') {
      return changePercentage > 10 ? 'comparison-strong-positive' : 'comparison-positive';
    } else if (direction === 'down') {
      return changePercentage < -10 ? 'comparison-strong-negative' : 'comparison-negative';
    }
    
    return 'comparison-neutral';
  }

  formatComparison(): string {
    const comparison = this.textConfig.singleValueMetadata?.comparison;
    if (!comparison) return '';
    
    const percentageStr = Math.abs(comparison.changePercentage).toFixed(1);
    const amountStr = this.formatNumber(Math.abs(comparison.changeAmount).toString());
    
    return `${percentageStr}% (${amountStr})`;
  }
}