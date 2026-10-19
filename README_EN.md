# Sony Headphones Controller for Windows

### Language / 言語 : [日本語](README.md) | English

> [!IMPORTANT]
> This software is an unofficial open-source implementation based on community reverse-engineering efforts and is not affiliated with, maintained, or endorsed by Sony Corporation or any of its subsidiaries.

> [!NOTE]
> This application uses Windows Native Bluetooth RFCOMM sockets to communicate directly with headphones. No third-party Bluetooth drivers are required; it runs purely on native Windows 10 / 11 APIs.

## Overview

**Sony Headphones Controller** is a modern Windows desktop application built with WinUI 3 and .NET 8, designed to provide full and seamless control over Sony Bluetooth headphones (specifically the WH-1000XM5).

Features that were previously only accessible via the official mobile apps (Sony Headphones Connect / Sound Connect) — such as Noise Cancelling adjustments, Ambient Sound levels, Multipoint device switching, and DSEE upscaling toggles — can now be controlled directly and elegantly from your Windows PC.

---

## Screenshots

### Japanese UI

| Sound & Media Control | Connected Devices (Multipoint) |
| :---: | :---: |
| ![Sound Control (JA)](docs/screenshots/ja_sound_control.png) | ![Connected Devices (JA)](docs/screenshots/ja_connected_devices.png) |

### English UI

| Sound & Media Control | Connected Devices (Multipoint) |
| :---: | :---: |
| ![Sound Control (EN)](docs/screenshots/en_sound_control.png) | ![Connected Devices (EN)](docs/screenshots/en_connected_devices.png) |

---

## Key Features

- 🎧 **Ambient Sound Control (NC / ASM)**
  - One-click switching between Noise Cancelling, Ambient Sound, and Off
  - Seamless slider control for Ambient Sound level (1–20) with debounce optimization
  - Voice Focus toggle to enhance vocal clarity
- 🔊 **Volume Control & Now Playing Media Info**
  - Synchronized headphone volume control via step buttons and slider
  - Real-time display of track metadata (Title, Artist, Album) over Bluetooth
  - Playback controls: Play / Pause, Next Track, Previous Track
- 🪄 **Audio Quality Status & DSEE Toggle**
  - Real-time display of active audio codec (LDAC, AAC, SBC, etc.)
  - DSEE (DSEE Extreme / HX) active status indicator
  - Interactive toggle switch to enable (Auto) or disable (Off) DSEE directly on the headphones
- 🔄 **Multipoint Device Management (Table 2 Protocol)**
  - View all paired devices stored in headphone memory (PCs, phones, tablets)
  - Real-time badge indicators for connected devices and active audio output focus
  - One-click "Switch Audio" button to change output priority
  - Connect, disconnect, and unpair devices directly from the desktop
- 🌐 **Seamless Multilingual Support (Japanese & English)**
  - Automatic language detection based on Windows OS culture (`CurrentUICulture`)
  - Instant on-the-fly language toggle from the top-right button without restarting the app
- 📊 **Status Bar & Floating Communication Log Panel**
  - Live status indicator with connection status, battery percentage, and charging badge
  - Floating communication log overlay that sits above the UI without shifting desktop contents
  - Inspect sent/received MDR binary frames and asynchronous event notifications in real time

---

## Architecture

The solution is divided into two focused projects:

```
[SonyHeadphoneWH] (WinUI 3 / Windows App SDK / .NET 8)
   │  - Modern Fluent Design GUI with Mica backdrop
   │  - MVVM pattern (MainViewModel, debouncing, feedback loop prevention)
   │  - Localization service (LocalizationService)
   ▼
[SonyHeadphonesNet] (.NET 8 Class Library)
   │  - MDR V2 Table 1 / Table 2 protocol stack (framing, byte stuffing, auto ACK)
   │  - Winsock RFCOMM native transport (ws2_32.dll AF_BTH / BTHPROTO_RFCOMM)
   │  - Device abstractions (ISonyHeadphoneDevice, SonyHeadphoneBase, Wh1000Xm5Device)
   ▼
[Bluetooth RFCOMM / Sony WH-1000XM5 Hardware]
```

---

## Getting Started & Building

### Prerequisites
- **OS**: Windows 10 (version 1809 or higher) / Windows 11
- **Hardware**: Windows PC with Bluetooth 4.0 or higher
- **Build Tools**: .NET 8.0 SDK, Visual Studio 2022 with Windows App Development workload

### 1. Pair Your Headphones
Pair your Sony headphones (e.g. WH-1000XM5) via standard Windows Settings (`Settings` > `Bluetooth & devices`).

### 2. Clone & Build
```bash
git clone https://github.com/NeonGraff/Sony_WH-1000XM5_Controller_WinUI3.git
cd Sony_WH-1000XM5_Controller_WinUI3

# Build Release for x64
dotnet build SonyHeadphoneWH/SonyHeadphoneWH.csproj -c Release -p:Platform=x64
```

### 3. Run
Launch the generated executable or debug via Visual Studio. Select your paired headphones from the top dropdown and click **Connect**.

---

## License

### Acknowledgments

Thank you very much to the developers of the following open-source projects for their valuable work in reverse engineering and protocol analysis:

[**SonyHeadphonesClient** (by mos9527, Amr Satrio and contributors)](https://github.com/mos9527/SonyHeadphonesClient)
```text
MIT License

Copyright (c) 2026 mos9527, Amr Satrio and other contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Trademarks

- "Sony", "WH-1000XM5", "LDAC", "DSEE", "DSEE Extreme", "Headphones Connect", and "Sound Connect" are registered trademarks or trademarks of Sony Corporation or its subsidiaries.
- "Windows" is a registered trademark of Microsoft Corporation.
- "Bluetooth" is a registered trademark of Bluetooth SIG, Inc.
- All other trademarks and registered trademarks are the properties of their respective holders.

### Project License

This project is licensed under the [MIT License](LICENSE).
Feel free to use, modify, and distribute this software for personal or commercial purposes.
