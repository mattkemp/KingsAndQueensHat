﻿using KingsAndQueensHat.Annotations;
using KingsAndQueensHat.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KingsAndQueensHat.ViewModel
{
    public class PlayerViewModel : INotifyPropertyChanged
    {
        public PlayerViewModel(Tournament tournament)
        {
            Tournament = tournament;
            NewPlayerSkill = "50";
            NewPlayerGender = Gender.Male;

            ResetNewPlayerSection();
        }

        public Tournament Tournament { get; private set; }

        private IPlayerProvider Players { get { return Tournament.PlayerProvider; } }

        public ObservableCollection<Player> AllPlayers
        {
            get { return Players.AllPlayers; }
        }

        // Player management:

        internal void ImportFrom(string filename)
        {
            Players.ImportFromCsv(filename);
        }


        public string NewPlayerName { get; set; }
        public Gender NewPlayerGender { get; set; }
        public string NewPlayerSkill { get; set; }

        public void AddPlayer(Func<bool> AddToCurrentRound, Action<string> ErrorAction)
        {
            if (NewPlayerName.Trim() == string.Empty)
            {
                ErrorAction("Enter a player's name");
                return;
            }
            else if (Players.PlayerExists(NewPlayerName))
            {
                ErrorAction("Player already exists");
                return;
            }
            int skill;
            if (!int.TryParse(NewPlayerSkill, out skill))
            {
                ErrorAction("Skill must be a whole number");
                return;
            }

            var player = Players.NewPlayer(NewPlayerName, NewPlayerGender, skill);

            if (Tournament.Rounds.Count > 0 && AddToCurrentRound())
            {
                Tournament.AddPlayerToLastRound(player);
            }

            ResetNewPlayerSection();
        }

        private void ResetNewPlayerSection()
        {
            NewPlayerName = string.Empty;
            OnPropertyChanged("NewPlayerName");
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
