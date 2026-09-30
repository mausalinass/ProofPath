# ProofPath — Ejecutar y verificar Résumé Intelligence

## Inicio local

Desde `C:\Users\mausa\Projects\ProofPath`, conservar Docker abierto y PostgreSQL healthy. La migración de Sprint 2 ya se aplicó localmente.

API, en una terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project apps/backend/src/ProofPath.Api --no-launch-profile -- --urls http://localhost:5001
```

Frontend, en otra terminal:

```powershell
cd apps/web
npm run dev
```

Worker, en una tercera terminal desde la raíz. Debe usar la misma base que la API. Su UserSecretsId es diferente: no se copiaron secretos entre proyectos. Configurar su `ConnectionStrings:DefaultConnection` de forma privada mediante user-secrets o entorno. El ejemplo siguiente solicita los valores ocultando su entrada; no pegarlos en el chat ni guardarlos en Git.

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = Read-Host 'Conexión PostgreSQL local de ProofPath' -MaskInput
$env:OpenAI__ApiKey = Read-Host 'Clave API OpenAI' -MaskInput
dotnet run --project apps/backend/src/ProofPath.Worker
```

Estos valores duran solo en esa terminal y sus procesos hijos. Si todavía no hay clave, el upload funciona y el Worker deja un error recuperable `PROVIDER_NOT_CONFIGURED`; configurar la clave, reiniciar Worker y usar **Retry analysis**. Nunca hay un proveedor sintético de fallback en producción.

El modelo por defecto es `gpt-6-astra`, reasoning `medium`. La configuración admite `OpenAI:Model`, `OpenAI:ReasoningEffort`, `OpenAI:TimeoutSeconds` (150 por defecto) y `OpenAI:ResumeMaxOutputTokens` (12.000 por defecto). El límite del Worker es cuatro minutos y la reserva dura cinco.

Almacenamiento local por defecto: `%LOCALAPPDATA%\ProofPath\private-resumes`. API y Worker deben usar la misma cuenta/directorio. Se puede configurar `Storage:PrivateRoot` en ambos. Usar un directorio privado fuera del repositorio, webroot y carpetas sincronizadas. El almacenamiento local se habilita solo en Development/Testing.

Abrir My Evidence o `/onboarding/resume`: subir PDF/DOCX → esperar análisis → revisar source citations → guardar correcciones → marcar confirmación → confirmar hechos. Un nuevo upload conserva los hechos activos hasta confirmar explícitamente su reemplazo.

## Contratos añadidos

Todos requieren sesión; escrituras requieren `X-CSRF-TOKEN` y cookie antiforgery.

| Método/ruta | Resultado |
| --- | --- |
| POST `/api/v1/resumes/` | multipart con exactamente un campo file; 202 `{id,analysisJobId}` |
| GET `/api/v1/resumes/` | versiones propias, estado y bandera active |
| GET `/api/v1/resumes/{id}` | metadata propia; 404 para otra cuenta |
| GET `/api/v1/resumes/{id}/download` | descarga autenticada como attachment; nunca storage key pública |
| GET `/api/v1/resumes/{id}/extraction` | snapshot máquina + borrador + revision + confirmation |
| PUT `/api/v1/resumes/{id}/extraction` | `{revision,draft}`; 409 si cambió otra pestaña o ya fue confirmado |
| POST `/api/v1/resumes/{id}/confirm` | `{revision}`; confirmación transaccional e idempotente |
| GET/POST `/api/v1/analysis-jobs/...` | polling/cancel/retry de la cola existente |

El borrado de cuenta elimina inmediatamente acceso, filas personales y trabajos; encola eliminación privada de archivos. El Worker debe permanecer operativo para terminar cleanup y reintentar outages. No se borra un bucket entero.

## S3: configuración local de desarrollo verificada

El bucket privado de desarrollo `proofpath-dev-resumes` está configurado en `us-east-2` con los cuatro flags de Block Public Access, cifrado SSE-S3, website hosting deshabilitado y acceso de mínimo privilegio para el usuario técnico local. El adaptador usa PutObject/GetObject, GetBucketPublicAccessBlock, ListBucketVersions, DeleteObject y DeleteObjectVersion sobre el bucket/prefijo autorizado. El borrado real confirmó que elimina versiones y delete markers de cada objeto exacto; Object Lock/retención legal no se omiten automáticamente.

Configuración no secreta:

```json
{
  "Storage": {
    "Provider": "S3",
    "Bucket": "proofpath-dev-resumes",
    "Region": "us-east-2",
    "FileSecurityReviewed": true
  }
}
```

`FileSecurityReviewed=true` documenta únicamente la revisión y prueba local con archivos sintéticos. No representa antivirus ni aprobación automática de privacidad o producción. P11 debe cerrarse antes de datos públicos, staging compartido o producción. Las credenciales permanecen fuera de appsettings mediante el perfil/cadena normal del SDK. No cambiar el proveedor sobre archivos locales existentes sin una migración de objetos revisada.

## Pruebas

Backend: `dotnet test apps/backend/ProofPath.slnx` (Docker abierto). Usa bases y almacenamiento temporales.

Frontend, desde apps/web: `npm test`, `npm run build`, `npm run lint`.

Navegador: crear una base PostgreSQL exclusiva de pruebas y configurar `PROOFPATH_E2E_CONNECTION` y `PROOFPATH_E2E_STORAGE` hacia recursos aislados. Ejecutar `npm exec playwright install chromium` y `npm exec playwright test`. El host de pruebas aplica las migraciones a esa base y usa un proveedor sintético separado del código de producción. Nunca apuntar esas variables a desarrollo con datos personales, staging o producción.

La CI incluye este mismo flujo con PostgreSQL de servicio. No hay resultado remoto hasta ejecutar el workflow mediante un push autorizado.

## Referencias

- [PdfPig: lectura de texto y advertencias sobre orden de contenido](https://github.com/UglyToad/PdfPig).
- [OpenAI: Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs).
- [OpenAI: GPT-6 Astra](https://developers.openai.com/api/docs/models/gpt-6-astra).
- [AWS: versiones de objetos y delete markers](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/S3/TS3ObjectVersion.html).

`store:false` solicita no almacenar la respuesta para recuperación posterior; las condiciones de tratamiento/retención de la cuenta OpenAI todavía necesitan revisión antes de producción.
