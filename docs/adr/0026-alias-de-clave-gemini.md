# ADR 0026 — Compatibilidad de la clave de Gemini por entorno

- **Estado:** Aceptado (2026-11-10).
- **Contexto:** el log y los comentarios indicaban `GEMINI_API_KEY`, pero el
  binding de la sección `Gemini` solo cargaba `Gemini:ApiKey` (user-secrets) o
  `Gemini__ApiKey` (entorno). Una clave válida quedaba sin leer.

## Decisión

Aceptar `GEMINI_API_KEY` como alias cuando `Gemini:ApiKey` esté vacío. La sección
conserva prioridad. Leer ambas claves mediante IConfiguration, sin consultar
directamente el registro de Windows ni registrar los valores.

Las variables de Windows deben estar presentes en el entorno del proceso al
iniciar la API. Cambiarlas en Windows requiere reiniciar la terminal/IDE y la
API, o actualizar explícitamente el entorno de la terminal antes de iniciarla.
El log de configuración ausente explica los nombres y ese requisito.
