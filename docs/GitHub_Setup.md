# Configuración local de GitHub App para ProofPath

Esta configuración habilita la validación real de Sprint 3. No copies claves, secretos ni el archivo PEM al repositorio o al chat.

## 1. Registrar la GitHub App

En GitHub abre **Settings → Developer settings → GitHub Apps → New GitHub App** y usa:

- **GitHub App name:** un nombre único, por ejemplo `ProofPath Development <tu usuario>`.
- **Homepage URL:** `http://localhost:5173`.
- **Callback URL:** `http://127.0.0.1:5173/api/v1/github/callback`.
- **Request user authorization (OAuth) during installation:** activado.
- **Setup URL:** vacío.
- **Webhook:** desactivado para el entorno local.
- **Repository permissions:** `Contents: Read-only`; `Metadata: Read-only` queda incluido por GitHub.
- **Account permissions:** ninguno.
- **Where can this GitHub App be installed?:** solo esta cuenta durante desarrollo.

Después de crearla, anota sin compartirlos: **App ID**, **Client ID**, **Client secret** y el slug presente en la URL `github.com/apps/<slug>`.

## 2. Crear y proteger la clave privada

Genera una private key desde la GitHub App y guarda el archivo `.pem` fuera del repositorio, por ejemplo en una carpeta privada del usuario. ProofPath solo necesita la ruta; el contenido de la clave no debe guardarse en `appsettings`, Git ni documentación.

## 3. Guardar configuración en user-secrets

Desde `apps/backend`, sustituye cada texto entre `<...>` localmente:

```powershell
dotnet user-secrets set "GitHub:AppId" "<APP_ID>" --project .\src\ProofPath.Api
dotnet user-secrets set "GitHub:ClientId" "<CLIENT_ID>" --project .\src\ProofPath.Api
dotnet user-secrets set "GitHub:ClientSecret" "<CLIENT_SECRET>" --project .\src\ProofPath.Api
dotnet user-secrets set "GitHub:AppSlug" "<APP_SLUG>" --project .\src\ProofPath.Api
dotnet user-secrets set "GitHub:PrivateKeyPath" "<RUTA_ABSOLUTA_AL_PEM>" --project .\src\ProofPath.Api
dotnet user-secrets set "GitHub:AppId" "<APP_ID>" --project .\src\ProofPath.Worker
dotnet user-secrets set "GitHub:PrivateKeyPath" "<RUTA_ABSOLUTA_AL_PEM>" --project .\src\ProofPath.Worker
```

No uses `PrivateKeyPem` en desarrollo si puedes utilizar `PrivateKeyPath`; así reduces el riesgo de copiar el contenido de la clave.

## 4. Validación real

1. Inicia PostgreSQL, API, Worker y frontend.
2. Registra o inicia sesión en ProofPath.
3. Abre **GitHub evidence** y pulsa **Connect GitHub**.
4. Instala la App solamente en repositorios de prueba y vuelve a ProofPath.
5. Verifica que ninguno esté seleccionado de forma automática.
6. Selecciona uno, guarda la selección y ejecuta el análisis.
7. Confirma que el trabajo termina y que la evidencia muestra repositorio, SHA y archivo fuente.
8. Desconecta GitHub y comprueba que la evidencia pase a `Stale`.

Si GitHub rechaza el callback, comprueba que el frontend esté activo en `127.0.0.1:5173` y que la Callback URL coincida exactamente.
