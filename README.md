# Quick Sticky Note
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
A very simple sticky notes application stripped down to the bare minimum.

I made this because my needs are very simple:
Open a note on the desktop → type in → delete when done.
No fancy customization.
No unnecessary features.
Super simple, super easy setup.

The goal is to have a quick, visible reminder for simple tasks — not to use the desktop as a calendar replacement.

## How it works
Right-click the desktop and use the context menu option for the .qnote file type:
<img width="578" height="445" alt="image" src="https://github.com/user-attachments/assets/24d99865-4bd3-48f3-8f5c-dbff4548e3fe" />

This will:
* Create a .qnote file in your %LOCALAPPDATA%\QuickSticky\ folder.

* Open a sticky note window at your current cursor position.

## Example

You can just take notes of whatever is in your mind.<br>
Copy paste texts with their format and color.<br>
Or you can just paste image in and draw on it...<br>
&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;because what else is faster than a screenshot.

<img width="544" height="466" alt="image" src="https://github.com/user-attachments/assets/83cb1e17-c84c-405a-995b-8141b5964e85" />



## Closing a note
The note stays open until you click the X three times.
This safety measure avoids accidental closures.
<img width="440" height="349" alt="image" src="https://github.com/user-attachments/assets/b11e2219-c9dd-44c6-a97b-c21bbc461766" />





When closed, the program will:
* Delete the associated .qnote file.
* Terminate the note process if it's the last note open.

## Startup behavior
The installer sets the app to run on system startup.
On startup, it will:
* Look for existing .qnote files and reopen them.
* If none are found, the app immediately exits — nothing runs in the background unless you have a note on screen.
