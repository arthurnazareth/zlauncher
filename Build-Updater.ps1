$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@('UpdateCore.cs','UpdaterProgram.cs','UpdateTests.cs','LongPaths.cs','AssemblyInfo.cs') | ForEach-Object { Join-Path $PSScriptRoot ('src\'+$_) }
$sources+=@("/resource:$(Join-Path $PSScriptRoot 'src\ZLauncher.exe.config'),updater.config")
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /define:STANDALONE_UPDATER /main:ZLauncherUpdates.UpdaterProgram "/out:$(Join-Path $PSScriptRoot 'ZLauncher.Updater.exe')" "/win32icon:$(Join-Path $PSScriptRoot 'assets\zlauncher.ico')" "/win32manifest:$(Join-Path $PSScriptRoot 'src\ZLauncher.manifest')" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $sources
if($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o Updater.' }
Copy-Item (Join-Path $PSScriptRoot 'src\ZLauncher.exe.config') (Join-Path $PSScriptRoot 'ZLauncher.Updater.exe.config') -Force
Write-Output 'Updater compilado. Testes: ZLauncher.Updater.exe --self-test --output <pasta-de-testes>'
