# ProofPath — integración del diseño Collage

Fecha: 2026-11-04. Estado actualizado 2026-11-09: rediseño activo en staging; correcciones de accesibilidad de la sección 5 verificadas localmente y pendientes de promover a staging.

La dirección aprobada **01 Collage** está integrada en `apps/web`, la aplicación React existente. La navegación conserva el documento y utiliza los clientes API, cookies de sesión y protección CSRF existentes. Los datos del prototipo no se utilizan como resultados del producto.

## Referencia y fidelidad

Referencia aprobada: `proofpath-app-unificado.html`, con las pantallas previas de landing, login, registro, fuentes y dashboard. Se conservaron la paleta, tipografía Arial/Helvetica, composición Collage, bordes negros, sombras duras, tarjetas redondeadas y textura CSS.

Tokens: fondo `#f5f2e9`, superficie `#fffef9`, tinta `#171914`, verde `#a7f83b`, amarillo `#f5e69b`. Las cuatro ilustraciones viven en `CollageArt.tsx` y `CollageArt.css`: cada tarjeta, icono y conector es un elemento independiente, sin imágenes planas ni dependencias externas.

El camino SVG de `ProgressPath.tsx` avanza y retrocede exclusivamente según el scroll de la página. Las capas flotan con CSS y pueden pausarse desde la navegación. `prefers-reduced-motion` elimina la flotación y actualiza el recorrido sin interpolación. El camino es decorativo: no representa resultados reales de análisis ni pasos completados de la cuenta.

La fidelidad es de sistema visual y composición, no una afirmación de equivalencia píxel a píxel: los datos reales, errores, formularios de revisión y controles de seguridad necesitan más espacio que las muestras del prototipo. Los formularios detallados existentes mantienen algunos textos en inglés.

## Pantallas y comportamiento real

| Ruta | Funcionalidad |
| --- | --- |
| `/` | Landing Collage, camino por scroll, acceso al flujo de cuenta |
| `/login`, `/signup` | Autenticación real, contraseña visible/oculta, confirmación de contraseña, consentimiento y verificación de correo según configuración |
| `/sources` | Conexión a GitHub App y subida privada de PDF/DOCX de hasta 10 MB; después de subir abre la versión correcta para revisión |
| `/home`, `/dashboard` | Dossier con repositorios seleccionados, evidencia activa, habilidades declaradas en el CV confirmado, gaps y recomendaciones de la última vacante confirmada |
| `/onboarding/profile`, `/evidence` | Edición del perfil existente |
| `/onboarding/github` | Selección explícita de repositorios, análisis, progreso y evidencia con revisión SHA; el análisis guarda antes la selección actual |
| `/onboarding/resume?resume=…` | Extracción, corrección, confirmación, versiones y descarga privada |
| `/jobs?job=…` | Descripción de vacante, revisión de requisitos, confirmación, matching determinista, trazabilidad, recomendaciones, historial y seguimiento |
| `/settings` | Estado GitHub, revocación confirmada, versiones de CV y eliminación de cuenta con contraseña |
| `/privacy`, `/forgot-password`, `/reset-password`, `/verify-email` | Aviso y acciones de cuenta existentes con el sistema visual común |

Los requisitos modificados deben guardarse antes de confirmarlos. Missing, Uncertain y NotEvaluated permanecen diferenciados y conservan texto además de color. La confianza no se presenta como probabilidad de contratación. Las habilidades declaradas en un CV no se etiquetan como dominio demostrado.

La revocación llama al endpoint real de GitHub, después de confirmar en Privacidad. La eliminación utiliza la operación de cuenta existente, explica su alcance irreversible y solicita la contraseña. No existe un botón ficticio de restauración. El historial muestra versiones reales de CV; no inventa un historial consolidado de todos los datos.

## Verificación local

