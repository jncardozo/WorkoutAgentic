# ADR 0005 — ESLint en el SPA (angular-eslint 21)

- **Estado:** Aceptado (2026-10-21)
- **Contexto:** `docs/conventions/angular21.md`; AGENTS.md (comando documentado `npm run lint`)

## S1 — ESLint flat config con angular-eslint fijado a la mayor del workspace

**Decisión:** linting con ESLint 10 (flat config) + `angular-eslint` 21.4, fijado a
la major de Angular del workspace (21). `ng add @angular-eslint/schematics` instala
la última por defecto (v22, pensada para Angular 22) e informa el mismatch después
de instalar; el camino correcto fue instalar `@angular-eslint/schematics@21` y
aplicar `ng generate @angular-eslint/schematics:add-eslint-to-project`. El
schematic genera `eslint.config.js` en CJS (`require` / `module.exports`) — válido
porque `package.json` no declara `"type": "module"`.

## S2 — Presets de la configuración

**Decisión:** la config generada aplica:

- `**/*.ts`: `@eslint/js` recommended + `typescript-eslint` recommended y
  stylistic + `angular-eslint` tsRecommended, con el procesador de templates
  inline. Selectores exigidos: componentes elementos `app-*` kebab-case y
  directivas atributo camelCase, prefijo `app` — igual que la convención.
- `**/*.html`: `templateRecommended` + `templateAccessibility`.

## S3 — Ejecución

**Decisión:** builder `@angular-eslint/builder:lint` en el target `lint` del
proyecto `web` (patterns `src/**/*.ts` y `src/**/*.html`) + script `npm run lint`
en `package.json`. Las dependencias van declaradas en devDependencies (builder,
plugins y parsers `@angular-eslint`, el paquete-paraguas `angular-eslint`,
`eslint@^10`, `typescript-eslint@^8`, `@eslint/js@^10`). Ojo: `@eslint/js` no
sigue el versionado 1:1 de `eslint` (`@eslint/js@10.13.0` no existe; la 10.x es
`10.0.1`).

## Consecuencias

- Las reglas de accesibilidad de template corren desde el día 1: la SPA (vista
  Alumno editable y vista Profesor de solo lectura) nace con la a11y vigilada en
  el lint, no como refactor tardío.
- Los presets son "recomendado": cuando el código lo pida, se aflojan reglas
  puntuales en `eslint.config.js` con comentario, no globalmente.
- El scaffold inicial pasa lint sin cambios de código; el coste de mantenimiento
  se paga en cada component nuevo, que es justo donde hace falta.
