# 🤖 Autonomous Agent: Workspace Configuration Update

> **📍 Target:** MiGente En Línea - ProyectoMigente Workspace
> **🎯 Objective:** Update workspace configurations based on 121 .md documentation files
> **🔧 Agent:** Claude Sonnet 4.5, GitHub Copilot Workspace, or other autonomous coding agents
> **📅 Created:** October 2025
> **⏱️ Estimated Time:** 2-3 hours

---

## 🎯 Mission Objective

You are an autonomous coding agent tasked with **updating VS Code workspace configurations** for the MiGente En Línea project. The project has **121 .md documentation files** (~15,000 lines) documenting completed backend work (123 REST endpoints, 28 GAPS, testing setup). Your mission is to ensure **all workspace configurations reflect the current state** and support **best practices for testing, debugging, and development**.

---

## 📚 Required Reading (DO THIS FIRST)

**CRITICAL:** Before making ANY changes, read these documents:

1. **`MiGenteEnLinea.Clean/INDICE_COMPLETO_DOCUMENTACION.md`** (MASTER INDEX)
   - Complete index of 121 .md files organized in 12 categories
   - Top 10 priority documents
   - Search by topic (Authentication, Empleadores, Contratistas, etc.)

2. **`BACKEND_100_COMPLETE_VERIFIED.md`** (BACKEND STATUS)
   - 123 REST endpoints verified (8 controllers)
   - Detailed breakdown by controller
   - All LOTE 6.0.2-6.0.5 completed

3. **`GAPS_AUDIT_COMPLETO_FINAL.md`** (GAPS STATUS)
   - 28 GAPS total
   - 19 complete (68%)
   - 3 blocked by EncryptionService (GAP-016, GAP-019, GAP-022)

4. **`INTEGRATION_TESTS_SETUP_REPORT.md`** (TESTING STATUS)
   - 58 tests configured
   - 4 issues identified (TestDataSeeder, namespaces, interfaces, duplicates)
   - Coverage ~45% (target: 80%+)

5. **`.github/copilot-instructions.md`** (WORKSPACE CONTEXT)
   - Already updated with 121 .md references
   - Contains complete project context

---

## 🔧 Tasks to Complete

### ✅ Task 1: Validate Existing Configurations (READ ONLY)

**Objective:** Understand what's already configured

**Files to Read:**
1. `.vscode/settings.json` - Workspace settings (already excellent, mostly complete)
2. `.vscode/tasks.json` - Build/test tasks
3. `.vscode/launch.json` - Debug configurations
4. `.vscode/extensions.json` - Recommended extensions

**What to Look For:**
- Testing tasks (coverage, unit tests, integration tests)
- Debug configurations for API and tests
- Missing or outdated settings

**Action:** Document findings, DO NOT modify yet

---

### ✅ Task 2: Read Testing Configuration Files

**Objective:** Understand the testing setup

**Files to Read:**
1. `MiGenteEnLinea.Clean/tests/MiGenteEnLinea.IntegrationTests/*.csproj`
2. `MiGenteEnLinea.Clean/tests/MiGenteEnLinea.Infrastructure.Tests/*.csproj`
3. `MiGenteEnLinea.Clean/src/Presentation/MiGenteEnLinea.API/MiGenteEnLinea.API.csproj`

**What to Extract:**
- Test framework (xUnit, NUnit, MSTest)
- Test runner settings
- Code coverage tools (Coverlet, etc.)
- Target frameworks (.NET 8.0)

**Action:** Document test setup, identify what tasks are needed

---

### ✅ Task 3: Update `.vscode/tasks.json` (CRITICAL)

**Objective:** Add comprehensive testing tasks for 80%+ coverage goal

**Required Tasks:**

