#!/usr/bin/env pwsh
# coralph-init.ps1: Bootstrap a new repository for use with Coralph
# Usage: ./coralph-init.ps1 [target-directory]
# Requires: PowerShell 7+ (pwsh)

param(
    [string]$TargetDir = "."
)

$ErrorActionPreference = "Stop"

Write-Host "🪸 Coralph Init - Setting up your repository for AI-assisted development" -ForegroundColor Cyan
Write-Host ""

# Determine Coralph root and navigate to target
$CoralphRoot = Split-Path -Parent $PSCommandPath
$TargetRepo = Resolve-Path $TargetDir | Select-Object -ExpandProperty Path

Write-Host "📁 Target repository: $TargetRepo" -ForegroundColor Gray
Write-Host ""

Push-Location $TargetRepo
try {
    # Function to detect project type
    function Detect-ProjectType {
        if (Test-Path "package.json") {
            return "javascript"
        } elseif ((Test-Path "pyproject.toml") -or (Test-Path "requirements.txt") -or (Test-Path "setup.py")) {
            return "python"
        } elseif (Test-Path "go.mod") {
            return "go"
        } elseif (Test-Path "Cargo.toml") {
            return "rust"
        } elseif ((Get-ChildItem -Filter "*.sln" -ErrorAction SilentlyContinue) -or (Get-ChildItem -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
            return "dotnet"
        } else {
            return "unknown"
        }
    }

    # Function to prompt user for project type
    function Prompt-ProjectType {
        Write-Host "❓ Could not automatically detect project type." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Please select your project type:"
        Write-Host "  1) JavaScript/TypeScript"
        Write-Host "  2) Python"
        Write-Host "  3) Go"
        Write-Host "  4) Rust"
        Write-Host "  5) .NET"
        Write-Host "  6) Other (use TypeScript template)"
        Write-Host ""
        
        $choice = Read-Host "Enter number (1-6)"
        
        switch ($choice) {
            "1" { return "javascript" }
            "2" { return "python" }
            "3" { return "go" }
            "4" { return "rust" }
            "5" { return "dotnet" }
            "6" { return "javascript" }
            default { return "javascript" }
        }
    }

    # Detect or prompt for project type
    $projectType = Detect-ProjectType
    
    if ($projectType -eq "unknown") {
        $projectType = Prompt-ProjectType
    }
    
    Write-Host "✅ Detected project type: $projectType" -ForegroundColor Green
    Write-Host ""

    # Install core artifacts
    Write-Host "📦 Installing core artifacts..." -ForegroundColor Cyan
    
    # 1. Create issues.json if it doesn't exist
    if (-not (Test-Path "issues.json")) {
        Write-Host "  ✓ Creating issues.json" -ForegroundColor Green
        Copy-Item "$CoralphRoot/issues.sample.json" "issues.json"
    } else {
        Write-Host "  ⊙ issues.json already exists, skipping" -ForegroundColor Gray
    }
    
    # 2. Create coralph.config.json if it doesn't exist
    if (-not (Test-Path "coralph.config.json")) {
        Write-Host "  ✓ Creating coralph.config.json" -ForegroundColor Green
        Copy-Item "$CoralphRoot/coralph.config.json" "coralph.config.json"
    } else {
        Write-Host "  ⊙ coralph.config.json already exists, skipping" -ForegroundColor Gray
    }
    
    # 3. Create prompt.md based on project type
    if (-not (Test-Path "prompt.md")) {
        $templatePath = switch ($projectType) {
            "javascript" { "$CoralphRoot/examples/javascript-prompt.md" }
            "python" { "$CoralphRoot/examples/python-prompt.md" }
            "go" { "$CoralphRoot/examples/go-prompt.md" }
            "rust" { "$CoralphRoot/examples/rust-prompt.md" }
            "dotnet" { "$CoralphRoot/prompt.md" }
            default { "$CoralphRoot/examples/javascript-prompt.md" }
        }
        
        $templateName = switch ($projectType) {
            "javascript" { "JavaScript/TypeScript" }
            "python" { "Python" }
            "go" { "Go" }
            "rust" { "Rust" }
            "dotnet" { ".NET" }
            default { "TypeScript (default)" }
        }
        
        Write-Host "  ✓ Creating prompt.md ($templateName template)" -ForegroundColor Green
        Copy-Item $templatePath "prompt.md"
    } else {
        Write-Host "  ⊙ prompt.md already exists, skipping" -ForegroundColor Gray
    }
    
    # 4. Create progress.txt if it doesn't exist
    if (-not (Test-Path "progress.txt")) {
        Write-Host "  ✓ Creating progress.txt" -ForegroundColor Green
        New-Item -ItemType File -Path "progress.txt" | Out-Null
    } else {
        Write-Host "  ⊙ progress.txt already exists, skipping" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "✅ Setup complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📝 Next steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  1. Review and customize prompt.md for your project"
    Write-Host "  2. Add your issues to issues.json (or use --refresh-issues)"
    Write-Host "  3. Run: coralph --max-iterations 5"
    Write-Host ""
    Write-Host "📚 For more information:" -ForegroundColor Cyan
    Write-Host "  - Documentation: https://github.com/dariuszparys/coralph"
    Write-Host "  - Using with other repos: $CoralphRoot/docs/using-with-other-repos.md"
    Write-Host ""
    Write-Host "🔧 Want to add support for another tech stack?" -ForegroundColor Cyan
    Write-Host "  - Create a new template in $CoralphRoot/examples/<stack>-prompt.md"
    Write-Host "  - Follow the pattern from existing templates (python-prompt.md, go-prompt.md, etc.)"
    Write-Host "  - Add detection logic to the Detect-ProjectType function in this script"
    Write-Host "  - Consider contributing your template back: https://github.com/dariuszparys/coralph/blob/main/CONTRIBUTING.md"
    Write-Host ""
} finally {
    Pop-Location
}
