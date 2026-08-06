$ErrorActionPreference = "Stop"

$Jetson = "jetson@192.168.137.179"
$Container = "kind_pare"

Write-Host "Starting container..."

ssh $Jetson "docker start $Container >/dev/null 2>&1 || true"

if ($LASTEXITCODE -ne 0) {
    throw "Không thể khởi động container $Container"
}

Start-Sleep -Seconds 2

$status = ssh $Jetson "docker inspect -f '{{.State.Status}}' $Container"

if ($LASTEXITCODE -ne 0) {
    throw "Không thể đọc trạng thái container"
}

$status = $status.Trim()

if ($status -ne "running") {
    ssh $Jetson "docker inspect $Container --format 'Status={{.State.Status}} ExitCode={{.State.ExitCode}} Error={{.State.Error}}'"
    throw "Container không chạy. Status: $status"
}

Write-Host "Container is running."
Write-Host "Starting Robot API..."

ssh -t $Jetson `
    "docker exec -it $Container python3 -u /root/robot_api.py"