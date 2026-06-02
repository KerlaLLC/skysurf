# Userbus Tools for SKY

Welcome! This repo contains tools for interacting with [Blackbaud's SKY
APIs](https://developer.sky.blackbaud.com/api).

* **Toms is a tool for managing SKY connections for headless workflows.** It
  gets tokens for you, automatically renewing refresh tokens as needed.
* **Skysurf is a SKY API browser.** It helps you quickly find the SKY endpoints
  you're looking for and get data from them. It keeps itself up to date with
  Blackbaud's latest API specs automatically.

## Installation

**Windows (PowerShell):**

```powershell
irm https://www.userbus.xyz/downloads/skysurf/install.ps1 | iex
```

This installs both `toms` and `skysurf` and adds them to your PATH. Restart
your terminal afterward.

Linux and macOS support is planned.