# 提权脚本：修复 Associations 键 ACL + 写入 LowRiskFileTypes
# （该键此前由提权实例创建，管理员所有权导致普通实例写入被拒）
$key = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Associations'
if (-not (Test-Path $key)) { New-Item $key -Force | Out-Null }

# 1) 授予当前用户完全控制
$acl = Get-Acl $key
$identity = "$env:COMPUTERNAME\$env:USERNAME"
$rule = New-Object System.Security.AccessControl.RegistryAccessRule(
    $identity, "FullControl", "ContainerInherit", "None", "Allow")
$acl.SetAccessRule($rule)
Set-Acl $key $acl
Write-Host "ACL fixed for $identity"

# 2) 写入低风险文件类型（附件管理器对 .exe 不再弹安全警告）
reg.exe add $key /v LowRiskFileTypes /t REG_SZ /d ".exe;.msi;.bat;.cmd" /f
$query = reg.exe query $key /v LowRiskFileTypes 2>&1
Write-Host ($query -join " | ")

Write-Host "DONE - security warning for .exe is now suppressed"
