import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

interface PromptCard {
  id: string;
  title: string;
  category: string;
  color: string;
  gradient: string;
  icon?: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent {
  searchQuery = '';
  selectedCategory = 'All';
  useEnhancedChat = true; // Default to enhanced chat
  
  categories = ['All', 'Tax Analysis', 'User Demographics', 'Compliance', 'Revenue Insights', 'Product Usage'];
  
  promptCards: PromptCard[] = [
    {
      id: '1',
      title: 'Help me with Users by filing status',
      category: 'User Demographics',
      color: 'bg-gradient-to-br from-blue-600 to-purple-700',
      gradient: 'linear-gradient(135deg, #2563eb 0%, #7c3aed 100%)'
    },
    {
      id: '2',
      title: 'Percentage of users who had Schedule C form',
      category: 'Tax Analysis',
      color: 'bg-gradient-to-br from-orange-500 to-red-600',
      gradient: 'linear-gradient(135deg, #f97316 0%, #dc2626 100%)'
    },
    {
      id: '3',
      title: 'Users who signed 7216 vs those who did not',
      category: 'Compliance',
      color: 'bg-gradient-to-br from-teal-500 to-green-600',
      gradient: 'linear-gradient(135deg, #14b8a6 0%, #16a34a 100%)'
    },
    {
      id: '4',
      title: 'Users with Schedule C form grouped by AGI in 5k increments',
      category: 'Tax Analysis',
      color: 'bg-gradient-to-br from-amber-500 to-orange-600',
      gradient: 'linear-gradient(135deg, #f59e0b 0%, #ea580c 100%)'
    },
    {
      id: '5',
      title: 'What percentage of users e-filed vs paper filed?',
      category: 'Tax Analysis',
      color: 'bg-gradient-to-br from-indigo-500 to-blue-700',
      gradient: 'linear-gradient(135deg, #6366f1 0%, #1d4ed8 100%)'
    },
    {
      id: '6',
      title: 'Average refund amount by state',
      category: 'Tax Analysis',
      color: 'bg-gradient-to-br from-pink-500 to-rose-600',
      gradient: 'linear-gradient(135deg, #ec4899 0%, #e11d48 100%)'
    },
    {
      id: '7',
      title: 'How many users claimed child tax credit?',
      category: 'Tax Analysis',
      color: 'bg-gradient-to-br from-yellow-400 to-amber-500',
      gradient: 'linear-gradient(135deg, #facc15 0%, #f59e0b 100%)'
    },
    {
      id: '8',
      title: 'User conversion rate by acquisition channel',
      category: 'User Demographics',
      color: 'bg-gradient-to-br from-purple-500 to-indigo-700',
      gradient: 'linear-gradient(135deg, #a855f7 0%, #4338ca 100%)'
    },
    {
      id: '9',
      title: 'Tax preparation completion time by product',
      category: 'Product Usage',
      color: 'bg-gradient-to-br from-slate-600 to-slate-800',
      gradient: 'linear-gradient(135deg, #475569 0%, #1e293b 100%)'
    }
  ];

  constructor(private router: Router) {}

  get filteredCards() {
    if (this.selectedCategory === 'All') {
      return this.promptCards;
    }
    return this.promptCards.filter(card => card.category === this.selectedCategory);
  }

  onSearch() {
    console.log('onSearch called with:', this.searchQuery);
    if (this.searchQuery.trim()) {
      // Navigate to appropriate chat based on toggle
      const chatRoute = this.useEnhancedChat ? '/enhanced-chat' : '/chat';
      this.router.navigate([chatRoute], { 
        queryParams: { q: this.searchQuery }
      });
    }
  }

  selectCard(card: PromptCard) {
    // Navigate to appropriate chat based on toggle
    const chatRoute = this.useEnhancedChat ? '/enhanced-chat' : '/chat';
    this.router.navigate([chatRoute], { 
      queryParams: { q: card.title }
    });
  }

  toggleFavorite(event: Event, card: PromptCard) {
    event.stopPropagation();
    // TODO: Implement favorite toggle
    console.log('Toggle favorite for', card.id);
  }
}