```jsonc
{
  "version": "2.0.0",
  "tasks": [
    // ========== BUILD TASKS ==========
    {
      "label": "build-clean",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/MiGenteEnLinea.Clean/MiGenteEnLinea.Clean.sln",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile",
      "group": {
        "kind": "build",
        "isDefault": true
      }
    },

    // ========== TEST TASKS ==========
    {
      "label": "test-all",
      "command": "dotnet",
      "type": "process",
      "args": [
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/MiGenteEnLinea.Clean.sln",
        "--no-build",
        "--logger:trx",
        "--logger:console;verbosity=detailed"
      ],
      "problemMatcher": "$msCompile",
      "group": "test",
      "dependsOn": "build-clean"
    },
    {
      "label": "test-integration",
      "command": "dotnet",
      "type": "process",
      "args": [
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/tests/MiGenteEnLinea.IntegrationTests/MiGenteEnLinea.IntegrationTests.csproj",
        "--no-build",
        "--logger:trx",
        "--logger:console;verbosity=detailed",
        "--filter:Category=Integration"
      ],
      "problemMatcher": "$msCompile",
      "group": "test",
      "dependsOn": "build-clean"
    },
    {
      "label": "test-unit",
      "command": "dotnet",
      "type": "process",
      "args": [
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/tests/MiGenteEnLinea.Infrastructure.Tests/MiGenteEnLinea.Infrastructure.Tests.csproj",
        "--no-build",
        "--logger:trx",
        "--logger:console;verbosity=detailed"
      ],
      "problemMatcher": "$msCompile",
      "group": "test",
      "dependsOn": "build-clean"
    },

    // ========== COVERAGE TASKS ==========
    {
      "label": "test-coverage",
      "command": "dotnet",
      "type": "process",
      "args": [
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/MiGenteEnLinea.Clean.sln",
        "--no-build",
        "--collect:XPlat Code Coverage",
        "--results-directory:${workspaceFolder}/MiGenteEnLinea.Clean/TestResults",
        "--logger:trx",
        "/p:CollectCoverage=true",
        "/p:CoverletOutputFormat=cobertura",
        "/p:Threshold=80",
        "/p:ThresholdType=line"
      ],
      "problemMatcher": "$msCompile",
      "group": "test",
      "dependsOn": "build-clean"
    },
    {
      "label": "coverage-report",
      "command": "reportgenerator",
      "type": "process",
      "args": [
        "-reports:${workspaceFolder}/MiGenteEnLinea.Clean/TestResults/**/coverage.cobertura.xml",
        "-targetdir:${workspaceFolder}/MiGenteEnLinea.Clean/TestResults/CoverageReport",
        "-reporttypes:Html;Badges"
      ],
      "problemMatcher": [],
      "dependsOn": "test-coverage",
      "presentation": {
        "reveal": "always",
        "panel": "new"
      }
    },

    // ========== RUN TASKS ==========
    {
      "label": "run-api",
      "command": "dotnet",
      "type": "process",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/MiGenteEnLinea.Clean/src/Presentation/MiGenteEnLinea.API/MiGenteEnLinea.API.csproj",
        "--launch-profile",
        "https"
      ],
      "problemMatcher": "$msCompile",
      "isBackground": true,
      "group": "build"
    },

    // ========== WATCH TASKS ==========
    {
      "label": "watch-api",
      "command": "dotnet",
      "type": "process",
      "args": [
        "watch",
        "run",
        "--project",
        "${workspaceFolder}/MiGenteEnLinea.Clean/src/Presentation/MiGenteEnLinea.API/MiGenteEnLinea.API.csproj",
        "--launch-profile",
        "https"
      ],
      "problemMatcher": "$msCompile",
      "isBackground": true,
      "group": "build"
    },
    {
      "label": "watch-tests",
      "command": "dotnet",
      "type": "process",
      "args": [
        "watch",
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/MiGenteEnLinea.Clean.sln",
        "--logger:console;verbosity=detailed"
      ],
      "problemMatcher": "$msCompile",
      "isBackground": true,
      "group": "test"
    },

    // ========== CLEANUP TASKS ==========
    {
      "label": "clean",
      "command": "dotnet",
      "type": "process",
      "args": [
        "clean",
        "${workspaceFolder}/MiGenteEnLinea.Clean/MiGenteEnLinea.Clean.sln",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "restore",
      "command": "dotnet",
      "type": "process",
      "args": [
        "restore",
        "${workspaceFolder}/MiGenteEnLinea.Clean/MiGenteEnLinea.Clean.sln"
      ],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

**Action:**
- If `tasks.json` exists: **MERGE** these tasks with existing ones (don't overwrite)
- If `tasks.json` doesn't exist: **CREATE** it with these tasks
- Test each task after creation to ensure it works

---

### ✅ Task 4: Update `.vscode/launch.json` (CRITICAL)

**Objective:** Add debug configurations for API and tests

**Required Configurations:**

```jsonc
{
  "version": "0.2.0",
  "configurations": [
    // ========== API DEBUG ==========
    {
      "name": ".NET Core Launch (API)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-clean",
      "program": "${workspaceFolder}/MiGenteEnLinea.Clean/src/Presentation/MiGenteEnLinea.API/bin/Debug/net8.0/MiGenteEnLinea.API.dll",
      "args": [],
      "cwd": "${workspaceFolder}/MiGenteEnLinea.Clean/src/Presentation/MiGenteEnLinea.API",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)",
        "uriFormat": "%s/swagger"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "https://localhost:5015;http://localhost:5014"
      },
      "sourceFileMap": {
        "/Views": "${workspaceFolder}/MiGenteEnLinea.Clean/src/Presentation/MiGenteEnLinea.API/Views"
      }
    },
    {
      "name": ".NET Core Attach",
      "type": "coreclr",
      "request": "attach"
    },

    // ========== TEST DEBUG ==========
    {
      "name": ".NET Core Test Explorer",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-clean",
      "program": "dotnet",
      "args": [
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/MiGenteEnLinea.Clean.sln",
        "--no-build",
        "--logger:console;verbosity=detailed"
      ],
      "cwd": "${workspaceFolder}/MiGenteEnLinea.Clean",
      "stopAtEntry": false,
      "console": "internalConsole"
    },
    {
      "name": "Debug Integration Tests",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-clean",
      "program": "dotnet",
      "args": [
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/tests/MiGenteEnLinea.IntegrationTests/MiGenteEnLinea.IntegrationTests.csproj",
        "--no-build",
        "--filter:Category=Integration",
        "--logger:console;verbosity=detailed"
      ],
      "cwd": "${workspaceFolder}/MiGenteEnLinea.Clean/tests/MiGenteEnLinea.IntegrationTests",
      "stopAtEntry": false,
      "console": "internalConsole",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Testing"
      }
    },
    {
      "name": "Debug Unit Tests",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-clean",
      "program": "dotnet",
      "args": [
        "test",
        "${workspaceFolder}/MiGenteEnLinea.Clean/tests/MiGenteEnLinea.Infrastructure.Tests/MiGenteEnLinea.Infrastructure.Tests.csproj",
        "--no-build",
        "--logger:console;verbosity=detailed"
      ],
      "cwd": "${workspaceFolder}/MiGenteEnLinea.Clean/tests/MiGenteEnLinea.Infrastructure.Tests",
      "stopAtEntry": false,
      "console": "internalConsole"
    }
  ],
  "compounds": [
    {
      "name": "API + Tests (Watch)",
      "configurations": [".NET Core Launch (API)", ".NET Core Test Explorer"],
      "stopAll": true
    }
  ]
}
```

**Action:**
- If `launch.json` exists: **MERGE** these configurations (don't overwrite existing ones)
- If `launch.json` doesn't exist: **CREATE** it
- Validate paths match actual project structure

---

### ✅ Task 5: Validate `.vscode/extensions.json` (REVIEW)

**Objective:** Ensure recommended extensions are listed

**Current Status:** Already exists with good extensions

**Review Checklist:**
- ✅ C# extension (`ms-dotnettools.csharp`)
- ✅ Test Explorer (`ms-dotnettools.csdevkit`)
- ✅ Coverage Gutters (for coverage visualization)
- ✅ REST Client (for testing API endpoints)

**Action:**
- Read existing file
- Add missing extensions if needed:
  ```json
  {
    "recommendations": [
      "ms-dotnettools.csharp",
      "ms-dotnettools.csdevkit",
      "ms-dotnettools.vscode-dotnet-runtime",
      "formulahendry.dotnet-test-explorer",
      "ryanluker.vscode-coverage-gutters",
      "humao.rest-client",
      "streetsidesoftware.code-spell-checker",
      "editorconfig.editorconfig",
      "ms-vscode.powershell"
    ]
  }
  ```

---

### ✅ Task 6: Update `.vscode/settings.json` (MINOR ADDITIONS)

**Objective:** Add testing-specific settings

**Settings to ADD (if not present):**

```jsonc
{
  // ==================== TESTING ====================
  "dotnet.test.runSettingsPath": "",
  "dotnet.test.showCodeLens": true,
  "dotnet.test.alwaysShowTestExplorer": true,
  "coverage-gutters.coverageFileNames": [
    "coverage.cobertura.xml",
    "coverage.opencover.xml"
  ],
  "coverage-gutters.showLineCoverage": true,
  "coverage-gutters.showRulerCoverage": true,
  "coverage-gutters.highlightdark": "rgba(255, 0, 0, 0.3)",
  "coverage-gutters.highlightlight": "rgba(255, 0, 0, 0.3)",

  // ==================== TEST EXPLORER ====================
  "testExplorer.useNativeTesting": true,
  "testExplorer.codeLens": true,
  "testExplorer.gutterDecoration": true,
  "testExplorer.showCollapseButton": true,
  "testExplorer.showExpandButton": true,
  "testExplorer.showOnRun": true,

  // ==================== PROBLEMS ====================
  "problems.decorations.enabled": true,
  "problems.showCurrentInStatus": true
}
```

**Action:**
- Read existing `settings.json`
- **APPEND** these settings (don't overwrite existing ones)
- Ensure no duplicates

---

### ✅ Task 7: Create Test Runner Scripts (OPTIONAL)

**Objective:** Add PowerShell scripts for quick testing

**File:** `MiGenteEnLinea.Clean/scripts/run-tests.ps1`

```powershell
#!/usr/bin/env pwsh
# run-tests.ps1 - Test runner script for MiGente En Línea

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("all", "unit", "integration", "coverage")]
    [string]$TestType = "all",

    [Parameter(Mandatory=$false)]
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$solutionPath = "$PSScriptRoot/../MiGenteEnLinea.Clean.sln"

