# Downloads YOLO26n and exports it to ONNX into ./models/yolo26n.onnx.
# Requires either `uv` (https://docs.astral.sh/uv/) or Python 3.10+ with pip.

$ErrorActionPreference = "Stop"

$repoRoot  = Split-Path -Parent $PSScriptRoot
$modelsDir = Join-Path $repoRoot "models"
$onnxPath  = Join-Path $modelsDir "yolo26n.onnx"

if (Test-Path $onnxPath) {
    Write-Host "Model already exists: $onnxPath"
    exit 0
}

New-Item -ItemType Directory -Force $modelsDir | Out-Null
Push-Location $modelsDir
try {
    if (Get-Command uvx -ErrorAction SilentlyContinue) {
        Write-Host "Exporting YOLO26n to ONNX via uvx + ultralytics (first run downloads ~150 MB of packages)..."
        uvx --from ultralytics --with onnx --with onnxslim yolo export model=yolo26n.pt format=onnx imgsz=640
    }
    elseif (Get-Command python -ErrorAction SilentlyContinue) {
        Write-Host "Exporting YOLO26n to ONNX via pip + ultralytics..."
        python -m pip install --quiet ultralytics onnx onnxslim
        python -c "from ultralytics import YOLO; YOLO('yolo26n.pt').export(format='onnx', imgsz=640)"
    }
    else {
        Write-Error "Neither 'uvx' nor 'python' found on PATH. Install uv or Python first."
    }
}
finally {
    Pop-Location
}

if (Test-Path $onnxPath) {
    Write-Host "Done: $onnxPath"
} else {
    Write-Error "Export finished but $onnxPath was not created - check the output above."
}
