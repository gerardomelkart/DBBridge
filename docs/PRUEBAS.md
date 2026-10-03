# Validación en la oficina

## Antes de ejecutar

Compilar la solución. Probar conectividad desde PowerShell:

```powershell
Test-NetConnection 10.251.80.6 -Port 1531
Test-NetConnection 10.106.1.52 -Port 1521
```

El esquema destino necesita CREATE TABLE y cuota. Comprobar espacio disponible
para millones de filas, los CLOB y redo/undo con el administrador de Oracle.

## Primera ejecución

Ejecutar desde Visual Studio con Ctrl+F5. El log informa versiones Oracle,
tiempo hasta la primera fila, lote elegido, tasas, RAM y conteo destino.
Conservar el log de una carga completa para ajustar lotes con mediciones reales.
No aumentar el lote por intuición: comparar tiempo total y memoria.

Reejecutar en el mismo mes para comprobar que se reconstruye solo la tabla G
correspondiente. Lanzar una segunda consola desde el mismo usuario/equipo
mientras corre: debe salir con código 2.

## Equivalencia de la consulta

Corregir VEM → VEC únicamente en la columna CICATRICES del original.
Comparar ambas consultas sobre una fuente estable o una misma instantánea Oracle:
conteo, distribución por entidad, nulos y presencia de indicadores. Para comparar
conjuntos usar MINUS en ambos sentidos y exigir cero diferencias. Si cambian datos
durante las consultas, los resultados dejan de ser comparables.
Esta comprobación pesada se realiza para aceptar el cambio, no en cada carga.

No se eliminó DISTINCT: los joins restantes pueden multiplicar expedientes.
La consulta puede usar TEMP y tardar antes de entregar la primera fila.
Si la extracción domina el tiempo, pedir al DBA el plan real con filas observadas,
lecturas y uso de TEMP. Revisar acceso por EXPEDIENTE en los indicadores y tablas
relacionadas, ESTEXP/EXPEDIENTE en IDENCOMP y llaves de catálogos.
No crear índices ni cambiar estadísticas sin revisar el plan y autorizarlo.

## Fallos

Una interrupción posterior al DROP puede dejar tabla vacía o incompleta.
Solo aceptar cargas cuyo log termina en EXITO y código 0.
Reejecutar íntegramente ante errores. No hay reintentos que puedan duplicar filas.

## Validación del paquete

Consultar VALIDACION.md para las comprobaciones efectuadas antes de entregar.
No se han probado conexiones privadas ni rendimiento real desde este entorno.
