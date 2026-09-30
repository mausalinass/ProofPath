# ProofPath — Sprint 3: GitHub Evidence

Actualización: 30 de septiembre de 2026.

## Estado

Implementación local, recorrido E2E sintético y gate externo con una GitHub App real completados. La aplicación quedó autorizada con acceso de solo lectura únicamente a `mausalinass/ProofPath`; la selección explícita y el análisis real contra un SHA fijo produjeron evidencia trazable.

## Funcionalidad terminada

- [x] Conexión GitHub App por candidato, con `state` protegido, nonce de un solo uso y prevención de replay.
- [x] No se solicita ni persiste un token OAuth de usuario; el token de instalación existe únicamente en memoria y la base de datos no almacena access tokens, refresh tokens ni claves privadas.
- [x] Verificación de identidad del usuario que autorizó la instalación y de permisos `Metadata: read` y `Contents: read`.
- [x] Descubrimiento de repositorios autorizados sin analizarlos automáticamente.
- [x] Selección explícita de hasta cinco repositorios y ownership validado por el servidor.
- [x] Un trabajo durable e independiente por repositorio mediante `AnalysisJob` y Worker.
- [x] Snapshot fijado al SHA de la rama predeterminada, sin clonar el repositorio.
- [x] Límites: 5.000 entradas del árbol, 150 archivos, 512 KiB por archivo y 10 MiB por repositorio.
- [x] Exclusión de binarios, dependencias, artefactos compilados, cobertura y contenido generado.
- [x] Registro versionado de tecnologías y detectores deterministas para lenguajes, manifiestos, dependencias, EF Core/PostgreSQL, React, Docker y GitHub Actions.
- [x] Evidencia con tipo, fuerza, confianza de extracción, repositorio, SHA, ruta y versión del detector.
- [x] Idempotencia por repositorio + SHA + versión de extracción + versión de política.
- [x] Resultados parciales, errores reintentables y pérdida de acceso representados por repositorio.
- [x] Pantalla de conexión, búsqueda/selección, progreso, reintento, evidencia y desconexión.
- [x] Política P13: desconectar detiene acceso y análisis nuevos, y marca la evidencia histórica como `Stale`; borrar la cuenta elimina en cascada la conexión, repositorios, análisis y evidencia.
- [x] Migraciones aditivas `20260929182047_AddGitHubConnection`, `20260929184051_AddGitHubEvidence` y `20260930181436_StoreGitHubConnectionStateServerSide` aplicadas a PostgreSQL local.

## Verificación local

- PostgreSQL Docker: `healthy`.
- Backend: compilación limpia, 0 advertencias y 0 errores.
- Backend: 56 pruebas aprobadas, 0 fallidas y 0 omitidas.
- Frontend: lint aprobado.
- Frontend: compilación de producción aprobada.
- Frontend: 7 pruebas aprobadas en 4 archivos.
- EF Core: migraciones aplicadas y modelo sin cambios pendientes.
- Playwright: recorrido aislado aprobado para conexión persistida, repositorio seleccionado, evidencia con SHA/ruta y composición con Job Intelligence y Matching.

Las pruebas cubren conexión y replay, paginación y rate limits, budgets y coherencia de SHA, revocación idempotente, consentimiento explícito, máximo de cinco repositorios, rechazo de repositorios no autorizados, ownership, análisis durable, persistencia de evidencia, falsos positivos, determinismo, desconexión y ciclo `Active`/`Stale`.

## Gate externo validado

Se registró una GitHub App real, se guardaron sus secretos fuera del repositorio, se autorizó únicamente `mausalinass/ProofPath` con permisos `Metadata: read` y `Contents: read`, y se completó el recorrido GitHub.com → callback local → selección explícita → Worker. El análisis finalizó en estado `Completed` y produjo evidencia con SHA, ruta y confianza de extracción.

No hubo commits, pushes ni despliegues. Todo el trabajo local anterior se conservó.
