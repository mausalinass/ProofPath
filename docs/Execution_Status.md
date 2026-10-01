# ProofPath — Estado actual de ejecución

> Sprints 1–7 implementados localmente. No existe despliegue público y el flujo de producción queda bloqueado hasta cerrar los gates críticos de seguridad.

Fecha: 1 de octubre de 2026.

## Estado actual

- Sprints 1–6: funciones principales implementadas y validadas localmente.
- Sprint 7: contenedores, infraestructura declarativa, CI/CD, landing, health checks, observabilidad base y documentación implementados localmente.
- GitHub App: instalada con acceso de solo lectura al repositorio seleccionado.
- AWS: cuenta, MFA, bucket privado y usuario técnico configurados; la base completa de producción y los roles OIDC mínimos siguen pendientes.
- Despliegue: no ejecutado.
- Cambios previos al despliegue: auditados y preparados como un único conjunto.

## Pendientes

La lista única y vigente está en [Auditoría previa al despliegue](Pre_Deployment_Audit.md). Los archivos de estado de cada sprint conservan evidencia detallada, pero esta auditoría prevalece si un texto histórico se contradice.

## Evidencia de validación

| Verificación | Resultado |
| --- | --- |
| Backend | 86 pruebas aprobadas; build sin advertencias ni errores |
| Frontend | 12 pruebas aprobadas; ESLint y build correctos |
| Navegador | 3 recorridos completos aprobados con PostgreSQL aislado |
| Dependencias | NuGet y npm: 0 vulnerabilidades conocidas |
| Contenedores | API, worker y web construyen como usuarios no administradores; smoke web y encabezados aprobados |
| AWS | bootstrap, producción y frontend aprobados por cfn-lint |
| Credenciales | Sin patrones de claves OpenAI, AWS, GitHub o PEM en árbol e historial Git |