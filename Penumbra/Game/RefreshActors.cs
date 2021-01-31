using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using System.Threading.Tasks;

namespace Penumbra
{
    public static class RefreshActors
    {
        private const int RenderModeOffset      = 0x0104;
        private const int RenderTaskPlayerDelay = 75;
        private const int RenderTaskOtherDelay  = 25;
        private const int ModelInvisibilityFlag = 0b10;

        private static async void Redraw(Actor actor)
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

            if (actor.ObjectKind == Dalamud.Game.ClientState.Actors.ObjectKind.Player)
            {
                DrawObject(RenderTaskPlayerDelay);
                await Task.Delay(RenderTaskPlayerDelay);
            }
            else
                DrawObject(RenderTaskOtherDelay);
            
        }

        public static void RedrawSpecific(ActorTable actors, Targets targets, string name)
        {
            if (name?.Length == 0)
            {
                RedrawAll(actors);
                return;
            }

            switch(name)
            {
                case "<me>":
                case "self":
                    Redraw(actors[0]);
                    return;
                case "<t>":
                case "target":
                    Redraw(targets.CurrentTarget);
                    return;
                case "<f>":
                case "focus":
                    Redraw(targets.FocusTarget);
                    return;
                case "<mo>":
                case "mouseover":
                    Redraw(targets.MouseOverTarget);
                    return;
            }

            foreach (var actor in actors)
                if (actor.Name == name)
                    Redraw(actor);
        }

        public static void RedrawAll(ActorTable actors)
        {
            foreach (var actor in actors)
                Redraw(actor);
        }
    }
}
