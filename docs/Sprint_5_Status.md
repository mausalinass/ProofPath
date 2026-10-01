# ProofPath — Sprint 5: Matching Engine

Fecha de cierre local: 2026-09-29.

## Estado

Implementación local completada. Sprint 6 se completó el 30 de septiembre de 2026.

## Alcance implementado

- Núcleo puro y determinista `matching-v1`, sin acceso a red, base de datos, archivos, AWS, GitHub ni LLM.
- Pesos congelados: Technical 45%, Evidence 25%, Experience 25% y Education 5%. Los componentes no aplicables se eliminan y los pesos restantes se normalizan.
- Agregación por fuente con strength, freshness, control de duplicados y corroboración independiente.
- Estados separados para `Missing`, `Uncertain` y `NotEvaluated`; una ausencia solo se afirma con cobertura suficiente.
- Manejo de exact canonical, related-only, AnyOf/AllOf, requisitos unresolved, experiencia profesional confirmada y educación en curso.
- Behavioral scoring desactivado. La evidencia conductual se muestra por separado y no modifica el porcentaje.
- Safeguards para requisitos críticos faltantes o inciertos y estado `Limited` cuando la cobertura no permite una clasificación fiable.
- Persistencia inmutable de `MatchResult`, `RequirementMatchRecord` y snapshots de evidencia/requisitos.
- Endpoints autenticados para calcular, listar historial, consultar el último resultado y recuperar un resultado por ID.
- UI integrada en Job Intelligence con resumen, componentes, trazabilidad por requisito, evidencia, gaps, confidence, coverage e historial.

## Decisiones de calibración v1

Los valores sensibles que la especificación delega a calibración quedan congelados dentro de `MatchingConfiguration.V1`: requisito preferred 0.50, unspecified 0.50, componente evaluable con coverage mínima 0.50, warning desde 0.60 y cobertura normal desde 0.80. Un grado explícitamente `InProgress` recibe factor 0.65. Cambiar cualquiera de estos valores después de release requiere una nueva ScoringVersion.

## Persistencia y migración

Migraciones revisadas y aplicadas: `20260929205619_AddMatchingEngine` (tablas aditivas) y `20260930014224_FreezeMatchingV1Calibration` y `20260930043607_FreezeMatchingV1ExperienceCalibration` (ambas actualizan únicamente el JSON congelado de configuración antes del release).

Crea únicamente:

- `MatchingScoringVersions`
- `MatchResults`
- `RequirementMatchRecords`
- `RequirementMatchEvidenceRecords`

La migración fue revisada como SQL, aplicada a PostgreSQL local y conserva Identity, CandidateProfile, résumé, GitHub y Job Intelligence.

## Verificación

- Build backend sin warnings ni errores.
- Pruebas unitarias de invariantes y fixtures matching-v1.
- Prueba de integración con PostgreSQL para ownership A/B, autenticación, historial inmutable y latest result.
- Frontend lint, build y suite de pruebas.
- Verificación de migración y tablas en PostgreSQL local.
- Matriz mínima de calibración cubierta por 21 pruebas: determinismo, duplicados, keyword-only frente a implementación, GitHub fuerte sin experiencia, experiencia sin GitHub, junior balanceado, junior frente a senior, críticos missing/uncertain, stale, coverage, related-only, educación N/A/in-progress y AnyOf.
- Recorrido Playwright aislado aprobado: RequirementSet confirmado → cálculo matching-v1 → porcentaje → requirement trace → evidencia GitHub con SHA y ruta → historial inmutable.

## Próximo bloque

Sprint 6 completado: recomendaciones deterministas, tracking, reanálisis e historial comparativo vinculados a `MatchResult` inmutables. El gate externo de GitHub del Sprint 3 quedó completado el 30 de septiembre de 2026.