Write-Host "🧪 Running tests: $TestType" -ForegroundColor Cyan

switch ($TestType) {
    "all" {
        if ($NoBuild) {
            dotnet test $solutionPath --no-build --logger "console;verbosity=detailed"
        } else {
            dotnet test $solutionPath --logger "console;verbosity=detailed"
        }
    }
    "unit" {
        if ($NoBuild) {
            dotnet test "$PSScriptRoot/../tests/MiGenteEnLinea.Infrastructure.Tests/MiGenteEnLinea.Infrastructure.Tests.csproj" --no-build --logger "console;verbosity=detailed"
        } else {
            dotnet test "$PSScriptRoot/../tests/MiGenteEnLinea.Infrastructure.Tests/MiGenteEnLinea.Infrastructure.Tests.csproj" --logger "console;verbosity=detailed"
        }
    }
    "integration" {
        if ($NoBuild) {
            dotnet test "$PSScriptRoot/../tests/MiGenteEnLinea.IntegrationTests/MiGenteEnLinea.IntegrationTests.csproj" --no-build --filter "Category=Integration" --logger "console;verbosity=detailed"
        } else {
            dotnet test "$PSScriptRoot/../tests/MiGenteEnLinea.IntegrationTests/MiGenteEnLinea.IntegrationTests.csproj" --filter "Category=Integration" --logger "console;verbosity=detailed"
        }
    }
    "coverage" {
        Write-Host "📊 Generating coverage report..." -ForegroundColor Yellow
        dotnet test $solutionPath --collect "XPlat Code Coverage" --results-directory "$PSScriptRoot/../TestResults" /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80

        Write-Host "📈 Generating HTML report..." -ForegroundColor Yellow
        reportgenerator -reports:"$PSScriptRoot/../TestResults/**/coverage.cobertura.xml" -targetdir:"$PSScriptRoot/../TestResults/CoverageReport" -reporttypes:Html

        Write-Host "✅ Coverage report generated at: TestResults/CoverageReport/index.html" -ForegroundColor Green
    }
}

