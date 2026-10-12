# DBBridge 1.16

Consola C# para Windows y .NET 8. Ejecuta ocho procesos independientes mediante menú o un argumento de línea de comandos. Las conexiones, credenciales y reglas siguen fijas en código; las consultas SQL se incluyen dentro del ejecutable al compilar.

## Actualizar desde Git

Después de esperar a que terminen las cargas y cerrar Visual Studio, actualizar la copia local con `git pull` (si hay cambios locales, revisarlos antes). Abrir la solución existente, restaurar NuGet, compilar y probar. Publicar nuevamente el EXE para las tareas; los ocho BATs están en `bats`.

## Instalar desde un ZIP

1. Cerrar Visual Studio y esperar a que terminen las cargas que estén en ejecución.
2. Respaldar la carpeta actual `src/DBBridge` fuera del repositorio. El respaldo también conserva cualquier log de pruebas que esté en `bin`.
3. Sustituir **completa** la carpeta `src/DBBridge` por la del ZIP. No dejar copias de archivos C# anteriores dentro de esa carpeta: el SDK compila todos los `.cs` que encuentre allí.
4. Copiar este README a la raíz del repositorio. Conservar la solución `DBBridge.sln` existente; su ruta de proyecto sigue siendo la misma.
5. Abrir la solución, restaurar NuGet y compilar. También puede usarse `dotnet build src/DBBridge/DBBridge.csproj -c Release`.
6. Probar desde Visual Studio con Ctrl+F5. Para las tareas, publicar otra vez y colocar los BATs junto al EXE actualizado.

El ZIP incluye el `.csproj` porque ahora incorpora las cinco consultas de `Queries`. No contiene ejecutables, DLLs, `bin`, `obj`, datos ni logs.

## Organización

| Carpeta / archivo | Responsabilidad |
|---|---|
| `Program.cs` | Arranque y selección de ejecución interactiva o por argumento. |
| `Processes` | Catálogo, opciones del menú y reglas propias de RNIP, RMJ, LIC y RND. |
| `Oracle` | Motor compartido, creación de tablas, tipos nativos, inserción por arreglos, errores y retención Oracle. |
| `SqlServer` | Motor RND/RND_FA, SqlBulkCopy, adaptación de estructura, métricas y retención SQL Server. |
| `Queries/Oracle` | Consultas RNIP, RMJ y LIC. |
| `Queries/SqlServer` | Consultas RND y RND_FA. |
| `Infrastructure` | Ejecución, mutex, cancelación, menú, recursos SQL y logs. |
| `Security` | Credenciales existentes. |
| `bats` (raíz) | Ocho lanzadores para copiar junto al EXE. |

Las consultas se editan en sus archivos `.sql` y se incorporan al compilar. No se leen archivos externos en producción, por lo que no hace falta copiar `Queries` junto al EXE.

## Procesos y fechas

Se conserva el comportamiento del código anterior a esta reorganización. Las fechas se calculan una vez al iniciar, usando la fecha local de Windows.

| Opción / argumento | Datos consultados | Tabla destino |
|---|---|---|
| 1 / `RNIP` | Activos actuales de la consulta original; el nombre mensual identifica el mes anterior, sin reconstrucción histórica ni filtro anual. | `CSNISPRNIP.Z_PYLOAD_RNIP_yyyyMM` |
| 2 / `RND` | Consulta completa de RNDetenciones con sus filtros y NOLOCK originales. | `RND.dbo.tablero_rnd_yyyyMMdd` |
| 3 / `RND_FA` | Consulta completa de RNDetenciones_FA con aislamiento normal del origen. | `RND.dbo.tablero_rnd_yyyyMMdd_fa` |
| 4 / `RMJ` | Histórico hasta el último día del mes anterior, incluyendo fechas de registro nulas. | `CSNISPMANDAMIENTOS.Z_PYLOAD_RMJJ_yyyyMM` |
| 5 / `RNIP_D` | Consulta RNIP con FECHA_REGISTRO hasta el día de ejecución, incluyendo nulos. | `CSNISPRNIP.Z_PYLOAD_RNIP_yyyyMMdd_D` |
| 6 / `RMJ_D` | Histórico hasta el día de ejecución, incluyendo fechas de registro nulas. | `CSNISPMANDAMIENTOS.Z_PYLOAD_RMJJ_yyyyMMdd_D` |
| 7 / `LIC` | Actualizaciones desde el 1 de enero hasta el último día del mes anterior; excluye fechas nulas. | `CSNISPLICENCIA.Z_PYLOAD_LC_yyyyMM` |
| 8 / `LIC_D` | Actualizaciones desde el 1 de enero hasta ayer; excluye fechas nulas. | `CSNISPLICENCIA.Z_PYLOAD_LC_yyyyMMdd_D` |