- `npm run build`: compilación de producción correcta.
- Smoke del bundle compilado con `vite preview`: entrada pública dirigida a login, navegación al dossier con sesión real, móvil sin desbordamiento y sin errores JavaScript de ejecución en ese recorrido.
- `npm run lint`: sin errores.
- `npm test`: 13 pruebas, 7 archivos, correctas.
- `scripts/test-browser-journeys.ps1`: 5 recorridos con PostgreSQL aislado, API real y Worker con proveedor sintético.
- Registro, cierre/inicio de sesión, perfil, borrado de cuenta; rechazo de archivo no permitido en la nueva pantalla Fuentes; subida, extracción, corrección y confirmación del CV.
- Evidencia GitHub sintética persistida, edición y guardado de requisitos antes de confirmar, scoring determinista, trazabilidad, recomendaciones, seguimiento y nuevos snapshots.
- Navegación sin recarga, cancelación de revocación/borrado, scroll reversible, pausa y movimiento reducido.
- Comprobaciones de anchura a 1440, 768, 390 y 320 px, incluyendo el resultado detallado del match. Se corrigió el desbordamiento causado por el tamaño mínimo de la rejilla y citas largas de código.
- Revisión visual de capturas de landing, login, registro, fuentes, dossier, matcher y privacidad. Capturas reproducibles en `apps/web/test-results`; selección conservada en `docs/mockups/collage-implementation`.
- `git diff --check` correcto; búsqueda acotada de patrones de claves privadas y tokens de alta confianza sin hallazgos en la implementación modificada. No equivale a una auditoría completa del historial Git.
- Enlace de salto, controles nativos etiquetados, foco visible y traslado de foco al contenido al navegar. Contraste de tokens principales entre 6.24:1 y 17.54:1; esto no sustituye una auditoría completa con lectores de pantalla.
- El inspector automático de Taste generó su reporte en `.taste-skill/collage-final`. No se declara aprobado: sus reglas genéricas rechazan sombras, símbolos de check, fondos crema, ausencia de fotografías y varios tamaños de texto presentes en la referencia aprobada. Aquí prevalece el diseño explícito del usuario. El collage es asimétrico deliberadamente, los títulos se adaptan a más líneas en móvil y el símbolo decorativo no es un encabezado semántico. Las comprobaciones de accesibilidad y desbordamiento se verificaron por separado, sin sustituir las ilustraciones por fotografías o eliminar las sombras.

## Abrir una revisión local

Con Docker, Node y .NET disponibles, desde la raíz:

```powershell
./scripts/local-design-review.ps1
```

Abrir `http://localhost:5173`. El script crea una base PostgreSQL local y aislada, arranca API, Worker sintético y Vite. No carga credenciales GitHub ni OpenAI. Los puertos 5001, 5002 y 5173 deben estar libres. Usar únicamente información sintética: el proveedor de extracción devuelve fixtures conocidos, no análisis de documentos personales.

Para detener exclusivamente esta revisión y eliminar su contenedor de datos sintéticos:

```powershell
./scripts/local-design-review.ps1 -Stop
```

Los archivos sintéticos y logs locales permanecen en `%LOCALAPPDATA%/ProofPath/DesignReview`; no se escriben secretos en el manifiesto de procesos ni en el repositorio. La revisión no publica puertos de base de datos fuera de loopback.

## Límites y siguiente etapa

No se modificó AWS ni se desplegó este rediseño. No se repitió el OAuth real externo de GitHub, ni la entrega real de correos, ni la extracción pagada de OpenAI. La revocación externa se deja para una cuenta sintética autorizada en staging; las pruebas locales comprueban su confirmación/cancelación. Las restricciones de registro público y procesamiento de datos reales siguen vigentes.

El repositorio ya tenía cambios de frontend sin commit al comenzar. Se preservó una copia previa fuera del repositorio; no se sobrescribieron fuentes sincronizadas ni se creó un commit mezclando automáticamente todo ese trabajo.

Siguiente paso del roadmap: revisión independiente de esta implementación, resolución de hallazgos y validación sintética completa en staging. La aprobación visual y esta verificación local no certifican producción.

## Correcciones de validación — 2026-11-09

La revisión independiente de las pantallas desplegadas no encontró un bloqueo funcional, pero dejó tres observaciones menores. Se corrigieron en conjunto y se añadieron aserciones de regresión:

- La navegación SPA ahora escucha activaciones de enlaces a nivel de documento y conserva enlaces semánticos. El contenedor visual `.collage` ya no tiene un manejador `onClick`; Cmd/Ctrl-click, enlaces externos, descargas, hashes y rutas API conservan su comportamiento nativo.
- `main#workspace-content` conserva el traslado de foco y muestra un outline de 3 px bajo `:focus-visible`. Playwright confirma que el skip link es el primer foco, que Enter traslada el foco a `main` y que el indicador calculado es sólido.
- Los campos internos de búsqueda de repositorios, oportunidades, seguimiento, revisión de requisitos, recomendaciones y perfil declaran nombres estables y política de autocompletado. Correos, URLs y búsqueda desactivan correcciones automáticas cuando corresponde.

Verificación posterior: `npm run lint`, 13 pruebas Vitest, build de producción y los 5 recorridos de `scripts/test-browser-journeys.ps1` aprobaron. El navegador confirmó navegación sin recarga, foco visible y ausencia de un manejador inline en el contenedor. La base y el almacenamiento sintéticos de cada recorrido fueron aislados y retirados al finalizar; no se utilizaron datos personales ni servicios de staging.
