#!/usr/bin/env pwsh

Write-Output "Starting test execution..."
Write-Output ""

# Run contract tests
Write-Output "Running Contract Tests..."
dotnet test --filter "FullyQualifiedName~ContractTests" --verbosity normal --no-build 2>&1 | Tee-Object -FilePath "test-output.txt"

Write-Output ""
Write-Output "Test execution complete. Results saved to test-output.txt"
