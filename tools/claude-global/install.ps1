# Claude Code genel kurulum (Windows / PowerShell).
# superpowers, context7, playwright ve Anthropic skills'i KULLANICI kapsamında kurar:
# bu makinedeki her projede otomatik etkin olur.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$claudeDir = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { Join-Path $env:USERPROFILE '.claude' }
New-Item -ItemType Directory -Force -Path $claudeDir | Out-Null

$target = Join-Path $claudeDir 'CLAUDE.md'
if (Test-Path $target) { Copy-Item $target "$target.bak" -Force }
Copy-Item (Join-Path $here 'CLAUDE.md') $target -Force
Write-Host "Kurallar yazildi: $target"

claude plugin marketplace add anthropics/skills
claude plugin install superpowers@claude-plugins-official --scope user
claude plugin install example-skills@anthropic-agent-skills --scope user
claude plugin install document-skills@anthropic-agent-skills --scope user

claude mcp add --scope user --transport http context7 https://mcp.context7.com/mcp
claude mcp add --scope user playwright -- cmd /c npx -y "@playwright/mcp@latest"

Write-Host 'Tamam. Yeni bir Claude Code oturumu ac ve /plugin ile /mcp ile kontrol et.'
