# Fexit — servicio de acciones sobre equipos

Ejecuta acciones sobre equipos de planta (PLC Siemens por S7, Schneider y otros por Modbus TCP) y
las expone en dos verbos. Lo consume Dixit, que es quien tiene la autorización, la cola, la
auditoría y el consumo.

**Diseño:** `DixitBE/docs/superpowers/specs/2026-09-08-fexit-servicio-acciones-design.md`

## Correr

```bash
dotnet run --project Web
```

Config en `Web/appsettings.json` → `FexitSettings`: `ApiKey`, `ConnectionString`, `TimeoutEquipoMs`.
Las migraciones se aplican solas al arrancar, con fail fast: si no corren, la app no levanta.

Para crear una migración nueva, `--startup-project` es **Infrastructure**, no Web (el paquete
`EntityFrameworkCore.Design` vive sólo ahí):

```bash
dotnet ef migrations add <Nombre> --project Infrastructure --startup-project Infrastructure
```

## El contrato

Todo pide `X-Api-Key`, menos `/health`.

| Verbo | Ruta | Para qué |
|---|---|---|
| GET | `/acciones` | El catálogo publicado. Lo lee el ABM de Dixit. |
| POST | `/acciones/{codigo}/ejecutar` | Ejecuta. Cuerpo: `{"modoEsperado":"escritura"}`. |
| GET/POST/DELETE | `/catalogo/equipos` | Carga de equipos. |
| GET/POST | `/catalogo/equipos/{id}/enclavamientos` | Carga de enclavamientos. |
| DELETE | `/catalogo/enclavamientos/{id}` | Borra uno. Ojo: cuelga de `/catalogo`, no de su equipo. |
| GET/POST/PUT/DELETE | `/catalogo/acciones` | Carga de acciones. |

`modoEsperado` no describe la acción: la **verifica**. Si en este catálogo el código no es de ese
modo, la respuesta es 409 y no se ejecuta nada.

## Probarlo sin PLC

Hay un protocolo `Simulado` que se comporta como un equipo: guarda lo escrito y lo devuelve al leer,
y una dirección que nadie escribió lee 0. La IP `10.255.255.255` simula un equipo caído.

```bash
K="-H X-Api-Key:CAMBIAR_EN_DEPLOY -H Content-Type:application/json"

# 1. Un equipo simulado
curl -s $K -X POST localhost:5279/catalogo/equipos -d '{
  "nombre":"bomba3","tipoEquipo":"plc","ip":"10.0.0.50","puerto":502,
  "protocolo":"Simulado","rack":0,"slot":0}'
# -> 1

# 2. Un enclavamiento del equipo (en condición sólo con el valor 1)
curl -s $K -X POST localhost:5279/catalogo/equipos/1/enclavamientos -d '{
  "direccion":"40010","tipoDireccion":"HoldingRegister",
  "nombre":"Portón de playa","valoresOk":[1],"orden":1}'

# 3. Una lectura y una escritura
curl -s $K -X POST localhost:5279/catalogo/acciones -d '{
  "codigo":"estado_bomba3","descripcion":"Estado de la bomba","modo":"lectura","equipoId":1,
  "usaEnclavamientos":true,"habilitada":true}'
curl -s $K -X POST localhost:5279/catalogo/acciones -d '{
  "codigo":"arrancar_bomba3","descripcion":"Arranca la bomba","modo":"escritura","equipoId":1,
  "direccion":"40021","tipoDireccion":"HoldingRegister","valor":1,
  "usaEnclavamientos":true,"habilitada":true}'

# 4. El catálogo que ve Dixit
curl -s $K localhost:5279/acciones

# 5. La lectura: el simulado arranca en 0, así que sale "no"
curl -s $K -X POST localhost:5279/acciones/estado_bomba3/ejecutar -d '{"modoEsperado":"lectura"}'
# -> {"exito":true,"detalle":"1 fuera de condición.",
#     "columnas":["enclavamiento","en condicion"],
#     "filas":[{"enclavamiento":"Portón de playa","en condicion":"no"}]}

# 6. La escritura aborta: la precondición no da
curl -s $K -X POST localhost:5279/acciones/arrancar_bomba3/ejecutar -d '{"modoEsperado":"escritura"}'
# -> {"exito":false,"detalle":"Precondición no cumplida: Portón de playa."}

# 7. El modo que no coincide: 409, y no se ejecuta nada
curl -si $K -X POST localhost:5279/acciones/arrancar_bomba3/ejecutar -d '{"modoEsperado":"lectura"}' | head -1
# -> HTTP/1.1 409 Conflict
```

## Conectarlo con Dixit

En `DixitBE/Web/appsettings.json`:

```json
"FexitSettings": { "BaseUrl": "http://localhost:5279", "ApiKey": "CAMBIAR_EN_DEPLOY", "TimeoutSeconds": 10 }
```

Y en Dixit, dar de alta la acción en `/api/acciones` eligiendo el código de
`GET /api/acciones/disponibles-en-fexit`, que es este mismo catálogo.

## Lo que Fexit NO hace

No autoriza a nadie: proyecto, rol y usuario los resuelve Dixit. No guarda ejecuciones: la auditoría
tiene una sola fuente de verdad y es Dixit. No tiene cola ni reintentos, por lo mismo. Y no se
publica a internet: escucha sólo en la red de planta.
