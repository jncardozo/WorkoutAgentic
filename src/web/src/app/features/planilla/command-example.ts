import { ChangeDetectionStrategy, Component, ElementRef, viewChild } from '@angular/core';

@Component({
  selector: 'app-command-example',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './command-example.html',
  styleUrl: './command-example.scss',
})
export class CommandExample {
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('exampleDialog');

  open(): void {
    const dialog = this.dialog().nativeElement;
    if (!dialog.open) dialog.showModal();
  }

  close(): void {
    this.dialog().nativeElement.close();
  }

  closeOnBackdrop(event: MouseEvent): void {
    const dialog = this.dialog().nativeElement;
    if (event.target !== dialog) return;
    const bounds = dialog.getBoundingClientRect();
    if (event.clientX < bounds.left || event.clientX > bounds.right
      || event.clientY < bounds.top || event.clientY > bounds.bottom) this.close();
  }
}
