# First prototype testing integration catalog

This is the Product Owner-approved technology matrix for ShowVault's first prototype testing phase. Prototype readiness and test planning must use this list from now on.

Previously implemented integrations that are not listed here remain valid product capabilities, but they are outside the first prototype test commitment. Their existence must not broaden the prototype matrix or imply that they are tested, validated, protected, verified, or recoverable in the prototype.

Manufacturer names do not imply support for every product the company has shipped. Coverage is credited only for named products, models, software or firmware versions, exported artifacts, or protocol capabilities with documented evidence and representative tests. Open protocols remain capabilities rather than vendor products.

## Audio manufacturers

- Yamaha
- Allen & Heath
- DiGiCo
- Avid
- SSL (Solid State Logic)
- Behringer
- Soundcraft
- Tascam
- Crest Audio
- Crown
- Dynacord
- d&b audiotechnik
- L-Acoustics
- Meyer Sound
- JBL Professional
- Martin Audio
- Funktion-One

## Audio networking and DSP

- Dante
- Dante Domain Manager
- Dante Controller
- AES67
- AVB
- Milan
- CobraNet
- RAVENNA
- Livewire+
- Q-SYS
- Tesira
- Open Sound Control (OSC)
- MIDI
- RTP-MIDI

## Lighting

- ETC
- MA Lighting
- ChamSys
- High End Systems
- Strand
- Avolites
- Obsidian Control Systems
- Zero 88
- Hog
- Pathway
- ENTTEC

## Lighting protocols

- DMX512
- RDM
- Art-Net
- sACN
- KiNET

## Video and media servers

- Resolume Arena
- Resolume Avenue
- disguise
- WATCHOUT
- Green Hippo Hippotizer
- PIXERA
- Millumin — automatic local detection deferred pending an official stable macOS application-bundle path or project root; current Millumin guidance documents downloading/running the app, portable `.millumin` project files, and collected-project folders chosen by the operator, but does not publish a dependable standard location for either the application or projects
- Ventuz — automatic local detection deferred pending an official stable Windows installation path or project root; current Ventuz guidance makes both the installer location and each `.vzp` project-folder location operator-selectable, so no dependable standard location is published
- Christie Pandoras Box — catalog detection checks `PandorasBox.exe` only within bounded documented `Pandoras Box <version>` directories beneath native Windows `C:\Program Files\Christie`; custom and 32-bit installs plus operator-selected project/content roots remain unsupported
- TouchDesigner — catalog detection checks standard macOS `/Applications/TouchDesigner.app` and bounded native Windows `C:\Program Files\Derivative\TouchDesigner.<build>\bin\TouchDesigner.exe` locations; renamed/custom and 32-bit installs plus operator-selected `.toe` project/media roots remain unsupported
- HeavyM — automatic local detection deferred pending an official stable macOS/Windows application path or project root; current HeavyM guidance tells operators to follow the `.dmg`/`.exe` installer without publishing an installed location, and Save As requires the operator to select where each `.hm` project folder is created. The documented `Documents/HeavyM/Project Backups` location is a safety copy, not the authoritative project root
- MadMapper 6 — catalog detection checks bounded versioned macOS `/Applications/MadMapper 6.x.app/Contents/MacOS/MadMapper` and native Windows `C:\Program Files\MadMapper 6.x\MadMapper.exe` locations, plus exact `.madproject` workspace directories beneath each user's documented default `Documents/MadMapper` root; legacy `.mad` files, custom project locations, unversioned/custom applications, and 32-bit Windows installs remain unsupported
- Isadora 4 — catalog detection checks the documented usual `/Applications/Isadora 4/Isadora.app` and native Windows `C:\Program Files\Isadora 4` locations; renamed/custom applications, 32-bit installs, and operator-selected `.izz` project roots remain unsupported
- Ventana — automatic local detection deferred because the catalog label does not resolve to a unique professional playback product; official sources identify distinct Ventuz real-time graphics, VNTANA cloud 3D content management, and Ventana Systems Vensim simulation products, so no application identity, standard path, or project root can be attributed safely

## Projection

- Christie — protocol 1.13 provides separately authorized, bounded PJLink identification for exact official `CHRISTIE` manufacturer responses paired with `LX41` or `LW41` model responses; authentication-enabled devices, other Christie models, configuration/control, and generic PJLink reachability remain unsupported
- Barco — automatic PJLink identification deferred because official G60 and Pulse documentation confirms PJLink support and the identity-query commands but does not publish literal `INF1` manufacturer or `INF2` model response values; authentication bypass is not assumed and generic PJLink support does not establish Barco identity
- Panasonic — protocol 1.13 provides separately authorized, bounded PJLink identification for exact official `Panasonic` manufacturer responses paired with `DZ770`, `VW431DEA`, `RZ470`, or `RW430` model responses; authentication-enabled devices, other Panasonic models, configuration/control, and generic PJLink reachability remain unsupported
- Epson — protocol 1.13 provides separately authorized, bounded PJLink identification for exact official `EPSON` manufacturer responses paired with `EPSON QB1000B` or `EPSON QB1000W` model responses; authentication-enabled devices, other Epson models, configuration/control, and generic PJLink reachability remain unsupported
- Digital Projection — automatic network identification deferred because the official E-Vision 8000i/10000i control workbook specifies only an unconstrained `<string>` response for its read-only `model.name ?` query; its UDP discovery example broadcasts privacy-bearing network/device fields and names an unrelated `HIGHLite 660`, so no target-bounded exact model signature is established
- NEC — protocol 1.13 provides separately authorized, bounded identification using the official read-only Base Model Type request on TCP 7142 and exact checksummed signatures for NP-PH3501QL, NP-PH2601QL, NP-PX2000UL, or NP-PX2201UL; other NEC models, malformed responses, configuration/control, and generic PJLink or port reachability remain unsupported
- Sony — automatic projector model identification deferred because the official common protocol manual publishes exact PJLink manufacturer response `SONY` but no literal `INF2` model value, enables authentication by default, and defines the alternative SDAP identity service as a privacy-bearing periodic broadcast; arbitrary model strings, authentication weakening, and broadcast collection remain unsupported

## Broadcast

- Blackmagic Design — protocol 1.14 provides separately authorized, bounded, zero-byte identification for the exact official Blackmagic Smart Videohub 16x16 status fixture on TCP 9990; addresses and raw status remain Agent-local, while other Videohub models, HyperDeck, ATEM hardware, configuration/control, and generic port reachability remain unsupported
- Sony
- NewTek
- AJA Video Systems

## Streaming and production

- OBS Studio

## DJ platforms

- rekordbox
- Serato DJ Pro
- Traktor Pro
- VirtualDJ

## Show control and playback

- QLab
- SCS (Show Cue System)
- PlaybackPro
- Mitti
- ProPresenter
- PVP
- Pandora's Box Manager
- CuePilot
- TinkerList

## PTZ and cameras

- PTZOptics
- BirdDog
- Panasonic
- Sony

## Delivery rule

Work remains vertical and evidence-based. Catalog placeholders, generic reachability presented as product support, and untested compatibility badges do not count. The Product Owner must explicitly approve additions to this first prototype testing matrix.
