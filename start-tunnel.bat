@echo off
echo ========================================================
echo   KHOI CHAY CLOUDFLARE TUNNEL CHO EDUFLYUP (PORT 8088)
echo ========================================================
echo.
echo Dam bao Docker container dang chay (docker compose up -d)
echo.
.\cloudflared.exe tunnel --url http://localhost:8088
pause
