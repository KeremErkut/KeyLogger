# KeyLogger
A key display application built with C# for my OOP Lecture weekly assignment.

> You can watch the video on Youtube: [Link](https://www.youtube.com/watch?v=8uLWyvfknLo)

## Features
* Displays CPU, RAM, network, and OS information on startup.
* Captures and displays keystrokes including special keys (Enter, Backspace, Shift, etc.).
* Saves keystrokes to a log file.

## Libraries
* `System.Management` — CPU and RAM info via WMI
* `System.Net.NetworkInformation` — IP and MAC address
* `System.Runtime.InteropServices` — Windows API (GetAsyncKeyState)

## Disclaimer
* This project is created for educational purposes only.
