import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { ChatComponent } from './components/chat/chat.component';
import { EnhancedChatComponent } from './components/enhanced-chat/enhanced-chat.component';
import { MyCollectionsComponent } from './components/my-collections/my-collections.component';
import { StarredComponent } from './components/starred/starred.component';

export const routes: Routes = [
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: 'home', component: HomeComponent },
  { path: 'chat', component: ChatComponent },
  { path: 'enhanced-chat', component: EnhancedChatComponent },
  { path: 'my-collections', component: MyCollectionsComponent },
  { path: 'starred', component: StarredComponent }
];