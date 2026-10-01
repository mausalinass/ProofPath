# ProofPath — Sprint 6: Recommendations & Tracking

Fecha de cierre local: 2026-09-30.

## Estado

Implementación local completada. El usuario puede guardar el estado de una candidatura, actuar sobre recomendaciones ordenadas y comparar versiones inmutables del match.

## Alcance implementado

- Tracking por empleo con estados `Saved`, `Preparing`, `Applied`, `Interviewing`, `Offer`, `Rejected` y `Withdrawn`.
- Notas privadas y fecha de próxima acción, con ownership derivado de la sesión.
- Recomendaciones deterministas ligadas al `MatchResult` que las originó; nunca se reescriben resultados anteriores.
- Ranking estable de hasta cinco gaps por prioridad, tipo y requisito, con acción concreta según el tipo de brecha.
- Estado accionable por recomendación: `Open`, `InProgress`, `Completed` o `Dismissed`.
- Recomendación de mantenimiento cuando el resultado no tiene gaps puntuados.
- Reanálisis explícito que crea un nuevo snapshot de match y un conjunto nuevo de recomendaciones.
- Historial comparativo con puntuación, diferencia frente a la versión previa, cobertura, clasificación y fecha.
- API autenticada y UI responsive integradas en Job Intelligence.

## Persistencia y migración

La migración `20260930231530_AddRecommendationsAndTracking` fue revisada y aplicada en PostgreSQL local. Agrega únicamente `JobTrackings` y `MatchRecommendations`, con cascadas desde `Jobs` y `MatchResults` y ranking único por snapshot.

## Verificación

- Build backend sin errores ni warnings.
- 82 pruebas backend aprobadas, incluida una prueba integral de ownership, tracking, ranking, cambio de estado, reanálisis e historial.
- 11 pruebas frontend aprobadas; el flujo de Job Intelligence cubre tracking, recomendaciones e historial.
- ESLint y build de producción aprobados.
- Recorrido Playwright de Sprints 3–6 extendido para guardar una candidatura, completar una recomendación, reanalizar y observar dos resultados inmutables.
- Modelo EF y migración sincronizados; migración aplicada en la base local.

## Próximo bloque

Sprint 7: endurecimiento para producción, CI/CD, observabilidad, seguridad operativa, staging y despliegue controlado.
