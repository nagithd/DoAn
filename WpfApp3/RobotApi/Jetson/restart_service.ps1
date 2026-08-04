param(
    [string]$Jetson = "jetson@192.168.137.179"
)

$ErrorActionPreference = "Stop"
$ServiceName = "dofbot-robot-api.service"

ssh -t $Jetson `
    "sudo systemctl restart '$ServiceName' && sudo systemctl --no-pager --full status '$ServiceName'"

if ($LASTEXITCODE -ne 0) {
    throw "Cannot restart $ServiceName. Exit code: $LASTEXITCODE"
}
