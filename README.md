# DBBridge — primera carga RNIP

Consola C# para Windows, .NET 8. Ejecuta RNIP sin argumentos ni configuración externa.
El mes anterior se calcula con la fecha local del equipo al iniciar (enero → diciembre del año anterior).

## Prueba desde Visual Studio

1. Extraer el ZIP y copiar todo su contenido a la carpeta del repositorio DBBridge.
2. Abrir `DBBridge.sln` con Visual Studio compatible con .NET 8 y el SDK .NET 8 instalado.
3. Restaurar NuGet y seleccionar DBBridge como proyecto de inicio.
4. Compilar primero. Ejecutar con Ctrl+F5 desde el equipo con acceso a ambos Oracle.
5. Revisar el resumen final y el archivo `logs/RNIP_*.log` junto al ejecutable
   (en Debug: `src/DBBridge/bin/Debug/net8.0/logs`).

**La ejecución borra y reconstruye la tabla G del mes anterior.**
Ejemplo al ejecutar en octubre de 2026: `CSNISPRNIP.G_PYLOAD_RNIP_202609`.
No modifica tablas X ni Z ni tablas de otros periodos. No crea bases de datos.

Origen: 10.251.80.6:1531, SERVICE_NAME drp_crmn, USR_CNI.
Destino: 10.106.1.52:1521, SID BBDDOrac, CSNISPRNIP.
El usuario destino debe poder crear y eliminar sus tablas y disponer de cuota suficiente.

## Credenciales

Las conexiones y contraseñas están fijas en código. El archivo
`src/DBBridge/Security/Credentials.Local.cs` se incluye en Git por decisión
expresa del propietario. No requiere configuración para esta prueba.
No se imprimen cadenas de conexión ni contraseñas en los logs.

## Millones de registros

- Un único cursor de origen: no hay paginación OFFSET, DataTable de datos ni carga completa en RAM.
- Fetch de 8 MiB y lotes de hasta 5000 filas, reducidos automáticamente según el ancho declarado.
- Array binding: un INSERT parametrizado para todas las filas del lote, con commit por lote.
- Fechas DATE y números NUMBER conservan sus tipos; OracleDecimal evita convertir números a decimal .NET.
- Longitudes amplias de texto se conservan en CLOB; no se recortan ni se convierten fechas con NLS.
- Primera lectura y validación de tipos antes del DROP; un error inicial deja la tabla previa intacta.
- Cada 10 segundos aproximadamente se registran filas confirmadas, lectura, inserción, velocidad y RAM.
- Conteo destino final comparado con las filas confirmadas. Cero filas es una carga válida y genera tabla vacía.
- Mutex global para evitar solapamiento entre procesos de este mismo equipo y usuario.
  No protege contra ejecuciones desde otras computadoras; usar un solo equipo ejecutor.

El lote limita cantidad de filas y estima memoria; valores CLOB muy grandes pueden elevar su consumo.
No se fuerza paralelismo, hints, NOLOGGING ni índices en las bases.

## Consulta

La consulta está embebida al compilar, en `src/DBBridge/Sql/RnipActivos.sql`.
`docs/RNIP_ACTIVOS_original.sql` conserva el TXT recibido para comparación.

Los joins usados únicamente para indicadores de existencia se sustituyeron por EXISTS,
para evitar multiplicar filas por fotografías, marcas, voz y demás indicadores.
CICATRICES ahora consulta V_EXISTE_CICATRIZ. El filtro activo se aplica en el INNER JOIN
con IDENCOMP; conserva el efecto del filtro original. Se mantiene DISTINCT y los joins
que generan delitos, domicilios, alias y otras filas legítimas.
No se supone que EXPEDIENTE sea único. Antes de aceptar la primera carga, comparar
conteo y resultados con la consulta original corrigiendo también CICATRICES a VEC.
La rapidez de EXISTS depende de los planes e índices reales: ver `docs/PRUEBAS.md`.

El sufijo mensual identifica el periodo de carga. Se consultan los activos actuales;
no se reconstruye el estado histórico al último día del mes anterior.

## Errores y ejecución posterior

Oracle confirma DDL automáticamente: el DROP y CREATE no se pueden deshacer con rollback.
Los lotes anteriores permanecen confirmados si falla uno posterior; el lote que falla se revierte.
Una ejecución fallida no debe consumirse como carga completa. Reejecutar reconstruye todo.
Si se pierde la conexión durante un commit, su resultado puede ser incierto; igualmente reejecutar.
Ctrl+C solicita cancelación; una inserción activa puede terminar o esperar el timeout (600 segundos).
La consulta no tiene timeout fijo; se cancela con Ctrl+C. No hay pausas ni ReadKey.

Códigos: 0 éxito con conteo validado; 1 error; 2 ejecución simultánea; 3 cancelación.

Publicación futura para tarea programada:

```powershell
dotnet publish src/DBBridge/DBBridge.csproj -c Release -r win-x64 --self-contained true -o publish
```

Ejecutar `publish/DBBridge.exe` directamente. Desactivar el inicio de una segunda instancia
en el Programador de tareas y mantener disponible la red con la cuenta ejecutora.
Primero validar manualmente; este paquete no crea ninguna tarea programada.

## Extensión

`ITransferProcess` separa cada carga del arranque y los logs. El proveedor Oracle y la
escritura por lotes están separados del proceso RNIP. Solo RNIP está implementado.
LIC, RMJ, RNPSP, VRYR y SQL Server se incorporarán como procesos nuevos con sus consultas
reales; SQL Server requerirá su proveedor y SqlBulkCopy. No se agregan procesos ficticios.
