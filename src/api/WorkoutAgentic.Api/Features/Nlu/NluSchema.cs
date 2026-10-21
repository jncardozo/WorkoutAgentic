using System.Text.Json.Nodes;

namespace WorkoutAgentic.Api.Features.Nlu;

// responseSchema que fuerza la salida del LLM al spec NLU §1/§2 (ADR-0002 S2).
// El prompt exige "no inventar"; el schema deja todo opcional en nullable
// menos intent+confianza. Los nombres de enum intencionalmente iguales al spec
// (CARGA_EJERCICIO…) y de unidad como el spec §2.
internal static class NluSchema
{
    public const string Json = """
    {
      "type": "object",
      "properties": {
        "intent": {
          "type": "string",
          "enum": ["CARGA_EJERCICIO", "CARGA_SERIE", "CIERRE", "AYUDA", "INVALIDO"]
        },
        "confianza": { "type": "number" },
        "ejercicio": { "type": "string", "nullable": true },
        "serie": { "type": "integer", "nullable": true },
        "cantidad_series": { "type": "integer", "nullable": true },
        "repeticiones": { "type": "integer", "nullable": true },
        "peso": { "type": "number", "nullable": true },
        "unidad": {
          "type": "string",
          "enum": ["kg", "lb", "peso_corporal", "tiempo"],
          "nullable": true
        },
        "tiempo_segundos": { "type": "integer", "nullable": true },
        "descanso_segundos": { "type": "integer", "nullable": true },
        "aclarar": {
          "type": "object",
          "nullable": true,
          "properties": {
            "campo": {
              "type": "string",
              "enum": ["ejercicio", "serie", "reps", "peso", "tiempo", "unidad", "valor_exacto"]
            },
            "pregunta": { "type": "string", "nullable": true }
          }
        }
      },
      "required": ["intent", "confianza"]
    }
    """;

    public static JsonNode ComoJson() => JsonNode.Parse(Json)!;
}
