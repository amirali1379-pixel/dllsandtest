foreach ($p in $projects) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $p.FullName -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Cyan

    dotnet build $p.FullName --no-restore

    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED: $($p.Name)" -ForegroundColor Red
        $failed += $p.FullName
        continue
    }

    Write-Host "BUILD PASS: $($p.Name)" -ForegroundColor Green

    dotnet test $p.FullName --no-restore --no-build

    if ($LASTEXITCODE -ne 0) {
        Write-Host "TEST FAILED: $($p.Name)" -ForegroundColor Red
        $failed += $p.FullName
    }
    else {
        Write-Host "TEST PASS: $($p.Name)" -ForegroundColor Green
    }
}