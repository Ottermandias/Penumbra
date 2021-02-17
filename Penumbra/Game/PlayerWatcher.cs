using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;

namespace Penumbra.Game
{
    public class PlayerWatcher : IDisposable
    {
        private const int ActorsPerFrame     = 2;
        private const int ActorListPlayerCap = 255;

        private readonly DalamudPluginInterface              _pi;
        private readonly Dictionary< string, CharEquipment > _equip = new();
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

        public void RemovePlayerFromWatch( string playerName )
            => _equip.Remove( playerName );

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

        // Min(255, actorsLength)
        private static int MinFrameTicker( int actorsLength )
            => actorsLength & ActorListPlayerCap;

        public void OnFrameworkUpdate( object framework )
        {
            var actors = _pi.ClientState.Actors;
            for( var i = 0; i < ActorsPerFrame; ++i )
            {
                _frameTicker = _frameTicker < MinFrameTicker( actors.Length )
                    ? _frameTicker + 2
                    : 0;

                var actor = actors[ _frameTicker ];
                if( (actor?.Name?.Length ?? 0) == 0 || actor.ObjectKind != ObjectKind.Player)
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