# Auditoría previa al despliegue

Fecha: 1 de octubre de 2026.

## Resultado por sprint

| Sprint | Estado del producto | Pendientes reales |
| --- | --- | --- |
| 1 — identidad y perfil | Implementado y probado localmente | Verificación de correo y recuperación de contraseña antes de habilitar registro público. El registro queda desactivado por defecto en producción. |
| 2 — résumé e inteligencia | Implementado y probado localmente | Incorporar antivirus/aislamiento de archivos, aprobar privacidad/retención del proveedor de IA y validar el flujo con datos sintéticos en staging. |
| 3 — GitHub | Implementado y probado con la GitHub App real | Cambiar y comprobar el callback público después de disponer de la URL final. |
| 4 — jobs | Implementado y probado localmente | Validación final en staging con datos sintéticos. |
| 5 — matching | Implementado y calibrado localmente | Observar resultados reales controlados antes de cambiar pesos o umbrales. |
| 6 — tracking y recomendaciones | Implementado y probado localmente | Validación final en staging. |
| 7 — preparación de producción | Artefactos locales implementados | Crear la base AWS y roles mínimos, cerrar TLS CloudFront→ALB, presupuesto/alertas, exportador OTLP, ejecutar CI remota, desplegar staging, obtener URL pública y grabar demo. |

## Bloqueos críticos antes de producción

1. **Seguridad de archivos:** integrar un scanner antimalware y una zona de cuarentena. El despliegue falla mientras PROOFPATH_FILE_SECURITY_REVIEWED no sea true.
2. **Privacidad de IA:** aprobar por escrito la política de privacidad y retención del proveedor. El despliegue falla mientras PROOFPATH_OPENAI_PRIVACY_REVIEWED no sea true.
3. **TLS del origen:** configurar HTTPS con certificado válido entre CloudFront y ALB. La plantilla actual conserva HTTP en ese tramo y el despliegue falla mientras PROOFPATH_TRANSPORT_REVIEWED no sea true.
4. **Base AWS mínima:** crear VPC/subredes, RDS privado y cifrado, Secrets Manager, ECR, bucket privado, roles de tarea/ejecución y rol OIDC de despliegue con privilegios mínimos.
5. **Staging:** aplicar migraciones y ejecutar las pruebas de humo con información sintética antes de producción.

## Pendientes importantes, no bloqueantes para un staging privado

- Verificación de correo y recuperación de contraseña.
- Presupuesto mensual y alarmas de coste; alarmas adicionales de latencia, salud y cola.
- Exportación OpenTelemetry hacia un collector revisado.
- Ejecutar el flujo CI en GitHub después de publicar este commit.
- Actualizar el callback de GitHub App a la URL pública.
- Crear URL pública y video de demostración.

## Protecciones comprobadas o añadidas

- Sin patrones de credenciales OpenAI, AWS, GitHub ni claves privadas en el árbol actual o en todo el historial Git.
- Los archivos .env, binarios, logs y artefactos de prueba están ignorados; .env.example contiene solo ejemplos.
- Secretos de producción se inyectan desde AWS Secrets Manager y no desde archivos del repositorio.
- La API no registra cuerpos, cookies, tokens, textos de résumé, job descriptions ni cadenas de conexión.
- Cookies de sesión y CSRF seguras; sesiones de ocho horas; bloqueo después de cinco fallos.
- Claves internas de cifrado de sesión persistidas en PostgreSQL para sobrevivir reinicios.
- Registro público desactivado por defecto en producción.
- S3 y el hosting web permanecen privados y cifrados.
- El ALB devuelve 403 salvo que reciba el encabezado secreto añadido por CloudFront.
- CI usa permisos de solo lectura, pruebas, auditorías de dependencias y construcción de contenedores.
- El workflow de producción usa OIDC y gates explícitos; no usa access keys persistentes.
## Evidencia de cierre local

- Backend: 86 pruebas aprobadas; compilación sin advertencias.
- Frontend: 12 pruebas, lint y build aprobados.
- Navegador: 3 recorridos completos con PostgreSQL temporal.
- Dependencias: 0 vulnerabilidades reportadas por NuGet y npm.
- Contenedores: tres imágenes construidas como usuarios no administradores; smoke web y encabezados aprobados.
- Infraestructura: tres plantillas aprobadas por cfn-lint.