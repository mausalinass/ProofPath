# ProofPath - baseline y Sprint 1.5B

Fecha: 18 de septiembre de 2026.
Repositorio: C:/Users/mausa/Projects/ProofPath.

## Resultado inicial (antes de habilitar Docker)

- [x] Ruta vigente y working tree reinspeccionados.
- [x] CandidateProfile y mapping existentes conservados: Guid Id, UserId string requerido/único, relación 1:1 a Identity y cascade.
- [x] Backend compilado con --no-restore: 0 errores, 0 advertencias.
- [x] Tests existentes ejecutados con --no-build --no-restore: 1 aprobado. Es un test de plantilla; no demuestra auth ni ownership.
- [x] Archivos de migraciones revisados: InitialCreate y AddIdentity; ninguna CandidateProfile identificada. Snapshot no contiene CandidateProfile.
- [x] Git status posterior coincide con el inicial; no se cambiaron archivos de producto, Identity, frontend ni migraciones.
- [ ] Estado real de PostgreSQL y destino EF verificados.
- [ ] Historial/esquema de BD comparados contra migraciones/snapshot.
- [ ] Migración CandidateProfile generada y SQL revisado.
- [ ] Migración aplicada y esquema/historial verificados.
- [ ] Resultado remoto de CI verificado.

## Bloqueo operativo

Docker CLI se localizó en AppData/Local/Programs/DockerDesktop/resources/bin/docker.exe. Las consultas Compose fallan porque no existe el pipe dockerDesktopLinuxEngine. Se intentó iniciar Docker Desktop; sus procesos aparecen, pero el motor siguió sin responder y 127.0.0.1:5432 rechazó la conexión durante las comprobaciones.

No se puede afirmar que CandidateProfiles no exista en la base persistente a partir de la ausencia de migración en este checkout. Por la precondición expresa del usuario, no se generó ni aplicó la migración sin descartar duplicados/drift en la base real. No se leyeron archivos de secretos ni se imprimieron credenciales. No se borraron ni recrearon contenedores/volúmenes.

## Archivos protegidos

Todos los cambios previos sin commit siguen presentes: Program.cs, DbContext, snapshot, csproj Infrastructure y nuevos AuthEndpoints, CandidateProfile, ApplicationUser, AddIdentity/Designer. Las compilaciones generaron únicamente sus resultados habituales bin/obj; el estado Git de fuente no cambió.

## Continuación

Cuando Docker Desktop muestre el motor listo, comprobar Compose/PostgreSQL y el destino local configurado sin imprimir secretos; consultar el historial EF y esquema Identity/perfil. Si no hay drift ni migración equivalente, ejecutar exclusivamente 1.5B con revisión de SQL y aplicación local aditiva autorizada. No comenzar 1.5C hasta verificar 1.5B y recibir confirmación del usuario.

## Actualización: 1.5B completado

Tras la confirmación del usuario de Docker abierto, se inició únicamente el servicio postgres del Compose existente, conservando su volumen. PostgreSQL está healthy.

- [x] EF confirmó destino: localhost:5432, base proofpath, entorno Development; no se imprimió la cadena de conexión.
- [x] Historial previo: InitialCreate y AddIdentity; CandidateProfiles no existía. Columnas e índices Identity consultados coincidían con AddIdentity.
- [x] Generada 20260918221119_AddCandidateProfile mediante EF; compilación correcta.
- [x] SQL revisado: CREATE TABLE CandidateProfiles, PK uuid, UserId text NOT NULL, UNIQUE UserId, FK AspNetUsers ON DELETE CASCADE, campos existentes y timestamps with time zone. Ningún cambio al esquema Identity.
- [x] Aplicada únicamente AddCandidateProfile en la base local autorizada.
- [x] Esquema e historial consultados después: constraints y tipos correctos; tercera migración registrada.
- [x] EF has-pending-model-changes: ningún cambio pendiente.

Archivos de código de esta unidad: nueva migración y Designer; snapshot actualizado automáticamente. Sin cambios al modelo, DbContext, handlers Identity/auth, frontend o configuración. La comprobación de tipos timestamp valida almacenamiento compatible con UTC; la asignación de DateTime UTC por los casos de uso corresponde al futuro bloque de endpoints.

No se crearon endpoints ni se comenzó 1.5C. No se hicieron commits, pushes ni despliegues. CI remoto y pruebas auth/ownership siguen fuera de la evidencia de esta unidad; el test de baseline aprobado anteriormente es de plantilla.

Siguiente bloque, después de confirmación del usuario: 1.5C, leer/crear perfil propio.
