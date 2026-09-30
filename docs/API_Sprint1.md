# ProofPath — Contratos implementados de Sprint 1 y cola de análisis

Todos los endpoints privados requieren cookie de sesión. Todas las escrituras bajo `/api` requieren cookie antiforgery y header `X-CSRF-TOKEN`, obtenido mediante `GET /api/v1/auth/csrf`. Solicitar un token nuevo después de login/logout evita reutilizar un token asociado a otra identidad. Las respuestas API tienen `Cache-Control: no-store`.

| Método/ruta | Contrato |
| --- | --- |
| GET `/api/v1/auth/csrf` | `{token}` y cookie antiforgery HttpOnly |
| POST `/api/v1/auth/register` | email, password, firstName requerido, lastName opcional; 201 o ValidationProblem |
| POST `/api/v1/auth/login` | email/password; 200 y cookie o 401 genérico |
| POST `/api/v1/auth/logout` | 204 y cookie retirada |
| GET `/api/v1/auth/me` | id/email; 401 anónimo |
| GET `/api/v1/profile` | perfil propio, 404 si el usuario anterior aún no lo creó |
| PUT `/api/v1/profile` | firstName, lastName, headline, location, workAuthorization, educationSummary; 200 perfil persistido |
| GET `/api/v1/home` | perfil real, profileComplete y nextAction; módulos futuros aún no disponibles |
| DELETE `/api/v1/account` | `{confirm:true,password}`; 204, o 400 sin confirmación/contraseña correcta |
| GET `/api/v1/analysis-jobs/{id}` | estado propio, tipo/recurso/intentos/error saneado; 404 si no pertenece al usuario |
| POST `/api/v1/analysis-jobs/{id}/cancel` | cancela Pending/Processing/Failed; 204 o 404 si no existe/no puede cancelarse |
| POST `/api/v1/analysis-jobs/{id}/retry` | fallo retryable → Pending; 202 o 404 si no existe/no es retryable |

Aliases `/api/auth/register`, `/login`, `/logout` y `/me` preservados. También requieren CSRF en escrituras. El límite de autenticación devuelve 429 y se configura con `Security:AuthRequestsPerMinute` (20 por IP/minuto por defecto). Se reutilizan la política de contraseña y los valores de lockout de Identity existentes.

El PUT de perfil admite solo los seis campos editables: id, userId, timestamps y otros miembros son rechazados con 400. Id y CreatedAt se preservan; UpdatedAt se actualiza en UTC. Campos omitidos equivalen a null: PUT reemplaza el contenido editable completo. El navegador nunca elige al propietario.

CORS usa `Frontend:Origins` explícitos; localhost:5173 es el default solo en Development/Testing. Producción exige configuración explícita y cookies Secure. El frontend local usa el proxy de Vite hacia la API en localhost:5001.

## Ejecución de verificaciones

Backend: `dotnet test apps/backend/ProofPath.slnx`. Docker debe estar abierto: Testcontainers crea su PostgreSQL aislado y no usa los datos locales.

Frontend, desde apps/web: `npm test`, `npm run lint`, `npm run build`.

E2E: configurar `PROOFPATH_E2E_CONNECTION` hacia una DB PostgreSQL exclusiva de pruebas, aplicar las migraciones existentes allí e instalar Chromium con `npm exec playwright install chromium`. Luego `npm exec playwright test`. La configuración rechaza ejecutar E2E sin esa variable y no reutiliza servidores existentes. No usar una conexión de staging/producción ni la DB de datos personales.

El Worker consume la cola con su configuración segura de conexión. Todavía no existen productores/handlers de résumé, repositorios o JD; esas funcionalidades no se presentan como implementadas.
