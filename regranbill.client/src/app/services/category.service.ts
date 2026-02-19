import { Injectable } from '@angular/core';
import { Category } from '../models/category.model';

@Injectable({
  providedIn: 'root'
})
export class CategoryService {
  private nextId = 7;

  private categories: Category[] = [
    { id: 1, name: 'Raw Materials' },
    { id: 2, name: 'Finished Goods' },
    { id: 3, name: 'Packaging' },
    { id: 4, name: 'Transport' },
    { id: 5, name: 'Office Supplies' },
    { id: 6, name: 'Utilities' },
  ];

  getAll(): Category[] {
    return [...this.categories];
  }

  add(name: string): Category | null {
    const trimmed = name.trim();
    if (!trimmed) return null;
    if (this.isDuplicate(trimmed)) return null;

    const category: Category = { id: this.nextId++, name: trimmed };
    this.categories.push(category);
    return category;
  }

  update(id: number, name: string): boolean {
    const trimmed = name.trim();
    if (!trimmed) return false;
    if (this.isDuplicate(trimmed, id)) return false;

    const cat = this.categories.find(c => c.id === id);
    if (!cat) return false;
    cat.name = trimmed;
    return true;
  }

  delete(id: number): boolean {
    const index = this.categories.findIndex(c => c.id === id);
    if (index === -1) return false;
    this.categories.splice(index, 1);
    return true;
  }

  isDuplicate(name: string, excludeId?: number): boolean {
    return this.categories.some(
      c => c.name.toLowerCase() === name.toLowerCase() && c.id !== excludeId
    );
  }
}
