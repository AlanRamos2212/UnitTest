# Resumen ejecutivo de pruebas

## Objetivo

Validar los endpoints de WebAppApi y comprobar respuestas exitosas y fallos
provocados por datos inválidos o recursos inexistentes.

## Herramienta y estrategia

- xUnit sobre .NET 8.
- `Microsoft.AspNetCore.Mvc.Testing` para levantar la API en memoria.
- SQLite en memoria para aislar cada ejecución de la base real.
- Pruebas HTTP automatizadas contra los endpoints existentes.
- El servidor TCP se desactiva durante estas pruebas HTTP para evitar ocupar el
  puerto `6061`; se valida separadamente con un cliente TCP.

## Cobertura

Se cubren los 11 endpoints HTTP actuales con 3 pruebas por endpoint, para un
total de 33 casos. Se incluyen respuestas exitosas, datos inválidos, recursos
inexistentes, conflictos de relaciones, contratos JSON, persistencia y efectos
posteriores de las operaciones DELETE.

## Resultados

Ejecutar desde la consola:

```text
docker run --rm -v "C:\WebAppApi:/src" -w /src ^
  mcr.microsoft.com/dotnet/sdk:8.0 ^
  dotnet test tests/WebAppApi.Tests/WebAppApi.Tests.csproj ^
  --logger "console;verbosity=minimal"
```

Resultado esperado: 33 pruebas ejecutadas, con 0 fallos. La captura de la
consola debe adjuntarse como evidencia de la práctica.