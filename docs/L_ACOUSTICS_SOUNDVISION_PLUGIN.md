# L-Acoustics Soundvision project recovery

ShowVault requires a non-empty current Soundvision `.xmlp` project in an exact operator-approved root. A venue `.xmls` file, LA Network Manager session backup, preset/layout export, log, or empty lookalike alone does not qualify. The complete root is preserved, including venue models, revisions, reports, LA Network Manager session backups, user presets/layouts, XML logs, M1 evidence, and commissioning notes.

Configure absolute roots in `Agent:LAcousticsSoundvisionProjectRoots`. The plugin ID is `showvault.l-acoustics-soundvision`.

Before restore, confirm Soundvision and LA Network Manager compatibility, controller/processor models and firmware, preset-library versions, unit identities and addressing, loudspeaker design and zoning, gain/delay/EQ/polarity, AVB/Milan routing and clocking, P1/M1 calibration, and user credentials. Restore does not authorize loading settings or firmware into live units. L-Acoustics requires saving the current Session and user presets/layouts before firmware updates and warns that firmware updates erase Session parameters and stored user data.

Official references:

- [Soundvision](https://www.l-acoustics.com/products/soundvision/)
- [LA Network Manager](https://www.l-acoustics.com/products/network-manager/)
- [LA Network Manager installation bulletin](https://www.l-acoustics.com/documentation/SOFTWARE/LA%20Network%20Manager/Installation/LA_NWM_Installation_TB_ML.pdf)
- [L-Acoustics software catalog](https://www.l-acoustics.com/software/)
