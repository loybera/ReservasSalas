# Reservas de salas

API REST en .NET 10 para reservar salas de reunión, con ASP.NET Core (Minimal APIs), Entity Framework Core y PostgreSQL.

## Cómo correrlo

Solo hace falta Docker. Los datos de PostgreSQL viven en un volumen externo, que se crea una sola vez:

```bash
docker volume create api-exercise-db
docker compose up --build
```

Si el volumen ya tenía una base inicializada con otro usuario o contraseña, PostgreSQL ignora las variables `POSTGRES_*` del compose. En ese caso hay que usar esas credenciales en `ConnectionStrings__Default`, o recrear el volumen vacío.

- API: http://localhost:8080
- Documento OpenAPI: http://localhost:8080/openapi/v1.json
- Health check: http://localhost:8080/health
- Ejemplos de requests listos para usar: `src/ReservasSalas.Api/ReservasSalas.Api.http` (Visual Studio, Rider o la extensión REST Client de VS Code).

### Tests

Requieren el SDK de .NET 10 y Docker en ejecución (los de integración levantan un PostgreSQL con Testcontainers):

```bash
dotnet test
```

## Endpoints

| Método | Ruta | Descripción | Respuestas |
| --- | --- | --- | --- |
| POST | `/api/salas` | Crea una sala | 201, 400, 409 (nombre repetido) |
| GET | `/api/salas` | Lista las salas | 200 |
| GET | `/api/salas/{id}/reservas?fecha=2026-12-01&zonaHoraria=America/Santiago` | Reservas activas de la sala en ese día | 200, 400, 404 |
| POST | `/api/reservas` | Crea una reserva | 201, 400, 404 (sala), 409 (superposición) |
| GET | `/api/reservas/{id}` | Obtiene una reserva | 200, 404 |
| DELETE | `/api/reservas/{id}` con header `X-User-Email` | Cancela (solo el organizador) | 204, 401, 403, 404 |

Todos los errores usan Problem Details (RFC 9457) con `Content-Type: application/problem+json` e incluyen un `traceId`.

## Decisiones de diseño

### 1. La no superposición la garantiza la base de datos

Es el punto central del ejercicio. La solución intuitiva, consultar si hay una reserva superpuesta y después insertar, **no funciona con requests simultáneos**: los dos pasan la consulta antes de que cualquiera inserte, y se guardan ambas.

Por eso la regla está en PostgreSQL como una restricción de exclusión:

```sql
EXCLUDE USING gist (
    sala_id WITH =,
    tstzrange(inicio, fin, '[)') WITH &&
)
WHERE (cancelada_en IS NULL);
```

- `sala_id WITH =` y `&&` (se superponen): no puede haber dos filas de la misma sala con rangos que se crucen.
- `'[)'`: el fin no se incluye, así 09:00–10:00 y 10:00–11:00 conviven.
- `WHERE (cancelada_en IS NULL)`: las reservas canceladas liberan el horario.
- La extensión `btree_gist` permite combinar la igualdad de `sala_id` con el rango en un mismo índice.

La API intenta insertar directamente y, si PostgreSQL rechaza la fila (código `23P01`), responde 409. Con inserciones superpuestas simultáneas, PostgreSQL puede abortar una por deadlock (`40P01`); también es un choque de horarios, así que se responde 409. Funciona aunque haya varias instancias de la API, y el test `ReservasSimultaneasDelMismoHorario_SoloSeConfirmaUna` lo prueba lanzando 20 reservas en paralelo.

**Alternativas que consideré:**

| Opción | Por qué no la elegí |
| --- | --- |
| Validar en código y después insertar | No es segura ante concurrencia |
| `SELECT ... FOR UPDATE` sobre la sala antes de validar | Correcta, pero serializa todas las reservas de una sala y depende de que todo el código recuerde tomar el bloqueo |
| Transacción `SERIALIZABLE` con reintentos | Correcta, pero agrega lógica de reintentos y más abortos bajo carga |
| Bloqueo en memoria (`lock`, `SemaphoreSlim`) | Deja de funcionar apenas hay dos instancias de la API |

La desventaja de la restricción es que ata la solución a PostgreSQL. Lo acepto porque el enunciado lo fija y porque es la opción más simple que es correcta.

### 2. Fechas siempre en UTC

- La API recibe `DateTimeOffset` en ISO 8601 con zona horaria (`2026-12-01T09:00:00-03:00`), así no hay ambigüedad.
- Se guarda en columnas `timestamptz`, normalizado a UTC.
- La consulta por día acepta una zona horaria IANA opcional, porque "el 1 de diciembre" no empieza a la misma hora en Santiago que en UTC.
- "Ahora" viene de un `TimeProvider` inyectado: los tests de dominio usan una fecha fija y no dependen del reloj.

### 3. Cancelación lógica e idempotente

Cancelar marca `cancelada_en` en lugar de borrar: queda el historial y el horario se libera. Cancelar dos veces devuelve 204 las dos veces.

La identificación por `X-User-Email` es la que pide el enunciado; sin el header se responde 401. En un sistema real el usuario saldría de un token validado (OpenID Connect).

### 4. Reglas de negocio en el dominio

`Sala` y `Reserva` validan sus propias reglas y devuelven un `Resultado<T>` con todos los errores juntos, en lugar de lanzar excepciones para casos esperados. Los endpoints solo traducen ese resultado a HTTP (`Endpoints/ErrorHttp.cs`). Las excepciones quedan para lo inesperado, y el manejador global las convierte en un 500 con Problem Details sin exponer detalles internos.

### 5. Otras decisiones

- **Minimal APIs** agrupadas por recurso: menos ceremonia que controllers para una API de este tamaño.
- **DTOs** separados de las entidades: la API no expone el modelo de persistencia.
- **Emails normalizados** (minúsculas, sin espacios), así `Ana@Empresa.com` puede cancelar la reserva de `ana@empresa.com`.
- **Nombres de sala únicos** sin distinguir mayúsculas.
- **Logs estructurados** en JSON fuera de desarrollo, sin datos personales (el email no se loguea).
- **Tests de integración contra PostgreSQL real**, no contra una base en memoria: la regla principal vive en el motor y solo se puede probar ahí.
- **Esquema con un script SQL idempotente** que se aplica al arrancar (`Data/Schema.sql`). Lo elegí sobre las migraciones de EF Core porque la restricción de exclusión igual hay que escribirla en SQL y el modelo es chico. Es lo primero que cambiaría en un proyecto real (ver abajo).

## Qué dejé afuera

- Reservas recurrentes y paginación (extras opcionales).
- Autenticación real.
- Editar una reserva (hoy se cancela y se crea otra).
- Zona horaria propia de cada sala.

## Qué haría con más tiempo

1. **Migraciones de EF Core** ejecutadas como paso separado del despliegue (un job o `dotnet ef migrations bundle`), en lugar de aplicar el esquema al arrancar. Con varias instancias, que cada una intente modificar el esquema no escala.
2. **Autenticación con OpenID Connect** y autorización por políticas.
3. **Paginación** en los listados y un rango de fechas máximo en las consultas.
4. **Reservas recurrentes** guardando la regla (RRULE) y materializando las ocurrencias, para que la restricción de exclusión siga cubriendo cada una.
5. **OpenTelemetry** para trazas y métricas (latencia p95 por endpoint, cantidad de 409).
6. **Rate limiting** en la creación de reservas.
7. Separar el dominio en su propio proyecto si el sistema crece.
