using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;

namespace Penumbra
{
    public class PlayerWatcher : IDisposable
    {
        private const int ActorsPerFrame = 4;

        private readonly DalamudPluginInterface _pi;
        private readonly Dictionary<string, CharEquipment> _equip = new();
        private int _frameTicker = 0;

        public PlayerWatcher(DalamudPluginInterface pi) => _pi = pi;

        public delegate void OnActorChange(Actor which);

        public event OnActorChange ActorChanged;

        public void AddPlayerToWatch(string playerName)
        {
            if (!_equip.ContainsKey(playerName))
                _equip[playerName] = new();
        }

        public void  EnableActorWatch()
        {
            _pi.Framework.OnUpdateEvent += OnFrameworkUpdate;
            _pi.ClientState.TerritoryChanged += OnTerritoryChange; 
            _pi.ClientState.OnLogout += OnLogout;
        }
        public void DisableActorWatch()
        {
            _pi.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            _pi.ClientState.TerritoryChanged -= OnTerritoryChange; 
            _pi.ClientState.OnLogout -= OnLogout;
        }

        public void           Dispose() => DisableActorWatch();

        public void OnTerritoryChange(object _1, ushort _2) => Clear();
        public void OnLogout(object _1, object _2) => Clear();

        public void Clear()
        {
            foreach (var kvp in _equip)
                kvp.Value.Clear();
            _frameTicker = 0;
        }

        public void OnFrameworkUpdate(object Framework)
        {
            var Actors = _pi.ClientState.Actors;
            for (var i = 0; i < ActorsPerFrame; ++i)
            {
                if (_frameTicker >= Actors.Length - 2)
                    _frameTicker = 0;
                else
                    _frameTicker += 2;

                var Actor = Actors[_frameTicker];
                if (Actor == null || Actor.ObjectKind != ObjectKind.Player
                    || Actor.Name == null || Actor.Name.Length == 0)
                    continue;

                if (_equip.TryGetValue(Actor.Name, out var equip))
                {
                    if (!equip.CompareAndUpdate(Actor))
                    {
                        ActorChanged?.Invoke(Actor);
                    }
                }
            }
        }
    }
}
