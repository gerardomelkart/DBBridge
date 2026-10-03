# Comprobaciones antes de entregar

- Repositorio DBBridge revisado por lectura: vacío. Ningún cambio remoto realizado.
- Código C# compilado directamente con Roslyn 4.11, referencias de .NET 8.0.31
  y Oracle.ManagedDataAccess.Core 23.26.301: cero errores y cero advertencias,
  con nullable y warnings-as-errors activos. Incluye el SQL como recurso embebido.
- Comprobaciones locales ejecutadas: credenciales presentes sin imprimirlas,
  arreglos OracleDecimal/OracleDate/string y reglas de texto multibyte/CLOB.
- Revisión estructural: ocho indicadores con EXISTS, cicatrices en su vista,
  filtro activo, DISTINCT y joins de datos restantes conservados; XML del proyecto válido.

No se completó `dotnet build`/restauración NuGet en este entorno:
MSBuild falla al consultar los procesos del contenedor. El compilador C# directo
sí funcionó y resolvió el ensamblado real del proveedor Oracle.
La inicialización del proveedor Oracle también falla aquí al consultar información
del proceso, antes de intentar conectarse. No se ejecutó ninguna carga ni DDL.

Pendiente en Windows/Visual Studio: restauración NuGet y build estándar, conexión a
ambas bases, permisos y cuota, equivalencia del resultado sobre fuente estable,
comportamiento del array binding con datos reales y mediciones de millones de filas.
