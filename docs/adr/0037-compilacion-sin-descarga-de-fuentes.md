# ADR 0037 — Compilación sin descarga de fuentes

- **Estado:** Aceptado (2026-11-12).

## Contexto

La compilación de producción de Angular intenta descargar el CSS de Google Fonts
para incrustarlo en el HTML. Durante la preparación de una instalación reproducible,
`npm run build` falló por falta de acceso a `fonts.googleapis.com`, aunque las
dependencias ya estaban instaladas y las pruebas del frontend pasaban.

## Decisión

Desactivar únicamente `optimization.fonts.inline` en la configuración de producción.
Se conservan las optimizaciones de scripts y estilos. El HTML mantiene el enlace
existente a Inter y JetBrains Mono y el navegador solicita sus fuentes en ejecución.

No se cambia la familia tipográfica ni se agregan paquetes o archivos de fuentes.
La compilación, con dependencias instaladas, deja de necesitar esa descarga externa.
La instalación inicial de paquetes y la interpretación por Gemini siguen requiriendo
red.

## Consecuencias y verificación

El CSS de fuentes conserva una petición de red en el navegador, con el posible costo
de carga frente al CSS incrustado. Si Google Fonts no está disponible, aplican las
familias de respaldo existentes. No se garantiza funcionamiento offline del producto.

Verificar `npm run build` y que el HTML generado conserve el enlace de Google Fonts.
Revisar que las optimizaciones de scripts/estilos y los presupuestos sigan activos.
