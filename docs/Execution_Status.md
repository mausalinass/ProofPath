# ProofPath — Estado actual de ejecución

Actualización: 30 de septiembre de 2026.

- [x] Sprint 1: completado y verificado localmente.
- [x] Sprint 2: carga privada PDF/DOCX, extracción durable, grounding, revisión, confirmación y versiones implementados.
- [x] Migración AddResumeIntelligence aplicada y verificada en PostgreSQL local.
- [x] 60 pruebas backend, 10 frontend y 2 recorridos de navegador aprobados localmente.
- [x] Política aprobada: último confirmado activo, versiones hasta borrar cuenta, corrección sin editar scores/strength.
- [x] Gate de integración externa local: OpenAI real (`gpt-6-astra`, medium) y S3 privado real verificados con résumés sintéticos.
- [x] Borrado durable verificado: 0 versiones/markers residuales en S3 y 0 tareas pendientes en PostgreSQL.
- [ ] P11 de producción: scanner/aislamiento, privacidad/retención del proveedor y validación en staging antes de datos públicos.
- [x] Sprint 3 implementado localmente: conexión GitHub App, selección explícita, análisis bounded/determinista, evidence trazable, retry y desconexión.
- [x] Migraciones `20260929182047_AddGitHubConnection`, `20260929184051_AddGitHubEvidence` y `20260930181436_StoreGitHubConnectionStateServerSide` aplicadas; modelo/snapshot sincronizados.
- [x] P13 cerrada: desconectar marca evidencia histórica `Stale`; borrar la cuenta revoca la instalación y elimina los derivados en cascada.
- [x] Gate externo Sprint 3: GitHub App real registrada, autorizada con acceso de solo lectura únicamente a `mausalinass/ProofPath`, conexión persistida y análisis real completado contra un SHA fijo.
- [x] Sprint 4 implementado localmente: CRUD de jobs, extracción candidato-independiente, normalización, revisión y RequirementSet confirmado/versionado.
- [x] Sprint 5 implementado localmente: matching-v1 determinista 45/25/25/5, coverage/confidence/safeguards, historial inmutable, API y UI trazable.
- [x] Migración 20260929191308_AddJobIntelligence revisada, aplicada y registrada en PostgreSQL local.
- [x] Gate externo Sprint 4: schema de Job Intelligence verificado con un JD sintético, PostgreSQL temporal y OpenAI real; 11 requisitos grounded.
- [x] Sprint 5 calibrado con 21 pruebas de motor y recorrido E2E aislado de trazabilidad completa.
- [x] E2E aislado de Sprints 3–5: GitHub evidence persistida → RequirementSet confirmado → match → evidence source.
- [x] Gate externo Sprint 3: recorrido real GitHub.com → callback local → selección explícita → Worker → evidencia trazable completado.
- [ ] Sprints 6–7B: pendientes.

Evidencia vigente: 81 pruebas backend, 11 frontend y 3 recorridos Playwright aprobados localmente; lint y builds correctos. Detalle: [Sprint 2](Sprint_2_Status.md), [Sprint 3](Sprint_3_Status.md), [Sprint 4](Sprint_4_Status.md), [Sprint 5](Sprint_5_Status.md). Configuración: [Résumé](Resume_Setup.md) y [GitHub App](GitHub_Setup.md).
Sin commits, pushes ni despliegues. Identity y el trabajo local previo se conservaron.

---

## Registro histórico previo al cierre local de Sprint 2

El texto siguiente conserva la auditoría anterior; sus pendientes y conteos antiguos están reemplazados por el estado vigente de arriba.

# ProofPath — Estado de ejecución

Fecha: 18 de septiembre de 2026. Repositorio: `C:\Users\mausa\Projects\ProofPath`.

## Alcance autorizado

El usuario amplió la primera unidad a cerrar Sprint 1 y continuar el plan en orden. No se requiere una confirmación por cada unidad técnica. Las decisiones de producto abiertas y las configuraciones externas se consultan cuando son necesarias. No se autorizan commits, pushes ni despliegues.

## Sprint 1 — verificado localmente

