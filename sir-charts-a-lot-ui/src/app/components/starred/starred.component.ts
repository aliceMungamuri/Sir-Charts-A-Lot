import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NgApexchartsModule } from 'ng-apexcharts';
import {
  ApexAxisChartSeries,
  ApexChart,
  ApexXAxis,
  ApexYAxis,
  ApexDataLabels,
  ApexTitleSubtitle,
  ApexLegend,
  ApexTooltip,
  ApexPlotOptions,
  ApexGrid
} from 'ng-apexcharts';

export type ChartOptions = {
  series: ApexAxisChartSeries | number[];
  chart: ApexChart;
  xaxis?: ApexXAxis;
  yaxis?: ApexYAxis;
  title?: ApexTitleSubtitle;
  labels?: string[];
  dataLabels?: ApexDataLabels;
  legend?: ApexLegend;
  tooltip?: ApexTooltip;
  plotOptions?: ApexPlotOptions;
  grid?: ApexGrid;
};

interface StarredChart {
  id: string;
  query: string;
  date: string;
  chartOptions: Partial<ChartOptions>;
}

@Component({
  selector: 'app-starred',
  standalone: true,
  imports: [CommonModule, RouterLink, NgApexchartsModule],
  templateUrl: './starred.component.html',
  styleUrl: './starred.component.css'
})
export class StarredComponent implements OnInit {
  viewMode: 'grid' | 'list' = 'grid';
  
  starredCharts: StarredChart[] = [
    {
      id: '1',
      query: 'Help me with Users by filing status',
      date: 'June 19, 2024',
      chartOptions: {
        series: [45, 32, 15, 8],
        chart: {
          type: 'pie',
          height: 300
        },
        labels: ['Single', 'Married Filing Jointly', 'Head of Household', 'Married Filing Separately'],
        title: {
          text: 'Users by Filing Status',
          align: 'left',
          style: {
            fontSize: '16px',
            fontWeight: 600,
            color: '#1f2937'
          }
        },
        dataLabels: {
          enabled: true,
          formatter: (val: number) => {
            return val.toFixed(0) + '%';
          }
        },
        legend: {
          position: 'bottom'
        },
        tooltip: {
          y: {
            formatter: (val: number) => {
              return val + '% of users';
            }
          }
        }
      }
    },
    {
      id: '2',
      query: 'Users who signed 7216 vs those who did not',
      date: 'June 10, 2024',
      chartOptions: {
        series: [{
          name: 'Form 7216 Status',
          data: [
            { x: 'Signed 7216', y: 8543 },
            { x: 'Did Not Sign', y: 2187 }
          ]
        }],
        chart: {
          type: 'bar',
          height: 300
        },
        plotOptions: {
          bar: {
            horizontal: true,
            distributed: true
          }
        },
        xaxis: {
          title: {
            text: 'Number of Users',
            style: {
              color: '#4b5563',
              fontSize: '12px',
              fontWeight: 600
            }
          }
        },
        yaxis: {
          title: {
            text: ''
          }
        },
        title: {
          text: 'Form 7216 Consent Status',
          align: 'left',
          style: {
            fontSize: '16px',
            fontWeight: 600,
            color: '#1f2937'
          }
        },
        dataLabels: {
          enabled: true,
          formatter: (val: number) => {
            return val.toLocaleString();
          }
        },
        legend: {
          show: false
        },
        tooltip: {
          y: {
            formatter: (val: number) => {
              return val.toLocaleString() + ' users';
            }
          }
        }
      }
    },
    {
      id: '3',
      query: 'Users by Product SKU - bar chart',
      date: 'June 19, 2024',
      chartOptions: {
        series: [{
          name: 'Users',
          data: [
            { x: 'Basic', y: 3420 },
            { x: 'Deluxe', y: 5680 },
            { x: 'Premier', y: 2890 },
            { x: 'Self-Employed', y: 1560 },
            { x: 'Business', y: 980 }
          ]
        }],
        chart: {
          type: 'bar',
          height: 300
        },
        xaxis: {
          title: {
            text: 'Product SKU',
            style: {
              color: '#4b5563',
              fontSize: '12px',
              fontWeight: 600
            }
          }
        },
        yaxis: {
          title: {
            text: 'Number of Users',
            style: {
              color: '#4b5563',
              fontSize: '12px',
              fontWeight: 600
            }
          },
          labels: {
            formatter: (val: number) => {
              return val.toLocaleString();
            }
          }
        },
        title: {
          text: 'Users by Product SKU',
          align: 'left',
          style: {
            fontSize: '16px',
            fontWeight: 600,
            color: '#1f2937'
          }
        },
        dataLabels: {
          enabled: false
        },
        plotOptions: {
          bar: {
            distributed: true,
            borderRadius: 4
          }
        },
        grid: {
          borderColor: '#e5e7eb',
          strokeDashArray: 4
        },
        tooltip: {
          y: {
            formatter: (val: number) => {
              return val.toLocaleString() + ' users';
            }
          }
        }
      }
    }
  ];

  constructor(private router: Router) {}

  ngOnInit(): void {}

  toggleView(mode: 'grid' | 'list'): void {
    this.viewMode = mode;
  }

  unstartChart(event: Event, chart: StarredChart): void {
    event.stopPropagation();
    console.log('Unstarring chart:', chart.query);
    // TODO: Implement unstar functionality
  }

  showMoreOptions(event: Event, chart: StarredChart): void {
    event.stopPropagation();
    console.log('More options for:', chart.query);
    // TODO: Show dropdown menu with options
  }

  createVisualRepresentation(chart: StarredChart): void {
    console.log('Creating visual representation for:', chart.query);
    // Navigate to chat with the query
    this.router.navigate(['/chat'], { 
      queryParams: { q: chart.query }
    });
  }
}