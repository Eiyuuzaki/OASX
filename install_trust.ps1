$cer = "D:\OAS-Kirin\OASX-WPF\oasx-dev.cer"
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\Root'
Write-Host "machine trust installed"
