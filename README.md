# Helix Blast v0.3.0 — Portable Windows edition

Helix Blast is a Windows WPF/Fluent application for screening a query gene or
protein against a folder of FASTA files.

## Major change in v0.3.0

WSL/Ubuntu is no longer used.

The build process downloads the official NCBI BLAST+ 2.17.0 Windows x64 package,
verifies its MD5 checksum, and embeds only the native tools needed by Helix Blast:

- `blastn.exe`
- `blastp.exe`
- `tblastn.exe`
- native DLLs shipped with the official NCBI package, if present

The final `HelixBlast.exe` therefore runs BLAST directly on Windows.

## Building

Requirements **only on the computer that builds the EXE**:

- Windows x64
- .NET 8 SDK
- Internet access on the first build (downloads the official NCBI BLAST archive)

Double-click:

`build-exe.bat`

The NCBI archive is cached under `.build-blast`, so subsequent builds do not
redownload the 137 MB package unless the cache is deleted.

The resulting file is:

`HelixBlast.exe`

## Windows installer

For a standard installer (Start-menu shortcut, optional desktop shortcut and
uninstaller), install [Inno Setup 6](https://jrsoftware.org/isdl.php), then run:

`build-installer.bat`

The installer is written to `dist\HelixBlast-Setup-0.3.0.exe`.

## Running on another computer

The colleague only needs the resulting `HelixBlast.exe` on a 64-bit Windows PC.
They do **not** need:

- WSL
- Ubuntu
- Python
- .NET runtime
- a separate NCBI BLAST installation

At first BLAST use, Helix Blast extracts its bundled native BLAST executables to:

`%LOCALAPPDATA%\HelixBlast\blast\2.17.0\bin`

This is automatic.

## Visual C++ runtime note

NCBI documents the Visual Studio C++ Redistributable as a Windows BLAST runtime
dependency. Many Windows computers already have it because it is shared by many
applications. If bundled BLAST cannot start on a very clean Windows installation,
install the current Microsoft Visual C++ 2015–2022 Redistributable (x64).

## Privacy

Helix Blast uses `-subject` and operates directly on local FASTA files. The query
and genome files stay on the local computer.

## NCBI BLAST+

BLAST is public domain software from NCBI. The official Windows x64 package is
downloaded from:

https://ftp.ncbi.nlm.nih.gov/blast/executables/blast+/2.17.0/
