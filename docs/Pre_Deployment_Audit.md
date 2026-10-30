# Auditoría previa al despliegue

Fecha de actualización: 30 de octubre de 2026.

## Cierre de los cinco pasos

| Paso | Estado técnico | Evidencia / acción final |
| --- | --- | --- |
| 1. Seguridad de archivos | Implementado | La imagen oficial de ClamAV está fijada por digest y analiza el archivo antes de reservar metadata o subirlo. Malware devuelve `MALWARE_DETECTED`; un scanner caído devuelve `FILE_SECURITY_UNAVAILABLE`. Ambos casos fallan cerrados. |
| 2. Privacidad y retención de IA | Documento listo; decisión protegida | `store=false`, binario no enviado, email/teléfono redactados. Staging queda limitado a datos sintéticos. Activar la variable únicamente tras aceptar `OpenAI_Privacy_Review.md`. |
| 3. Infraestructura AWS | Staging desplegado y producción definida | Staging está operativo. Producción agrega Multi-AZ, 35 días de backup y protección contra eliminación mediante un parámetro explícito que no cambia el costo de staging. |
| 4. Seguridad de red | Implementada | ALB interno en subredes aisladas, CloudFront VPC Origin, lista administrada de CloudFront y encabezado secreto. No existe origen público directo. |
| 5. Validación de staging | Ejecutada | Servicios estables, HTTPS/readiness, rechazo sin sesión, almacenamiento privado y recorrido sintético fueron verificados en las Sesiones 1-4. |

## Staging completado

La fundación, secretos, entorno protegido, CI/CD, callback y recorrido sintético ya fueron configurados y validados. Staging continúa limitado a datos sintéticos.

## Pendientes antes de producción con datos reales

- Aceptación separada de la retención del proveedor o aprobación ZDR/MAM.
- Configurar y verificar la identidad remitente de SES.
- Ejecutar una restauración de snapshot RDS pagada en un entorno aislado.
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
