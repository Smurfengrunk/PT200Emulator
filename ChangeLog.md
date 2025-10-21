# Terminal Core Libraries

Detta repo innehåller kärnkomponenterna för terminalhantering:
- **Transport** – ansvarar för kommunikation (t.ex. Telnet, TCP).
- **Parser** – tolkar inkommande data och CSI‑sekvenser.
- **InputHandler** – hanterar tangentbordsinmatning och översätter till buffertoperationer.
- **ScreenBuffer** – gemensam datastruktur för rendering.

Rendering hålls som underprojekt i respektive UI (Console, WinForms, WPF, MAUI).

---

## 📦 NuGet‑paket
- `Terminal.Transport`
- `Terminal.Parser`
- `Terminal.InputHandler`

Alla tre versioneras gemensamt.

---

## 🚀 Release Notes

### 1.1.0
- Nytt paket: **InputHandler** (för tangentbordsinmatning).
- Parser: utökad CSI‑tabell, förbättrad loggning (*"inte implementerat"* istället för *"kommando finns inte"*).
- Transport: stabiliserad och testad tillsammans med Parser och InputHandler.
- Rendering hålls som underprojekt i respektive UI.

### 1.0.0
- Första publika versionen av **Transport** och **Parser**.