LIC usa `LI.FECHA_ACTUALIZA`. Sus rangos incluyen todo el último día mediante un límite superior exclusivo a medianoche. El inicio anual pertenece al año de la última fecha incluida: al ejecutar el 1 de enero, el corte hasta el 31 de diciembre abarca el año anterior. La tabla diaria mantiene la fecha de ejecución en el nombre.

## Carga y retención

Oracle comparte un único motor con metadatos y primera lectura antes de recrear la tabla. Conserva fetch de 32 MiB, lotes automáticos de hasta 5000 filas, valores Oracle tipados y commit por lote. Si falla después de iniciar la recreación, puede quedar una carga parcial; el log indica la etapa y se debe reejecutar.

SQL Server conserva el motor de carga 1.7: un lector, copia síncrona en ventanas de 50000 filas, lotes de 10000 y una transacción para preparación, carga y limpieza. La validación final compara COUNT_BIG antes del commit. RND y RND_FA deben programarse de forma secuencial por los bloqueos que pueden compartir en el destino.

Cada serie diaria conserva al menos 31 tablas. Después de validar una carga, elimina **una** tabla, la más antigua, únicamente si hay más de 31 válidas. El conteo Oracle reconoce los sufijos antiguos `D` y actuales `_D`. No mezcla series ni tablas mensuales; excluye nombres inválidos y fechas futuras. Las tablas mensuales de otros periodos se conservan. Reejecutar un mismo periodo recrea o recarga su tabla.

Cero filas es una carga válida, igual que antes. Oracle confirma DROP/CREATE automáticamente; sus lotes anteriores no se revierten como una transacción completa.

## Logs y ejecución programada

Los logs son diarios y acumulativos, en `logs` junto al EXE: `RNIP_yyyyMMdd.log`, `LIC_D_yyyyMMdd.log`, etc. El archivo cambia al pasar de día. Los estados aparecen cada 15 segundos y las cargas reportan conteos, tiempos y memoria. La reorganización no introduce borrado automático de logs.

El mutex evita repetir simultáneamente el mismo proceso en este equipo. Los nombres de los argumentos y los BATs mantienen las tareas existentes.

```powershell
cd "D:\CNI\DBBridge"
$destinoDBBridge = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'DBBridge'
dotnet publish ".\src\DBBridge\DBBridge.csproj" -c Release -r win-x64 --self-contained true -o "$destinoDBBridge"
Copy-Item ".\bats\*.bat" "$destinoDBBridge" -Force
```

Cada BAT llama al EXE desde su propia carpeta, pasa el nombre del proceso y devuelve su código. No pausa ni solicita opciones. La periodicidad se configura en el Programador de tareas de Windows.

Códigos: 0 éxito; 1 error; 2 ejecución duplicada; 3 cancelación; 64 argumento inválido. Ctrl+C solicita cancelación.

## Validación de la reorganización

Código compilado con referencias .NET 8 y advertencias tratadas como errores. Se compararon las cinco consultas ejecutadas con el código del repo base `73bd3816049e4a44dfb2f85688302a467621c26f`, sin diferencias salvo espacios de formato. Se verificaron los recursos embebidos del proyecto, nombres del menú/CLI, cortes de mes/año, parámetros de las seis variantes Oracle y retención independiente de todas las series. Las operaciones del motor RND, los tipos, escritores, conexiones y logs se conservaron.

Las pruebas locales no se conectaron a las bases. Hace falta probar las cargas desde el equipo con acceso a los servidores y volver a publicar el EXE antes de usar esta versión en las tareas.
