param(
    [string]$ExePath = (Join-Path $PSScriptRoot "..\OpenMd\bin\Release\net9.0-windows\win-x64\publish\OpenMd.exe"),
    [switch]$OpenDefaultAppsSettings
)

$ErrorActionPreference = "Stop"

$resolvedExe = (Resolve-Path $ExePath).Path
$progId = "OpenMd.MarkdownFile"
$command = "`"$resolvedExe`" `"%1`""

$extensionKey = "HKCU:\Software\Classes\.md"
$progIdKey = "HKCU:\Software\Classes\$progId"
$commandKey = "$progIdKey\shell\open\command"
$iconKey = "$progIdKey\DefaultIcon"
$appCommandKey = "HKCU:\Software\Classes\Applications\OpenMd.exe\shell\open\command"

New-Item -Path $extensionKey -Force | Out-Null
Set-Item -Path $extensionKey -Value $progId
New-ItemProperty -Path $extensionKey -Name "Content Type" -Value "text/markdown" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $extensionKey -Name "PerceivedType" -Value "text" -PropertyType String -Force | Out-Null

New-Item -Path $progIdKey -Force | Out-Null
Set-Item -Path $progIdKey -Value "Markdown Document"

New-Item -Path $iconKey -Force | Out-Null
Set-Item -Path $iconKey -Value "`"$resolvedExe`",0"

New-Item -Path $commandKey -Force | Out-Null
Set-Item -Path $commandKey -Value $command

New-Item -Path $appCommandKey -Force | Out-Null
Set-Item -Path $appCommandKey -Value $command

Write-Host "Registered OpenMd for .md files:"
Write-Host "  $resolvedExe"
Write-Host ""
Write-Host "If Windows still shows another default app, choose OpenMd from:"
Write-Host "  Settings > Apps > Default apps > .md"

if ($OpenDefaultAppsSettings) {
    Start-Process "ms-settings:defaultapps"
}
