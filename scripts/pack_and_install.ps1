# 1. Uninstall existing
dotnet new uninstall .

# 2. Clean artifacts
dotnet clean
Remove-Item -Path "**/bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "**/obj" -Recurse -Force -ErrorAction SilentlyContinue

# 3. Install Template
dotnet new install .

Write-Host "GOD SEED INSTALLED. Spawn new agents using: dotnet new agent-seed -n YourAgentName" -ForegroundColor Green