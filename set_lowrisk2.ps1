$key = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Associations'
# reg.exe 直写（Set-ItemProperty 在这个键上行为异常，读回为空）
reg.exe add $key /v LowRiskFileTypes /t REG_SZ /d ".exe;.msi;.bat;.cmd" /f
$query = reg.exe query $key /v LowRiskFileTypes 2>&1
Write-Host ($query -join "`n")
