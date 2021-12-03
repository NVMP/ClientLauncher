using System.Windows.Markup;

#if DEBUG
[assembly: XmlnsDefinition("debug-mode", "ClientLauncher")]
#endif

#if NEXUS_CANDIDATE
[assembly: XmlnsDefinition("nexus-candidate-mode", "ClientLauncher")]
#else
[assembly: XmlnsDefinition("main-build-mode", "ClientLauncher")]
#endif

