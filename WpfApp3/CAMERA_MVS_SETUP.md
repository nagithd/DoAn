# IMITECH IMB-770GC / MVS setup

The WPF application now uses `MvCameraControl.Net.dll` from the installed
MVS SDK. It does not use OpenCV camera indexes for the IMITECH industrial
camera.

## Current network finding

MVS has the following manually added camera address:

```text
192.168.5.199
```

The Windows machine currently has:

```text
Ethernet: 192.168.137.1/24
```

Therefore the camera and Windows Ethernet interface are not currently on
the same IPv4 subnet. A direct ping to `192.168.5.199` also fails.

## Recommended network setup

Keep `192.168.137.1/24` because the Jetson currently uses
`192.168.137.179`. Add `192.168.5.10/24` as a secondary IPv4 address on
the same Ethernet adapter.

Run Windows PowerShell as Administrator:

```powershell
New-NetIPAddress `
  -InterfaceAlias "Ethernet" `
  -IPAddress 192.168.5.10 `
  -PrefixLength 24
```

Verify:

```powershell
Get-NetIPAddress -InterfaceAlias "Ethernet" -AddressFamily IPv4
Test-Connection 192.168.5.199 -Count 2
```

If `192.168.5.10` is already used by another device, choose another free
address between `192.168.5.1` and `192.168.5.254`, excluding
`192.168.5.199`.

To remove the secondary address later:

```powershell
Remove-NetIPAddress `
  -InterfaceAlias "Ethernet" `
  -IPAddress 192.168.5.10
```

## Connect from the WPF application

1. Open MVS and confirm that the camera is visible.
2. Stop grabbing and close MVS so it does not own the image stream.
3. Open the Camera tab in the WPF application.
4. Select **Refresh**.
5. Select the IMITECH camera discovered by MVS.
6. Press **Start Camera**.

The System Log now reports the detailed MVS error if opening the camera
fails.

## Device test still required

The code builds successfully with the installed MVS SDK. A final hardware
test is still required after Windows can ping the camera and MVS grabbing
has been stopped.
