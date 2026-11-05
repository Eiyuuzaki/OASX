# LowRiskFileTypes 策略：把 .exe 列为低风险 → 附件管理器对其不弹
# "打开文件 - 安全警告"（用户级，免管理员，可逆：删掉这个值即恢复）
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Associations'
if (-not (Test-Path $key)) { New-Item $key -Force | Out-Null }
New-ItemProperty -Path $key -Name LowRiskFileTypes -Value ".exe;.msi;.bat;.cmd" `
    -PropertyType String -Force | Out-Null
$v = (Get-ItemProperty $key).LowRiskFileTypes
Write-Host ("LowRiskFileTypes = [$v]")
if ($v -ne ".exe;.msi;.bat;.cmd") { Write-Host "WRITE FAILED"; exit 1 }
Write-Host "policy OK"
