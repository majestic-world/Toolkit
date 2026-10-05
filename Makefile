.DEFAULT_GOAL := build
CONFIGURATION ?= Release
POWERSHELL ?= pwsh
BUILD_SCRIPT := scripts/build.ps1
RUN_BUILD = $(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(BUILD_SCRIPT)" -Configuration "$(CONFIGURATION)"

.PHONY: build macos linux dist run

# Windows (win-x64, Native AOT)
build:
	$(RUN_BUILD) -Platform windows

# macOS (osx-<host arch>, Native AOT + signed .app bundle); run on a Mac
macos:
	$(RUN_BUILD) -Platform macos

# Linux (linux-<host arch>, Native AOT); run on Linux
linux:
	$(RUN_BUILD) -Platform linux

# Windows build + Inno Setup installer
dist:
	$(RUN_BUILD) -Platform windows -Installer

# Development run (Debug, build/Debug/)
run:
	dotnet run --project src
