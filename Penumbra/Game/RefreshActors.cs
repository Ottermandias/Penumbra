using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using System.Threading.Tasks;
using Penumbra.Mods;

namespace Penumbra
{
    public static class RefreshActors
    {
        private const int RenderModeOffset      = 0x0104;
        private const int RenderTaskPlayerDelay = 75;
        private const int RenderTaskOtherDelay  = 25;
        private const int ModelInvisibilityFlag = 0b10;

        public static async void RedrawWithSettings(ModManager mods, Actor actor, bool onlyWithSettings)
        {
            if (actor == null)
                return;

            var changedSettings = false;
            if (mods.CharacterSettings.CharacterConfigs.TryGetValue(actor.Name, out var settings) && settings.Enabled)
            {
                mods.ExchangeFileLists(settings.ResolvedFiles, settings.SwappedFiles);
                changedSettings = true; 
            }
            else if (onlyWithSettings)
                return;

            var ptr = actor.Address;
            var renderModePtr = ptr + RenderModeOffset;
            var renderStatus = Marshal.ReadInt32(renderModePtr);

            async void DrawObject(int delay)
            {
                Marshal.WriteInt32(renderModePtr, renderStatus | ModelInvisibilityFlag);
                await Task.Delay(delay);
                Marshal.WriteInt32(renderModePtr, renderStatus & ~ModelInvisibilityFlag);
            }

            if (actor.ObjectKind == ObjectKind.Player)
            {
                DrawObject(RenderTaskPlayerDelay);
                await Task.Delay(RenderTaskPlayerDelay);
            }
            else
                DrawObject(RenderTaskOtherDelay);
            
            if (changedSettings)
            {
                await Task.Delay(RenderTaskPlayerDelay);
                mods.RestoreDefaultFileLists();
            }
        }

        public static async void Redraw(Actor actor)
        {
            if (actor == null)
                return;

            var ptr = actor.Address;
            var renderModePtr = ptr + RenderModeOffset;
            var renderStatus = Marshal.ReadInt32(renderModePtr);

            async void DrawObject(int delay)
            {
                Marshal.WriteInt32(renderModePtr, renderStatus | ModelInvisibilityFlag);
                await Task.Delay(delay);
                Marshal.WriteInt32(renderModePtr, renderStatus & ~ModelInvisibilityFlag);
            }

            if (actor.ObjectKind == ObjectKind.Player)
            {
                DrawObject(RenderTaskPlayerDelay);
                await Task.Delay(RenderTaskPlayerDelay);
            }
            else
                DrawObject(RenderTaskOtherDelay);
        }

        private static Actor GetName(ActorTable actors, Targets targets, string name)
        {
            switch(name)
            {
                case "<me>":
                case "self":
                    return actors[0];
                case "<t>":
                case "target":
                    return targets.CurrentTarget;
                case "<f>":
                case "focus":
                    return targets.FocusTarget;
                case "<mo>":
                case "mouseover":
                    return targets.MouseOverTarget;
            }
            return null;
        }

        public static void RedrawSpecific(ActorTable actors, Targets targets, string name)
        {
            if (name?.Length == 0)
            {
                RedrawAll(actors);
                return;
            }

            var Actor = GetName(actors, targets, name);
            if (Actor != null)
                Redraw(Actor);
            else
                foreach (var actor in actors)
                    if (actor.Name == name)
                        Redraw(actor);
        }

        public static void RedrawSpecificWithSettings(ModManager mods, ActorTable actors, Targets targets, string name, bool onlyWithSettings)
        {
            if (name?.Length == 0)
            {
                RedrawAllWithSettings(mods, actors, onlyWithSettings);
                return;
            }

            var Actor = GetName(actors, targets, name);
            if (Actor != null)
                RedrawWithSettings(mods, Actor, onlyWithSettings);
            else
                foreach (var actor in actors)
                    if (actor.Name == name)
                        RedrawWithSettings(mods, Actor, onlyWithSettings);
        }

        public static void RedrawAll(ActorTable actors)
        {
            foreach (var actor in actors)
                Redraw(actor);
        }

        public static void RedrawAllWithSettings(ModManager mods, ActorTable actors, bool onlyWithSettings)
        {
            foreach (var actor in actors)
                RedrawWithSettings(mods, actor, onlyWithSettings);
        }
    }
}
