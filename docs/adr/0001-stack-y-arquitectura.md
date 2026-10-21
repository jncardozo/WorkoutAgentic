# ADR 0001 — Stack y arquitectura inicial

- **Estado:** Aceptado
- **Fecha:** 2026-10-20

## Contexto

WorkoutAgentic es un agente de automatización de rutinas de gym: los clientes conversan
por WhatsApp, un agente IA genera/ajusta rutinas, y existe un panel web de administración.

## Decisión

1. **Backend**: API REST en .NET (C#) en `src/api`. Recibe los webhooks de WhatsApp,
   orquesta el agente IA, persiste clientes/rutinas/historial y expone la API del panel.
   Minimal APIs con vertical slices.
2. **Frontend**: SPA Angular 21 standalone en `src/web`, zoneless (default de v21),
   signals, Vitest.
3. **Mensajería**: WhatsApp Cloud API de Meta (oficial) — webhook entrante con validación
   de firma HMAC-SHA256 y envío saliente vía Graph API. Ventana de 24 h respetada.
4. **Harness de desarrollo**: OpenCode con modelo `opencode/glm-5.3-flash-free` (gratis),
   MCP Context7 para documentación actualizada, subagentes `reviewer` y
   `fitness-domain-expert`, skills `whatsapp-integration` y `dotnet-migrations`.

## Consecuencias

- La Cloud API requiere cuenta de negocio en Meta y verificación del webhook; para dev
  se usa el número de prueba + ngrok.
- El proveedor de BD (SQL Server, PostgreSQL, SQLite) queda **pendiente** — se registrará
  en un ADR propio antes de la primera migración.
- El LLM que alimenta al agente en runtime (generación de rutinas) queda **pendiente**;
  candidatos: el mismo GLM-5.3 Flash Free vía OpenCode, un modelo local vía Ollama
  (qwen3.6:27b en GPU 16–24 GB) o una API con free tier.