- [x] Foundation e Identity existentes reutilizados.
- [x] CandidateProfile y mapping existentes preservados.
- [x] Migración `20260918221119_AddCandidateProfile` revisada, aplicada y verificada localmente.
- [x] Perfil GET/PUT con propietario derivado de la sesión, upsert atómico y UTC.
- [x] Signup con FirstName requerido, LastName opcional y perfil creado en la misma transacción.
- [x] Seis campos del perfil editables; trim y strings vacíos a null; sin límites de longitud nuevos.
- [x] Rutas `/api/v1/auth` y aliases originales `/api/auth`.
- [x] CSRF en escrituras, cookies HttpOnly, Secure en producción y CORS explícito con credentials.
- [x] Lockout de Identity activado en login y rate limit configurable de autenticación.
- [x] Home, My Evidence/perfil, Jobs vacío, Settings, signup/login/logout y shell responsive.
- [x] Borrado con contraseña actual y confirmación explícita; cascadas de datos actuales y revocación de otras sesiones.
- [x] Estados de carga, error/retry y conservación de formulario.
- [x] Pruebas de ownership A/B, intentos de imponer propietario, persistencia y concurrencia.
- [x] Prueba real de navegador: signup → perfil → logout → login → persistencia → borrado.
- [x] CI base extendida con lint y pruebas frontend.
- [ ] CI remota: no ejecutada porque no hubo push.
- [ ] Validación staging: entorno externo pendiente.

La política de nombre y edición fue aprobada por el usuario. Email verification/reset y política definitiva de sesión/lockout para lanzamiento siguen como P10 del plan; no se inventó un gate de email.

## Sprint 2 — base implementada, flujo de résumé pendiente

- [x] `AnalysisJob` con FK directa a CandidateProfile, estados, versiones, intentos y reservas.
- [x] Migración `20260918223629_AddDurableAnalysisJobs` aditiva; SQL revisado, aplicada y verificada localmente.
- [x] PostgreSQL claim atómico con SKIP LOCKED; no broker añadido.
- [x] Deduplicación por candidato/tipo/recurso/versión.
- [x] Recuperación de reservas vencidas; token de reserva impide commits tardíos.
- [x] Máximo tres intentos automáticos; backoff de 15/30 segundos entre intentos; retry manual solo para fallos retryable.
- [x] Worker sustituye el temporizador de plantilla por consumo real de la cola.
- [x] Polling/cancel/retry owner-scoped con CSRF en escrituras.
- [x] Resultados borradores se guardan solo al completar una reserva válida; los handlers no materializan hechos confirmados.
- [x] Puerto de almacenamiento privado y adaptador local de desarrollo: keys opacas, SHA-256, límite de 10 MB, escritura temporal seguida de rename y limpieza ante fallos/cancelación.
- [x] Pruebas de cola, ownership HTTP y almacenamiento.
- [ ] Entidades Resume/ResumeExtraction y hechos/evidence confirmados.
- [ ] Upload/download autorizados y validación de PDF/DOCX.
- [ ] Parser PDF y extracción DOCX/source blocks.
- [ ] Proveedor OpenAI, schema/grounding, review y confirmación versionada.
- [ ] Integración S3 real y borrado de archivos/derivados.
- [ ] Gate G2.

El almacenamiento local todavía no está expuesto por un endpoint ni registrado como fallback de producción. Se debe configurar un directorio privado dedicado fuera del repositorio, webroot y carpetas sincronizadas. El Worker requiere su propia `ConnectionStrings:DefaultConnection` configurada de forma segura; no se copiaron ni imprimieron secretos de la API.

La reserva dura cinco minutos y el handler tiene timeout de cuatro minutos. Shutdown deja el trabajo recuperable tras expirar su reserva. La cancelación invalida la reserva y descarta un resultado tardío; la coordinación de cancelación con proveedores concretos se completa con los handlers. No hay handlers de résumé/GitHub/JD registrados todavía: un tipo sin handler falla explícitamente, nunca aparenta éxito.

## Evidencia de verificación

| Verificación | Resultado |
| --- | --- |
| Compilación backend | Correcta; compilación de las pruebas finales sin warnings reportados |
| Pruebas backend | 27 passed, 0 failed, 0 skipped; PostgreSQL aislado en Testcontainers |
| TypeScript + Vite | Correctos |
| ESLint | Correcto |
| Vitest | 3 passed: formulario/retry, perfil vacío y cancelación de borrado |
| Playwright/Chromium | 1 passed: recorrido completo de Sprint 1 con DB temporal separada |
| PostgreSQL local | Compose healthy; tablas Identity, CandidateProfiles y AnalysisJobs presentes |
| Historial local EF | InitialCreate, AddIdentity, AddCandidateProfile y AddDurableAnalysisJobs |
| Modelo/snapshot | Sin cambios de modelo pendientes |
| CI remota/staging/producción | Sin evidencia; no se declaran aprobados |

