using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;

namespace Penumbra
{
    public class PlayerWatcher : IDisposable
    {
        private const int ActorsPerFrame = 4;

        private readonly DalamudPluginInterface              _pi;
        private readonly Dictionary< string, CharEquipment > _equip       = new();
        private          int                                 _frameTicker;

        public PlayerWatcher( DalamudPluginInterface pi )
            => _pi = pi;

        public delegate void OnActorChange( Actor which );
        public event OnActorChange ActorChanged;

        public void AddPlayerToWatch( string playerName )
        {
            if( !_equip.ContainsKey( playerName ) )
            {
                _equip[ playerName ] = new CharEquipment();
            }
        }

        public void SetActorWatch( bool on )
        {
            if( on )
            {
                EnableActorWatch();
            }
            else
            {
                DisableActorWatch();
            }
        }

        public void EnableActorWatch()
        {
            _pi.Framework.OnUpdateEvent      += OnFrameworkUpdate;
            _pi.ClientState.TerritoryChanged += OnTerritoryChange;
            _pi.ClientState.OnLogout         += OnLogout;
        }

        public void DisableActorWatch()
        {
            _pi.Framework.OnUpdateEvent      -= OnFrameworkUpdate;
            _pi.ClientState.TerritoryChanged -= OnTerritoryChange;
            _pi.ClientState.OnLogout         -= OnLogout;
        }

        public void Dispose()
            => DisableActorWatch();

        public void OnTerritoryChange( object _1, ushort _2 )
            => Clear();

        public void OnLogout( object _1, object _2 )
            => Clear();

        public void Clear()
        {
            foreach( var kvp in _equip )
            {
                kvp.Value.Clear();
            }

            _frameTicker = 0;
        }

        public void OnFrameworkUpdate( object framework )
        {
            var actors = _pi.ClientState.Actors;
            for( var i = 0; i < ActorsPerFrame; ++i )
            {
                _frameTicker = (_frameTicker < actors.Length - 2)
                    ? _frameTicker + 2
                    : 0;

                var actor = actors[ _frameTicker ];
                if( actor == null || actor.ObjectKind != ObjectKind.Player
                    || actor.Name == null || actor.Name.Length == 0 )
                {
                    continue;
                }

                if( _equip.TryGetValue( actor.Name, out var equip ) && !equip.CompareAndUpdate( actor ) )
                {
                    ActorChanged?.Invoke( actor );
                }
            }
        }
    }
}