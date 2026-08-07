# Electro-Voice IRIS-Net project recovery

ShowVault discovers an Electro-Voice IRIS-Net project only inside an exact operator-approved root. The root must contain an outer `.ds` project archive whose ZIP structure includes a non-empty, top-level `main.ds` entry. A plain file with a `.ds` suffix, an incomplete archive, or loose files extracted from inside a project do not qualify.

The complete approved root is recovery content. This preserves the project archive together with colocated revisions, device inventories, reports, diagrams, commissioning records, and restore instructions. Operators should dedicate one folder to each recoverable project and copy the whole folder when moving it between systems.

## Configuration

Add absolute project-folder paths to `Agent:ElectroVoiceIrisNetProjectRoots`. Discovery accepts only a configured root itself, never an unapproved parent or child path.

```json
{
  "Agent": {
    "ElectroVoiceIrisNetProjectRoots": [
      "/Users/operator/Documents/IRIS-Net/Venue A"
    ]
  }
}
```

The plugin ID is `showvault.electro-voice-iris-net`.

## Supervised restore prerequisites

Before restoring or opening a protected project, record and confirm:

- the IRIS-Net version and compatible Windows environment;
- every controlled device model, firmware version, address, and network role;
- controller, amplifier, DSP, wall-control, and supervision topology;
- audio-network routing and clocking state, including Dante where applicable;
- presets, protection parameters, credentials, and any site-specific deployment procedure.

Restoring files does not authorize deployment to live hardware. Device matching, firmware compatibility, routing, protection settings, and online synchronization remain supervised commissioning work.

## Product boundary

This integration covers IRIS-Net project archives demonstrated by Electro-Voice's official ELX200 example. It does not claim recovery support for QuickSmart Mobile, whose official material does not establish a portable project export, or for PREVIEW loudspeaker software until a dependable exported-artifact signature is verified. Dynacord SONICUE remains a separate ShowVault target.

## Official references

- [Electro-Voice downloads](https://products.electrovoice.com/na/en/downloads)
- [IRIS-Net product page](https://products.electrovoice.com/ap/en/iris-net)
- [IRIS-Net configuration manual](https://products.electrovoice.com/binary/IRIS-Net_Configuration_Manual_enUS.pdf)
- [Electro-Voice apps and tools](https://electrovoice.com/support/apps-and-tools/)
