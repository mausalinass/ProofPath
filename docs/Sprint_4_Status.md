# ProofPath — Sprint 4: Job Intelligence

Actualización: 30 de septiembre de 2026.

## Estado

Implementación local completada. El schema de Job Intelligence también fue verificado mediante una ejecución real con OpenAI usando exclusivamente una descripción laboral sintética y una base temporal aislada.

## Funcionalidad terminada

- [x] CRUD owner-scoped de trabajos con compañía, cargo, URL opcional y descripción requerida entre 100 y 50.000 caracteres.
- [x] La descripción original se persiste antes de encolar el análisis durable.
- [x] El Worker procesa AnalysisKind.JobDescription sin recibir perfil, résumé, repositorios, evidencias, gaps ni scores del candidato.
- [x] Proveedor OpenAI Responses API con gpt-6-astra, razonamiento medium, salida JSON Schema estricta y store=false.
- [x] Instrucciones explícitas contra prompt injection, requisitos inventados, ranking, hiring probability y scoring.
- [x] Extracción en TechnicalSkill, Experience, EducationCredential, Behavioral y Contextual.
- [x] Required, Preferred y Unspecified separados de importancia Critical, High, Medium y Low.
- [x] Conservación de wording, cita, bloque fuente, qualifiers y relaciones AnyOf/AllOf.
- [x] Normalización determinista exacta → alias → equivalente → sugerencia controlada → unresolved, sin crear skills automáticamente.
- [x] Catálogo canónico ampliado de forma aditiva y no-equivalencias conservadas.
- [x] Requisitos unsupported visibles como Unresolved y NotEvaluated; exclusiones conservadas para auditoría.
- [x] Behavioral y Contextual visibles, pero fuera de scoring. Behavioral scoring permanece desactivado.
- [x] Revisión editable, conflicto por revisión, confirmación idempotente y RequirementSet versionado e inmutable.
- [x] Editar la descripción incrementa la versión, marca la extracción previa como outdated y desactiva el set confirmado anterior.
- [x] Pantalla Jobs con creación, edición, polling, retry, revisión por categorías, selección canónica, grupos y confirmación.
- [x] Ownership server-side y borrado de cuenta en cascada para jobs, extracciones, sets y requisitos.

## Persistencia

Migración 20260929191308_AddJobIntelligence aplicada a PostgreSQL local.

Tablas nuevas:

- Jobs
- JobRequirementExtractions
- RequirementSets
- JobRequirements

El SQL revisado fue aditivo: no contiene DROP, no recrea ni altera tablas Identity y conserva las migraciones anteriores. Copia revisada: output/ProofPath/AddJobIntelligence.sql, SHA-256 4E69DA296AD1A9A34ABAE23F12EEEBDC0007DD1A2FC43CD74B2CACC827CA7151.

## Verificación local

- Docker PostgreSQL: healthy.
- Backend: compilación correcta, 0 advertencias y 0 errores.
- Backend: 60 pruebas aprobadas, 0 fallidas y 0 omitidas.
- Frontend: compilación de producción y lint aprobados.
- Frontend: 10 pruebas aprobadas en 5 archivos.
- API: arranque local correcto en Development.
- EF Core: AddJobIntelligence registrada y ninguna migración pendiente.
- OpenAI real: `gpt-6-astra` produjo 11 requisitos grounded; todos conservaron quote y source block.
- Playwright: revisión y confirmación de RequirementSet verificadas dentro del recorrido aislado de Sprints 3–5.

Las pruebas de Sprint 4 cubren grounding, estado confirmado no falsificable, request candidato-independiente, prompt injection, ownership A/B, límites de JD, persistencia previa al análisis, AnyOf/AllOf, contexto AWS, skill unsupported, no-equivalencias, revisión concurrente, confirmación idempotente, flags de evaluación/scoring, invalidación por nueva versión y borrado en cascada.

## Validación externa

El contrato HTTP continúa cubierto con respuestas controladas. Además, el Worker procesó un JD sintético específico de Sprint 4 en PostgreSQL temporal mediante OpenAI real. La ejecución terminó correctamente con `gpt-6-astra`, 11 requisitos y grounding válido. La base, el usuario temporal y los procesos se eliminaron al terminar; no se enviaron trabajos guardados ni datos privados.

No hubo commits, pushes ni despliegues. Identity, résumé intelligence, GitHub Evidence y el working tree previo se conservaron.
