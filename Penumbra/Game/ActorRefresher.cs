using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using Penumbra.Mods;

namespace Penumbra.Game
{
    public enum Redraw
    {
        WithoutSettings,
        WithSettings,
        OnlyWithSettings,
        Unload,
        RedrawWithoutSettings,
        RedrawWithSettings
    }

    public class ActorRefresher
    {
        private const int RenderModeOffset      = 0x0104;
        private const int ModelInvisibilityFlag = 0b10;
        private const int UnloadAllRedrawDelay  = 250;
        private const int NpcActorId            = -536870912;

        private readonly DalamudPluginInterface                        _pi;
        private readonly ModManager                                    _mods;
        private readonly Queue< (int actorId, string name, Redraw s) > _actorIds = new();

        private int    _currentFrame     = 0;
        private bool   _changedSettings  = false;
        private int    _currentActorId   = -1;
        private string _currentActorName = null;

        public ActorRefresher( DalamudPluginInterface pi, ModManager mods )
        {
            _pi   = pi;
            _mods = mods;
        }

        private void ChangeSettings( string name )
        {
            if( _mods.CharacterSettings.CharacterConfigs.TryGetValue( name, out var settings ) && settings.Enabled )
            {
                _mods.ExchangeFileLists( settings.ResolvedFiles, settings.SwappedFiles );
                _changedSettings = true;
            }
        }

        private void RestoreSettings()
        {
            _mods.RestoreDefaultFileLists();
            _changedSettings = false;
        }

        private static void WriteInvisible( IntPtr renderPtr )
        {
            if( renderPtr != IntPtr.Zero )
            {
                var renderStatus = Marshal.ReadInt32( renderPtr ) | ModelInvisibilityFlag;
                Marshal.WriteInt32( renderPtr, renderStatus );
            }
        }

        private static void WriteVisible( IntPtr renderPtr )
        {
            if( renderPtr != IntPtr.Zero )
            {
                var renderStatus = Marshal.ReadInt32( renderPtr ) & ~ModelInvisibilityFlag;
                Marshal.WriteInt32( renderPtr, renderStatus );
            }
        }

        private bool CheckActor( Actor actor )
        {
            if( _currentActorId != actor.ActorId )
            {
                return false;
            }

            if( _currentActorId != NpcActorId )
            {
                return true;
            }

            return _currentActorName == actor.Name;
        }

        private Actor FindCurrentActor()
            => _pi.ClientState.Actors.FirstOrDefault( CheckActor );

        private void ChangeSettingsAndUndraw()
        {
            if( _actorIds.Count > 0 )
            {
                var (id, name, s) = _actorIds.Dequeue();
                _currentActorName = name;
                _currentActorId   = id;
                var actor = FindCurrentActor();
                if( actor == null )
                {
                    return;
                }

                switch( s )
                {
                    case Redraw.Unload:
                        WriteInvisible( actor.Address + RenderModeOffset );
                        _currentFrame = 0;
                        break;
                    case Redraw.RedrawWithSettings:
                        ChangeSettings( actor.Name );
                        ++_currentFrame;
                        break;
                    case Redraw.RedrawWithoutSettings:
                        WriteVisible( actor.Address + RenderModeOffset );
                        _currentFrame = 0;
                        break;
                    case Redraw.WithoutSettings:
                        WriteInvisible( actor.Address + RenderModeOffset );
                        ++_currentFrame;
                        break;
                    case Redraw.WithSettings:
                        ChangeSettings( actor.Name );
                        WriteInvisible( actor.Address + RenderModeOffset );
                        ++_currentFrame;
                        break;
                    case Redraw.OnlyWithSettings:
                        ChangeSettings( actor.Name );
                        if( !_changedSettings )
                        {
                            return;
                        }

                        WriteInvisible( actor.Address + RenderModeOffset );
                        ++_currentFrame;
                        break;
                }
            }
            else
            {
                _pi.Framework.OnUpdateEvent -= OnUpdateEvent;
            }
        }

        private void StartRedraw()
        {
            var actor = FindCurrentActor();
            if( actor == null )
            {
                _currentFrame = 0;
                RestoreSettings();
            }

            WriteVisible( actor.Address + RenderModeOffset );
            _currentFrame = _changedSettings ? _currentFrame + 1 : 0;
        }

        private void RevertSettings()
        {
            RestoreSettings();
            _currentFrame = 0;
        }

        private void OnUpdateEvent( object framework )
        {
            switch( _currentFrame )
            {
                case 0:
                    ChangeSettingsAndUndraw();
                    break;
                case 1:
                    StartRedraw();
                    break;
                case 2:
                    RevertSettings();
                    break;
                default:
                    _currentFrame = 0;
                    break;
            }
        }

        private void RedrawActorIntern( int actorId, string actorName, Redraw settings )
        {
            if( _actorIds.Contains( ( actorId, actorName, settings ) ) )
            {
                return;
            }

            _actorIds.Enqueue( ( actorId, actorName, settings ) );
            if( _actorIds.Count == 1 )
            {
                _pi.Framework.OnUpdateEvent += OnUpdateEvent;
            }
        }

        public void RedrawActor( Actor actor, Redraw settings = Redraw.WithSettings )
        {
            if( actor != null )
            {
                RedrawActorIntern( actor.ActorId, actor.Name, settings );
            }
        }

        private Actor GetName( string name )
        {
            if( name == null )
            {
                return null;
            }

            switch( name )
            {
                case "":
                    return null;
                case "<me>":
                case "self":
                    return _pi.ClientState.Actors[ 0 ];
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
                    return _pi.ClientState.Actors.FirstOrDefault( A => A.Name == name );
            }
        }

        public void RedrawActor( string name, Redraw settings = Redraw.WithSettings )
            => RedrawActor( GetName( name ), settings );

        public void RedrawAll( Redraw settings = Redraw.WithSettings )
        {
            foreach( var actor in _pi.ClientState.Actors )
            {
                RedrawActor( actor, settings );
            }
        }

        private void UnloadAll()
        {
            foreach( var A in _pi.ClientState.Actors )
            {
                WriteInvisible( A.Address + RenderModeOffset );
            }
        }

        private void RedrawAllWithoutSettings()
        {
            foreach( var A in _pi.ClientState.Actors )
            {
                WriteVisible( A.Address + RenderModeOffset );
            }
        }

        public async void UnloadAtOnceRedrawWithSettings()
        {
            UnloadAll();
            await Task.Delay( UnloadAllRedrawDelay );
            RedrawAll( Redraw.RedrawWithSettings );
        }

        public async void UnloadAtOnceRedrawWithoutSettings()
        {
            UnloadAll();
            await Task.Delay( UnloadAllRedrawDelay );
            RedrawAllWithoutSettings();
        }
    }
}