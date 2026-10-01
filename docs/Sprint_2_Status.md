# ProofPath — Sprint 2: implementación y verificación

Fecha: 29 de septiembre de 2026. Repositorio: `C:\Users\mausa\Projects\ProofPath`.

## Resultado

El flujo de Résumé Intelligence está implementado y verificado localmente con documentos sintéticos: upload privado → PostgreSQL/Worker → extracción determinista → respuesta estructurada → revisión/corrección → confirmación → sustitución del conjunto activo → historial → borrado. Las pruebas de navegador usan la API, PostgreSQL y Worker reales; únicamente el proveedor LLM se sustituye por un fixture en un host exclusivo de pruebas.

**Gate de integración externa local aprobado:** se verificó una llamada real a OpenAI con `gpt-6-astra` y reasoning `medium`, y el flujo real de upload/download/versionado/borrado sobre el bucket privado de desarrollo `proofpath-dev-resumes` en `us-east-2`. La prueba usó résumés sintéticos y confirmó 0 versiones/markers residuales y 0 tareas de cleanup pendientes. Esto no es aprobación de producción: P11, privacidad/retención del proveedor, staging y CI remota siguen abiertos.

## Decisiones incorporadas

- Upload máximo: 10 MB, elegido bajo la delegación del usuario.
- PDF: PdfPig 0.1.16; DOCX: ZIP/XML de .NET. No OCR; PDF sin texto produce una guía para subir PDF textual o DOCX.
- OpenAI: `gpt-6-astra`, reasoning `medium`, configurable por backend.
- El usuario aprobó que el último résumé **confirmado** reemplace los hechos activos del anterior. Se conserva el historial hasta borrar la cuenta; subir un archivo no sustituye hechos por sí solo.
- Edición de hechos, fechas y skill mentions; el usuario no envía scores ni strength.
- Los proyectos del résumé permanecen separados de futuros repositorios GitHub.

## Implementado

| Bloque | Comportamiento |
| --- | --- |
| Modelo | Resume y ResumeExtraction versionados; Experience, Education, Credential y Project relacionales; catálogo inicial Skill/SkillAlias; EvidenceItem y BehavioralEvidenceItem |
| Upload | Un archivo multipart; MIME/extensión/firma coherentes; key opaca; límite comprobado en servidor; reserva durable antes del almacenamiento |
| Privacidad | Ownership desde la sesión; downloads autorizados como attachment, no URLs públicas; HttpOnly/CSRF/CORS existentes reutilizados |
| PDF/DOCX | Source blocks y páginas cuando corresponden; XML sin DTD/resolver; límites de ZIP; rechazo de macros/embeddings/plantillas externas; errores de documentos corruptos |
| Worker | Cola existente reutilizada; resultado y extracción se guardan en una transacción protegida por lease; cancelación comprueba el lease cada dos segundos y cancela el handler |
| OpenAI | Responses API, JSON Schema estricto, `store:false`, timeout, máximo de output, circuit breaker y reintentos existentes; errores saneados |
| Grounding | Citas exactas presentes en los bloques; campos de máquina deben estar en la fuente; fechas ambiguas permanecen como texto; campos desconocidos se conservan null |
| Correcciones | Borrador separado del snapshot de máquina; revisión optimista para impedir sobrescribir otra pestaña; campos adicionales como score rechazados |
| Confirmación | Transacción serializada por candidato; idempotencia; hechos y evidencia con provenance; replay de una confirmación anterior no la vuelve activa |
| Evidencia | Normalización exacta/alias sobre catálogo controlado; términos desconocidos quedan unmapped; el LLM no crea skills globales |
| Conductual | Statements explícitos, catálogo de diez temas, confirmación y presentación separadas; un statement inválido se omite con warning sin descartar facts técnicos válidos |
| Borrado | Cascadas de datos y jobs; tareas durables de eliminación de archivos sobreviven al borrado de cuenta; reintentos ante almacenamiento temporalmente caído |
| S3 | Adaptador con bloqueo público comprobado, cifrado SSE-S3, keys opacas, create-only y borrado de todas las versiones/markers de la key exacta |
| UI | Upload, progreso, errores/retry/cancel, descarga, citas, correcciones, confirmación explícita, versión activa e historial; formulario preservado tras errores |
| CI | Checks existentes conservados; job E2E con PostgreSQL temporal y proveedor sintético |

## Límites reales y gates abiertos

