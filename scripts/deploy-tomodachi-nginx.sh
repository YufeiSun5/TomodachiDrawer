#!/usr/bin/env bash
set -euo pipefail

sudo mkdir -p /etc/nginx/backup-tomodachi
sudo cp /etc/nginx/sites-available/sunyufei5.art "/etc/nginx/backup-tomodachi/sunyufei5.art.bak.$(date +%Y%m%d%H%M%S)"
sudo cp /etc/nginx/sites-enabled/20k-ai-ip.conf "/etc/nginx/backup-tomodachi/20k-ai-ip.conf.bak.$(date +%Y%m%d%H%M%S)"

sudo python3 - <<'PY'
from pathlib import Path

snippet = '''
    # BEGIN TomodachiDrawer-CN
    location = /tomodachi {
        return 302 /tomodachi/;
    }

    location /tomodachi/api/ {
        proxy_pass http://127.0.0.1:5080/api/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_buffering off;
        proxy_cache off;
        proxy_read_timeout 300s;
    }

    location /tomodachi/ {
        alias /opt/tomodachi-drawer-cn-web/;
        index index.html;
        try_files $uri $uri/ /tomodachi/index.html;
    }
    # END TomodachiDrawer-CN
'''

for raw in ["/etc/nginx/sites-available/sunyufei5.art", "/etc/nginx/sites-enabled/20k-ai-ip.conf"]:
    path = Path(raw)
    text = path.read_text()
    start = text.find("    # BEGIN TomodachiDrawer-CN")
    if start != -1:
        end = text.find("    # END TomodachiDrawer-CN", start)
        if end == -1:
            raise SystemExit(f"missing END marker in {raw}")
        end = text.find("\n", end)
        text = text[:start] + text[end + 1 :]
    marker = "    location /api/ {"
    idx = text.find(marker)
    if idx == -1:
        raise SystemExit(f"marker not found in {raw}")
    text = text[:idx] + snippet + "\n" + text[idx:]
    path.write_text(text)
PY

sudo nginx -t
sudo systemctl reload nginx
