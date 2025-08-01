import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface TimelineEvent {
  id: string;
  agent: 'domain-expert' | 'sql-expert' | 'executor' | 'viz-agent';
  stage: string;
  message: string;
  timestamp: Date;
  status: 'active' | 'completed' | 'error';
  details?: string[];
}

@Component({
  selector: 'app-timeline',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './timeline.component.html',
  styleUrls: ['./timeline.component.css']
})
export class TimelineComponent implements OnInit, OnDestroy {
  @Input() events: TimelineEvent[] = [];
  @Input() isThinking: boolean = false;
  @Input() thinkingDuration: number = 0;
  
  isExpanded = false;
  startTime: Date | null = null;
  elapsedTime = 0;
  private intervalId: any;

  ngOnInit() {
    // Start expanded when actively thinking
    if (this.isThinking) {
      this.isExpanded = true;
      if (!this.startTime) {
        this.startTime = new Date();
        this.startTimer();
      }
    }
  }

  ngOnDestroy() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }

  private startTimer() {
    this.intervalId = setInterval(() => {
      if (this.startTime) {
        this.elapsedTime = Math.floor((new Date().getTime() - this.startTime.getTime()) / 1000);
      }
    }, 100);
  }

  stopThinking() {
    this.isThinking = false;
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }

  toggleExpanded() {
    this.isExpanded = !this.isExpanded;
  }

  getAgentIcon(agent: string): string {
    switch (agent) {
      case 'domain-expert':
        return '🧠';
      case 'sql-expert':
        return '💾';
      case 'executor':
        return '⚡';
      case 'viz-agent':
        return '📊';
      default:
        return '•';
    }
  }

  getAgentColor(agent: string): string {
    switch (agent) {
      case 'domain-expert':
        return 'text-blue-600';
      case 'sql-expert':
        return 'text-purple-600';
      case 'executor':
        return 'text-green-600';
      case 'viz-agent':
        return 'text-orange-600';
      default:
        return 'text-gray-600';
    }
  }

  getAgentName(agent: string): string {
    switch (agent) {
      case 'domain-expert':
        return 'Domain Expert';
      case 'sql-expert':
        return 'SQL Expert';
      case 'executor':
        return 'Query Executor';
      case 'viz-agent':
        return 'Visualization Agent';
      default:
        return 'Agent';
    }
  }
}