- El scanner ClamAV obligatorio analiza el documento antes de persistirlo. Malware y fallos del scanner se rechazan; el modo S3 exige además `Storage:FileSecurityReviewed=true`.
- Parser acotado a 100 páginas PDF, 1.000 entradas ZIP, 40 MB descomprimidos, 10 MB por entrada XML, 2.000 source blocks y 120.000 caracteres. Son guardas técnicas de esta implementación. Los documentos que exceden el análisis fallan de forma explícita; no se truncan silenciosamente.
- PdfPig es síncrono: se respeta cancelación entre páginas, pero no hay terminación forzada de un parse que se atasque dentro de una página. El aislamiento del parser antes de exposición pública requiere revisión de P11.
- PDF siempre advierte revisar el orden de lectura; imágenes no analizadas generan warnings. DOCX cubre cuerpo/tablas/text boxes, headers y footers mediante párrafos XML; imágenes no se interpretan.
- Datos de contacto reconocibles (email/teléfono) se redactan en el texto enviado al proveedor. Esto no equivale a anonimización completa. El original y las citas permanecen privados para revisión.
- `store:false` no equivale a una garantía de Zero Data Retention. Condiciones y retención del proveedor deben verificarse antes de producción.
- Strength técnico del résumé es conservador: Weak para claims, sin convertir mención/repetición en proof of implementation. La detección conductual explícita reconoce un vocabulario acotado en inglés/español; asigna Weak a self-claims y como máximo Moderate a acciones reconocidas. No implementa inferencias conductuales ni promoción automática a Strong. Debe calibrarse con fixtures más amplios antes del gate completo de AI/behavioral.
- Confirmados históricos son inmutables. Correcciones posteriores requieren nueva versión. Solo extracciones `Active=true` se usan para futuros snapshots downstream.
- El catálogo inicial contiene el stack documentado de ProofPath; términos fuera de él se conservan sin normalizar. Su ampliación controlada corresponde a Evidence/Matching.
- Telemetría persistida: modelo, versiones, duración, input/output tokens. No se presenta un costo estimado sin una tarifa aprobada; presupuesto/costo por task queda pendiente de configuración.
- S3 y OpenAI reales están verificados localmente con datos sintéticos. Privacidad/retención del proveedor, scanner/aislamiento P11, staging y CI remota siguen sin aprobarse. El código S3 no crea buckets/IAM y usa la cadena normal de credenciales/roles del SDK.
- Cambiar de almacenamiento local a S3 con archivos existentes requiere una migración de objetos planificada; no cambiar simplemente `Storage:Provider` sobre datos locales existentes.

## Migración

`20260918230012_AddResumeIntelligence` crea 11 tablas y seeds del catálogo. SQL revisado: solo CREATE/INSERT e índices; no altera ni recrea Identity ni CandidateProfile. Aplicada a `proofpath` en `tcp://localhost:5432` tras verificar destino, historial y ausencia de tablas equivalentes. Se comprobó historial posterior y `has-pending-model-changes` sin diferencias.

Tablas: Resumes, ResumeExtractions, Experiences, Educations, Credentials, Projects, Skills, SkillAliases, EvidenceItems, BehavioralEvidenceItems, PrivateFileDeletions. Las claves de cleanup no contienen nombre/email ni texto del candidato.

## Evidencia de pruebas

- Backend: **49 passed**, 0 failed, 0 skipped, PostgreSQL aislado con Testcontainers.
- Frontend: **6 passed**, 0 failed; edición preservada tras error, confirmación explícita, historial readonly, rechazo de archivo no soportado y controles de Sprint 1.
- Chromium: **2 passed**, recorridos completos de Sprint 1 y Sprint 2. El de Sprint 2 usa un host de pruebas con el Worker real y un LLM sintético, sin una llamada externa.
- Compilación .NET: 0 warnings, 0 errors. TypeScript/Vite y ESLint correctos.
- Contratos OpenAI: modelo/effort solicitados, schema estricto, no response storage, ausencia de clave sin tráfico, throttling/circuit breaker, salida incompleta rechazada.
- Contratos S3: bucket privado, cifrado/create-only, eliminación paginada de versiones y markers sin eliminar keys vecinas.
- Seguridad/datos: ownership A/B en archivos/review, CSRF, confirmación idempotente, conflicto de revisión, historial, cascadas/borrado y reintento durable de storage cleanup.
- Worker: concurrencia, lease expirado, reserva persistente, retry finito y cancelación del handler en ejecución.

## Archivos principales

| Capa | Archivos |
| --- | --- |
| Domain | Entities/Resume.cs; Entities/BehaviorEvidenceRules.cs |
| Application | Resumes/ResumeContracts.cs, ResumeValidation.cs, ResumeAnalysisHandler.cs, ResumeWorkspaceService.cs, IResumePersistence.cs; extensiones mínimas de Files/PrivateFiles.cs y Analysis/AnalysisContracts.cs |
| Infrastructure | Resumes/DocumentTextExtractor.cs, OpenAiResumeProvider.cs, ResumeProviderInput.cs, ResumePersistence.cs, ResumeCompletion.cs, PrivateFileCleanup.cs, ResumeRegistration.cs; Files/S3PrivateFileStore.cs y extensión de DevelopmentPrivateFileStore.cs |
| EF | Persistence/ResumeModelConfiguration.cs, ProofPathDbContext.cs y migración/snapshot incremental |
| API | Endpoints/ResumeEndpoints.cs; registro en Program.cs; Home y borrado extendidos en CandidateEndpoints.cs |
| Worker | Registro del módulo en Program.cs y monitor de cancelación en Worker.cs |
| React | src/api/resumes.ts y extensión de client.ts; pages/ResumePage.tsx y CSS; integración en App, Home y Settings |
| Pruebas | ResumeTests.cs, ProviderTests.cs, S3ContractTests.cs, ResumeRecoveryTests.cs, WorkerCancellationTests.cs; ResumePage.test.tsx; e2e/sprint2.spec.ts; tests/ProofPath.E2EHost (solo pruebas) |
| CI/docs | .github/workflows/ci.yml; docs/Sprint_2_Status.md; docs/Resume_Setup.md; docs/Execution_Status.md |

Los cinco proyectos de producción se conservan; E2EHost es un proyecto de pruebas fuera de la solución de producción. No agrega un servicio desplegable. No hay dependencia de EF/Identity/AWS/HTTP en Domain, ni SDK de proveedor en Application.

## Siguiente paso

Sprint 2 queda cerrado para desarrollo local: implementación, pruebas automatizadas e integración real OpenAI/S3 con datos sintéticos están verificadas. El siguiente bloque es Sprint 3 — GitHub Evidence. Antes de datos reales en staging o producción siguen pendientes P11, privacidad/retención del proveedor, CI remota y los gates de release correspondientes.
