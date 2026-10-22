import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

const CLAVE_ALUMNO = 'wa.alumnoId';

/**
 * Entrada provisional del alumno (hasta AuthApi, arquitectura S6): pide el
 * número de alumno, lo guarda en localStorage y abre su planilla. La vista de
 * planilla no cambia: solo el borde de identificación.
 */
@Component({
  selector: 'app-alumno-entrada',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      max-width: 520px;
      margin: 10vh auto 0;
    }

    h1 {
      font-size: 22px;
      margin-bottom: 6px;
    }

    p {
      color: var(--texto-2);
      font-size: 14px;
      margin: 0 0 16px;
    }

    .fila-entrada {
      display: flex;
      gap: 10px;
    }

    input {
      flex: 1;
      min-width: 0;
      background: var(--panel-2);
      border: 1px solid var(--borde);
      border-radius: 10px;
      color: var(--texto);
      font-family: inherit;
      font-size: 15px;
      padding: 10px 12px;
      outline: none;
    }

    input:focus {
      border-color: color-mix(in srgb, var(--acento) 55%, var(--borde));
    }

    .error {
      color: var(--peligro);
      font-size: 13px;
      margin: 10px 0 0;
    }

    .acceso-profesor {
      color: var(--texto-2);
      font-size: 13px;
      margin: 14px 0 0;
    }

    .acceso-profesor a {
      color: var(--acento);
    }
  `,
  template: `
    <section class="tarjeta">
      <h1>¿Quién carga su entrenamiento?</h1>
      <p>
        Identificación provisional hasta activar el login: escribí tu número de alumno y abrí tu
        planilla. Lo guardamos en este navegador para la próxima vez.
      </p>
      <div class="fila-entrada">
        <input
          #campo
          type="number"
          min="1"
          placeholder="Número de alumno"
          aria-label="Número de alumno"
          [value]="valor()"
          (input)="valor.set(campo.value)"
          (keydown.enter)="abrir()"
        />
        <button type="button" class="btn btn-primario" [disabled]="!idValido()" (click)="abrir()">
          Abrir planilla
        </button>
      </div>
      @if (error(); as mensaje) {
        <p class="error" role="alert">{{ mensaje }}</p>
      }
      <p class="acceso-profesor">
        ¿Sos el profesor? Abrí la <a href="/profesor">vista solo lectura</a>.
      </p>
    </section>
  `,
})

export class AlumnoEntrada {
  private readonly router = inject(Router);

  readonly valor = signal(localStorage.getItem(CLAVE_ALUMNO) ?? '');
  readonly error = signal<string | null>(null);

  idValido(): boolean {
    return this.numeroValido(this.valor()) !== null;
  }

  abrir(): void {
    const numero = this.numeroValido(this.valor());
    if (numero === null) {
      this.error.set('El número de alumno va de 1 en adelante.');
      return;
    }
    localStorage.setItem(CLAVE_ALUMNO, String(numero));
    this.error.set(null);
    void this.router.navigate(['/alumno', numero, 'planilla']);
  }

  /** Entero ≥ 1; null si el valor escrito todavía no es válido. */
  private numeroValido(valor: string): number | null {
    const limpio = valor.trim();
    if (!/^\d+$/.test(limpio)) {
      return null;
    }
    const numero = Number(limpio);
    return Number.isSafeInteger(numero) && numero >= 1 ? numero : null;
  }
}
