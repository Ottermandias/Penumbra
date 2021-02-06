using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;
using Penumbra.Mods;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penumbra
{
    public enum Redraw
    {
        WithoutSettings,
        WithSettings,
        OnlyWithSettings
    }

    public class ActorRefresher
    {
        private const int RenderModeOffset      = 0x0104;
        private const int ModelInvisibilityFlag = 0b10;
        private const int NpcActorId            = -536870912;

        private readonly DalamudPluginInterface    _pi;
        private readonly ModManager                _mods;
        private readonly Queue<(int actorId, string name, Redraw s)> _actorIds = new();

        private int    _currentFrame     = 0;
        private bool   _changedSettings  = false;
        private int    _currentActorId   = -1;
        private string _currentActorName = null;

        public ActorRefresher(DalamudPluginInterface pi, ModManager mods)
        { 
            _pi   = pi; 
            _mods = mods;
        }


        private void ChangeSettings(string name)
        {
            if (_mods.CharacterSettings.CharacterConfigs.TryGetValue(name, out var settings) && settings.Enabled)
            {
                _mods.ExchangeFileLists(settings.ResolvedFiles, settings.SwappedFiles);
                _changedSettings = true;
            }
        }

        private void RestoreSettings()
        {
            if (_changedSettings)
            {
                _mods.RestoreDefaultFileLists();
                _changedSettings = false;
            }
        }

        private static void WriteInvisible(IntPtr renderPtr)
        {
            if (renderPtr != IntPtr.Zero)
            {
                var renderStatus = Marshal.ReadInt32(renderPtr) | ModelInvisibilityFlag;
                Marshal.WriteInt32(renderPtr, renderStatus);
            }
        }

        private static void WriteVisible(IntPtr renderPtr)
        {
            if (renderPtr != IntPtr.Zero)
            {
                var renderStatus = Marshal.ReadInt32(renderPtr) & ~ModelInvisibilityFlag;
                Marshal.WriteInt32(renderPtr, renderStatus);
            }
        }

        private bool CheckActor(Actor A)
        {
            if (_currentActorId != A.ActorId)
                return false;
            if (_currentActorId != NpcActorId)
                return true;
            return _currentActorName == A.Name;
        }

        private Actor FindCurrentActor() => _pi.ClientState.Actors.FirstOrDefault( A => CheckActor(A) );

        private void InitialStep()
        {
            if (_actorIds.Count > 0)
            {
                var id = _actorIds.Dequeue();
                _currentActorName = id.name;
                _currentActorId   = id.actorId;
                var actor = FindCurrentActor();
                if (actor == null)
                    return;

                if (id.s != Redraw.WithoutSettings)
                    ChangeSettings(actor.Name);
                if (id.s == Redraw.OnlyWithSettings && !_changedSettings)
                    return;

                WriteInvisible(actor.Address + RenderModeOffset);
                ++_currentFrame;
            }
            else
                _pi.Framework.OnUpdateEvent -= OnUpdateEvent;
        }

        private void SecondStep()
        {
            var actor = FindCurrentActor();
            if (actor == null)
            {
                _currentFrame = 0;
                RestoreSettings();
            }

            WriteVisible(actor.Address + RenderModeOffset);
            if (!_changedSettings)
                _currentFrame = 0;
            else
                ++_currentFrame;
        }

        private void FinalStep()
        {
            RestoreSettings();
            _currentFrame = 0;
        }

        private void OnUpdateEvent(object Framework)
        {
            switch (_currentFrame)
            {
                case  0: InitialStep();     break;
                case  1: SecondStep();      break;
                case  2: FinalStep();       break;
                default: _currentFrame = 0; break;
            }
        }

        private void RedrawActorIntern(int actorId, string actorName, Redraw settings)
        {
            if (_actorIds.Contains((actorId, actorName, settings)))
                return;
            _actorIds.Enqueue((actorId, actorName, settings));
            if (_actorIds.Count == 1)
                _pi.Framework.OnUpdateEvent += OnUpdateEvent;
        }

        public void RedrawActor(Actor actor, Redraw settings = Redraw.WithSettings)
        {
            if (actor?.Name != null) 
                RedrawActorIntern(actor.ActorId, actor.Name, settings);
        }

        private Actor GetName(string name)
        {
            if (name == null)
                return null;

            switch(name)
            {
                case "":
                    return null;
                case "<me>":
                case "self":
                    return _pi.ClientState.Actors[0];
                case "<t>":
                case "target":
                    return _pi.ClientState.Targets.CurrentTarget;
                case "<f>":
                case "focus":
                    return _pi.ClientState.Targets.FocusTarget;
                case "<mo>":
                case "mouseover":
                    return _pi.ClientState.Targets.MouseOverTarget;
                default:
                    return _pi.ClientState.Actors.FirstOrDefault( A => A.Name == name);
            }
        }

        public void RedrawActor(string name, Redraw settings = Redraw.WithSettings) => RedrawActor(GetName(name), settings);

        public void RedrawAll(Redraw settings = Redraw.WithSettings)
        {
            foreach (var A in _pi.ClientState.Actors)
                RedrawActor(A, settings);
        }
    }
}
