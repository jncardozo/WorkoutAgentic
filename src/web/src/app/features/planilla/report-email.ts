import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Injector, afterNextRender, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Subscription } from 'rxjs';
import { FormatoPlanillaExportable, PlanillaApi, PlanillaConfirmadaVista, fallaDe } from '../../core/planilla-api';

@Component({
  selector: 'app-report-email',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  templateUrl: './report-email.html',
  styleUrl: './report-email.scss',
})
export class ReportEmail {
  private readonly api = inject(PlanillaApi);
  readonly plan = input.required<PlanillaConfirmadaVista>();
  readonly studentId = input.required<number>();
  readonly initialFormat = input<FormatoPlanillaExportable>('pdf');
  readonly recipient = signal('');
  readonly format = signal<FormatoPlanillaExportable>('pdf');
  readonly sending = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly isOpen = signal(false);
  readonly configured = signal<boolean | null>(null);
  readonly checkingConfiguration = signal(false);
  readonly configurationError = signal(false);
  private readonly recipientInput = viewChild<ElementRef<HTMLInputElement>>('recipientInput');
  private readonly launchButton = viewChild<ElementRef<HTMLButtonElement>>('launchButton');
  private readonly injector = inject(Injector);
  private request?: Subscription;
  private configurationRequest?: Subscription;

  constructor() {
    effect(() => {
      this.plan();
      this.studentId();
      untracked(() => {
        this.request?.unsubscribe();
        this.configurationRequest?.unsubscribe();
        this.isOpen.set(false);
        this.configured.set(null);
        this.checkingConfiguration.set(false);
        this.configurationError.set(false);
        this.recipient.set('');
        this.sending.set(false);
        this.message.set(null);
        this.error.set(null);
      });
    });
    effect(() => {
      const format = this.initialFormat();
      untracked(() => {
        this.format.set(format);
        this.message.set(null);
        this.error.set(null);
      });
    });
    inject(DestroyRef).onDestroy(() => {
      this.request?.unsubscribe();
      this.configurationRequest?.unsubscribe();
    });
  }

  open(): void {
    this.isOpen.set(true);
    this.checkConfiguration();
  }

  close(): void {
    if (this.sending()) return;
    this.configurationRequest?.unsubscribe();
    this.checkingConfiguration.set(false);
    this.isOpen.set(false);
    afterNextRender(() => this.launchButton()?.nativeElement.focus(), { injector: this.injector });
  }

  checkConfiguration(): void {
    if (this.sending() || this.checkingConfiguration()) return;
    this.checkingConfiguration.set(true);
    this.configurationError.set(false);
    this.configured.set(null);
    this.configurationRequest = this.api.emailConfigurationStatus().subscribe({
      next: status => {
        this.configured.set(status.configured);
        this.checkingConfiguration.set(false);
        if (status.configured) {
          afterNextRender(() => this.focus(), { injector: this.injector });
        }
      },
      error: () => {
        this.checkingConfiguration.set(false);
        this.configurationError.set(true);
      },
    });
  }

  focus(): void {
    this.recipientInput()?.nativeElement.focus();
  }

  send(): void {
    if (this.sending() || this.configured() !== true || !this.isOpen()) return;
    const recipient = this.recipient().trim();
    this.message.set(null);
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(recipient)) {
      this.error.set('Escribí una dirección de correo válida.');
      this.focus();
      return;
    }
    this.error.set(null);
    this.sending.set(true);
    this.request = this.api.enviarPlanillaPorCorreo(this.plan().id, {
      alumnoId: this.studentId(), correo: recipient, formato: this.format(),
    }).subscribe({
      next: response => {
        this.sending.set(false);
        this.message.set(response.mensaje);
      },
      error: (error: unknown) => {
        this.sending.set(false);
        const failure = fallaDe(error);
        if (failure.estadoHttp === 503) this.configured.set(false);
        this.error.set(failure.estadoHttp === 503
          ? 'El envío por email no está disponible en este momento. Podés descargar la planilla.'
          : failure.estadoHttp === 502
            ? 'No pudimos enviar el email. Revisá la dirección e intentá nuevamente.'
            : failure.detalle ?? failure.titulo);
      },
    });
  }
}
