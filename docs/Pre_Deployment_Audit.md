# Auditoría previa al despliegue

Fecha: 1 de octubre de 2026.

## Cierre de los cinco pasos

| Paso | Estado técnico | Evidencia / acción final |
| --- | --- | --- |
| 1. Seguridad de archivos | Implementado | La imagen oficial de ClamAV está fijada por digest y analiza el archivo antes de reservar metadata o subirlo. Malware devuelve `MALWARE_DETECTED`; un scanner caído devuelve `FILE_SECURITY_UNAVAILABLE`. Ambos casos fallan cerrados. |
| 2. Privacidad y retención de IA | Documento listo; decisión protegida | `store=false`, binario no enviado, email/teléfono redactados. Staging queda limitado a datos sintéticos. Activar la variable únicamente tras aceptar `OpenAI_Privacy_Review.md`. |
| 3. Infraestructura AWS | Declarada y validada localmente | `foundation.yml` crea VPC, subredes, RDS, Secrets Manager, S3, ECR, roles y OIDC. La creación real espera la aprobación del costo. |
| 4. Seguridad de red | Implementada | ALB interno en subredes aisladas, CloudFront VPC Origin, lista administrada de CloudFront y encabezado secreto. No existe origen público directo. |
| 5. Validación de staging | Automatizada; pendiente de ejecución real | El workflow espera servicios estables, comprueba HTTPS/readiness, rechazo sin sesión y bloqueo público de S3. La prueba funcional utilizará solo datos sintéticos. |

## Pendientes antes de ejecutar staging

1. Aceptar expresamente la revisión de privacidad para datos sintéticos.
2. Aprobar la creación de recursos AWS con costo y desplegar `proofpath-foundation` desde una sesión administrativa con MFA.
3. Sustituir los valores `REPLACE` del secreto de aplicación en Secrets Manager.
4. Crear el entorno protegido `staging` en GitHub y colocar sus dos variables y un secreto.
5. Ejecutar CI y `Deploy staging`; actualizar el callback de GitHub App a la URL resultante y realizar el recorrido sintético.

## Pendientes antes de producción con datos reales

- Aceptación separada de la retención del proveedor o aprobación ZDR/MAM.
- Aviso de privacidad al usuario.
- Verificación de correo y recuperación de contraseña antes de habilitar registro público.
- Observación controlada de resultados reales antes de recalibrar matching.

## Protecciones vigentes

- Secretos solo en AWS Secrets Manager o secretos de entorno protegidos.
- `.env`, claves, binarios, logs y artefactos ignorados por Git.
- S3 cifrado, versionado y sin acceso público.
- RDS cifrado, sin acceso público y en subredes aisladas.
- Registro público desactivado por defecto.
- API y worker con roles separados y acceso mínimo al bucket.
- CI usa permisos de lectura; despliegue usa OIDC sin access keys persistentes.
- CloudFront es el único punto público y usa HTTPS para usuarios y conexión VPC privada al ALB.