Write-Host "✅ Tests completed!" -ForegroundColor Green
```

**Action:** Create this file if it doesn't exist

---

## 🔒 Safety Checks & Validation

**CRITICAL: DO NOT proceed without these checks**

### Pre-Modification Checklist

- [ ] ✅ Read all 5 required documents (INDICE_COMPLETO_DOCUMENTACION.md, BACKEND_100_COMPLETE_VERIFIED.md, GAPS_AUDIT_COMPLETO_FINAL.md, INTEGRATION_TESTS_SETUP_REPORT.md, copilot-instructions.md)
- [ ] ✅ Validated actual project structure matches documentation
- [ ] ✅ Checked `.vscode/` folder exists
- [ ] ✅ Backed up existing configurations (mentally noted what exists)

### Post-Modification Checklist

- [ ] ✅ All JSON files are valid (no syntax errors)
- [ ] ✅ File paths in tasks.json match actual project structure
- [ ] ✅ Debug configurations point to correct DLLs
- [ ] ✅ Test tasks can be run from VS Code (test at least one)
- [ ] ✅ Coverage task generates report successfully

---

## 📝 Expected Outputs

After completing all tasks, the workspace should have:

1. **`.vscode/tasks.json`** - 15+ tasks (build, test, coverage, run, watch, clean)
2. **`.vscode/launch.json`** - 5+ configurations (API debug, test debug, compound)
3. **`.vscode/settings.json`** - Testing settings added
4. **`.vscode/extensions.json`** - Complete recommended extensions
5. **`MiGenteEnLinea.Clean/scripts/run-tests.ps1`** - Test runner script

---

## 🎯 Success Criteria

You have successfully completed this mission when:

- ✅ All tasks can be run from VS Code Tasks menu
- ✅ API can be debugged with F5 (opens Swagger on launch)
- ✅ Tests can be debugged with breakpoints
- ✅ Coverage report is generated and shows current ~45% coverage
- ✅ Test Explorer shows all 58 tests
- ✅ No JSON syntax errors in any `.vscode/*.json` files

---

## 🚨 Common Pitfalls to Avoid

1. **DO NOT overwrite existing configurations** - Always merge
2. **DO NOT assume file paths** - Validate actual structure first
3. **DO NOT skip reading documentation** - Context is critical
4. **DO NOT create files outside `.vscode/` or `scripts/`** - Stay focused
5. **DO NOT modify source code** - This is configuration update only

---

## 📞 Questions & Clarifications

If you encounter ambiguity:

1. **Read the documentation first** - Answers are in the 121 .md files
2. **Check existing patterns** - `.vscode/settings.json` has excellent examples
3. **Err on the side of caution** - When in doubt, document and skip
4. **DO NOT guess** - Better to leave incomplete than create incorrect configs

---

## 📊 Reporting Format

When you complete this mission, provide:

1. **Files Modified:** List of all files changed
2. **Files Created:** List of all files created
3. **Tasks Added:** List of task labels added to tasks.json
4. **Debug Configs Added:** List of configurations added to launch.json
5. **Issues Encountered:** Any problems or ambiguities found
6. **Validation Results:** Did you test the configurations?

---

## 🎉 Final Notes

This is a **focused, limited-scope mission**. Your goal is NOT to:
- Fix the 4 testing issues (TestDataSeeder, namespaces, etc.)
- Implement GAP-022 (EncryptionService)
- Write new tests or code
- Migrate frontend to Blazor

Your ONLY goal is to:
- **Update VS Code workspace configurations**
- **Enable efficient testing workflow**
- **Support 80%+ coverage goal**

Stay focused, read the documentation, and validate your work. Good luck! 🚀

---

_Last Updated: October 2025_
_For Questions: See `.github/copilot-instructions.md` or `MiGenteEnLinea.Clean/INDICE_COMPLETO_DOCUMENTACION.md`_
