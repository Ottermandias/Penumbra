using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using EmbedIO;
using EmbedIO.WebApi;
using Penumbra.API;
using Penumbra.Game;
using Penumbra.Mods;
using Penumbra.UI;

namespace Penumbra
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Penumbra";

        private const string CommandName = "/penumbra";

        public DalamudPluginInterface PluginInterface { get; set; }

        public Configuration Configuration { get; set; }

        public ResourceLoader ResourceLoader { get; set; }

        public ModManager ModManager { get; set; }

        public SettingsInterface SettingsInterface { get; set; }

        public GameUtils GameUtils { get; set; }

        public string PluginDebugTitleStr { get; private set; }
        public ActorRefresher ActorRefresher { get; set; }
        public PlayerWatcher ActorWatcher{ get; set; }

        private WebServer _webServer;

        public void Initialize( DalamudPluginInterface pluginInterface )
        {
            PluginInterface = pluginInterface;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize( PluginInterface );

            GameUtils = new GameUtils( PluginInterface );
            ActorWatcher = new(PluginInterface);

            ModManager = new ModManager( this );
            ModManager.CharacterSettings.ReadAll( new(Configuration.CurrentCollection) );
            ModManager.DiscoverMods( Configuration.CurrentCollection );

            ActorRefresher = new(PluginInterface, ModManager);

            foreach ( var actor in ModManager.CharacterSettings.CharacterConfigs.Keys )
                ActorWatcher.AddPlayerToWatch(actor);
            ActorWatcher.ActorChanged += RedrawWithConfig;

            ResourceLoader = new ResourceLoader( this );

            PluginInterface.CommandManager.AddHandler( CommandName, new CommandInfo( OnCommand )
            {
                HelpMessage = "/penumbra - toggle ui\n/penumbra reload - reload mod file lists & discover any new mods"
            } );

            ResourceLoader.Init();
            ResourceLoader.Enable();

            GameUtils.ReloadPlayerResources();

            SettingsInterface = new SettingsInterface( this );
            PluginInterface.UiBuilder.OnBuildUi += SettingsInterface.Draw;

            PluginDebugTitleStr = $"{Name} - Debug Build";

            if( Configuration.EnableHttpApi )
            {
                CreateWebServer();
            }

            if (Configuration.EnableActorWatch)
            {
                ActorWatcher.EnableActorWatch();
            }
        }

        private void RedrawWithConfig(Actor actor)
        {
            PluginInterface.Framework.Gui.Chat.Print("Event!");
            ActorRefresher.RedrawActor(actor, Redraw.OnlyWithSettings);
        }

        public void CreateWebServer()
        {
            var prefix = "http://localhost:42069/";
            
            ShutdownWebServer();

            _webServer = new WebServer( o => o
                    .WithUrlPrefix( prefix )
                    .WithMode( HttpListenerMode.EmbedIO ) )
                .WithCors( prefix )
                .WithWebApi( "/api", m => m
                    .WithController( () => new ModsController( this ) ) );

            _webServer.StateChanged += ( s, e ) => PluginLog.Information( $"WebServer New State - {e.NewState}" );

            _webServer.RunAsync();
        }

        public void ShutdownWebServer()
        {
            _webServer?.Dispose();
            _webServer = null;
        }

        public void Dispose()
        {
            ActorWatcher?.Dispose();

            ModManager?.Dispose();

            PluginInterface.UiBuilder.OnBuildUi -= SettingsInterface.Draw;

            PluginInterface.CommandManager.RemoveHandler( CommandName );
            PluginInterface.Dispose();

            ResourceLoader.Dispose();

            ShutdownWebServer();
        }

        private void OnCommand( string command, string rawArgs )
        {
            var args = rawArgs.Split( ' ' );
            if( args.Length > 0 && args[ 0 ].Length > 0 )
            {
                switch( args[ 0 ] )
                {
                    case "reload":
                    {
                        ModManager.DiscoverMods();
                        PluginInterface.Framework.Gui.Chat.Print(
                            $"Reloaded Penumbra mods. You have {ModManager.Mods.ModSettings.Count} mods, {ModManager.Mods.EnabledMods.Length} of which are enabled."
                        );
                        break;
                    }
                    case "redraw":
                    {
                        if (args.Length > 1)
                            ActorRefresher.RedrawActor(string.Join(" ", args.Skip(1)), Redraw.WithSettings);
                        else
                            ActorRefresher.RedrawAll(Redraw.WithSettings);
                        break;
                    }
                }

                return;
            }

            SettingsInterface.FlipVisibility();
        }
    }
}
