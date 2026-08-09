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
- Sony — automatic broadcast-device identification deferred because the official LMD-1951MD protocol publishes target-bounded SDCP status/control but no exact model-identity request and literal response, while its SDAP identity service broadcasts product name with serial, location, community, power, and network metadata; official XVS-9000 material documents NMOS and optional SNMP capabilities without an exact bounded product-identity exchange, so generic protocol participation, ports, reachability, broadcast collection, and projector/PTZ evidence remain unsupported
- NewTek — protocol 1.15 provides separately authorized, bounded, read-only identification for the exact official TriCaster TC1 `GET /version` fixture on TCP 80; HTTP 200 must contain exactly one `TC1` model and `TriCaster TC1` product name, addresses and privacy-bearing product/session XML remain Agent-local, authentication challenges are safe false negatives without using default credentials or weakening protection, and other models, generic NDI/HTTP reachability, configuration/control, backup, verification, and restore remain unsupported
- AJA Video Systems — automatic broadcast-device identification deferred because AJA's official REST API guide documents bounded read-only HTTP GET mechanics but no cross-product model-identity parameter with literal response values; its official IP-converter guidance assigns AJA REST discovery to SSDP or mDNS, so generic REST behavior, product-varying descriptor tables, NMOS, media-protocol participation, multicast discovery, ports, and web reachability do not establish an exact AJA product identity

## Streaming and production

- OBS Studio — catalog detection checks only the official standard macOS `/Applications/OBS.app` bundle, native Windows `C:\Program Files\obs-studio\bin\64bit\obs64.exe`, and each user's standard `obs-studio/basic/profiles` and `obs-studio/basic/scenes` recovery roots; portable/custom installations, recordings, media, plugins, file contents, validation, backup, verification, and restore remain unsupported

## DJ platforms

- rekordbox
- Serato DJ Pro
- Traktor Pro
- VirtualDJ

## Show control and playback

- QLab — automatic local detection deferred because Figure 53 documents `/Applications/QLab.app` but requires the operator to choose where each workspace or project folder is saved; automatic workspace backups are stored beside that chosen workspace, so no dependable bounded recovery root is published
- SCS (Show Cue System) — automatic local detection deferred because official SCS 11 guidance publishes `C:\Program Files\SCS 11\scs11.exe` but no dependable show root; cue files and optional portable production folders may be placed in operator-selected locations, while the broad Documents initial folder and per-user application-data device maps are not authoritative recovery roots
- PlaybackPro — automatic local detection deferred because DT Videolabs directs users to place downloaded PlaybackPro-family applications in Applications but does not publish an exact stable bundle path for this unversioned catalog row or a dependable show/playlist root; playlists reference media at operator-selected locations, so neither application identity nor bounded recovery data can be inferred safely
- Mitti — automatic local detection deferred because Imimot identifies the macOS bundle as `Mitti.app` but does not publish a dependable standard installation location; its portable Bundle Playlist workflow creates an operator-named directory at a user-selected location, and the saved project can continue referencing media at its original locations, so no bounded project/media recovery root can be inferred safely
- ProPresenter — catalog detection checks only the official standard macOS `/Applications/ProPresenter.app` bundle, default Windows `C:\Program Files\Renewed Vision\ProPresenter` directory, and each user's default `Documents/ProPresenter` recovery-data root; custom workspaces/support locations, externally referenced media, file contents, validation, backup, verification, and restore remain unsupported
- PVP — automatic local detection deferred because Renewed Vision documents placing the macOS application in Applications but publishes no dependable bounded Show or media root; each Show is operator-saved, excludes its media, and references media at arbitrary absolute paths unless the operator creates and relocates a relative-path folder
- Pandora's Box Manager — current V8 installation is already covered by `showvault.christie-pandoras-box`; Christie replaced the former separate Manager and other edition licenses with one V8 software license containing the full feature suite. Candidate existence does not establish activation, enabled Manager capability, legacy standalone Manager licensing, project data, validation, backup, verification, or restore support
- CuePilot — automatic local detection deferred because official guidance publishes a macOS Applications workflow but no dependable Windows installed path or bounded recovery root; collaborative projects are cloud-synchronized, the local SOLO project's path is unpublished, exports are operator-located, and media is either selected from arbitrary local paths or uploaded to CuePilot's cloud
- TinkerList — automatic local detection deferred because the current product is Cuez by TinkerList, whose rundown and project workflow is cloud-based; official Automator guidance publishes no stable installed executable or bundle path, and downloaded media is stored in an operator-defined location that may be on the Automator computer, another production computer, or separate storage

## PTZ and cameras

- PTZOptics
- BirdDog
- Panasonic
- Sony

## Delivery rule

Work remains vertical and evidence-based. Catalog placeholders, generic reachability presented as product support, and untested compatibility badges do not count. The Product Owner must explicitly approve additions to this first prototype testing matrix.
