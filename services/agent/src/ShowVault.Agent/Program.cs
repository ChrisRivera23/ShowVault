using ShowVault.Agent;
using ShowVault.Agent.Identity;
using ShowVault.Agent.Communication;
using ShowVault.Agent.Execution;
using ShowVault.Agent.Plugins;
using ShowVault.Agent.Recovery;
using ShowVault.Agent.Queue;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.ControlPlaneUri.Scheme == Uri.UriSchemeHttps ||
            (options.ControlPlaneUri.Scheme == Uri.UriSchemeHttp && options.ControlPlaneUri.IsLoopback),
        "The control plane URI must use HTTPS except for loopback development.")
    .Validate(
        options => options.DiscoveryRoots.All(Path.IsPathFullyQualified),
        "Every discovery root must be an absolute path.")
    .Validate(
        options => options.ResolumeDiscoveryRoots.All(Path.IsPathFullyQualified),
        "Every Resolume discovery root must be an absolute path.")
    .Validate(
        options => options.ResolumeUserDataRoots.All(Path.IsPathFullyQualified),
        "Every Resolume user-data root must be an absolute path.")
    .Validate(
        options => options.GrandMa2ExportRoots.All(Path.IsPathFullyQualified),
        "Every grandMA2 export root must be an absolute path.")
    .Validate(
        options => options.GrandMa3ExportRoots.All(Path.IsPathFullyQualified),
        "Every grandMA3 export root must be an absolute path.")
    .Validate(
        options => options.YamahaDm7ExportRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha DM7 export root must be an absolute path.")
    .Validate(
        options => options.YamahaRivageExportRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha RIVAGE PM export root must be an absolute path.")
    .Validate(
        options => options.YamahaClQlExportRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha CL/QL export root must be an absolute path.")
    .Validate(
        options => options.YamahaTfExportRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha TF export root must be an absolute path.")
    .Validate(
        options => options.YamahaDm3ExportRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha DM3 export root must be an absolute path.")
    .Validate(
        options => options.YamahaDme7ProjectRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha DME7 project root must be an absolute path.")
    .Validate(
        options => options.YamahaMtxMrxProjectRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha MTX/MRX project root must be an absolute path.")
    .Validate(
        options => options.YamahaPcDdiProjectRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha PC-D/DI project root must be an absolute path.")
    .Validate(
        options => options.YamahaProVisionaireControlProjectRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha ProVisionaire Control project root must be an absolute path.")
    .Validate(
        options => options.YamahaDme5Dme3ProjectRoots.All(Path.IsPathFullyQualified),
        "Every Yamaha DME5/DME3 project root must be an absolute path.")
    .Validate(
        options => options.QsysDesignerProjectRoots.All(Path.IsPathFullyQualified),
        "Every Q-SYS Designer project root must be an absolute path.")
    .Validate(
        options => options.EtcEosShowArchiveRoots.All(Path.IsPathFullyQualified),
        "Every ETC Eos show archive root must be an absolute path.")
    .Validate(
        options => options.DanteControllerPresetRoots.All(Path.IsPathFullyQualified),
        "Every Dante Controller preset root must be an absolute path.")
    .Validate(
        options => options.AllenHeathSqShowRoots.All(Path.IsPathFullyQualified),
        "Every Allen & Heath SQ show root must be an absolute path.")
    .Validate(
        options => options.CrestronSimplProjectRoots.All(Path.IsPathFullyQualified),
        "Every Crestron SIMPL project root must be an absolute path.")
    .Validate(
        options => options.ShureDesignerRoomRoots.All(Path.IsPathFullyQualified),
        "Every Shure Designer room root must be an absolute path.")
    .Validate(
        options => options.BlackmagicAtemStateRoots.All(Path.IsPathFullyQualified),
        "Every Blackmagic ATEM state root must be an absolute path.")
    .Validate(
        options => options.DigicoSdQuantumSessionRoots.All(Path.IsPathFullyQualified),
        "Every DiGiCo SD/Quantum session root must be an absolute path.")
    .Validate(
        options => options.SslLiveShowRoots.All(Path.IsPathFullyQualified),
        "Every SSL Live show root must be an absolute path.")
    .Validate(
        options => options.LawoMc2ProductionRoots.All(Path.IsPathFullyQualified),
        "Every Lawo mc² production root must be an absolute path.")
    .Validate(
        options => options.CalrecApolloArtemisShowRoots.All(Path.IsPathFullyQualified),
        "Every Calrec Apollo/Artemis show root must be an absolute path.")
    .Validate(
        options => options.StuderVistaTitleBackupRoots.All(Path.IsPathFullyQualified),
        "Every Studer Vista title-backup root must be an absolute path.")
    .Validate(
        options => options.MidasProShowRoots.All(Path.IsPathFullyQualified),
        "Every Midas PRO Series show root must be an absolute path.")
    .Validate(
        options => options.BehringerWingShowRoots.All(Path.IsPathFullyQualified),
        "Every Behringer WING show root must be an absolute path.")
    .Validate(
        options => options.SoundcraftViShowRoots.All(Path.IsPathFullyQualified),
        "Every Soundcraft Vi show root must be an absolute path.")
    .Validate(
        options => options.TascamModelMtrSongRoots.All(Path.IsPathFullyQualified),
        "Every Tascam Model-series MTR song root must be an absolute path.")
    .Validate(
        options => options.RolandM5000ProjectRoots.All(Path.IsPathFullyQualified),
        "Every Roland M-5000 project root must be an absolute path.")
    .Validate(
        options => options.PreSonusStudioLiveSeriesIiiBackupRoots.All(Path.IsPathFullyQualified),
        "Every PreSonus StudioLive Series III backup root must be an absolute path.")
    .Validate(
        options => options.BiampTesiraConfigurationRoots.All(Path.IsPathFullyQualified),
        "Every Biamp Tesira configuration root must be an absolute path.")
    .Validate(
        options => options.SymetrixComposerSiteRoots.All(Path.IsPathFullyQualified),
        "Every Symetrix Composer site root must be an absolute path.")
    .Validate(
        options => options.BoseControlSpaceProjectRoots.All(Path.IsPathFullyQualified),
        "Every Bose ControlSpace project root must be an absolute path.")
    .Validate(
        options => options.PeaveyNwareProjectRoots.All(Path.IsPathFullyQualified),
        "Every Peavey MediaMatrix NWare project root must be an absolute path.")
    .Validate(
        options => options.AshlyProteaNeProjectRoots.All(Path.IsPathFullyQualified),
        "Every Ashly Protea NE project root must be an absolute path.")
    .Validate(
        options => options.PowersoftArmoniaPlusProjectRoots.All(Path.IsPathFullyQualified),
        "Every Powersoft ArmoniaPlus project root must be an absolute path.")
    .Validate(
        options => options.CrownAudioArchitectVenueRoots.All(Path.IsPathFullyQualified),
        "Every Crown Audio Architect venue root must be an absolute path.")
    .Validate(
        options => options.LabGruppenLakeSystemRoots.All(Path.IsPathFullyQualified),
        "Every Lab Gruppen Lake system root must be an absolute path.")
    .Validate(
        options => options.DynacordSonicueProjectRoots.All(Path.IsPathFullyQualified),
        "Every Dynacord SONICUE project root must be an absolute path.")
    .Validate(
        options => options.ElectroVoiceIrisNetProjectRoots.All(Path.IsPathFullyQualified),
        "Every Electro-Voice IRIS-Net project root must be an absolute path.")
    .Validate(
        options => options.DbAudiotechnikR1ProjectRoots.All(Path.IsPathFullyQualified),
        "Every d&b audiotechnik R1 project root must be an absolute path.")
    .Validate(
        options => options.LAcousticsSoundvisionProjectRoots.All(Path.IsPathFullyQualified),
        "Every L-Acoustics Soundvision project root must be an absolute path.")
    .Validate(
        options => options.MeyerSoundMapp3dProjectRoots.All(Path.IsPathFullyQualified),
        "Every Meyer Sound MAPP 3D project root must be an absolute path.")
    .Validate(
        options => options.NexoNs1ProjectRoots.All(Path.IsPathFullyQualified),
        "Every NEXO NS-1 project root must be an absolute path.")
    .Validate(
        options => options.JblVenueSynthesisProjectRoots.All(Path.IsPathFullyQualified),
        "Every JBL Venue Synthesis project root must be an absolute path.")
    .Validate(
        options => options.MartinAudioVuNetProjectRoots.All(Path.IsPathFullyQualified),
        "Every Martin Audio Vu-Net project root must be an absolute path.")
    .Validate(
        options => options.DasAudioAlmaDataRoots.All(Path.IsPathFullyQualified),
        "Every DAS Audio ALMA data root must be an absolute path.")
    .Validate(
        options => options.RestoreRoots.All(Path.IsPathFullyQualified),
        "Every restore root must be an absolute path.")
    .Validate(
        options => options.NetworkDiscoveryTargets.Count <= 128 &&
            options.NetworkDiscoveryTargets.All(NetworkTarget.IsValid) &&
            options.NetworkDiscoveryTargets.Select(NetworkTarget.Parse).Distinct().Count() ==
                options.NetworkDiscoveryTargets.Count,
        "Network discovery requires at most 128 unique host:port targets.")
    .Validate(
        options => string.IsNullOrWhiteSpace(options.PackageDirectory) ||
            Path.IsPathFullyQualified(options.PackageDirectory),
        "The package directory must be an absolute path when configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient<AgentEnrollmentClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>();
    client.BaseAddress = options.Value.ControlPlaneUri;
});
builder.Services.AddHttpClient<AgentEventClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>();
    client.BaseAddress = options.Value.ControlPlaneUri;
});
builder.Services.AddHttpClient<AgentCommandClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>();
    client.BaseAddress = options.Value.ControlPlaneUri;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AgentQueueStore>();
builder.Services.AddSingleton<AgentEventDispatcher>();
builder.Services.AddSingleton<AgentCommandPoller>();
builder.Services.AddSingleton<IDiscoveryPlugin, FileSystemDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, ResolumeDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, GrandMa2ShowDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, GrandMa3ShowDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaDm7DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaRivageDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaClQlDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaTfDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaDm3DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaDme7DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaMtxMrxDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaPcDdiDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaProVisionaireControlDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, YamahaDme5Dme3DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, QsysDesignerDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, EtcEosShowDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, DanteControllerDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, AllenHeathSqShowDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, CrestronSimplDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, ShureDesignerDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, BlackmagicAtemDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, DigicoSdQuantumDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, SslLiveDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, LawoMc2DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, CalrecApolloArtemisDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, StuderVistaDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, MidasProDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, BehringerWingDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, SoundcraftViDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, TascamModelMtrDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, RolandM5000DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, PreSonusStudioLiveSeriesIiiDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, BiampTesiraDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, SymetrixComposerDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, BoseControlSpaceDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, PeaveyNwareDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, AshlyProteaNeDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, PowersoftArmoniaPlusDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, CrownAudioArchitectDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, LabGruppenLakeDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, DynacordSonicueDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, ElectroVoiceIrisNetDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, DbAudiotechnikR1DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, LAcousticsSoundvisionDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, MeyerSoundMapp3dDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, NexoNs1DiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, JblVenueSynthesisDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, MartinAudioVuNetDiscoveryPlugin>();
builder.Services.AddSingleton<IDiscoveryPlugin, DasAudioAlmaDiscoveryPlugin>();
builder.Services.AddSingleton<DiscoveryPluginRegistry>();
builder.Services.AddSingleton<SystemInventoryPlugin>();
builder.Services.AddSingleton<INetworkEndpointConnector, TcpNetworkEndpointConnector>();
builder.Services.AddSingleton<NetworkDeviceDiscoveryPlugin>();
builder.Services.AddSingleton<RecoveryPackageWriter>();
builder.Services.AddSingleton<RecoveryPackageVerifier>();
builder.Services.AddSingleton<RecoveryPackageRestorer>();
builder.Services.AddSingleton<AgentCommandExecutor>();
builder.Services.AddSingleton<AgentIdentityBootstrapper>();
if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
{
    builder.Services.AddSingleton<IAgentCredentialStore, WindowsCredentialStore>();
}
else if (OperatingSystem.IsMacOS())
{
    builder.Services.AddSingleton<IAgentCredentialStore, MacOsKeychainCredentialStore>();
}
else
{
    throw new PlatformNotSupportedException(
        "ShowVault Agent credential storage currently supports Windows and macOS.");
}

builder.Services.AddHostedService<AgentWorker>();

await builder.Build().RunAsync();
