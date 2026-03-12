import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface ConfirmModal {
  title: string;
  message: string;
  type: 'confirm' | 'info';
  confirmText?: string;
  cancelText?: string;
  onConfirm?: () => void;
}

@Injectable({ providedIn: 'root' })
export class ConfirmModalService {
  private modalSubject = new Subject<ConfirmModal | null>();
  modal$ = this.modalSubject.asObservable();

  confirm(options: { title: string; message: string; confirmText?: string; cancelText?: string }): Promise<boolean> {
    return new Promise(resolve => {
      this.modalSubject.next({
        title: options.title,
        message: options.message,
        type: 'confirm',
        confirmText: options.confirmText || 'Confirm',
        cancelText: options.cancelText || 'Cancel',
        onConfirm: () => resolve(true)
      });

      const sub = this.modal$.subscribe(val => {
        if (val === null) {
          resolve(false);
          sub.unsubscribe();
        }
      });
    });
  }

  info(options: { title: string; message: string }): void {
    this.modalSubject.next({
      title: options.title,
      message: options.message,
      type: 'info'
    });
  }

  close(): void {
    this.modalSubject.next(null);
  }
}
