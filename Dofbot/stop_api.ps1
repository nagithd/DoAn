$Jetson = "jetson@192.168.137.179"
$Container = "kind_pare"

ssh $Jetson `
    "docker exec $Container pkill -f '/root/robot_api.py' || true"

Write-Host "Robot API stopped."