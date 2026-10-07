cd "D:\CNI\DBBridge"

$destinoDBBridge = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'DBBridge'

dotnet publish ".\src\DBBridge\DBBridge.csproj" -c Release -r win-x64 --self-contained true -o "$destinoDBBridge"