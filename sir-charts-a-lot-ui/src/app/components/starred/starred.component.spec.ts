import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StarredComponent } from './starred.component';

describe('StarredComponent', () => {
  let component: StarredComponent;
  let fixture: ComponentFixture<StarredComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StarredComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(StarredComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