El primer intento del navegador agotó cinco segundos esperando el primer acceso a PostgreSQL. Usando IPv4 explícito para la DB temporal y un timeout de expectativa de 15 segundos, el flujo completo terminó en seis segundos. Esta configuración se limita a las pruebas.

## Decisiones y configuración pendientes

1. P05: aprobación solicitada para PdfPig (Apache 2.0) y DOCX vía ZIP/XML de .NET. No se instaló un parser sin aprobación.
2. OpenAI: usuario eligió `gpt-6-astra`, reasoning `medium`; disponibilidad de clave API configurada pendiente de respuesta. No se hicieron llamadas pagadas ni se leyeron claves.
3. P06/P04: propuesta pendiente de aprobación para reemplazar el conjunto activo con el último résumé confirmado, conservar versiones hasta borrar cuenta y permitir editar hechos/fechas/skills sin strength ni scores.
4. P11: cerrar estrategia de seguridad de archivos antes de uploads reales.
5. GitHub App y AWS: usuario indicó que probablemente no están configurados. Integración real y gates externos quedan pendientes.
6. P08 y restantes decisiones del plan: resolver en sus bloques; no adelantar curvas de scoring, retención de integraciones ni presupuestos como decisiones aprobadas.

Límites delegados por el usuario: résumé 10 MB; propuesta de JD entre 100 y 50.000 caracteres, a implementar en Sprint 4. No se presentan como valores originarios de los PDFs.

## Preservación y próximos pasos

Se conservan los cambios locales anteriores, las tablas Identity, las migraciones anteriores, la solución y las versiones existentes. Program/AuthEndpoints se extendieron para brechas concretas; Domain/CandidateProfile original no se recreó. Las referencias bajo sources permanecen de solo lectura. No hubo commits, pushes ni despliegues.

Próximo trabajo: completar Sprint 2 según las decisiones pendientes. Sprints 3–7B permanecen sin implementar; sus gates reales requieren GitHub/AWS/proveedor, calibración, staging y una instrucción explícita para desplegar.

## Archivos del delta implementado

Rutas relativas al repositorio vigente; los archivos originales no listados aquí se conservaron.

| Área | Archivos creados o extendidos |
| --- | --- |
| API | apps/backend/src/ProofPath.Api/Program.cs; Endpoints/AuthEndpoints.cs (extendido); Endpoints/CandidateEndpoints.cs; Endpoints/AnalysisEndpoints.cs |
| Application | apps/backend/src/ProofPath.Application/Candidates/CandidateProfileService.cs; Analysis/AnalysisContracts.cs; Files/PrivateFiles.cs |
| Domain | apps/backend/src/ProofPath.Domain/Entities/AnalysisJob.cs (nuevo); CandidateProfile.cs original sin cambios |
| Infrastructure | apps/backend/src/ProofPath.Infrastructure/Persistence/CandidateProfileStore.cs; AnalysisJobConfiguration.cs; PostgresAnalysisQueue.cs; ProofPathDbContext.cs (extendido); Files/DevelopmentPrivateFileStore.cs |
| Migraciones | Persistence/Migrations/20260918221119_AddCandidateProfile.cs y Designer; 20260918223629_AddDurableAnalysisJobs.cs y Designer; ProofPathDbContextModelSnapshot.cs |
| Worker | apps/backend/src/ProofPath.Worker/Program.cs y Worker.cs |
| Pruebas backend | apps/backend/tests/ProofPath.Api.Tests/ProofPath.Api.Tests.csproj; Sprint1Tests.cs; AnalysisQueueTests.cs; AnalysisApiTests.cs; PrivateFileTests.cs |
| Frontend | apps/web/package.json y package-lock.json; src/App.tsx y App.css; src/main.tsx e index.css; src/api/client.ts; src/components/Feedback.tsx; src/pages/AuthPage.tsx, HomePage.tsx, ProfilePage.tsx, SettingsPage.tsx |
| Pruebas frontend | apps/web/vitest.config.ts; src/test/setup.ts; src/pages/ProfilePage.test.tsx y SettingsPage.test.tsx; playwright.config.ts; e2e/sprint1.spec.ts |
| Configuración/documentación | apps/web/vite.config.ts; .github/workflows/ci.yml; .gitignore; docs/Sprint_1_5B_Status.md; docs/Execution_Status.md; docs/API_Sprint1.md |

La migración AddIdentity y ApplicationUser ya existían en el working tree: no son parte del código generado en esta ejecución. Infrastructure.csproj tenía cambios locales previos y no se atribuyen a este delta.
