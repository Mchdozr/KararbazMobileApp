#!/usr/bin/env bash
# Claude Code genel kurulum (macOS / Linux / WSL).
# superpowers, context7, playwright ve Anthropic skills'i KULLANICI kapsamında kurar:
# bu makinedeki her projede otomatik etkin olur.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
claude_dir="${CLAUDE_CONFIG_DIR:-$HOME/.claude}"
mkdir -p "$claude_dir"

target="$claude_dir/CLAUDE.md"
[ -f "$target" ] && cp "$target" "$target.bak"
cp "$here/CLAUDE.md" "$target"
echo "Kurallar yazıldı: $target"

mkdir -p "$claude_dir/skills"
cp -R "$here/skills/." "$claude_dir/skills/"
echo "Skill'ler kopyalandı: $claude_dir/skills (/caveman)"

claude plugin marketplace add anthropics/skills
claude plugin install superpowers@claude-plugins-official --scope user
claude plugin install example-skills@anthropic-agent-skills --scope user
claude plugin install document-skills@anthropic-agent-skills --scope user

claude mcp add --scope user --transport http context7 https://mcp.context7.com/mcp
claude mcp add --scope user playwright -- npx -y @playwright/mcp@latest

echo "Tamam. Yeni bir Claude Code oturumu aç; /plugin ve /mcp ile kontrol et."
