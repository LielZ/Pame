<p align="center"><img src="https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/pame-banner.svg" alt="Pame - Your PC. Your console." width="100%"></p>

# Proper portrait covers in Games

Pame 0.4.5 prefers **LaunchBox Box - Front artwork** for games from Epic, EA and every other non-Steam source. If no front cover is available, the existing store image stays in place. Steam covers, game backgrounds and logos remain unchanged.

Existing libraries receive the new covers automatically. LaunchBox images with `r2_` filenames are now recognized too, fixing missing artwork for games such as Battlefield 6. The catalog index is rebuilt once; no account or API key is needed. Cached artwork remains available offline.

**[Download Pame 0.4.5 for Windows x64](https://github.com/LielZ/Pame/releases/download/v0.4.5/Pame-Setup-0.4.5-x64.exe)** · [SHA-256 checksum](https://github.com/LielZ/Pame/releases/download/v0.4.5/SHA256SUMS.txt)

Existing users can use **Settings > Updates > Check for updates**, then **Restart & update**. Automatic updates download in the background and install on the next Pame startup.

206 automated tests pass. Real-library checks cover Fortnite, Battlefield 6, EA SPORTS FC 26 and Split Fiction, including preservation of their original backgrounds and logos. [Validation](https://github.com/LielZ/Pame/blob/main/docs/VALIDATION-0.4.5.md) · [Artwork sources](https://github.com/LielZ/Pame/blob/main/docs/GAME-DISCOVERY.md)
