# Test script to check complaint detail page
$baseUrl = "https://localhost:53472"

Write-Host "Testing Complaint Detail Page..." -ForegroundColor Cyan

# Test accessing complaint detail page
for ($i = 1; $i -le 4; $i++) {
    $url = "$baseUrl/Complaint/Detail/$i"
    Write-Host "`nTesting complaint ID $i at: $url" -ForegroundColor Yellow

    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -SkipCertificateCheck -SessionVariable session -ErrorAction Stop
        Write-Host "✓ Status Code: $($response.StatusCode)" -ForegroundColor Green

        # Check if the response contains expected content
        if ($response.Content -match "Test Student") {
            Write-Host "✓ Student name found in response" -ForegroundColor Green
        } else {
            Write-Host "✗ Student name NOT found in response" -ForegroundColor Red
        }

        if ($response.Content -match "Complaint Details") {
            Write-Host "✓ Page title found" -ForegroundColor Green
        }
    }
    catch {
        # Check if it's a redirect to login (expected for unauthenticated access)
        if ($_.Exception.Response.StatusCode -eq 302 -or $_.Exception.Response.StatusCode -eq 401) {
            Write-Host "→ Redirected to login (expected - authentication required)" -ForegroundColor Yellow
        }
        else {
            Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "✗ Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
        }
    }
}

Write-Host "`n`nTest completed." -ForegroundColor Cyan
