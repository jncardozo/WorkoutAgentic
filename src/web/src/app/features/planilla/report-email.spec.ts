import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PlanillaApi, PlanillaConfirmadaVista, RespuestaEnviarPlanillaCorreo } from '../../core/planilla-api';
import { ReportEmail } from './report-email';
import { ReporteWorkspace } from './reporte-workspace';
import { ReportePreview } from './reporte-preview';

const plan: PlanillaConfirmadaVista = { id: 19, alumnoId: 2, sesionId: 8, confirmadaUtc: '2026-11-19T15:00:00Z', filas: [] };

async function mount(configuration: Observable<{ configured: boolean }> = of({ configured: true })) {
  const response = new Subject<RespuestaEnviarPlanillaCorreo>();
  const send = vi.fn(() => response.asObservable());
  const availability = vi.fn(() => configuration);
  TestBed.configureTestingModule({ providers: [{ provide: PlanillaApi, useValue: { enviarPlanillaPorCorreo: send, emailConfigurationStatus: availability } }] });
  const fixture = TestBed.createComponent(ReportEmail);
  fixture.componentRef.setInput('plan', plan);
  fixture.componentRef.setInput('studentId', 2);
  fixture.componentRef.setInput('initialFormat', 'excel');
  await fixture.whenStable();
  fixture.componentInstance.open();
  await fixture.whenStable();
  return { fixture, component: fixture.componentInstance, response, send, availability };
}

describe('ReportEmail', () => {
  it('envía la planilla y formato seleccionados una sola vez mientras espera', async () => {
    const { fixture, component, response, send } = await mount();
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'qa@example.test';
    input.dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    component.send();
    await fixture.whenStable();
    expect(send).toHaveBeenCalledExactlyOnceWith(19, { alumnoId: 2, correo: 'qa@example.test', formato: 'excel' });
    expect(fixture.nativeElement.textContent).toContain('Planilla #19');
    expect(fixture.nativeElement.querySelector('button[type="submit"]').disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Enviando…');
    response.next({ mensaje: 'La planilla se envió a qa@example.test.' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('qa@example.test');
    expect(component.sending()).toBe(false);
  });

  it('rechaza una dirección inválida sin llamar a la API', async () => {
    const { fixture, component, send } = await mount();
    component.recipient.set('dirección inválida');
    component.send();
    await fixture.whenStable();
    expect(send).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('dirección de correo válida');
  });

  it.each([503, 502])('conserva el destinatario y permite reintentar tras un error %s', async status => {
    const { fixture, component, response } = await mount();
    component.recipient.set('qa@example.test');
    component.send();
    response.error(new HttpErrorResponse({ status }));
    await fixture.whenStable();
    expect(component.recipient()).toBe('qa@example.test');
    expect(component.sending()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    if (status === 502) expect(fixture.nativeElement.querySelector('button[type="submit"]').disabled).toBe(false);
    else expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('limpia el formulario al cambiar de planilla e ignora una respuesta anterior', async () => {
    const { fixture, component, response } = await mount();
    component.recipient.set('qa@example.test');
    component.send();
    fixture.componentRef.setInput('plan', { ...plan, id: 20 });
    await fixture.whenStable();
    response.next({ mensaje: 'Respuesta de otra planilla' });
    expect(component.recipient()).toBe('');
    expect(component.message()).toBeNull();
    expect(component.sending()).toBe(false);
    expect(component.isOpen()).toBe(false);
    component.open();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Planilla #20');
  });

  it('limpia el resultado y conserva el destinatario al elegir otro formato', async () => {
    const { fixture, component, response } = await mount();
    component.recipient.set('qa@example.test');
    component.send();
    response.next({ mensaje: 'Enviada' });
    fixture.componentRef.setInput('initialFormat', 'pdf');
    await fixture.whenStable();
    expect(component.format()).toBe('pdf');
    expect(component.recipient()).toBe('qa@example.test');
    expect(component.message()).toBeNull();
  });

  it('explica la falta de configuración antes de pedir destinatario y no permite enviar', async () => {
    const { fixture, component, send, availability } = await mount(of({ configured: false }));
    expect(availability).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('El responsable debe configurar el servicio de correo');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    component.recipient.set('qa@example.test');
    component.send();
    expect(send).not.toHaveBeenCalled();
    availability.mockReturnValue(of({ configured: true }));
    component.checkConfiguration();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
  });

  it('espera la comprobación de configuración antes de habilitar el formulario', async () => {
    const configuration = new Subject<{ configured: boolean }>();
    const { fixture, component, send } = await mount(configuration);
    expect(fixture.nativeElement.textContent).toContain('Comprobando el servicio');
    component.send();
    expect(send).not.toHaveBeenCalled();
    configuration.next({ configured: true });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
  });

  it('distingue una falla de conexión de un servicio sin configurar y permite reintentar', async () => {
    const { fixture, component, availability } = await mount(throwError(() => new HttpErrorResponse({ status: 0 })));
    expect(fixture.nativeElement.textContent).toContain('No pudimos comprobar el servicio');
    availability.mockReturnValue(of({ configured: true }));
    component.checkConfiguration();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
  });
});

describe('Acceso al envío de reportes', () => {
  it('ofrece un único botón antes de las tarjetas y permite elegir el formato en el formulario', async () => {
    TestBed.configureTestingModule({ providers: [{ provide: PlanillaApi, useValue: { emailConfigurationStatus: () => of({ configured: true }) } }] });
    const fixture = TestBed.createComponent(ReporteWorkspace);
    fixture.componentRef.setInput('planilla', plan);
    fixture.componentRef.setInput('studentId', 2);
    fixture.componentRef.setInput('modo', 'seleccion');
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('.email-launch')).toHaveLength(1);
    expect(host.querySelector('.opciones-reporte .email-launch')).toBeNull();
    expect(host.querySelector('app-report-email')!.compareDocumentPosition(host.querySelector('.opciones-reporte')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    host.querySelector<HTMLButtonElement>('.email-launch')!.click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelectorAll('app-report-email')).toHaveLength(1);
    expect(fixture.nativeElement.querySelector('select').value).toBe('pdf');
    expect([...host.querySelectorAll('option')].map(option => option.value)).toEqual(['pdf', 'excel']);
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('input'));
  });

  it.each(['pdf', 'excel'] as const)('ofrece envío en el detalle %s', async format => {
    TestBed.configureTestingModule({ providers: [{ provide: PlanillaApi, useValue: { emailConfigurationStatus: () => of({ configured: true }) } }] });
    const fixture = TestBed.createComponent(ReporteWorkspace);
    fixture.componentRef.setInput('planilla', plan);
    fixture.componentRef.setInput('studentId', 2);
    fixture.componentRef.setInput('modo', format);
    await fixture.whenStable();
    const button = [...fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>]
      .find(item => item.textContent?.includes('Enviar planilla por email'))!;
    button.click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('select').value).toBe(format);
  });

  it.each(['pdf', 'excel'] as const)('ofrece envío en la vista de impresión %s', async format => {
    TestBed.configureTestingModule({ providers: [{ provide: PlanillaApi, useValue: { emailConfigurationStatus: () => of({ configured: true }) } }] });
    const fixture = TestBed.createComponent(ReportePreview);
    fixture.componentRef.setInput('planilla', plan);
    fixture.componentRef.setInput('studentId', 2);
    fixture.componentRef.setInput('formato', format);
    await fixture.whenStable();
    fixture.nativeElement.querySelector('.email-launch').click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('select').value).toBe(format);
  });
});
