# FsUnboundMapper
Unpacks files from several FromSoftware games into a loose setup,  
Allowing them to be loose-loaded by their game.  

Primarily for Armored Core games before AC6 for now.

# Supported Games
| Game                     |  Platforms                                         |  Regions                                           |
| :----------------------- | :------------------------------------------------- | :------------------------------------------------- |
| Armored Core 4           | <ul><li>- [ ] PS3</li><li>- [ ] Xbox 360</li></ul> | <ul><li>- [ ] US</li><li>- [ ] JP</li></ul> |
| Armored Core For Answer  | <ul><li>- [x] PS3</li><li>- [x] Xbox 360\*</li></ul> | <ul><li>- [x] US</li><li>- [x] JP</li></ul> |
| Armored Core V           | <ul><li>- [x] PS3</li><li>- [x] Xbox 360</li></ul> | <ul><li>- [x] US</li><li> \? JP</li></ul> |
| Armored Core Verdict Day | <ul><li>- [x] PS3</li><li>- [x] Xbox 360</li></ul> | <ul><li>- [x] US</li><li> \? JP</li></ul> |

\* Armored Core For Answer Xbox 360 support unpacks like the others but the game does not run in this state.

# Requirements for use
The game files must be removed from any XISO, ISO, zip, 7z, or rar they are in.   

Make sure the .NET 8.0 runtime is installed or the terminal window will immediately close:  
https://dotnet.microsoft.com/en-us/download/dotnet/8.0  

Select ".NET Desktop Runtime" if you are on Windows and don't mind extra support for other .NET 8.0 programs using UI.  
Select ".NET Runtime" otherwise.  

Most users will need the x64 installer.  
I'm not sure if this program works on other operating systems or CPU architectures.  
This program has only been tested on Windows x64.  

On Windows, clicking on the terminal window breaks the program due to quick edit mode.  
There is code to call into windows APIs to lock it while in progress.  
I'm not sure if other platforms have similar issues.  
Try not to click the window while the program works, at least until it is fully finished.  

# Building
Clone this project with the following command:  
```
git clone --recursive https://github.com/WarpZephyr/FsUnboundMapper.git  
```
This is recursive because the project also makes use of the following submodule:  
https://github.com/JKAnderson/BinderKeys.git  

This project requires the following libraries to be cloned alongside it.  
Place them in the same top-level folder as this project.  
```
git clone https://github.com/WarpZephyr/Edoke.git  
git clone https://github.com/WarpZephyr/libps3.git  
git clone https://github.com/WarpZephyr/XenonFormats.git  
git clone https://github.com/soulsmods/SoulsFormatsNEXT.git  
```
Dependencies are subject to change at any time depending on what is deemed necessary.