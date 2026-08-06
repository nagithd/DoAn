$BaseUrl = "http://192.168.137.179:7000"

try {
    $health = Invoke-RestMethod "$BaseUrl/health" -TimeoutSec 3
    $health | Format-List
}
catch {
    Write-Host "Robot API is not reachable."
    Write-Host $_.Exception.Message
}