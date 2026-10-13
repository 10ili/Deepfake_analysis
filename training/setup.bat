@echo off
rem Creates .venv next to this file and installs PyTorch (CUDA 12.8 build: RTX 30/40/50) with the other packages.
setlocal
cd /d "%~dp0"

if not exist .venv\Scripts\python.exe (
    py -3.12 -m venv .venv 2>nul || py -3 -m venv .venv 2>nul || python -m venv .venv || goto :error
)

.venv\Scripts\python -m pip install --upgrade pip || goto :error
.venv\Scripts\python -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128 || goto :error
.venv\Scripts\python -m pip install -r requirements.txt || goto :error

echo.
echo === GPU check ===
.venv\Scripts\python -c "import torch; print('PyTorch', torch.__version__); print('GPU:', torch.cuda.get_device_name(0)); print('GPU test:', (torch.ones(4, device='cuda') * 2).sum().item() == 8)" || goto :gpu_error

echo.
echo Setup finished.
exit /b 0

:gpu_error
echo.
echo PyTorch is installed, but the GPU does not work. Update the NVIDIA driver and run setup.bat again.
exit /b 1

:error
echo.
echo Setup failed. See the message above.
exit /b 1
