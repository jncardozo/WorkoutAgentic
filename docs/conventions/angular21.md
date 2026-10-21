# Convenciones Angular 21 — `src/web`

> Fuente de verdad para el frontend. Verificado contra la documentación oficial
> (angular.dev) al crear este documento; ante duda, consultar Context7.

## Creación del workspace

```sh
cd src && ng new web --style=scss --skip-git
```

- **Zoneless viene activado por defecto en v21** — NO agregar `provideZoneChangeDetection`.
- El runner de tests por defecto en proyectos nuevos es **Vitest** (no Karma).
- `ng new` soporta `--ai-config` para generar archivos para agentes IA — no es necesario
  aquí porque el proyecto ya tiene su propio `AGENTS.md` raíz.

## Componentes

- **Standalone siempre** (default desde v19): no usar NgModules.
- Estado local con signals: `signal()` para estado, `computed()` para derivados.
- Inputs con la función `input()` (signal inputs), outputs con `output()`; NO usar
  decoradores `@Input()`/`@Output()`.
- Control flow moderno en templates: `@if`, `@for (item of items; track item.id)`,
  `@switch`. Nunca `*ngIf`/`*ngFor`.
- Sin ZoneJS, la detección reacciona a signals: actualizar estado SIEMPRE con `set()`
  o `update()`, nunca mutar objetos fuera de signals y esperar que la vista se refresque.

```typescript
@Component({
  selector: 'app-routine-list',
  templateUrl: './routine-list.html',
})
export class RoutineList {
  private readonly api = inject(RoutinesApi);
  routines = signal<Routine[]>([]);
  loading = signal(false);
  filtered = computed(() => /* ... */);
}
```

- Inyección con `inject()` en el campo, no constructor.

## HTTP y estado

- Servicios tipados por dominio (ej. `RoutinesApi`) que envuelven `HttpClient`.
- Interceptors funcionales (`HttpInterceptorFn`) para auth, errores y loading global.
- Errores HTTP → estado de señal `error` en el componente; nunca `alert()`.

## Estructura

```
src/web/src/app/
├── core/        → interceptors, guards, servicios transversales
├── features/    → una carpeta por feature (routines, clients, settings)
└── shared/      → componentes y pipes reutilizables
```

- Rutas con lazy loading por defecto: `loadComponent` / `loadChildren`.
- Angular Material o Tailwind — decidir con ADR antes de la primera pantalla.

## Pruebas (Vitest)

- Tests de componente: `vitest` con `TestBed`. Verificar rendering con señales, no con
  `whenStable` de zone.
- E2E: Playwright (MCP `playwright` en `opencode.jsonc` se activará cuando exista el proyecto).

## Estilo

- 2 espacios, comillas simples, TypeScript estricto (`strict: true` no se relaja).
- Nombres de archivos kebab-case con sufijo de tipo: `routine-list.ts` / `routine-list.html`.
- Los mensajes visibles y textos de UI van en español.
