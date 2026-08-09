# L-Acoustics Soundvision project recovery

ShowVault requires a non-empty current Soundvision `.xmlp` project in an exact operator-approved root. A venue `.xmls` file, LA Network Manager session backup, preset/layout export, log, or empty lookalike alone does not qualify. The complete root is preserved, including venue models, revisions, reports, LA Network Manager session backups, user presets/layouts, XML logs, M1 evidence, and commissioning notes.

Configure absolute roots in `Agent:LAcousticsSoundvisionProjectRoots`. The plugin ID is `showvault.l-acoustics-soundvision`.

Before restore, confirm Soundvision and LA Network Manager compatibility, controller/processor models and firmware, preset-library versions, unit identities and addressing, loudspeaker design and zoning, gain/delay/EQ/polarity, AVB/Milan routing and clocking, P1/M1 calibration, and user credentials. Restore does not authorize loading settings or firmware into live units. L-Acoustics requires saving the current Session and user presets/layouts before firmware updates and warns that firmware updates erase Session parameters and stored user data.

Official references:

- [Soundvision](https://www.l-acoustics.com/products/soundvision/)
- [LA Network Manager](https://www.l-acoustics.com/products/network-manager/)
- [LA Network Manager installation bulletin](https://www.l-acoustics.com/documentation/SOFTWARE/LA%20Network%20Manager/Installation/LA_NWM_Installation_TB_ML.pdf)
- [L-Acoustics software catalog](https://www.l-acoustics.com/software/)

## Network identification research decision

As of August 8, 2026, automatic L-Acoustics network identification is deliberately deferred. The public LA Network Manager material confirms that the product discovers, identifies, configures, and synchronizes physical units, but it does not publish a discovery request/response contract or isolate a demonstrably read-only identification operation. L-Acoustics also documents that LA Device Scanner can discover units and display identity information, but the same utility can rename devices and manage IP addresses; its public material does not define the wire protocol or a read-only subset.

L-Acoustics publishes an Electronics HTTP API for third-party control of supported installation controllers, but the API documentation requires submitting identity and project information and accepting separate terms before download. The public registration page does not establish an endpoint, method, response schema, supported model set, authentication rule, or immutable identity field that ShowVault can safely implement and test.

ShowVault therefore does not send an L-Acoustics probe, advance the Agent protocol, or credit an open port, generic HTTP response, Milan/AVDECC metadata, Dante metadata, or generic reachability as product evidence. Identification can be reconsidered when the Product Owner supplies terms-authorized API documentation plus a representative hardware or vendor simulator fixture, or when L-Acoustics publishes a primary read-only discovery contract. Any future slice must remain separately manager-authorized, bound to one exact retained responder set, address-local, non-authenticating where the contract permits, non-synchronizing, and limited to explicitly documented read-only queries.

Network-identification references:

- [LA Network Manager product workflow](https://www.l-acoustics.com/products/network-manager/)
- [L-Acoustics Documentation Center and HTTP API registration](https://www.l-acoustics.com/result-documentation-center/?choice=LA7.16)
- [LC16D feature overview describing LA Device Scanner](https://www.l-acoustics.com/documentation/ELECTRONICS/LC16D/EN/Feature%20overview/L-Acoustics_LC16D_Feature_Overview_1.0.pdf)
