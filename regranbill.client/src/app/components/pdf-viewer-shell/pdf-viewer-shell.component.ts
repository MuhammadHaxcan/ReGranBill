import { Component, EventEmitter, Input, Output } from '@angular/core';
import { SafeResourceUrl } from '@angular/platform-browser';

@Component({
  selector: 'app-pdf-viewer-shell',
  templateUrl: './pdf-viewer-shell.component.html',
  styleUrl: './pdf-viewer-shell.component.css',
  standalone: false
})
export class PdfViewerShellComponent {
  @Input() pdfUrl: SafeResourceUrl | null = null;
  @Input() loading = true;
  @Input() error = '';
  @Output() loaded = new EventEmitter<void>();

  onIframeLoad(): void {
    this.loaded.emit();
  }
}
