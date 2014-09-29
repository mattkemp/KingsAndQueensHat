﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using KingsAndQueensHat.Annotations;
using KingsAndQueensHat.TeamGeneration;
using KingsAndQueensHat.Utils;
using System.Xml.Serialization;

namespace KingsAndQueensHat.Model
{
    public class Team : INotifyPropertyChanged
    {
        public string Name { get; set; }
        private CommandHandler _wonCommand;
        private CommandHandler _lostCommand;
        private CommandHandler _drewCommand;

        public Team(string name)
        {
            Name = name;
            Players = new List<Player>();
            GameResult = GameResult.NoneYet;
        }

        /// <summary>
        /// For serialisation
        /// </summary>
        protected Team()
        {
        }

        public event EventHandler OnGameDone;

        public List<Player> Players { get; private set; }

        public GameResult GameResult { get; set; }

        public string GameResultStr
        {
            get 
            {
                if (GameResult == GameResult.NoneYet)
                {
                    return string.Empty;
                }
                return GameResult.ToString();
            }
        }

        public void AddPlayer(Player player)
        {
            Players.Add(player);
        }

        [XmlIgnore]
        public int TotalSkill
        {
            get { return Players.Sum(p => p.GetSkill()); }
        }

        [XmlIgnore]
        public int PlayerCount
        {
            get { return Players.Count; }
        }

        [XmlIgnore]
        public int Men
        {
            get { return Players.Count(p => p.Gender == Gender.Male); }
        }

        [XmlIgnore]
        public int Women
        {
            get { return Players.Count(p => p.Gender == Gender.Female); }
        }


        public IEnumerable<PlayerPairing> PlayerPairings()
        {
            for (int i = 0; i < PlayerCount; ++i)
            {
                for (int j = i + 1; j < PlayerCount; ++j)
                {
                    yield return new PlayerPairing(Players[i], Players[j]);
                }
            }
        }

        /// <summary>
        /// For each pair of players, record that they played together
        /// </summary>
        public void AddToPairingsCount(PlayerPairings pairings)
        {
            foreach (var playerPairing in PlayerPairings())
            {
                pairings.PlayedTogether(playerPairing);
            }
        }

        public void GameDone(GameResult gameResult)
        {
            Trace.Assert(GameResult == GameResult.NoneYet);
            GameResult = gameResult;
            foreach (var player in Players)
            {
                player.GameDone(gameResult);
            }

            OnPropertyChanged("GameResultStr");
            Won.RaiseCanExecuteChanged();
            Drew.RaiseCanExecuteChanged();
            Lost.RaiseCanExecuteChanged();
            var @event = OnGameDone;
            if (@event != null)
            {
                @event(this, new EventArgs());
            }
        }

        public CommandHandler Won
        {
            get
            {
                return _wonCommand ?? (_wonCommand = new CommandHandler(() => GameDone(GameResult.Won), () => GameResult == GameResult.NoneYet));
            }
        }

        public CommandHandler Drew
        {
            get
            {
                return _drewCommand ?? (_drewCommand = new CommandHandler(() => GameDone(GameResult.Draw), () => GameResult == GameResult.NoneYet));
            }
        }

        public CommandHandler Lost
        {
            get
            {
                return _lostCommand ?? (_lostCommand = new CommandHandler(() => GameDone(GameResult.Lost), () => GameResult == GameResult.NoneYet));
            }
        }

        public override string ToString()
        {
            return string.Format("{0} Men, {1} Women. Skill: {2}", Men, Women, TotalSkill);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
