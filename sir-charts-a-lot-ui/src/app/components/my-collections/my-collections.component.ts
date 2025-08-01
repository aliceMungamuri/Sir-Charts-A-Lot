import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';

interface Collection {
  id: string;
  name: string;
  itemCount: number;
}

@Component({
  selector: 'app-my-collections',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './my-collections.component.html',
  styleUrl: './my-collections.component.css'
})
export class MyCollectionsComponent implements OnInit {
  collections: Collection[] = [
    { id: '1', name: 'TPR', itemCount: 11 },
    { id: '2', name: 'Block Advisors', itemCount: 11 },
    { id: '3', name: 'Conversion', itemCount: 11 },
    { id: '4', name: 'Take-Rate', itemCount: 11 },
    { id: '5', name: 'Utilization', itemCount: 11 },
    { id: '6', name: 'By Sku', itemCount: 11 },
    { id: '7', name: 'Spruce', itemCount: 11 },
    { id: '8', name: 'DIY', itemCount: 11 },
    { id: '9', name: 'Small Biz', itemCount: 11 },
    { id: '10', name: 'Pricing', itemCount: 11 },
    { id: '11', name: 'Financial Services 2025', itemCount: 11 },
    { id: '12', name: 'Financial Services 2024', itemCount: 11 }
  ];

  constructor(private router: Router) {}

  ngOnInit(): void {}

  openCollection(collection: Collection): void {
    console.log('Opening collection:', collection.name);
    // TODO: Navigate to collection detail page
  }

  showMoreOptions(event: Event, collection: Collection): void {
    event.stopPropagation();
    console.log('More options for:', collection.name);
    // TODO: Show dropdown menu with options
  }
}