# Sobe o canal Telegram: túnel Cloudflare -> n8n -> fluxo publicado -> webhook do bot apontando para o túnel.
# Precisa de: Docker Desktop aberto, .env com TELEGRAM_BOT_TOKEN e TELEGRAM_WEBHOOK_SECRET, e a API rodando em http://localhost:5080.
# A URL do túnel rápido muda a cada subida, por isso o webhook é registrado de novo toda vez que este script roda.

$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

function Read-DotEnv([string]$path) {
    $map = @{}
    Get-Content $path | Where-Object { $_ -match '^\s*[^#=\s]+=' } | ForEach-Object {
        $key, $value = $_ -split '=', 2
        $map[$key.Trim()] = $value.Trim()
    }
    return $map
}

$cfg = Read-DotEnv ".env"
foreach ($required in "TELEGRAM_BOT_TOKEN", "TELEGRAM_WEBHOOK_SECRET") {
    if (-not $cfg[$required]) { throw "Defina $required no .env (veja o .env.example)." }
}

Write-Host "1/5 Subindo o túnel..."
docker compose up -d cloudflared | Out-Null
$tunnelUrl = $null
for ($i = 0; $i -lt 40 -and -not $tunnelUrl; $i++) {
    Start-Sleep -Seconds 2
    $logs = docker compose logs cloudflared 2>&1 | Out-String
    if ($logs -match 'https://[a-z0-9-]+\.trycloudflare\.com') { $tunnelUrl = $Matches[0] }
}
if (-not $tunnelUrl) { throw "Não consegui ler a URL do túnel nos logs do cloudflared." }
Write-Host "    túnel: $tunnelUrl"

Write-Host "2/5 Importando e publicando o fluxo no n8n..."
$env:N8N_WEBHOOK_URL = "$tunnelUrl/"
docker compose run --rm --no-deps n8n import:workflow --input=/workflows/telegram-agent.json | Out-Null
docker compose run --rm --no-deps n8n publish:workflow --id=telegram-agent | Out-Null

Write-Host "3/5 Subindo o n8n com a URL pública do túnel..."
docker compose up -d --force-recreate n8n | Out-Null
$ready = $false
for ($i = 0; $i -lt 40 -and -not $ready; $i++) {
    Start-Sleep -Seconds 2
    try { $ready = (Invoke-WebRequest "http://localhost:5678/healthz" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { }
}
if (-not $ready) { throw "O n8n não respondeu em http://localhost:5678/healthz." }

Write-Host "4/5 Esperando o túnel encaminhar para o n8n..."
$tunnelOk = $false
for ($i = 0; $i -lt 30 -and -not $tunnelOk; $i++) {
    try { $tunnelOk = (Invoke-WebRequest "$tunnelUrl/healthz" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { Start-Sleep -Seconds 2 }
}
if (-not $tunnelOk) { Write-Warning "O túnel ainda não respondeu; vou registrar o webhook mesmo assim." }

Write-Host "5/5 Registrando o webhook do bot no Telegram..."
$body = @{
    url                  = "$tunnelUrl/webhook/telegram"
    secret_token         = $cfg["TELEGRAM_WEBHOOK_SECRET"]
    allowed_updates      = @("message")
    drop_pending_updates = $true
} | ConvertTo-Json
$result = Invoke-RestMethod -Method Post "https://api.telegram.org/bot$($cfg['TELEGRAM_BOT_TOKEN'])/setWebhook" -ContentType "application/json" -Body $body
if (-not $result.ok) { throw "setWebhook falhou: $($result.description)" }

Write-Host "Pronto. Mande uma mensagem para o bot no Telegram."
