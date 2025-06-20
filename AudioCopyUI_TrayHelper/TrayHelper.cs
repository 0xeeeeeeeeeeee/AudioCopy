namespace AudioCopyUI_MiddleWare
{
    public class TrayHelper
    {
        public static Action? BootApp;

        public static Action? CloseApp;

        public static Action? Shutdown;

        public static Action? GetSMTC;

        public static bool IsNotStandalone = false;

        public static string Title = "Unknown";

        public static string Artist = "Unknown";

        public static int listeningClient = 0;

        public static bool KeepBackendAsDefault;

        public static bool GUIRunning;
        public static bool NoKeepClone;

        public class Resource
        {
            public static string Warn;

            public static string Shutdown = "Exit";

            public static string ListeningClients;

            public static string Launch = "Start Audiocopy";

            public static string DisconnectWarn;

            public static string Close;
            public static string Exit;
        }

    }
}
